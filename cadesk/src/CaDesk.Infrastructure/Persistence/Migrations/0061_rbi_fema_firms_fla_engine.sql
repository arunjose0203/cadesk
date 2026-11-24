-- 0061_rbi_fema_firms_fla_engine.sql: RBI FEMA FIRMS Portal (FC-GPR / FC-TRS) & Annual FLA Return Engine

-- 1. FEMA FIRMS Filings (FC-GPR / FC-TRS)
create table if not exists fema_firms_filings (
  id                                      uuid primary key default gen_random_uuid(),
  firm_id                                 uuid not null references firms(id) on delete cascade,
  client_id                               uuid not null references clients(id) on delete cascade,
  filing_type                             text not null, -- FcGpr, FcTrs
  reference_number                        text not null,
  indian_entity_cin                       text not null default '',
  indian_entity_name                      text not null default '',
  pan                                     text not null default '',
  non_resident_party_name                 text not null default '',
  non_resident_country                    text not null default '',
  non_resident_country_code               text not null default '',
  instrument_type                         text not null default 'EquityShares',
  number_of_instruments                  bigint not null default 0,
  face_value_per_instrument               numeric(16,2) not null default 10.00,
  issue_or_transfer_price_per_instrument  numeric(16,2) not null default 0,
  fair_value_per_instrument               numeric(16,2) not null default 0,
  total_consideration_inr                 numeric(18,2) not null default 0,
  total_consideration_fc                  numeric(18,2) not null default 0,
  currency                                text not null default 'USD',
  exchange_rate                           numeric(12,4) not null default 85.00,
  valuation_method                        text not null default 'DiscountedCashFlow',
  valuer_name                             text not null default '',
  valuation_report_date                   timestamptz,
  date_of_allotment_or_transfer           timestamptz not null,
  statutory_filing_deadline               timestamptz not null,
  days_elapsed                            integer not null default 0,
  days_remaining                          integer not null default 0,
  is_late_submission                      boolean not null default false,
  late_submission_fee_estimated           numeric(16,2) not null default 0,
  fdi_route                               text not null default 'AutomaticRoute',
  entry_route_approval_ref                text,
  sectoral_cap_pct                        numeric(5,2) not null default 100.00,
  post_allotment_foreign_shareholding_pct numeric(5,2) not null default 0,
  transfer_direction                      text not null default 'ResidentToNonResident',
  transfer_mode                           text not null default 'Sale',
  buyer_ad_bank_name                      text,
  seller_ad_bank_name                     text,
  pricing_guidelines_met                  boolean not null default true,
  pricing_compliance_remarks              text not null default '',
  generated_compliance_certificate        text,
  status                                  text not null default 'Draft',
  udin                                    text,
  certified_at                            timestamptz,
  certified_by_ca_name                    text,
  ca_membership_number                    text,
  ca_firm_name                            text,
  rbi_acknowledgment_reference            text,
  created_at                              timestamptz not null default now(),
  updated_at                              timestamptz
);

create index if not exists ix_fema_firms_filings_firm_client on fema_firms_filings(firm_id, client_id);
create index if not exists ix_fema_firms_filings_type_status on fema_firms_filings(filing_type, status);

alter table fema_firms_filings enable row level security;

drop policy if exists fema_firms_filings_tenant on fema_firms_filings;
create policy fema_firms_filings_tenant on fema_firms_filings
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on fema_firms_filings to cadesk_app;

-- 2. RBI Annual Return on Foreign Liabilities and Assets (FLA Return)
create table if not exists rbi_annual_fla_returns (
  id                                      uuid primary key default gen_random_uuid(),
  firm_id                                 uuid not null references firms(id) on delete cascade,
  client_id                               uuid not null references clients(id) on delete cascade,
  financial_year                          text not null default '2024-25',
  company_cin                             text not null default '',
  company_name                            text not null default '',
  pan                                     text not null default '',
  account_closing_date                    timestamptz not null default '2025-03-31',
  statutory_filing_deadline               timestamptz not null default '2025-07-15',
  audit_status                            text not null default 'Audited',

  -- Section I: Financial Particulars
  paid_up_capital_inr                     numeric(18,2) not null default 0,
  reserves_and_surplus_inr                numeric(18,2) not null default 0,
  net_worth_inr                           numeric(18,2) not null default 0,
  total_sales_turnover_inr                numeric(18,2) not null default 0,
  net_profit_loss_after_tax_inr           numeric(18,2) not null default 0,
  total_assets_inr                        numeric(18,2) not null default 0,
  total_liabilities_inr                   numeric(18,2) not null default 0,

  -- Section II: FDI in India
  foreign_equity_holding_pct              numeric(5,2) not null default 0,
  fdi_at_face_value_inr                   numeric(18,2) not null default 0,
  fdi_at_net_asset_value_inr              numeric(18,2) not null default 0,
  fdi_at_market_or_dcf_value_inr          numeric(18,2) not null default 0,
  fresh_fdi_inflows_during_year_inr       numeric(18,2) not null default 0,
  disinvestment_outflows_during_year_inr  numeric(18,2) not null default 0,
  retained_earnings_attributable_to_fdi_inr numeric(18,2) not null default 0,

  -- Section III: ODI Outside India
  has_odi                                 boolean not null default false,
  overseas_entity_name                    text,
  overseas_country                        text,
  odi_equity_holding_pct                  numeric(5,2) not null default 0,
  odi_equity_investment_inr               numeric(18,2) not null default 0,
  odi_loan_outstanding_inr                numeric(18,2) not null default 0,
  odi_guarantee_issued_inr                numeric(18,2) not null default 0,
  total_odi_financial_commitment_inr      numeric(18,2) not null default 0,

  -- Section IV: Other Foreign Liabilities & Assets
  external_commercial_borrowings_ecb_inr  numeric(18,2) not null default 0,
  foreign_trade_credit_suppliers_inr      numeric(18,2) not null default 0,
  foreign_trade_credit_buyers_inr         numeric(18,2) not null default 0,
  foreign_bank_balances_inr               numeric(18,2) not null default 0,
  other_foreign_assets_inr                numeric(18,2) not null default 0,
  net_foreign_liabilities_position_inr    numeric(18,2) not null default 0,

  -- Statutory Evaluation & Status
  is_mandatory_filing                     boolean not null default true,
  days_remaining_to_deadline              integer not null default 0,
  is_deadline_breached                    boolean not null default false,
  filing_status_alert                     text not null default '',
  status                                  text not null default 'Draft',
  rbi_flair_ack_number                    text,
  submitted_at                            timestamptz,
  udin                                    text,
  certified_at                            timestamptz,
  certified_by_ca                         text,
  verification_notes                      text,
  created_at                              timestamptz not null default now(),
  updated_at                              timestamptz
);

create index if not exists ix_rbi_annual_fla_returns_firm_client on rbi_annual_fla_returns(firm_id, client_id);
create index if not exists ix_rbi_annual_fla_returns_fy on rbi_annual_fla_returns(financial_year);

alter table rbi_annual_fla_returns enable row level security;

drop policy if exists rbi_annual_fla_returns_tenant on rbi_annual_fla_returns;
create policy rbi_annual_fla_returns_tenant on rbi_annual_fla_returns
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on rbi_annual_fla_returns to cadesk_app;
