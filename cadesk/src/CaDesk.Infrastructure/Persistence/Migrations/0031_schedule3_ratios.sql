-- 0031_schedule3_ratios.sql: Schedule III 11 Analytical Ratios & Mandatory Corporate Disclosures Engine

create table if not exists schedule3_assessments (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  client_id                       uuid not null references clients(id) on delete cascade,
  audit_engagement_id             uuid references audit_engagements(id) on delete set null,
  financial_year                  text not null, -- e.g. "2024-25"
  preceding_financial_year        text not null, -- e.g. "2023-24"
  accounting_standard             text not null default 'Division I - AS (Non-Ind AS)',
  rounding_unit                   text not null default 'Lakhs',
  total_income_current            numeric(16,2) not null default 0,
  total_income_preceding          numeric(16,2) not null default 0,
  current_assets_current          numeric(16,2) not null default 0,
  current_assets_preceding        numeric(16,2) not null default 0,
  current_liabilities_current     numeric(16,2) not null default 0,
  current_liabilities_preceding   numeric(16,2) not null default 0,
  total_debt_current              numeric(16,2) not null default 0,
  total_debt_preceding            numeric(16,2) not null default 0,
  shareholders_equity_current     numeric(16,2) not null default 0,
  shareholders_equity_preceding   numeric(16,2) not null default 0,
  net_profit_after_taxes_current  numeric(16,2) not null default 0,
  net_profit_after_taxes_preceding numeric(16,2) not null default 0,
  ebit_current                    numeric(16,2) not null default 0,
  ebit_preceding                  numeric(16,2) not null default 0,
  debt_service_current            numeric(16,2) not null default 0,
  debt_service_preceding          numeric(16,2) not null default 0,
  cost_of_goods_sold_current      numeric(16,2) not null default 0,
  cost_of_goods_sold_preceding    numeric(16,2) not null default 0,
  average_inventory_current       numeric(16,2) not null default 0,
  average_inventory_preceding     numeric(16,2) not null default 0,
  revenue_from_operations_current numeric(16,2) not null default 0,
  revenue_from_operations_preceding numeric(16,2) not null default 0,
  average_trade_receivables_current numeric(16,2) not null default 0,
  average_trade_receivables_preceding numeric(16,2) not null default 0,
  net_credit_purchases_current    numeric(16,2) not null default 0,
  net_credit_purchases_preceding  numeric(16,2) not null default 0,
  average_trade_payables_current  numeric(16,2) not null default 0,
  average_trade_payables_preceding numeric(16,2) not null default 0,
  capital_employed_current        numeric(16,2) not null default 0,
  capital_employed_preceding      numeric(16,2) not null default 0,
  income_from_investments_current numeric(16,2) not null default 0,
  income_from_investments_preceding numeric(16,2) not null default 0,
  average_investments_current     numeric(16,2) not null default 0,
  average_investments_preceding   numeric(16,2) not null default 0,
  is_rounding_rule_compliant      boolean not null default true,
  rounding_rule_recommendation    text,
  significant_variances_count     integer not null default 0,
  missing_explanations_count      integer not null default 0,
  assessment_status               text not null default 'draft', -- draft, in_review, completed, finalized
  auditor_remarks                 text,
  generated_disclosure_note_text  text,
  finalized_by_user_id            uuid references users(id) on delete set null,
  finalized_at                    timestamptz,
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_schedule3_assessments_client_fy
  on schedule3_assessments (firm_id, client_id, financial_year);

alter table schedule3_assessments enable row level security;
alter table schedule3_assessments force row level security;
drop policy if exists tenant_isolation on schedule3_assessments;
create policy tenant_isolation on schedule3_assessments
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on schedule3_assessments to cadesk_app;

-- Schedule III 11 Ratios
create table if not exists schedule3_ratio_items (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  assessment_id                   uuid not null references schedule3_assessments(id) on delete cascade,
  ratio_code                      text not null, -- "R01", "R02", ... "R11"
  ratio_name                      text not null,
  numerator_description           text not null default '',
  denominator_description         text not null default '',
  current_year_value              numeric(12,4) not null default 0,
  preceding_year_value            numeric(12,4) not null default 0,
  variance_percent                numeric(8,2) not null default 0,
  is_variance_significant         boolean not null default false,
  variance_explanation            text
);

create index if not exists ix_schedule3_ratio_items_assessment
  on schedule3_ratio_items (firm_id, assessment_id);

alter table schedule3_ratio_items enable row level security;
alter table schedule3_ratio_items force row level security;
drop policy if exists tenant_isolation on schedule3_ratio_items;
create policy tenant_isolation on schedule3_ratio_items
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on schedule3_ratio_items to cadesk_app;

-- Schedule III Ageing Items (Trade Receivables, Trade Payables, CWIP, Intangibles)
create table if not exists schedule3_ageing_items (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  assessment_id                   uuid not null references schedule3_assessments(id) on delete cascade,
  schedule_type                   text not null default 'TradeReceivables', -- TradeReceivables, TradePayables, CWIP, Intangibles
  particulars_category            text not null,
  less_6_months_or_1_year         numeric(14,2) not null default 0,
  one_to_two_years                numeric(14,2) not null default 0,
  two_to_three_years              numeric(14,2) not null default 0,
  more_than_three_years           numeric(14,2) not null default 0,
  unbilled_dues                   numeric(14,2) not null default 0
);

create index if not exists ix_schedule3_ageing_items_assessment
  on schedule3_ageing_items (firm_id, assessment_id);

alter table schedule3_ageing_items enable row level security;
alter table schedule3_ageing_items force row level security;
drop policy if exists tenant_isolation on schedule3_ageing_items;
create policy tenant_isolation on schedule3_ageing_items
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on schedule3_ageing_items to cadesk_app;

-- Seed permissions
insert into permissions (code, area, description) values
  ('schedule3.read',     'audit', 'View Schedule III financial ratios, variance analysis, and ageing disclosures'),
  ('schedule3.manage',   'audit', 'Compute Schedule III ratios, enter age buckets, and record variance explanations'),
  ('schedule3.finalize', 'audit', 'Finalize Schedule III analytical ratios and corporate disclosure notes')
on conflict (code) do nothing;

-- Grant to existing Partner roles
insert into role_permissions (role_id, permission_code, firm_id)
select r.id, p.code, r.firm_id
from roles r
cross join (values
  ('schedule3.read'),
  ('schedule3.manage'),
  ('schedule3.finalize')
) as p(code)
where r.name = 'Partner'
on conflict do nothing;
