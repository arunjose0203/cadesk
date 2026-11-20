-- 0028_presumptive_taxation.sql: Presumptive Taxation & Minimal Financials Generator (Section 44AD / 44ADA / 44AE)

create table if not exists presumptive_tax_computations (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  client_id                       uuid not null references clients(id) on delete cascade,
  financial_year                  text not null, -- e.g. "2024-25"
  assessment_year                 text not null, -- e.g. "2025-26"
  section                         text not null, -- "44AD", "44ADA", "44AE"
  entity_type                     text not null default 'Individual', -- Individual, HUF, Firm
  business_nature_code            text not null default '',
  trade_name                      text not null default '',

  -- Section 44AD / 44ADA fields
  gross_turnover                  numeric(14,2) not null default 0,
  digital_turnover                numeric(14,2) not null default 0,
  cash_turnover                   numeric(14,2) not null default 0,
  digital_percentage              numeric(5,2) not null default 0,
  is_digital_threshold_eligible   boolean not null default false,
  turnover_limit                  numeric(14,2) not null default 20000000,
  is_turnover_within_limit        boolean not null default true,
  deemed_profit_digital_rate      numeric(5,2) not null default 6.00,
  deemed_profit_cash_rate         numeric(5,2) not null default 8.00,
  minimum_deemed_profit           numeric(14,2) not null default 0,
  declared_profit                 numeric(14,2) not null default 0,

  -- Section 44AD(4) 5-Year Lockout Tracking
  opted_out_prior_year            boolean not null default false,
  opt_out_assessment_year         text,
  is_lockout_active               boolean not null default false,
  lockout_expiry_ay               text,

  -- Section 44AB Audit Trigger Checks
  is_audit_mandatory              boolean not null default false,
  audit_trigger_reason            text,

  -- Section 44AA Minimal Financials (ITR-4 Particulars)
  sundry_debtors                  numeric(14,2) not null default 0,
  sundry_creditors                numeric(14,2) not null default 0,
  stock_in_trade                  numeric(14,2) not null default 0,
  cash_balance                    numeric(14,2) not null default 0,
  bank_balance                    numeric(14,2) not null default 0,
  fixed_assets                    numeric(14,2) not null default 0,
  other_assets                    numeric(14,2) not null default 0,
  partner_or_proprietor_capital   numeric(14,2) not null default 0,
  loans_and_borrowings            numeric(14,2) not null default 0,

  -- Status & Lifecycle
  status                          text not null default 'draft', -- draft, computed, approved, filed_itr4
  notes                           text,
  created_by_user_id              uuid references users(id) on delete set null,
  approved_by_user_id             uuid references users(id) on delete set null,
  approved_at                     timestamptz,
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_presumptive_computations_client_fy
  on presumptive_tax_computations (firm_id, client_id, financial_year);

alter table presumptive_tax_computations enable row level security;
alter table presumptive_tax_computations force row level security;
drop policy if exists tenant_isolation on presumptive_tax_computations;
create policy tenant_isolation on presumptive_tax_computations
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on presumptive_tax_computations to cadesk_app;

-- Section 44AE Goods Carriage Vehicles
create table if not exists presumptive_44ae_vehicles (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  computation_id                  uuid not null references presumptive_tax_computations(id) on delete cascade,
  registration_number             text not null,
  vehicle_type                    text not null default 'other', -- heavy, other
  gross_vehicle_weight_tons       numeric(6,2) not null default 0,
  months_owned                    integer not null default 12,
  rate_per_month                  numeric(10,2) not null default 7500,
  deemed_monthly_profit           numeric(12,2) not null default 0,
  deemed_annual_profit            numeric(12,2) not null default 0,
  declared_profit                 numeric(12,2) not null default 0,
  created_at                      timestamptz not null default now()
);

create index if not exists ix_presumptive_44ae_vehicles_computation
  on presumptive_44ae_vehicles (firm_id, computation_id);

alter table presumptive_44ae_vehicles enable row level security;
alter table presumptive_44ae_vehicles force row level security;
drop policy if exists tenant_isolation on presumptive_44ae_vehicles;
create policy tenant_isolation on presumptive_44ae_vehicles
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on presumptive_44ae_vehicles to cadesk_app;

-- Seed permissions
insert into permissions (code, area, description) values
  ('presumptive.read',    'tax', 'View presumptive tax computations and minimal financials (44AD/44ADA/44AE)'),
  ('presumptive.manage',  'tax', 'Create, update, and compute presumptive tax calculations and vehicles'),
  ('presumptive.approve', 'tax', 'Approve presumptive tax computations and mark ready for ITR-4')
on conflict (code) do nothing;

-- Grant to existing Partner roles
insert into role_permissions (role_id, permission_code, firm_id)
select r.id, p.code, r.firm_id
from roles r
cross join (values
  ('presumptive.read'),
  ('presumptive.manage'),
  ('presumptive.approve')
) as p(code)
where r.name = 'Partner'
on conflict do nothing;
