-- 0005_clients: client groups, contacts, notes, custom fields, numbering, tax ID rules per country.

-- target for same-firm foreign keys
alter table clients add constraint ux_clients_id_firm unique (id, firm_id);

-- ---------------------------------------------------------------- groups (family / business group)

create table client_groups (
  id         uuid primary key,
  firm_id    uuid not null references firms(id),
  name       text not null,
  created_at timestamptz not null default now(),
  constraint ux_client_groups_name unique (firm_id, name),
  constraint ux_client_groups_id_firm unique (id, firm_id)
);

alter table clients
  add constraint fk_clients_group foreign key (group_id, firm_id) references client_groups(id, firm_id);
create index ix_clients_group on clients (firm_id, group_id) where group_id is not null;

-- ---------------------------------------------------------------- contacts and notes

create table client_contacts (
  id                 uuid primary key,
  firm_id            uuid not null references firms(id),
  client_id          uuid not null,
  name               text not null,
  role               text,                         -- Director, Accountant, Owner ...
  email              citext,
  phone              text,
  is_primary         boolean not null default false,
  receives_reminders boolean not null default true,
  created_at         timestamptz not null default now(),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade
);
create index ix_client_contacts_client on client_contacts (client_id);
create unique index ux_client_contacts_one_primary on client_contacts (client_id) where is_primary;

create table client_notes (
  id         uuid primary key,
  firm_id    uuid not null references firms(id),
  client_id  uuid not null,
  body       text not null check (length(body) between 1 and 4000),
  created_by uuid not null,
  created_at timestamptz not null default now(),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade,
  foreign key (created_by, firm_id) references users(id, firm_id)
);
create index ix_client_notes_client on client_notes (client_id, created_at desc);

-- ---------------------------------------------------------------- custom fields

create table custom_field_definitions (
  id               uuid primary key,
  firm_id          uuid not null references firms(id),
  entity           text not null check (entity in ('client')),   -- more entities in later sprints
  key              text not null check (key ~ '^[a-z][a-z0-9_]{0,39}$'),
  label            text not null,
  field_type       text not null check (field_type in ('text','number','date','dropdown','bool')),
  options          jsonb,                                         -- dropdown choices: ["A","B"]
  is_required      boolean not null default false,
  validation_regex text,
  sort_order       int not null default 0,
  is_active        boolean not null default true,
  created_at       timestamptz not null default now(),
  constraint ux_custom_fields_key unique (firm_id, entity, key)
);

-- ---------------------------------------------------------------- numbering

create table number_sequences (
  firm_id    uuid not null references firms(id),
  entity     text not null,                -- client, client_invoice, computation ...
  period     text not null default '',     -- '' = never resets; '2026-27' yearly; '2026-11' monthly
  next_value bigint not null default 1,
  primary key (firm_id, entity, period)
);

-- Returns the next number for this firm, atomically (two requests never get the same number).
create function next_number(p_entity text, p_period text default '') returns bigint
language sql volatile as $$
  insert into number_sequences (firm_id, entity, period, next_value)
  values (app_current_firm(), p_entity, coalesce(p_period, ''), 2)
  on conflict (firm_id, entity, period) do update set next_value = number_sequences.next_value + 1
  returning next_value - 1
$$;

-- ---------------------------------------------------------------- tax ID rules (platform data, per country)

create table tax_id_types (
  country_code char(2) not null references countries(code),
  code         text not null,             -- PAN, GSTIN, TAN, TRN, VAT, CR
  label        text not null,
  pattern      text not null,             -- regex the normalised value must match
  checksum     text not null default 'none' check (checksum in ('none','gstin')),
  is_unique    boolean not null default false, -- true: two clients of one firm can't share it (GSTIN)
  example      text,
  sort_order   int not null default 0,
  primary key (country_code, code)
);

insert into tax_id_types (country_code, code, label, pattern, checksum, is_unique, example, sort_order) values
  ('IN', 'PAN',   'PAN',                      '^[A-Z]{5}[0-9]{4}[A-Z]$',                        'none',  false, 'ABCDE1234F',      10),
  ('IN', 'GSTIN', 'GSTIN',                    '^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$', 'gstin', true,  '27AAPFU0939F1ZV', 20),
  ('IN', 'TAN',   'TAN',                      '^[A-Z]{4}[0-9]{5}[A-Z]$',                        'none',  false, 'MUMA12345B',      30),
  ('AE', 'TRN',   'Tax Registration Number',  '^[0-9]{15}$',                                    'none',  false, '100123456700003', 10),
  ('SA', 'VAT',   'VAT number',               '^3[0-9]{13}3$',                                  'none',  false, '300000000000003', 10),
  ('SA', 'CR',    'Commercial Registration',  '^[0-9]{10}$',                                    'none',  true,  '1010000000',      20);

-- ---------------------------------------------------------------- row-level security

do $$
declare t text;
begin
  foreach t in array array['client_groups','client_contacts','client_notes','custom_field_definitions','number_sequences']
  loop
    execute format('alter table %I enable row level security', t);
    execute format(
      'create policy tenant_isolation on %I using (firm_id = app_current_firm()) with check (firm_id = app_current_firm())', t);
  end loop;
end $$;

grant select on tax_id_types to cadesk_app;
grant select, insert, update, delete on client_groups, client_contacts, client_notes, custom_field_definitions to cadesk_app;
grant select, insert, update on number_sequences to cadesk_app;
grant execute on function next_number(text, text) to cadesk_app;

-- searching clients by tax ID (PAN, GSTIN ...) and duplicate checks
create index ix_clients_pan   on clients (firm_id, (tax_ids->>'PAN'))   where deleted_at is null;
create index ix_clients_gstin on clients (firm_id, (tax_ids->>'GSTIN')) where deleted_at is null;

-- ---------------------------------------------------------------- settings for the client module

-- "required fields" may name any tax ID type, incl. the Saudi ones
update config_definitions
   set json_schema = '{"type":"array","items":{"type":"string","enum":["email","phone","PAN","GSTIN","TAN","TRN","VAT","CR"]},"uniqueItems":true}'
 where key = 'client.required_fields';

-- Saudi Arabia: client types and the Commercial Registration number by default
insert into config_values (id, key, scope, scope_ref, firm_id, value, is_locked) values
  (gen_random_uuid(), 'client.entity_types',    1, 'SA', null, '["Individual","Establishment","LLC","Joint Stock Company","Branch"]', false),
  (gen_random_uuid(), 'client.required_fields', 1, 'SA', null, '["CR"]', false);
