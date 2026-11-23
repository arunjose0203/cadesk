-- 0056_practice_health_succession_valuation.sql
-- CA Practice Health, Succession & Firm Valuation Benchmarking Desk

-- 1. Practice Valuations
create table if not exists practice_valuations (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  valuation_name                            text not null default 'Annual Partnership Valuation',
  valuation_date                            date not null,
  method                                    text not null default 'BlendedWeight',
  status                                    text not null default 'Draft',

  annual_gross_revenue                      numeric(15,2) not null default 0,
  recurring_compliance_revenue              numeric(15,2) not null default 0,
  advisory_and_one_off_revenue              numeric(15,2) not null default 0,
  ebitda                                    numeric(15,2) not null default 0,
  partner_normalized_compensation           numeric(15,2) not null default 0,
  net_working_capital                       numeric(15,2) not null default 0,
  net_tangible_assets                       numeric(15,2) not null default 0,

  projected_revenue_growth_rate             numeric(6,4) not null default 0.12,
  discount_rate_wacc                        numeric(6,4) not null default 0.14,
  terminal_growth_rate                      numeric(6,4) not null default 0.05,
  forecast_years                            integer not null default 5,
  revenue_multiple_base                     numeric(6,2) not null default 1.35,
  ebitda_multiple_base                      numeric(6,2) not null default 5.50,
  audit_franchise_stickiness_premium_pct     numeric(6,4) not null default 0.10,
  partner_concentration_discount_pct        numeric(6,4) not null default 0.15,
  recurring_revenue_premium_pct             numeric(6,4) not null default 0.10,

  dcf_enterprise_value                      numeric(15,2) not null default 0,
  multiple_enterprise_value                 numeric(15,2) not null default 0,
  blended_firm_fair_market_value            numeric(15,2) not null default 0,
  firm_goodwill_value                       numeric(15,2) not null default 0,
  value_per_equity_point                    numeric(15,2) not null default 0,
  valuation_summary_json                    text,

  prepared_by_user_id                       uuid references users(id) on delete set null,
  prepared_by_name                          text,
  approved_by_user_id                       uuid references users(id) on delete set null,
  approved_by_name                          text,
  approved_at                               timestamptz,
  notes                                     text,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_practice_valuations_firm on practice_valuations (firm_id);
alter table practice_valuations enable row level security;
drop policy if exists tenant_isolation on practice_valuations;
create policy tenant_isolation on practice_valuations
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on practice_valuations to cadesk_app;

-- 2. Partner Equities
create table if not exists partner_equities (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  user_id                                   uuid not null references users(id) on delete cascade,
  partner_name                              text not null,
  partner_email                             text not null default '',
  partner_membership_no                     text not null default '',
  equity_percentage                         numeric(6,2) not null default 0,
  capital_account_balance                   numeric(15,2) not null default 0,
  joining_date                              date not null,
  target_retirement_date                    date,
  vesting_status                            text not null default 'FullyVested',
  vested_percentage                         numeric(6,2) not null default 100,
  annual_draw_share                         numeric(15,2) not null default 0,
  role                                      text not null default 'SeniorPartner',
  is_active                                 boolean not null default true,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_partner_equities_firm on partner_equities (firm_id);
create index if not exists ix_partner_equities_user on partner_equities (firm_id, user_id);
alter table partner_equities enable row level security;
drop policy if exists tenant_isolation on partner_equities;
create policy tenant_isolation on partner_equities
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on partner_equities to cadesk_app;

-- 3. Partner Retirement Plans
create table if not exists partner_retirement_plans (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  partner_equity_id                         uuid not null references partner_equities(id) on delete cascade,
  partner_name                              text not null,
  retirement_date                           date not null,
  agreed_firm_valuation_at_retirement       numeric(15,2) not null default 0,
  partner_equity_pct                        numeric(6,2) not null default 0,
  gross_equity_value                        numeric(15,2) not null default 0,
  capital_account_refund_amount             numeric(15,2) not null default 0,
  goodwill_buyout_amount                    numeric(15,2) not null default 0,
  payout_period_years                       integer not null default 5,
  annual_interest_rate_pct                  numeric(6,2) not null default 8.0,
  annual_installment_amount                 numeric(15,2) not null default 0,
  status                                    text not null default 'Draft',
  amortization_schedule_json                text,
  partnership_notes                         text,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_partner_retirement_plans_firm on partner_retirement_plans (firm_id);
create index if not exists ix_partner_retirement_plans_equity on partner_retirement_plans (firm_id, partner_equity_id);
alter table partner_retirement_plans enable row level security;
drop policy if exists tenant_isolation on partner_retirement_plans;
create policy tenant_isolation on partner_retirement_plans
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on partner_retirement_plans to cadesk_app;

-- 4. Client Succession Transitions
create table if not exists client_succession_transitions (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  retiring_partner_id                       uuid not null references users(id) on delete cascade,
  retiring_partner_name                     text not null,
  successor_partner_id                      uuid not null references users(id) on delete cascade,
  successor_partner_name                    text not null,
  client_id                                 uuid not null references clients(id) on delete cascade,
  client_name                               text not null,
  annual_client_fees                        numeric(15,2) not null default 0,
  stage                                     text not null default 'Identified',
  risk                                      text not null default 'Low',
  target_handover_date                      date not null,
  handover_notes                            text,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_client_succession_transitions_firm on client_succession_transitions (firm_id);
alter table client_succession_transitions enable row level security;
drop policy if exists tenant_isolation on client_succession_transitions;
create policy tenant_isolation on client_succession_transitions
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on client_succession_transitions to cadesk_app;

-- 5. Practice Benchmarks
create table if not exists practice_benchmarks (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  benchmark_year                            integer not null default extract(year from current_date),
  firm_tier                                 text not null default 'Tier2MidSizedMultiPartner',

  actual_revenue_per_partner                numeric(15,2) not null default 0,
  actual_revenue_per_employee               numeric(15,2) not null default 0,
  actual_realization_rate                   numeric(6,2) not null default 0,
  actual_dso_days                           numeric(6,1) not null default 0,
  actual_staff_leverage_ratio               numeric(6,2) not null default 0,
  actual_wip_lockup_days                    numeric(6,1) not null default 0,
  actual_ebitda_margin_pct                  numeric(6,2) not null default 0,

  benchmark_revenue_per_partner             numeric(15,2) not null default 0,
  benchmark_revenue_per_employee            numeric(15,2) not null default 0,
  benchmark_realization_rate                numeric(6,2) not null default 0,
  benchmark_dso_days                        numeric(6,1) not null default 0,
  benchmark_staff_leverage_ratio            numeric(6,2) not null default 0,
  benchmark_wip_lockup_days                 numeric(6,1) not null default 0,
  benchmark_ebitda_margin_pct               numeric(6,2) not null default 0,

  overall_health_score                      numeric(5,1) not null default 0,
  health_grade                              text not null default 'A',
  health_insights_json                      text,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_practice_benchmarks_firm on practice_benchmarks (firm_id);
alter table practice_benchmarks enable row level security;
drop policy if exists tenant_isolation on practice_benchmarks;
create policy tenant_isolation on practice_benchmarks
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on practice_benchmarks to cadesk_app;
