-- 0025_advance_tax: Advance Tax Planner, Self-Assessment & Section 234A/B/C Statutory Interest Engine.
-- Tracks quarterly installments (June 15, Sept 15, Dec 15, March 15), Sec 208 threshold (₹10,000),
-- Section 44AD/44ADA presumptive single installment, Section 234C installment deferment interest,
-- Section 234B shortfall (<90%) interest, Section 234A late filing interest, and ITNS 280 challans.

create table if not exists advance_tax_plans (
  id                            uuid primary key,
  firm_id                       uuid not null references firms(id),
  client_id                     uuid not null references clients(id) on delete cascade,
  financial_year                text not null, -- e.g. 2024-25
  assessment_year               text not null, -- e.g. 2025-26
  regime                        text not null default 'NewRegime', -- NewRegime, OldRegime
  is_presumptive                boolean not null default false,    -- Section 44AD / 44ADA (single March 15 installment)
  is_senior_citizen             boolean not null default false,    -- Section 207(2) exemption if no business income
  estimated_gross_income        numeric(14,2) not null default 0,
  estimated_deductions          numeric(14,2) not null default 0,
  estimated_taxable_income      numeric(14,2) not null default 0,
  estimated_tax_before_credits  numeric(14,2) not null default 0,
  estimated_tds_tcs             numeric(14,2) not null default 0,
  other_reliefs                 numeric(14,2) not null default 0,
  assessed_tax                  numeric(14,2) not null default 0, -- Net advance tax liability
  status                        text not null default 'Draft',     -- Draft, Active, Completed
  notes                         text,
  created_at                    timestamptz not null default now(),
  created_by                    uuid,
  updated_at                    timestamptz,
  updated_by                    uuid,
  constraint ux_advance_tax_plans_client_fy unique (firm_id, client_id, financial_year)
);

create index if not exists ix_advance_tax_plans_client on advance_tax_plans (firm_id, client_id);
create index if not exists ix_advance_tax_plans_fy on advance_tax_plans (firm_id, financial_year);

alter table advance_tax_plans enable row level security;
drop policy if exists tenant_isolation on advance_tax_plans;
create policy tenant_isolation on advance_tax_plans
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on advance_tax_plans to cadesk_app;

-- Quarterly Installments
create table if not exists advance_tax_installments (
  id                            uuid primary key,
  firm_id                       uuid not null references firms(id),
  plan_id                       uuid not null references advance_tax_plans(id) on delete cascade,
  installment_number            integer not null, -- 1 to 4
  due_date                      date not null,
  target_percentage             numeric(5,2) not null, -- 15, 45, 75, 100
  cumulative_target_amount      numeric(14,2) not null default 0,
  amount_paid                   numeric(14,2) not null default 0,
  shortfall_amount              numeric(14,2) not null default 0,
  interest_234c                 numeric(14,2) not null default 0,
  challan_bsr_code              text,
  challan_number                text,
  challan_date                  date,
  is_paid                       boolean not null default false,
  created_at                    timestamptz not null default now(),
  created_by                    uuid,
  updated_at                    timestamptz,
  updated_by                    uuid,
  constraint ux_advance_tax_installments unique (firm_id, plan_id, installment_number)
);

create index if not exists ix_advance_tax_installments_plan on advance_tax_installments (firm_id, plan_id);

alter table advance_tax_installments enable row level security;
drop policy if exists tenant_isolation on advance_tax_installments;
create policy tenant_isolation on advance_tax_installments
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on advance_tax_installments to cadesk_app;

-- Section 234A/B/C Statutory Interest Computations
create table if not exists tax_interest_computations (
  id                            uuid primary key,
  firm_id                       uuid not null references firms(id),
  plan_id                       uuid not null references advance_tax_plans(id) on delete cascade,
  assessment_year               text not null,
  assessed_tax                  numeric(14,2) not null default 0,
  advance_tax_paid              numeric(14,2) not null default 0,
  tds_tcs_credit                numeric(14,2) not null default 0,
  self_assessment_tax_paid      numeric(14,2) not null default 0,
  return_due_date               date not null,
  actual_return_filing_date     date not null,
  months_delayed_234a           integer not null default 0,
  interest_234a                 numeric(14,2) not null default 0,
  is_234b_applicable            boolean not null default false,
  months_delayed_234b           integer not null default 0,
  interest_234b                 numeric(14,2) not null default 0,
  interest_234c_total           numeric(14,2) not null default 0,
  total_statutory_interest      numeric(14,2) not null default 0,
  net_tax_payable               numeric(14,2) not null default 0,
  computed_at                   timestamptz not null default now(),
  created_at                    timestamptz not null default now(),
  created_by                    uuid,
  updated_at                    timestamptz,
  updated_by                    uuid
);

create index if not exists ix_tax_interest_computations_plan on tax_interest_computations (firm_id, plan_id);

alter table tax_interest_computations enable row level security;
drop policy if exists tenant_isolation on tax_interest_computations;
create policy tenant_isolation on tax_interest_computations
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on tax_interest_computations to cadesk_app;

-- Permissions Catalogue
insert into permissions (code, area, description) values
  ('advancetax.read', 'advancetax', 'View advance tax plans, quarterly installments, and interest computations'),
  ('advancetax.manage', 'advancetax', 'Create and modify advance tax estimates and record challan payments'),
  ('advancetax.calculate', 'advancetax', 'Run Section 234A, 234B, and 234C statutory interest engine')
on conflict (code) do nothing;
