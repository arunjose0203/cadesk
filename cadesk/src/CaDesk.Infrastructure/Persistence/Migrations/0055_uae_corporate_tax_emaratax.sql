-- 0055_uae_corporate_tax_emaratax.sql
-- UAE Corporate Tax & Federal Tax Authority (FTA) EmaraTax Desk
-- Implements Federal Decree-Law No. 47 of 2022, Cabinet Decision No. 55/2023 (QFZP),
-- and Ministerial Decision No. 73 of 2023 (Small Business Relief).

create table if not exists uae_tax_profiles (
  id                              uuid primary key,
  firm_id                         uuid not null references firms(id),
  client_id                       uuid not null references clients(id) on delete cascade,
  corporate_tax_trn               text not null, -- 15-digit FTA TRN
  emirate                         text not null default 'Dubai',
  free_zone_name                  text not null default 'Mainland',
  category                        text not null default 'ResidentJuridicalPerson',
  free_zone_regime                text not null default 'Mainland',
  financial_year_start_month      integer not null default 1,
  is_mne_group_constituent        boolean not null default false,
  accounting_standard             text not null default 'IFRS',
  audited_financials_available    boolean not null default true,
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_uae_tax_profiles_firm_client 
  on uae_tax_profiles (firm_id, client_id);
create index if not exists ix_uae_tax_profiles_trn 
  on uae_tax_profiles (firm_id, corporate_tax_trn);

alter table uae_tax_profiles enable row level security;
drop policy if exists tenant_isolation on uae_tax_profiles;
create policy tenant_isolation on uae_tax_profiles
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on uae_tax_profiles to cadesk_app;

create table if not exists uae_corporate_tax_returns (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  tax_profile_id                            uuid not null references uae_tax_profiles(id) on delete cascade,
  client_id                                 uuid not null references clients(id) on delete cascade,
  return_reference                          text not null,
  return_year                               integer not null,
  tax_period_start                          date not null,
  tax_period_end                            date not null,
  due_date                                  date not null,
  status                                    text not null default 'Draft',
  functional_currency                       text not null default 'AED',
  exchange_rate_to_inr                      numeric(10,4) not null default 22.8500,

  gross_revenue                             numeric(15,2) not null default 0,
  cost_of_sales                             numeric(15,2) not null default 0,
  operating_expenses                        numeric(15,2) not null default 0,
  net_accounting_profit_loss                numeric(15,2) not null default 0,

  small_business_relief_election            text not null default 'NotEligible',
  prior_year1_revenue                       numeric(15,2) not null default 0,
  prior_year2_revenue                       numeric(15,2) not null default 0,

  is_qfzp                                   boolean not null default false,
  qualifying_revenue                        numeric(15,2) not null default 0,
  non_qualifying_revenue                    numeric(15,2) not null default 0,
  de_minimis_threshold                      numeric(15,2) not null default 0,
  de_minimis_satisfied                      boolean not null default true,
  qualifying_income                         numeric(15,2) not null default 0,
  taxable_non_qualifying_income             numeric(15,2) not null default 0,

  exempt_income                             numeric(15,2) not null default 0,
  unrealized_gains_losses_adjustment        numeric(15,2) not null default 0,
  entertainment_expenditure_total           numeric(15,2) not null default 0,
  entertainment_expenditure_disallowed      numeric(15,2) not null default 0,
  fines_and_penalties_disallowed            numeric(15,2) not null default 0,
  recoverable_vat_disallowed                numeric(15,2) not null default 0,
  donations_to_unapproved_entities_disallowed numeric(15,2) not null default 0,

  net_interest_expense                      numeric(15,2) not null default 0,
  adjusted_ebitda                           numeric(15,2) not null default 0,
  interest_cap_threshold                    numeric(15,2) not null default 0,
  disallowed_interest_expense               numeric(15,2) not null default 0,

  brought_forward_tax_losses                numeric(15,2) not null default 0,
  tax_losses_utilized                       numeric(15,2) not null default 0,
  carried_forward_tax_losses                numeric(15,2) not null default 0,

  adjusted_taxable_income                   numeric(15,2) not null default 0,
  taxable_income_subject_to_zero_pct        numeric(15,2) not null default 0,
  taxable_income_subject_to_nine_pct        numeric(15,2) not null default 0,
  corporate_tax_payable_aed                 numeric(15,2) not null default 0,
  foreign_tax_credit_relief_aed             numeric(15,2) not null default 0,
  net_tax_payable_aed                       numeric(15,2) not null default 0,

  fta_submission_reference                  text,
  submission_payload_json                   text,
  prepared_by_user_id                       uuid references users(id) on delete set null,
  prepared_by_name                          text,
  approved_by_user_id                       uuid references users(id) on delete set null,
  approved_by_name                          text,
  approved_at                               timestamptz,
  partner_notes                             text,
  deleted_at                                timestamptz,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_uae_ct_returns_firm_profile 
  on uae_corporate_tax_returns (firm_id, tax_profile_id);
create index if not exists ix_uae_ct_returns_firm_client 
  on uae_corporate_tax_returns (firm_id, client_id);
create index if not exists ix_uae_ct_returns_status 
  on uae_corporate_tax_returns (firm_id, status);

alter table uae_corporate_tax_returns enable row level security;
drop policy if exists tenant_isolation on uae_corporate_tax_returns;
create policy tenant_isolation on uae_corporate_tax_returns
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on uae_corporate_tax_returns to cadesk_app;
