-- 0013_tax_computations: Income Tax Computation Engine (Direct Tax).
-- Computes Income across 5 Heads (Salary, House Property, PGBP, Capital Gains, Other Sources),
-- Chapter VI-A Deductions, Surcharge, 4% Health & Education Cess, TDS/Advance tax credits,
-- and automated Old vs New Tax Regime (Section 115BAC) comparative evaluation.

create table if not exists tax_computations (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  client_id                   uuid not null,
  assessment_year             text not null check (assessment_year ~ '^[0-9]{4}-[0-9]{2}$'),
  financial_year              text not null check (financial_year ~ '^[0-9]{4}-[0-9]{2}$'),
  taxpayer_category           text not null default 'individual' check (taxpayer_category in ('individual','senior_citizen','super_senior','huf','firm','company')),
  opted_regime                text not null default 'new' check (opted_regime in ('new','old')),
  status                      text not null default 'draft' check (status in ('draft','computed','approved','filed')),
  gross_salary                numeric(14,2) not null default 0,
  salary_deductions           numeric(14,2) not null default 0,
  net_salary                  numeric(14,2) not null default 0,
  house_property_income       numeric(14,2) not null default 0,
  business_income             numeric(14,2) not null default 0,
  capital_gains_stcg          numeric(14,2) not null default 0,
  capital_gains_ltcg          numeric(14,2) not null default 0,
  other_sources_income        numeric(14,2) not null default 0,
  gross_total_income          numeric(14,2) not null default 0,
  chapter_via_deductions      numeric(14,2) not null default 0,
  total_taxable_income        numeric(14,2) not null default 0,
  tax_on_income               numeric(14,2) not null default 0,
  rebate87_a                  numeric(14,2) not null default 0,
  surcharge                   numeric(14,2) not null default 0,
  cess                        numeric(14,2) not null default 0,
  total_tax_liability         numeric(14,2) not null default 0,
  tds_tcs_credit              numeric(14,2) not null default 0,
  advance_tax_paid            numeric(14,2) not null default 0,
  self_assessment_tax         numeric(14,2) not null default 0,
  net_tax_payable_or_refund   numeric(14,2) not null default 0,
  computation_breakdown       jsonb not null default '{}'::jsonb,
  comparison_data             jsonb not null default '{}'::jsonb,
  notes                       text check (notes is null or length(notes) <= 4000),
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_tax_computations_id_firm unique (id, firm_id),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade,
  foreign key (created_by, firm_id) references users(id, firm_id),
  foreign key (updated_by, firm_id) references users(id, firm_id)
);

create index if not exists ix_tax_computations_firm_client on tax_computations (firm_id, client_id, assessment_year);

-- ---------------------------------------------------------------- row-level security

alter table tax_computations enable row level security;
drop policy if exists tenant_isolation on tax_computations;
create policy tenant_isolation on tax_computations
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on tax_computations to cadesk_app;

-- ---------------------------------------------------------------- permissions

insert into permissions (code, area, description) values
  ('computations.run',     'tax', 'Compute income tax liabilities and run regime comparisons'),
  ('computations.approve', 'tax', 'Approve and finalize client income tax computations')
on conflict (code) do nothing;
