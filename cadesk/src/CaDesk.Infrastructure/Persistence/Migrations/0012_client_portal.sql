-- 0012_client_portal: Client Self-Service Portal & Service Request Desk.
-- Enables external client users (directors, accounts heads) to view filings, invoices, DSCs,
-- and submit/track interactive service requests and tickets.

-- ---------------------------------------------------------------- client portal users

create table if not exists client_portal_users (
  id              uuid primary key,
  firm_id         uuid not null references firms(id),
  client_id       uuid not null,
  email           text not null check (email ~* '^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$'),
  name            text not null check (length(name) between 1 and 200),
  phone           text check (phone is null or length(phone) <= 30),
  password_hash   text not null,
  role            text not null default 'client_admin' check (role in ('client_admin','client_viewer')),
  is_active       boolean not null default true,
  last_login_at   timestamptz,
  created_at      timestamptz not null default now(),
  created_by      uuid,
  constraint ux_client_portal_users_id_firm unique (id, firm_id),
  constraint ux_client_portal_users_firm_email unique (firm_id, email),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade,
  foreign key (created_by, firm_id) references users(id, firm_id)
);

create index if not exists ix_client_portal_users_firm_client on client_portal_users (firm_id, client_id);
create index if not exists ix_client_portal_users_email on client_portal_users (lower(email));

-- ---------------------------------------------------------------- client service requests

create table if not exists service_requests (
  id                uuid primary key,
  firm_id           uuid not null references firms(id),
  client_id         uuid not null,
  portal_user_id    uuid,
  request_number    text not null,
  category          text not null check (category in ('tax_filing','notice_reply','certification','advisory','billing_query','general')),
  subject           text not null check (length(subject) between 3 and 250),
  description       text not null check (length(description) <= 5000),
  priority          text not null default 'medium' check (priority in ('low','medium','high','urgent')),
  status            text not null default 'submitted' check (status in ('submitted','in_progress','resolved','closed')),
  assigned_user_id  uuid,
  resolution_notes  text check (resolution_notes is null or length(resolution_notes) <= 4000),
  resolved_at       timestamptz,
  created_at        timestamptz not null default now(),
  updated_at        timestamptz,
  constraint ux_service_requests_id_firm unique (id, firm_id),
  constraint ux_service_requests_num unique (firm_id, request_number),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade,
  foreign key (portal_user_id, firm_id) references client_portal_users(id, firm_id) on delete set null (portal_user_id),
  foreign key (assigned_user_id, firm_id) references users(id, firm_id) on delete set null (assigned_user_id)
);

create index if not exists ix_service_requests_firm_client on service_requests (firm_id, client_id);
create index if not exists ix_service_requests_status on service_requests (firm_id, status);

-- ---------------------------------------------------------------- request messages / timeline

create table if not exists service_request_messages (
  id                uuid primary key,
  firm_id           uuid not null references firms(id),
  request_id        uuid not null,
  sender_type       text not null check (sender_type in ('client','staff')),
  sender_name       text not null,
  sender_user_id    uuid,
  message           text not null check (length(message) between 1 and 4000),
  attachment_doc_id uuid,
  created_at        timestamptz not null default now(),
  constraint ux_service_request_messages_id_firm unique (id, firm_id),
  foreign key (request_id, firm_id) references service_requests(id, firm_id) on delete cascade
);

create index if not exists ix_service_request_messages_req on service_request_messages (request_id, created_at);

-- ---------------------------------------------------------------- row-level security

do $$
declare t text;
begin
  foreach t in array array['client_portal_users','service_requests','service_request_messages']
  loop
    execute format('alter table %I enable row level security', t);
    execute format('drop policy if exists tenant_isolation on %I', t);
    execute format(
      'create policy tenant_isolation on %I using (firm_id = app_current_firm()) with check (firm_id = app_current_firm())', t);
  end loop;
end $$;

grant select, insert, update, delete on client_portal_users, service_requests, service_request_messages to cadesk_app;

-- ---------------------------------------------------------------- security definer lookup for login

create or replace function auth_find_portal_user(p_email text)
returns table (id uuid, firm_id uuid, client_id uuid, email text, name text, phone text,
               password_hash text, role text, is_active boolean, client_name text)
language sql stable security definer set search_path = public, pg_temp as $$
  select u.id, u.firm_id, u.client_id, u.email, u.name, u.phone, u.password_hash, u.role, u.is_active, c.name as client_name
  from client_portal_users u
  join clients c on c.id = u.client_id
  where lower(u.email) = lower(p_email)
  limit 1
$$;

revoke all on function auth_find_portal_user(text) from public;
grant execute on function auth_find_portal_user(text) to cadesk_app;

-- ---------------------------------------------------------------- permissions

insert into permissions (code, area, description) values
  ('portal.read',              'portal', 'View client portal users and activity'),
  ('portal.manage',            'portal', 'Invite and manage client portal user accounts'),
  ('service_requests.manage',  'portal', 'Manage, assign and resolve client service requests')
on conflict (code) do nothing;
