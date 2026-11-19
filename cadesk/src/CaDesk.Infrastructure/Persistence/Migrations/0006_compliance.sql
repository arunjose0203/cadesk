-- 0006_compliance: due-date rules per country (platform data), government extensions, holidays,
-- which compliances apply to each client, and the generated deadlines.
--
-- IMPORTANT: the seeded due dates reflect the rules known in Nov 2026. Government dates change by notification;
-- the platform team must keep compliance_types / compliance_extensions / holidays current. Code never hard-codes them.

-- weekly off days per country (0 = Sunday): used to move statutory due dates that fall on a closed day
alter table countries add column weekend_days smallint[] not null default '{0}';
update countries set weekend_days = '{0,6}' where code = 'AE';
update countries set weekend_days = '{5,6}' where code in ('SA','OM','BH','QA','KW');

-- the tax year the law uses for annual returns (India: April–March), independent of a firm's own books;
-- where the law lets a business pick its year (UAE corporate tax), it is set per client
alter table countries add column statutory_fy_start_month smallint not null default 1 check (statutory_fy_start_month between 1 and 12);
update countries set statutory_fy_start_month = 4 where code = 'IN';

-- ---------------------------------------------------------------- rules (platform data)

create table compliance_types (
  code              text primary key,                -- GSTR3B_M, TDS_PAY, AE_VAT ...
  country_code      char(2) not null references countries(code),
  name              text not null,
  authority         text not null,                   -- GSTN, Income Tax Dept, FTA, ZATCA
  description       text,
  frequencies       text[] not null check (frequencies <@ array['monthly','quarterly','annual']::text[] and cardinality(frequencies) > 0),
  default_frequency text not null,
  -- due date = (period end month + due_months_after), day due_day (31 = last day of that month)
  --         or period end + due_days_after days (when set)
  due_months_after  smallint not null default 1 check (due_months_after between 0 and 24),
  due_day           smallint not null default 31 check (due_day between 1 and 31),
  due_days_after    smallint check (due_days_after between 1 and 400),
  -- exceptions keyed by the period's end month, e.g. {"3": {"monthsAfter": 1, "day": 30}} (March TDS paid by 30 April)
  period_overrides  jsonb not null default '{}',
  -- true: a due date on a weekend / public holiday moves to the next working day
  shift_on_holiday  boolean not null default false,
  -- who it applies to: {"taxId": "GSTIN", "entityTypes": [...], "excludeEntityTypes": [...]}
  applies_when      jsonb not null default '{}',
  auto_apply        boolean not null default true,   -- false: a firm turns it on per client
  is_active         boolean not null default true,
  sort_order        int not null default 0,
  check (default_frequency = any(frequencies))
);

-- a government extension for one period ("GSTR-3B for Oct 2026 extended to 25 Nov")
create table compliance_extensions (
  type_code     text not null references compliance_types(code),
  period_key    text not null,                       -- M2026-10, Q2026-12, FY2026-27
  extended_due  date not null,
  reason        text not null,                       -- notification number / circular
  created_at    timestamptz not null default now(),
  primary key (type_code, period_key)
);

-- public holidays (firm_id null, platform data) and the firm's own closed days (firm_id set)
create table holidays (
  id           uuid primary key,
  country_code char(2) not null references countries(code),
  firm_id      uuid references firms(id),
  holiday_date date not null,
  name         text not null check (length(name) between 1 and 100),
  created_at   timestamptz not null default now()
);
create unique index ux_holidays_public on holidays (country_code, holiday_date) where firm_id is null;
create unique index ux_holidays_firm on holidays (firm_id, holiday_date) where firm_id is not null;

-- ---------------------------------------------------------------- per client choices

-- Only differences from the rules are stored: switching an automatic compliance off, turning a manual one on,
-- or choosing the frequency / quarter pattern / financial year.
create table client_compliances (
  id                uuid primary key,
  firm_id           uuid not null references firms(id),
  client_id         uuid not null,
  type_code         text not null references compliance_types(code),
  is_enabled        boolean not null,
  frequency         text check (frequency in ('monthly','quarterly','annual')),
  quarter_end_month smallint check (quarter_end_month in (1,2,3)),  -- quarters end Jan/Apr.., Feb/May.., Mar/Jun..
  fy_start_month    smallint check (fy_start_month between 1 and 12),
  created_at        timestamptz not null default now(),
  constraint ux_client_compliances unique (client_id, type_code),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade
);

-- ---------------------------------------------------------------- deadlines

create table deadlines (
  id               uuid primary key,
  firm_id          uuid not null references firms(id),
  client_id        uuid not null,
  type_code        text not null references compliance_types(code),
  period_key       text not null,
  period_start     date not null,
  period_end       date not null,
  statutory_due    date not null,          -- by the rule (or a government extension)
  due_date         date not null,          -- after moving off weekends / public holidays
  internal_due     date not null,          -- the firm's own target, on a working day
  status           text not null default 'pending' check (status in ('pending','in_progress','filed','not_applicable')),
  assigned_user_id uuid,
  filed_on         date,
  ack_no           text check (length(ack_no) <= 100),
  remarks          text check (length(remarks) <= 1000),
  created_at       timestamptz not null default now(),
  created_by       uuid,
  updated_at       timestamptz,
  updated_by       uuid,
  constraint ux_deadlines_period unique (client_id, type_code, period_key),
  constraint ck_deadlines_filed check (status <> 'filed' or filed_on is not null),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade,
  foreign key (assigned_user_id, firm_id) references users(id, firm_id)
);
create index ix_deadlines_open on deadlines (firm_id, due_date) where status in ('pending','in_progress');
create index ix_deadlines_client on deadlines (client_id, due_date);

-- ---------------------------------------------------------------- row-level security

alter table client_compliances enable row level security;
create policy tenant_isolation on client_compliances
  using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());

alter table deadlines enable row level security;
create policy tenant_isolation on deadlines
  using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());

-- everyone sees public holidays; a firm sees and changes only its own
alter table holidays enable row level security;
create policy holidays_read on holidays for select using (firm_id is null or firm_id = app_current_firm());
create policy holidays_write on holidays for insert with check (firm_id = app_current_firm());
create policy holidays_delete on holidays for delete using (firm_id = app_current_firm());

grant select on compliance_types, compliance_extensions to cadesk_app;
grant select, insert, delete on holidays to cadesk_app;
grant select, insert, update, delete on client_compliances, deadlines to cadesk_app;

-- deadlines a year ahead by default, so a new client shows its whole year
update config_definitions
   set json_schema = '{"type":"integer","minimum":7,"maximum":400}', default_value = '365'
 where key = 'compliance.generate_ahead_days';

-- ---------------------------------------------------------------- seed: rules

insert into compliance_types (code, country_code, name, authority, description, frequencies, default_frequency,
  due_months_after, due_day, due_days_after, period_overrides, shift_on_holiday, applies_when, auto_apply, sort_order) values
-- India: GST (online returns; due dates don't move for holidays unless extended)
('GSTR1_M',   'IN', 'GSTR-1 (monthly)',          'GSTN', 'Outward supplies',                      '{monthly}',   'monthly',   1, 11, null, '{}', false, '{"taxId":"GSTIN"}', true,  10),
('GSTR3B_M',  'IN', 'GSTR-3B (monthly)',         'GSTN', 'Summary return and tax payment',        '{monthly}',   'monthly',   1, 20, null, '{}', false, '{"taxId":"GSTIN"}', true,  20),
('GSTR1_Q',   'IN', 'GSTR-1 (QRMP quarterly)',   'GSTN', 'Outward supplies, QRMP scheme',         '{quarterly}', 'quarterly', 1, 13, null, '{}', false, '{"taxId":"GSTIN"}', false, 30),
('GSTR3B_Q',  'IN', 'GSTR-3B (QRMP quarterly)',  'GSTN', 'QRMP: 22nd or 24th by state; 22nd used to be safe', '{quarterly}', 'quarterly', 1, 22, null, '{}', false, '{"taxId":"GSTIN"}', false, 40),
('GSTR9',     'IN', 'GSTR-9 annual return',      'GSTN', 'Annual return',                         '{annual}',    'annual',    9, 31, null, '{}', false, '{"taxId":"GSTIN"}', true,  50),
-- India: TDS and income tax
('TDS_PAY',   'IN', 'TDS payment',               'Income Tax Dept', 'Deposit of tax deducted; March by 30 April', '{monthly}', 'monthly', 1, 7, null,
  '{"3":{"monthsAfter":1,"day":30}}', true, '{"taxId":"TAN"}', true, 60),
('TDS_RETURN','IN', 'TDS/TCS quarterly returns',  'Income Tax Dept', 'Quarterly statements; Jan–Mar quarter by 31 May', '{quarterly}', 'quarterly', 1, 31, null,
  '{"3":{"monthsAfter":2,"day":31}}', true, '{"taxId":"TAN"}', true, 70),
('ADV_TAX',   'IN', 'Advance tax instalment',    'Income Tax Dept', '15 Jun, 15 Sep, 15 Dec, 15 Mar',  '{quarterly}', 'quarterly', 0, 15, null, '{}', true,
  '{"entityTypes":["Company","LLP","Firm"]}', true, 80),
('ITR',       'IN', 'Income tax return (no business / audit)', 'Income Tax Dept', 'Due 31 July', '{annual}', 'annual', 4, 31, null, '{}', true,
  '{"entityTypes":["Individual","HUF"]}', true, 90),
('ITR_BUS',   'IN', 'Income tax return (business, no audit)', 'Income Tax Dept', 'Due 31 August (from tax year 2025-26)', '{annual}', 'annual', 5, 31, null, '{}', true,
  '{"entityTypes":["Firm","LLP"]}', true, 100),
('TAX_AUDIT', 'IN', 'Tax audit report',          'Income Tax Dept', 'Due 30 September',              '{annual}',    'annual',    6, 30, null, '{}', true, '{}', false, 110),
('ITR_AUDIT', 'IN', 'Income tax return (audit cases)', 'Income Tax Dept', 'Due 31 October',          '{annual}',    'annual',    7, 31, null, '{}', true,
  '{"entityTypes":["Company"]}', true, 120),
-- UAE
('AE_VAT',    'AE', 'VAT return',                'FTA', 'By the 28th day after the tax period ends', '{quarterly,monthly}', 'quarterly', 1, 28, null, '{}', true, '{"taxId":"TRN"}', true, 10),
('AE_CT',     'AE', 'Corporate tax return',      'FTA', 'Within 9 months of the financial year end',  '{annual}', 'annual', 9, 31, null, '{}', true,
  '{"excludeEntityTypes":["Individual"]}', true, 20),
-- Saudi Arabia
('SA_VAT',    'SA', 'VAT return',                'ZATCA', 'By the last day of the month after the period', '{quarterly,monthly}', 'quarterly', 1, 31, null, '{}', true, '{"taxId":"VAT"}', true, 10),
('SA_ZAKAT',  'SA', 'Zakat / income tax return', 'ZATCA', 'Within 120 days of the financial year end',     '{annual}', 'annual', 0, 31, 120, '{}', true,
  '{"excludeEntityTypes":["Individual"]}', true, 20);

-- ---------------------------------------------------------------- seed: fixed-date public holidays 2026–2028
-- Festival holidays (Diwali, Eid ...) change every year: the platform team adds them from the official lists.

insert into holidays (id, country_code, firm_id, holiday_date, name)
select gen_random_uuid(), h.country, null, make_date(y, h.m, h.d), h.name
from generate_series(2026, 2028) y
cross join (values
  ('IN', 1, 26, 'Republic Day'), ('IN', 8, 15, 'Independence Day'), ('IN', 10, 2, 'Gandhi Jayanti'),
  ('AE', 1, 1, 'New Year''s Day'), ('AE', 12, 2, 'National Day'), ('AE', 12, 3, 'National Day holiday'),
  ('SA', 2, 22, 'Founding Day'), ('SA', 9, 23, 'National Day')
) as h(country, m, d, name);
