-- 0009_documents: the document vault. Per-client folders by year and filing, uploads by staff and by clients
-- (upload links), virus scan status, retention and links from tasks to the documents they use.
-- The file bytes live in the file store (Documents:StoragePath), never in the database.

-- ---------------------------------------------------------------- folders (platform data)

-- General folders. Filing folders come from compliance_types (one per filing, e.g. GSTR-3B).
create table document_folders (
  code       text primary key check (code ~ '^[a-z0-9_]{1,40}$'),
  name       text not null,
  per_year   boolean not null,          -- false = kept for the client's whole life (KYC, deeds), no year
  sort_order int not null default 0
);

insert into document_folders (code, name, per_year, sort_order) values
('permanent',      'Permanent (KYC, deeds, registrations)', false, 10),
('bank',           'Bank statements',                       true,  20),
('sales',          'Sales and invoices',                    true,  30),
('purchases',      'Purchases and expenses',                true,  40),
('financials',     'Books and financial statements',        true,  50),
('notices',        'Notices and orders',                    true,  60),
('correspondence', 'Letters and correspondence',            true,  70),
('other',          'Other',                                 true,  90);

grant select on document_folders to cadesk_app;

-- ---------------------------------------------------------------- client upload links

create table document_upload_links (
  id             uuid primary key,
  firm_id        uuid not null references firms(id),
  client_id      uuid not null,
  token_hash     text not null unique,             -- SHA-256 of the token in the link; the token itself is never stored
  message        text check (length(message) <= 500),
  folder         text references document_folders(code),
  type_code      text references compliance_types(code),
  fy             text check (fy ~ '^[0-9]{4}(-[0-9]{2})?$'),
  period_key     text,
  task_id        uuid,
  max_files      int not null default 20 check (max_files between 1 and 100),
  uploaded_count int not null default 0,
  expires_at     timestamptz not null,
  revoked_at     timestamptz,
  last_used_at   timestamptz,
  created_by     uuid,
  created_at     timestamptz not null default now(),
  constraint ux_upload_links_id_firm unique (id, firm_id),
  constraint ck_upload_links_folder check (folder is null or type_code is null),
  foreign key (client_id, firm_id) references clients(id, firm_id),
  foreign key (task_id, firm_id) references tasks(id, firm_id) on delete set null (task_id),
  foreign key (created_by, firm_id) references users(id, firm_id)
);
create index ix_upload_links_client on document_upload_links (client_id, created_at desc);

-- ---------------------------------------------------------------- documents

create table documents (
  id             uuid primary key,
  firm_id        uuid not null references firms(id),
  client_id      uuid not null,
  folder         text references document_folders(code),
  type_code      text references compliance_types(code),     -- filing folder (GSTR-3B, ITR ...)
  fy             text check (fy ~ '^[0-9]{4}(-[0-9]{2})?$'), -- null only in a permanent folder
  period_key     text,                                       -- the filing period, when uploaded for a deadline
  name           text not null check (length(name) between 1 and 200),
  file_name      text not null check (length(file_name) between 1 and 255),
  extension      text not null check (extension ~ '^[a-z0-9]{1,10}$'),
  content_type   text not null,
  size_bytes     bigint not null check (size_bytes > 0),
  sha256         text not null check (sha256 ~ '^[0-9a-f]{64}$'),
  storage_key    text unique,                                -- null once the file is removed (purged or infected)
  scan_status    text not null check (scan_status in ('pending','clean','infected','not_scanned')),
  scan_detail    text check (length(scan_detail) <= 300),
  scanned_at     timestamptz,
  scan_attempts  int not null default 0,
  source         text not null check (source in ('staff','portal')),
  upload_link_id uuid,
  notes          text check (length(notes) <= 1000),
  retention_from date,                                       -- retention counts from here; null = permanent folder
  legal_hold     boolean not null default false,             -- never removed while on hold
  deleted_at     timestamptz,                                -- in the trash
  deleted_by     uuid,
  purged_at      timestamptz,                                -- file removed; the row stays as a record
  purge_reason   text check (purge_reason in ('trash','retention','infected')),
  created_at     timestamptz not null default now(),
  created_by     uuid,
  updated_at     timestamptz,
  updated_by     uuid,
  constraint ux_documents_id_firm unique (id, firm_id),
  constraint ck_documents_folder check ((folder is null) <> (type_code is null)),
  foreign key (client_id, firm_id) references clients(id, firm_id),
  foreign key (upload_link_id, firm_id) references document_upload_links(id, firm_id),
  foreign key (created_by, firm_id) references users(id, firm_id),
  foreign key (deleted_by, firm_id) references users(id, firm_id)
);
create index ix_documents_client_folder on documents (firm_id, client_id, fy, folder, type_code) where deleted_at is null;
create index ix_documents_hash on documents (firm_id, client_id, sha256) where deleted_at is null and purged_at is null;
create index ix_documents_pending_scan on documents (firm_id, created_at) where scan_status = 'pending' and purged_at is null;
create index ix_documents_trash on documents (firm_id, deleted_at) where deleted_at is not null and purged_at is null;

-- which documents a task uses (a bank statement can support several filings)
create table task_documents (
  firm_id     uuid not null references firms(id),
  task_id     uuid not null,
  document_id uuid not null,
  linked_by   uuid,
  linked_at   timestamptz not null default now(),
  primary key (task_id, document_id),
  foreign key (task_id, firm_id) references tasks(id, firm_id) on delete cascade,
  foreign key (document_id, firm_id) references documents(id, firm_id) on delete cascade,
  foreign key (linked_by, firm_id) references users(id, firm_id)
);
create index ix_task_documents_document on task_documents (document_id);

-- ---------------------------------------------------------------- row-level security

do $$
declare t text;
begin
  foreach t in array array['documents','document_upload_links','task_documents']
  loop
    execute format('alter table %I enable row level security', t);
    execute format(
      'create policy tenant_isolation on %I using (firm_id = app_current_firm()) with check (firm_id = app_current_firm())', t);
  end loop;
end $$;

-- documents are records: the app removes the file (purged_at), never the row
grant select, insert, update on documents, document_upload_links to cadesk_app;
grant select, insert, delete on task_documents to cadesk_app;

-- A client opens an upload link before we know the firm: this finds exactly one link by its token hash, nothing more.
create function portal_find_upload_link(p_hash text)
returns table (id uuid, firm_id uuid, client_id uuid, expires_at timestamptz, revoked_at timestamptz)
language sql stable security definer set search_path = public, pg_temp as $$
  select id, firm_id, client_id, expires_at, revoked_at from document_upload_links where token_hash = p_hash
$$;
revoke all on function portal_find_upload_link(text) from public;
grant execute on function portal_find_upload_link(text) to cadesk_app;

-- ---------------------------------------------------------------- settings

insert into config_definitions (key, area, value_type, json_schema, default_value, lowest_scope, plan_value_is_cap, labels, sort_order) values
('doc.upload_link_days', 'documents', 'int', '{"type":"integer","minimum":1,"maximum":90}', '14', 3, false, '{"en":"Client upload links work for (days)"}', 50),
('doc.trash_days',       'documents', 'int', '{"type":"integer","minimum":7,"maximum":90}', '30', 3, false, '{"en":"Deleted documents stay in the trash for (days)"}', 60);
