-- 0060_ibc_cirp_statutory_desk.sql
-- IBC 2016 Insolvency & CIRP Statutory Desk

-- 1. CIRP Cases Master Registry
create table if not exists cirp_cases (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  client_id                                 uuid not null references clients(id) on delete cascade,
  corporate_debtor_name                     text not null,
  cin                                       text not null default '',
  nclt_bench                                text not null default 'NCLT New Delhi Bench-III',
  court_order_number                        text not null default '',
  admission_date                            date not null,
  irp_name                                  text not null default '',
  irp_ip_reg_number                         text not null default '',
  rp_name                                   text not null default '',
  rp_ip_reg_number                          text not null default '',
  moratorium_section_14_declared            boolean not null default true,
  claims_last_date                          date not null,
  initial_180_days_deadline                 date not null,
  extended_270_days_deadline                date not null,
  final_330_days_limit                      date not null,
  stage                                     text not null default 'AdmissionAndMoratorium',
  total_admitted_financial_debt             numeric(15,2) not null default 0,
  total_admitted_operational_debt           numeric(15,2) not null default 0,
  status_note                               text not null default '',
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_cirp_cases_firm_client on cirp_cases (firm_id, client_id);
create index if not exists ix_cirp_cases_stage on cirp_cases (firm_id, stage);
create index if not exists ix_cirp_cases_admission on cirp_cases (firm_id, admission_date);
alter table cirp_cases enable row level security;
drop policy if exists tenant_isolation on cirp_cases;
create policy tenant_isolation on cirp_cases
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on cirp_cases to cadesk_app;

-- 2. Committee of Creditors (CoC) Members
create table if not exists coc_members (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  cirp_case_id                              uuid not null references cirp_cases(id) on delete cascade,
  creditor_name                             text not null,
  category                                  text not null default 'SecuredFinancialCreditor',
  claim_submitted_amount                    numeric(15,2) not null default 0,
  admitted_claim_amount                     numeric(15,2) not null default 0,
  voting_share_pct                          numeric(6,2) not null default 0,
  security_interest_description             text not null default '',
  is_related_party                          boolean not null default false,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_coc_members_case on coc_members (firm_id, cirp_case_id);
alter table coc_members enable row level security;
drop policy if exists tenant_isolation on coc_members;
create policy tenant_isolation on coc_members
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on coc_members to cadesk_app;

-- 3. CoC Resolutions & Statutory Voting Thresholds (51%, 66%, 90%)
create table if not exists coc_resolutions (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  cirp_case_id                              uuid not null references cirp_cases(id) on delete cascade,
  meeting_number                            integer not null default 1,
  resolution_number                         text not null default 'RES-01',
  resolution_title                          text not null,
  statutory_section                         text not null default 'Section 30(4)',
  required_threshold_pct                    numeric(5,2) not null default 66,
  voting_share_in_favour_pct                numeric(5,2) not null default 0,
  voting_share_against_pct                  numeric(5,2) not null default 0,
  voting_share_abstained_pct                numeric(5,2) not null default 0,
  status                                    text not null default 'Pending',
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_coc_resolutions_case on coc_resolutions (firm_id, cirp_case_id);
alter table coc_resolutions enable row level security;
drop policy if exists tenant_isolation on coc_resolutions;
create policy tenant_isolation on coc_resolutions
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on coc_resolutions to cadesk_app;

-- 4. PUFE Forensic Avoidance Transactions (Sections 43, 45, 50, 66)
create table if not exists pufe_transactions (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  cirp_case_id                              uuid not null references cirp_cases(id) on delete cascade,
  pufe_type                                 text not null default 'PreferentialSec43',
  transaction_date                          date not null,
  counterparty_name                         text not null,
  is_related_party                          boolean not null default false,
  transaction_amount                        numeric(15,2) not null default 0,
  estimated_avoidance_recoverable_amount    numeric(15,2) not null default 0,
  lookback_period_years                     integer not null default 1,
  is_within_lookback_period                 boolean not null default true,
  forensic_auditor_observations             text not null default '',
  nclt_application_filed                    boolean not null default false,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_pufe_transactions_case on pufe_transactions (firm_id, cirp_case_id);
alter table pufe_transactions enable row level security;
drop policy if exists tenant_isolation on pufe_transactions;
create policy tenant_isolation on pufe_transactions
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on pufe_transactions to cadesk_app;

-- 5. Asset Valuations (Regulation 35 Fair Value & Liquidation Value)
create table if not exists asset_valuations (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  cirp_case_id                              uuid not null references cirp_cases(id) on delete cascade,
  asset_class                               text not null default 'LandAndBuilding',
  asset_description                         text not null,
  valuer1_name                              text not null default '',
  valuer1_fair_value                        numeric(15,2) not null default 0,
  valuer1_liquidation_value                 numeric(15,2) not null default 0,
  valuer2_name                              text not null default '',
  valuer2_fair_value                        numeric(15,2) not null default 0,
  valuer2_liquidation_value                 numeric(15,2) not null default 0,
  average_fair_value                        numeric(15,2) not null default 0,
  average_liquidation_value                 numeric(15,2) not null default 0,
  third_valuer_required                     boolean not null default false,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_asset_valuations_case on asset_valuations (firm_id, cirp_case_id);
alter table asset_valuations enable row level security;
drop policy if exists tenant_isolation on asset_valuations;
create policy tenant_isolation on asset_valuations
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on asset_valuations to cadesk_app;
