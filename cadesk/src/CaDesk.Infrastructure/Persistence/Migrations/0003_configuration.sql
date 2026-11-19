-- 0003_configuration: settings catalogue, values at six levels, change log, secrets.
--
-- Levels (scope): 0 platform · 1 country · 2 plan · 3 firm · 4 client · 5 user.
-- The most specific value wins, unless a higher level has LOCKED it.
-- Firms can only write levels 3-5 for themselves; platform/country/plan values come from
-- migrations or the platform console (owner role).

-- ---------------------------------------------------------------- global version (platform/country/plan changes)

create table platform_state (
  id             smallint primary key default 1 check (id = 1),
  config_version int not null default 1
);
insert into platform_state default values;

-- ---------------------------------------------------------------- catalogue

create table config_definitions (
  key               text primary key,
  area              text not null,
  value_type        text not null check (value_type in ('string','int','decimal','bool','list','secret_ref')),
  json_schema       jsonb not null,         -- subset of JSON Schema: type, enum, minimum, maximum, minLength,
                                            -- maxLength, pattern, items, minItems, maxItems, uniqueItems
  default_value     jsonb not null,
  lowest_scope      smallint not null check (lowest_scope between 0 and 5),
  plan_value_is_cap boolean not null default false,   -- plan-level value is a ceiling, not a default
  labels            jsonb not null,         -- {"en": "..."}
  help_text         jsonb,
  sort_order        int not null default 0,
  is_active         boolean not null default true
);

-- ---------------------------------------------------------------- values

create table config_values (
  id         uuid primary key,
  key        text not null references config_definitions(key),
  scope      smallint not null check (scope between 0 and 5),
  scope_ref  text not null,                 -- '*', country code, plan code, firm/client/user id
  firm_id    uuid references firms(id),     -- null for platform, country, plan
  value      jsonb not null,
  is_locked  boolean not null default false,
  updated_by uuid,
  updated_at timestamptz not null default now(),
  constraint ux_config_values unique (key, scope, scope_ref),
  constraint ck_config_values_firm check ((scope <= 2) = (firm_id is null))
);
create index ix_config_values_firm on config_values (firm_id) where firm_id is not null;

create table config_change_log (
  id         uuid primary key,
  firm_id    uuid references firms(id),
  key        text not null,
  scope      smallint not null,
  scope_ref  text not null,
  old_value  jsonb,                         -- null = was inherited
  new_value  jsonb,                         -- null = reset to inherited
  old_locked boolean not null default false,
  new_locked boolean not null default false,
  reason     text,
  changed_by uuid,
  changed_at timestamptz not null default now()
);
create index ix_config_change_log_firm on config_change_log (firm_id, changed_at desc);

create table secrets (
  id          uuid primary key,
  firm_id     uuid not null references firms(id),
  name        text not null,
  ciphertext  text not null,                -- ASP.NET Data Protection output; plaintext never stored
  created_by  uuid,
  created_at  timestamptz not null default now(),
  rotated_at  timestamptz,
  constraint ux_secrets_firm_name unique (firm_id, name)
);

-- ---------------------------------------------------------------- cache invalidation
-- Any change bumps the firm's config_version (or the global one), so every API server
-- sees a new cache key on its next request.

create function config_bump_version() returns trigger
language plpgsql security definer set search_path = public, pg_temp as $$
declare f uuid := coalesce(new.firm_id, old.firm_id);
begin
  if f is null then
    update platform_state set config_version = config_version + 1 where id = 1;
  else
    update firms set config_version = config_version + 1 where id = f;
  end if;
  return null;
end $$;

create trigger trg_config_values_version
after insert or update or delete on config_values
for each row execute function config_bump_version();

-- catalogue changes (new keys, new rules) also refresh every server's cache
create function config_bump_platform_version() returns trigger
language plpgsql security definer set search_path = public, pg_temp as $$
begin
  update platform_state set config_version = config_version + 1 where id = 1;
  return null;
end $$;

create trigger trg_config_definitions_version
after insert or update or delete on config_definitions
for each statement execute function config_bump_platform_version();

-- ---------------------------------------------------------------- row-level security

alter table config_values enable row level security;
create policy read_visible on config_values for select
  using (firm_id is null or firm_id = app_current_firm());
create policy write_own_insert on config_values for insert
  with check (firm_id = app_current_firm() and scope >= 3);
create policy write_own_update on config_values for update
  using (firm_id = app_current_firm()) with check (firm_id = app_current_firm() and scope >= 3);
create policy write_own_delete on config_values for delete
  using (firm_id = app_current_firm());

alter table config_change_log enable row level security;
create policy tenant_isolation on config_change_log
  using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());

alter table secrets enable row level security;
create policy tenant_isolation on secrets
  using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());

grant select on platform_state, config_definitions to cadesk_app;
grant select, insert, update, delete on config_values, secrets to cadesk_app;
grant select, insert on config_change_log to cadesk_app;   -- the log is append-only for the app

-- ---------------------------------------------------------------- seed: settings catalogue
-- More keys are added by each module's migration.

insert into config_definitions (key, area, value_type, json_schema, default_value, lowest_scope, plan_value_is_cap, labels, sort_order) values
-- general
('firm.time_zone',                 'general', 'string', '{"type":"string","enum":["Asia/Kolkata","Asia/Dubai","Asia/Riyadh","Asia/Muscat","Asia/Bahrain","Asia/Qatar","Asia/Kuwait"]}', '"Asia/Kolkata"', 3, false, '{"en":"Time zone"}', 10),
('firm.fiscal_year_start_month',   'general', 'int',    '{"type":"integer","minimum":1,"maximum":12}', '4', 3, false, '{"en":"Financial year starts (month)"}', 20),
('firm.working_days',              'general', 'list',   '{"type":"array","items":{"type":"integer","minimum":0,"maximum":6},"minItems":1,"maxItems":7,"uniqueItems":true}', '[1,2,3,4,5,6]', 3, false, '{"en":"Working days (0 = Sunday)"}', 30),
('firm.date_format',               'general', 'string', '{"type":"string","enum":["dd-MM-yyyy","dd/MM/yyyy","yyyy-MM-dd"]}', '"dd-MM-yyyy"', 5, false, '{"en":"Date format"}', 40),
('ui.language',                    'general', 'string', '{"type":"string","enum":["en","ar","hi","ml"]}', '"en"', 5, false, '{"en":"Language"}', 50),
-- clients
('client.code_format',             'clients', 'string', '{"type":"string","minLength":3,"maxLength":40,"pattern":"\\{SEQ(:[1-9])?\\}"}', '"C{SEQ:4}"', 3, false, '{"en":"Client code format"}', 10),
('client.entity_types',            'clients', 'list',   '{"type":"array","items":{"type":"string","minLength":2,"maxLength":40},"minItems":1,"maxItems":30,"uniqueItems":true}', '["Individual","HUF","Firm","LLP","Company","Trust"]', 3, false, '{"en":"Entity types offered"}', 20),
('client.required_fields',         'clients', 'list',   '{"type":"array","items":{"type":"string","enum":["email","phone","PAN","GSTIN","TAN","TRN"]},"uniqueItems":true}', '["PAN"]', 3, false, '{"en":"Required client fields"}', 30),
-- documents
('doc.allowed_types',              'documents', 'list', '{"type":"array","items":{"type":"string","enum":["pdf","jpg","png","xlsx","xls","csv","docx"]},"minItems":1,"uniqueItems":true}', '["pdf","jpg","png","xlsx"]', 3, false, '{"en":"Allowed file types"}', 10),
('doc.max_size_mb',                'documents', 'int',  '{"type":"integer","minimum":1,"maximum":50}', '15', 3, false, '{"en":"Maximum file size (MB)"}', 20),
('doc.retention_years',            'documents', 'int',  '{"type":"integer","minimum":1,"maximum":20}', '8', 3, false, '{"en":"Keep documents for (years)"}', 30),
('doc.client_upload_enabled',      'documents', 'bool', '{"type":"boolean"}', 'true', 4, false, '{"en":"Clients can upload"}', 40),
-- AI
('ai.enabled',                     'ai', 'bool', '{"type":"boolean"}', 'true', 3, false, '{"en":"AI extraction on"}', 10),
('ai.auto_extract',                'ai', 'bool', '{"type":"boolean"}', 'true', 3, false, '{"en":"Extract automatically on upload"}', 20),
('ai.monthly_page_quota',          'ai', 'int',  '{"type":"integer","minimum":0,"maximum":100000}', '200', 3, true, '{"en":"AI pages per month"}', 30),
('ai.review_required',             'ai', 'bool', '{"type":"boolean"}', 'true', 3, false, '{"en":"Staff review before data is used"}', 40),
-- compliance
('compliance.generate_ahead_days', 'compliance', 'int', '{"type":"integer","minimum":7,"maximum":120}', '45', 3, false, '{"en":"Create deadlines this many days ahead"}', 10),
('compliance.internal_due_offset_days', 'compliance', 'int', '{"type":"integer","minimum":0,"maximum":30}', '3', 4, false, '{"en":"Finish this many days before the due date"}', 20),
-- reminders
('reminder.schedule_days',         'reminders', 'list',   '{"type":"array","items":{"type":"integer","minimum":0,"maximum":60},"minItems":1,"maxItems":6,"uniqueItems":true}', '[7,3,0]', 4, false, '{"en":"Remind clients this many days before"}', 10),
('reminder.channels',              'reminders', 'list',   '{"type":"array","items":{"type":"string","enum":["email","whatsapp","sms"]},"minItems":1,"uniqueItems":true}', '["email"]', 4, false, '{"en":"Reminder channels"}', 20),
('reminder.quiet_start',           'reminders', 'string', '{"type":"string","pattern":"^([01][0-9]|2[0-3]):[0-5][0-9]$"}', '"21:00"', 4, false, '{"en":"No reminders after"}', 30),
('reminder.quiet_end',             'reminders', 'string', '{"type":"string","pattern":"^([01][0-9]|2[0-3]):[0-5][0-9]$"}', '"08:00"', 4, false, '{"en":"No reminders before"}', 40),
('reminder.escalate_after_days',   'reminders', 'int',    '{"type":"integer","minimum":0,"maximum":30}', '2', 3, false, '{"en":"Tell the partner after (days overdue)"}', 50),
-- tasks
('task.auto_assign_rule',          'tasks', 'string', '{"type":"string","enum":["client_staff","round_robin","unassigned"]}', '"client_staff"', 3, false, '{"en":"Assign new tasks to"}', 10),
('task.review_required',           'tasks', 'bool',   '{"type":"boolean"}', 'true', 3, false, '{"en":"Partner review before filing"}', 20),
-- tax
('tax.default_regime',             'tax', 'string', '{"type":"string","enum":["new","old"]}', '"new"', 4, false, '{"en":"Default income-tax regime"}', 10),
('tax.override_needs_partner',     'tax', 'bool',   '{"type":"boolean"}', 'true', 3, false, '{"en":"Overrides in computations need partner approval"}', 20),
-- billing
('billing.invoice_number_format',  'billing', 'string',  '{"type":"string","minLength":3,"maxLength":40,"pattern":"\\{SEQ(:[1-9])?\\}"}', '"INV/{FY}/{SEQ:5}"', 3, false, '{"en":"Invoice number format"}', 10),
('billing.payment_terms_days',     'billing', 'int',     '{"type":"integer","minimum":0,"maximum":120}', '15', 4, false, '{"en":"Payment terms (days)"}', 20),
('billing.fee_tax_rate',           'billing', 'decimal', '{"type":"number","minimum":0,"maximum":100}', '0', 3, false, '{"en":"Tax on professional fees (%)"}', 30),
-- e-invoicing / integrations
('einv.enabled',                   'einvoicing', 'bool',       '{"type":"boolean"}', 'false', 4, false, '{"en":"E-invoicing on"}', 10),
('einv.environment',               'einvoicing', 'string',     '{"type":"string","enum":["sandbox","production"]}', '"sandbox"', 4, false, '{"en":"E-invoicing environment"}', 20),
('einv.credentials_ref',           'einvoicing', 'secret_ref', '{"type":"string","pattern":"^[a-z0-9_.-]{1,60}$"}', '""', 4, false, '{"en":"E-invoicing credentials (secret name)"}', 30),
('whatsapp.credentials_ref',       'integrations', 'secret_ref', '{"type":"string","pattern":"^[a-z0-9_.-]{1,60}$"}', '""', 3, false, '{"en":"WhatsApp API token (secret name)"}', 10),
-- security
('security.session_timeout_min',   'security', 'int',  '{"type":"integer","minimum":5,"maximum":480}', '30', 3, false, '{"en":"Sign out after inactivity (minutes)"}', 10),
('security.password_min_length',   'security', 'int',  '{"type":"integer","minimum":10,"maximum":64}', '10', 3, false, '{"en":"Minimum password length"}', 20),
('security.mfa_required_roles',    'security', 'list', '{"type":"array","items":{"type":"string","maxLength":60},"uniqueItems":true}', '[]', 3, false, '{"en":"Roles that must use two-step sign-in"}', 30),
('security.data_region',           'security', 'string', '{"type":"string","enum":["in","ae","sa"]}', '"in"', 1, false, '{"en":"Data region"}', 40);

-- ---------------------------------------------------------------- seed: platform, country and plan values

insert into config_values (id, key, scope, scope_ref, firm_id, value, is_locked) values
  -- platform: staff must always review AI output before it's used
  (gen_random_uuid(), 'ai.review_required',          0, '*',  null, 'true', true),
  -- India
  (gen_random_uuid(), 'firm.time_zone',              1, 'IN', null, '"Asia/Kolkata"', false),
  (gen_random_uuid(), 'firm.fiscal_year_start_month',1, 'IN', null, '4', false),
  (gen_random_uuid(), 'billing.fee_tax_rate',        1, 'IN', null, '18', false),
  (gen_random_uuid(), 'security.data_region',        1, 'IN', null, '"in"', true),
  -- UAE
  (gen_random_uuid(), 'firm.time_zone',              1, 'AE', null, '"Asia/Dubai"', false),
  (gen_random_uuid(), 'firm.fiscal_year_start_month',1, 'AE', null, '1', false),
  (gen_random_uuid(), 'firm.working_days',           1, 'AE', null, '[1,2,3,4,5]', false),
  (gen_random_uuid(), 'billing.fee_tax_rate',        1, 'AE', null, '5', false),
  (gen_random_uuid(), 'client.entity_types',         1, 'AE', null, '["Individual","Sole Establishment","LLC","Free Zone Company","Branch"]', false),
  (gen_random_uuid(), 'client.required_fields',      1, 'AE', null, '["TRN"]', false),
  (gen_random_uuid(), 'security.data_region',        1, 'AE', null, '"ae"', true),
  -- Saudi Arabia
  (gen_random_uuid(), 'firm.time_zone',              1, 'SA', null, '"Asia/Riyadh"', false),
  (gen_random_uuid(), 'firm.fiscal_year_start_month',1, 'SA', null, '1', false),
  (gen_random_uuid(), 'firm.working_days',           1, 'SA', null, '[0,1,2,3,4]', false),
  (gen_random_uuid(), 'billing.fee_tax_rate',        1, 'SA', null, '15', false),
  (gen_random_uuid(), 'security.data_region',        1, 'SA', null, '"sa"', true),
  -- plan ceilings for AI pages (plan_value_is_cap)
  (gen_random_uuid(), 'ai.monthly_page_quota',       2, 'STARTER', null, '200', false),
  (gen_random_uuid(), 'ai.monthly_page_quota',       2, 'GROWTH',  null, '1000', false),
  (gen_random_uuid(), 'ai.monthly_page_quota',       2, 'PRO',     null, '5000', false);
