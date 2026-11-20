-- 0035_related_party_transactions.sql: Related Party Transactions & Section 188 / AS 18 / Ind AS 24 Arm's Length Pricing Desk

create table if not exists related_party_entities (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  client_id                       uuid not null references clients(id) on delete cascade,
  name                            text not null,
  relationship_type               text not null default 'HoldingCompany', -- HoldingCompany, Subsidiary, FellowSubsidiary, Associate, JointVenture, KeyManagementPersonnel, RelativeOfKMP, EnterpriseControlledByKMP
  pan                             text not null default '',
  cin                             text not null default '',
  director_or_kmp_name            text not null default '',
  nature_of_interest              text not null default '',
  is_active                       boolean not null default true,
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_related_party_entities_client
  on related_party_entities (firm_id, client_id);

alter table related_party_entities enable row level security;
alter table related_party_entities force row level security;
drop policy if exists tenant_isolation on related_party_entities;
create policy tenant_isolation on related_party_entities
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on related_party_entities to cadesk_app;

-- Audit Committee Section 177 Omnibus Approvals
create table if not exists related_party_omnibus_approvals (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  client_id                       uuid not null references clients(id) on delete cascade,
  financial_year                  text not null,
  audit_committee_meeting_date    timestamptz not null,
  title                           text not null,
  maximum_approved_limit          numeric(16,2) not null default 0,
  utilized_amount                 numeric(16,2) not null default 0,
  approval_criteria               text not null default '',
  validity_period_to              timestamptz not null,
  is_active                       boolean not null default true,
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_related_party_omnibus_client_fy
  on related_party_omnibus_approvals (firm_id, client_id, financial_year);

alter table related_party_omnibus_approvals enable row level security;
alter table related_party_omnibus_approvals force row level security;
drop policy if exists tenant_isolation on related_party_omnibus_approvals;
create policy tenant_isolation on related_party_omnibus_approvals
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on related_party_omnibus_approvals to cadesk_app;

-- Related Party Transactions (Section 188 / AS 18 / Ind AS 24)
create table if not exists related_party_transactions (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  client_id                       uuid not null references clients(id) on delete cascade,
  related_party_id                uuid not null references related_party_entities(id) on delete cascade,
  financial_year                  text not null,
  transaction_date                timestamptz not null,
  contract_reference              text not null default '',
  transaction_type                text not null default 'SaleOfGoods', -- SaleOfGoods, PurchaseOfGoods, LeaseOfProperty, RenderingOfServices, AvailingOfServices, RemunerationOfficeOfProfit, LoanGiven, LoanReceived, GuaranteeUnderwriting, Other
  description                     text not null default '',
  transaction_amount              numeric(16,2) not null default 0,
  currency                        text not null default 'INR',

  -- Benchmarking & Rule 15 Thresholds
  annual_turnover_benchmark       numeric(16,2) not null default 0,
  net_worth_benchmark             numeric(16,2) not null default 0,
  threshold_limit_amount          numeric(16,2) not null default 0,
  threshold_percentage            numeric(8,2) not null default 10.00,
  exceeds_statutory_threshold     boolean not null default false,

  -- Section 188(1) 4th Proviso & Arm's Length
  is_ordinary_course_of_business  boolean not null default true,
  is_arm_length                   boolean not null default true,
  arm_length_method               text not null default 'CUP',
  benchmark_arm_length_price      numeric(16,2) not null default 0,
  price_variance_percentage       numeric(8,2) not null default 0,

  -- Approvals
  audit_committee_approval_status text not null default 'Approved',
  audit_committee_meeting_date    timestamptz,
  omnibus_approval_id             uuid references related_party_omnibus_approvals(id) on delete set null,
  board_resolution_date           timestamptz,
  shareholders_resolution_date    timestamptz,
  requires_shareholder_approval   boolean not null default false,

  -- Tax & Disclosures
  section_40a2b_risk              boolean not null default false,
  mbp4_entered_date               timestamptz,
  outstanding_balance_at_year_end numeric(16,2) not null default 0,
  balance_type                    text not null default 'Debit',
  status                          text not null default 'Draft',
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_related_party_transactions_client_fy
  on related_party_transactions (firm_id, client_id, financial_year);

alter table related_party_transactions enable row level security;
alter table related_party_transactions force row level security;
drop policy if exists tenant_isolation on related_party_transactions;
create policy tenant_isolation on related_party_transactions
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on related_party_transactions to cadesk_app;
