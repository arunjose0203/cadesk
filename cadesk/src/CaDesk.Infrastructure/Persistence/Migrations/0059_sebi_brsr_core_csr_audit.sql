-- 0059_sebi_brsr_core_csr_audit.sql
-- SEBI BRSR Core & Companies Act Section 135 CSR Audit Engine

-- 1. CSR Assessments Master
create table if not exists csr_assessments (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  client_id                                 uuid not null references clients(id) on delete cascade,
  financial_year                            text not null default '2024-25',
  net_worth                                 numeric(15,2) not null default 0,
  turnover                                  numeric(15,2) not null default 0,
  net_profit_previous_year                  numeric(15,2) not null default 0,
  is_csr_applicable                         boolean not null default false,
  applicability_criterion                   text not null default '',
  net_profit_fy_minus_1                     numeric(15,2) not null default 0,
  net_profit_fy_minus_2                     numeric(15,2) not null default 0,
  net_profit_fy_minus_3                     numeric(15,2) not null default 0,
  average_net_profit                        numeric(15,2) not null default 0,
  mandatory_csr_obligation_2_pct            numeric(15,2) not null default 0,
  set_off_available_from_prior_years        numeric(15,2) not null default 0,
  net_csr_obligation_for_current_fy         numeric(15,2) not null default 0,
  total_actual_csr_spent                    numeric(15,2) not null default 0,
  total_spent_ongoing_projects              numeric(15,2) not null default 0,
  total_spent_other_projects                numeric(15,2) not null default 0,
  administrative_overheads_spent            numeric(15,2) not null default 0,
  impact_assessment_spent                   numeric(15,2) not null default 0,
  unspent_ongoing_projects                  numeric(15,2) not null default 0,
  unspent_other_projects                    numeric(15,2) not null default 0,
  unspent_ongoing_transfer_date             date,
  schedule_vii_fund_transfer_date           date,
  excess_spent_carried_forward              numeric(15,2) not null default 0,
  caro_clause_xx_a_remark                   text not null default '',
  caro_clause_xx_b_remark                   text not null default '',
  caro_qualification_required               boolean not null default false,
  status                                    text not null default 'Draft',
  certified_by_name                         text,
  certified_membership_no                   text,
  certified_at                              timestamptz,
  udin                                      text,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_csr_assessments_firm_client on csr_assessments (firm_id, client_id);
create index if not exists ix_csr_assessments_fy on csr_assessments (firm_id, financial_year);
create index if not exists ix_csr_assessments_status on csr_assessments (firm_id, status);
alter table csr_assessments enable row level security;
drop policy if exists tenant_isolation on csr_assessments;
create policy tenant_isolation on csr_assessments
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on csr_assessments to cadesk_app;

-- 2. CSR Project Items (Schedule VII Categorization & Ongoing Escrow Tracking)
create table if not exists csr_project_items (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  csr_assessment_id                         uuid not null references csr_assessments(id) on delete cascade,
  project_title                             text not null,
  schedule_vii_category                     text not null default 'PromotingEducationAndSkillDevelopment',
  nature                                    text not null default 'OngoingProject',
  state_or_district                         text not null default '',
  is_aspirational_district                  boolean not null default false,
  total_project_outlay                      numeric(15,2) not null default 0,
  allocated_budget                          numeric(15,2) not null default 0,
  actual_spent                              numeric(15,2) not null default 0,
  implementation_mode                       text not null default 'Direct',
  implementing_agency_name                  text,
  implementing_agency_csr1_number           text,
  project_status                            text not null default 'OngoingYear1',
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_csr_project_items_assessment on csr_project_items (firm_id, csr_assessment_id);
alter table csr_project_items enable row level security;
drop policy if exists tenant_isolation on csr_project_items;
create policy tenant_isolation on csr_project_items
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on csr_project_items to cadesk_app;

-- 3. SEBI BRSR Core Reports (9 Principles, Scope 1 & 2 Emissions, Water, Gender, ESG Scoring)
create table if not exists brsr_core_reports (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  client_id                                 uuid not null references clients(id) on delete cascade,
  financial_year                            text not null default '2024-25',
  turnover_in_crores                        numeric(15,2) not null default 100,
  diesel_consumed_litres                    numeric(15,2) not null default 0,
  natural_gas_consumed_sm3                  numeric(15,2) not null default 0,
  grid_electricity_purchased_mwh            numeric(15,2) not null default 0,
  scope1_direct_emissions_mt_co2e           numeric(15,4) not null default 0,
  scope2_indirect_emissions_mt_co2e         numeric(15,4) not null default 0,
  ghg_intensity_per_crore_turnover          numeric(15,4) not null default 0,
  total_water_withdrawal_kl                 numeric(15,2) not null default 0,
  total_water_consumption_kl                numeric(15,2) not null default 0,
  water_intensity_per_crore_turnover        numeric(15,4) not null default 0,
  operates_zero_liquid_discharge            boolean not null default true,
  total_energy_consumption_gj               numeric(15,2) not null default 0,
  energy_intensity_per_crore_turnover       numeric(15,4) not null default 0,
  renewable_energy_share_pct                numeric(5,2) not null default 0,
  lost_time_injury_frequency_rate_ltifr     numeric(8,4) not null default 0,
  fatalities_count                          integer not null default 0,
  posh_complaints_received                  integer not null default 0,
  posh_complaints_resolved                  integer not null default 0,
  median_remuneration_lakhs                 numeric(10,2) not null default 0,
  total_permanent_employees                 integer not null default 0,
  female_employees_count                    integer not null default 0,
  female_employees_pct                      numeric(5,2) not null default 0,
  total_board_directors                     integer not null default 0,
  female_board_directors                    integer not null default 0,
  female_board_pct                          numeric(5,2) not null default 0,
  total_kmp_count                           integer not null default 0,
  female_kmp_count                          integer not null default 0,
  female_kmp_pct                            numeric(5,2) not null default 0,
  overall_brsr_core_score                   numeric(5,2) not null default 0,
  assurance_type                            text not null default 'ReasonableAssurance',
  assurance_auditor_name                    text,
  assurance_auditor_membership_no           text,
  assurance_udin                            text,
  assurance_certified_at                    timestamptz,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_brsr_core_reports_firm_client on brsr_core_reports (firm_id, client_id);
create index if not exists ix_brsr_core_reports_fy on brsr_core_reports (firm_id, financial_year);
alter table brsr_core_reports enable row level security;
drop policy if exists tenant_isolation on brsr_core_reports;
create policy tenant_isolation on brsr_core_reports
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on brsr_core_reports to cadesk_app;
