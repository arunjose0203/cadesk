-- 0001_initial: reference data, firms, clients, row-level security.
-- Runs as the database owner (cadesk_owner). The API connects as cadesk_app,
-- which is not a table owner, so row-level security always applies to it.

create extension if not exists citext;

-- current firm for this connection, set by TenantConnectionInterceptor; null when not set
create or replace function app_current_firm() returns uuid
language sql stable as $$
  select nullif(current_setting('app.firm_id', true), '')::uuid
$$;

-- ---------------------------------------------------------------- reference data

create table countries (
  code              char(2) primary key,
  name              text not null,
  currency          char(3) not null,
  default_time_zone text not null,
  default_locale    text not null,
  data_region       text not null,
  week_start        smallint not null default 1,
  is_active         boolean not null default false
);

insert into countries (code, name, currency, default_time_zone, default_locale, data_region, week_start, is_active) values
  ('IN', 'India',        'INR', 'Asia/Kolkata', 'en-IN', 'in', 1, true),
  ('AE', 'United Arab Emirates', 'AED', 'Asia/Dubai', 'en-AE', 'ae', 1, false),
  ('SA', 'Saudi Arabia', 'SAR', 'Asia/Riyadh',  'ar-SA', 'sa', 0, false),
  ('OM', 'Oman',         'OMR', 'Asia/Muscat',  'ar-OM', 'ae', 0, false),
  ('BH', 'Bahrain',      'BHD', 'Asia/Bahrain', 'ar-BH', 'ae', 0, false),
  ('QA', 'Qatar',        'QAR', 'Asia/Qatar',   'ar-QA', 'ae', 0, false),
  ('KW', 'Kuwait',       'KWD', 'Asia/Kuwait',  'ar-KW', 'ae', 0, false);

create table plans (
  code       text primary key,
  name       text not null,
  sort_order int not null,
  is_active  boolean not null default true
);

insert into plans (code, name, sort_order) values
  ('STARTER', 'Starter', 1), ('GROWTH', 'Growth', 2), ('PRO', 'Pro', 3);

-- ---------------------------------------------------------------- firms (tenants)

create table firms (
  id             uuid primary key,
  name           text not null,
  legal_name     text,
  country_code   char(2) not null references countries(code),
  data_region    text not null,
  base_currency  char(3) not null,
  time_zone      text not null,
  plan_code      text not null references plans(code),
  status         text not null default 'trial'
                 check (status in ('trial','active','suspended','closed')),
  subdomain      text unique,
  tax_ids        jsonb not null default '{}',
  config_version int not null default 1,
  trial_ends_at  timestamptz,
  created_at     timestamptz not null default now(),
  updated_at     timestamptz
);

-- ---------------------------------------------------------------- clients

create table clients (
  id               uuid primary key,
  firm_id          uuid not null references firms(id),
  branch_id        uuid,                 -- FK to firm_branches added in a later migration
  group_id         uuid,                 -- FK to client_groups added in a later migration
  code             text not null,
  name             text not null,
  legal_name       text,
  country_code     char(2) not null references countries(code),
  entity_type      text not null,
  tax_ids          jsonb not null default '{}',
  assigned_user_id uuid,                 -- FK to users added in Sprint 2
  email            citext,
  phone            text,
  custom           jsonb not null default '{}',
  is_active        boolean not null default true,
  created_at       timestamptz not null default now(),
  created_by       uuid,
  updated_at       timestamptz,
  updated_by       uuid,
  deleted_at       timestamptz,
  constraint ux_clients_firm_code unique (firm_id, code)
);
create index ix_clients_firm_name on clients (firm_id, name);
create index ix_clients_tax_ids on clients using gin (tax_ids);
create index ix_clients_custom on clients using gin (custom);

-- ---------------------------------------------------------------- row-level security

alter table firms enable row level security;
create policy firm_self on firms
  using (id = app_current_firm());

alter table clients enable row level security;
create policy tenant_isolation on clients
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

-- ---------------------------------------------------------------- privileges for the app role

grant usage on schema public to cadesk_app;
grant select on countries, plans to cadesk_app;
grant select, update on firms to cadesk_app;
grant select, insert, update, delete on clients to cadesk_app;
grant execute on function app_current_firm() to cadesk_app;
