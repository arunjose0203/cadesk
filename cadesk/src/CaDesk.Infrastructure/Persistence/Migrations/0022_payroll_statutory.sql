-- 0022_payroll_statutory: Payroll, Salary TDS & Statutory PF / ESIC Engine.
-- Tracks client employee master, monthly payroll runs, payslips with automated
-- EPF (12% + 8.33% EPS / 3.67% EPF split), ESIC (0.75% / 3.25%), State Professional Tax (PT),
-- and Section 192 Salary TDS deductions, plus EPFO ECR file generation.

create table if not exists payroll_employees (
  id                    uuid primary key,
  firm_id               uuid not null references firms(id),
  client_id             uuid not null references clients(id) on delete cascade,
  employee_code         text not null,
  name                  text not null,
  pan                   text,
  uan                   text,          -- 12-digit EPF Universal Account Number
  esic_number           text,          -- 17-digit ESIC Insurance Number
  date_of_joining       date not null,
  designation           text not null,
  department            text,
  state                 text not null default 'Maharashtra', -- Determines PT slabs
  tax_regime            text not null default 'NewRegime',    -- NewRegime, OldRegime
  monthly_ctc           numeric(12,2) not null default 0,
  basic_salary          numeric(12,2) not null default 0,
  hra                   numeric(12,2) not null default 0,
  special_allowance     numeric(12,2) not null default 0,
  other_allowances      numeric(12,2) not null default 0,
  is_pf_eligible        boolean not null default true,
  is_esi_eligible       boolean not null default true,
  is_pt_eligible        boolean not null default true,
  is_active             boolean not null default true,
  deleted_at            timestamptz,
  created_at            timestamptz not null default now(),
  created_by            uuid,
  updated_at            timestamptz,
  updated_by            uuid,
  constraint ux_payroll_employees_client_code unique (firm_id, client_id, employee_code)
);

create index if not exists ix_payroll_employees_firm_client on payroll_employees (firm_id, client_id);
create index if not exists ix_payroll_employees_pan on payroll_employees (firm_id, pan);

alter table payroll_employees enable row level security;
drop policy if exists tenant_isolation on payroll_employees;
create policy tenant_isolation on payroll_employees
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on payroll_employees to cadesk_app;

-- Monthly Payroll Runs
create table if not exists payroll_runs (
  id                    uuid primary key,
  firm_id               uuid not null references firms(id),
  client_id             uuid not null references clients(id) on delete cascade,
  payroll_period        text not null check (payroll_period ~ '^[0-9]{4}-[0-9]{2}$'), -- e.g. "2024-10"
  month                 integer not null check (month between 1 and 12),
  year                  integer not null,
  status                text not null default 'Draft', -- Draft, Approved, Disbursed, Cancelled
  total_employees       integer not null default 0,
  total_gross_pay       numeric(14,2) not null default 0,
  total_net_pay         numeric(14,2) not null default 0,
  total_employee_pf     numeric(14,2) not null default 0,
  total_employer_pf     numeric(14,2) not null default 0,
  total_employee_esi    numeric(14,2) not null default 0,
  total_employer_esi    numeric(14,2) not null default 0,
  total_pt              numeric(14,2) not null default 0,
  total_tds             numeric(14,2) not null default 0,
  processed_at          timestamptz not null default now(),
  processed_by_user_id  uuid references users(id) on delete set null,
  notes                 text,
  created_at            timestamptz not null default now(),
  created_by            uuid,
  updated_at            timestamptz,
  updated_by            uuid,
  constraint ux_payroll_runs_client_period unique (firm_id, client_id, payroll_period)
);

create index if not exists ix_payroll_runs_firm_client on payroll_runs (firm_id, client_id);

alter table payroll_runs enable row level security;
drop policy if exists tenant_isolation on payroll_runs;
create policy tenant_isolation on payroll_runs
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on payroll_runs to cadesk_app;

-- Employee Monthly Payslips
create table if not exists payroll_payslips (
  id                    uuid primary key,
  firm_id               uuid not null references firms(id),
  payroll_run_id        uuid not null references payroll_runs(id) on delete cascade,
  employee_id           uuid not null references payroll_employees(id) on delete cascade,
  employee_code         text not null,
  employee_name         text not null,
  pan                   text,
  uan                   text,
  days_worked           integer not null default 30,
  days_in_month         integer not null default 30,
  basic_salary          numeric(12,2) not null default 0,
  hra                   numeric(12,2) not null default 0,
  special_allowance     numeric(12,2) not null default 0,
  bonus_incentive       numeric(12,2) not null default 0,
  gross_salary          numeric(12,2) not null default 0,
  employee_pf           numeric(12,2) not null default 0,
  employee_esi          numeric(12,2) not null default 0,
  professional_tax      numeric(12,2) not null default 0,
  salary_tds            numeric(12,2) not null default 0,
  other_deductions      numeric(12,2) not null default 0,
  total_deductions      numeric(12,2) not null default 0,
  employer_eps          numeric(12,2) not null default 0,
  employer_epf          numeric(12,2) not null default 0,
  employer_esi          numeric(12,2) not null default 0,
  net_salary            numeric(12,2) not null default 0,
  created_at            timestamptz not null default now(),
  created_by            uuid,
  updated_at            timestamptz,
  updated_by            uuid
);

create index if not exists ix_payroll_payslips_run on payroll_payslips (firm_id, payroll_run_id);
create index if not exists ix_payroll_payslips_employee on payroll_payslips (firm_id, employee_id);

alter table payroll_payslips enable row level security;
drop policy if exists tenant_isolation on payroll_payslips;
create policy tenant_isolation on payroll_payslips
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on payroll_payslips to cadesk_app;

-- Permissions Catalogue
insert into permissions (code, area, description) values
  ('payroll.read', 'payroll', 'View employee salary structures, monthly payroll runs, payslips and statutory returns'),
  ('payroll.manage', 'payroll', 'Manage employee profiles, salary structures, and payroll settings'),
  ('payroll.process', 'payroll', 'Process monthly payroll runs, approve salary registers and export EPFO ECR / ESIC returns')
on conflict (code) do nothing;
