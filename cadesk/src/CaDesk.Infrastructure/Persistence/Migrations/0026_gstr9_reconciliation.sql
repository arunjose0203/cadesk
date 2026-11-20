-- 0026_gstr9_reconciliation: GSTR-9 Annual Return and GSTR-9C Audit Reconciliation Desk.
-- Compiles GSTR-9 annual return tables (Part II Outward, Part III ITC Table 6/7/8, Part IV Tax Table 9, Part V Table 10-14),
-- evaluates turnover thresholds (Exempt < 2 Cr, GSTR-9 > 2 Cr, GSTR-9 + 9C > 5 Cr),
-- reconciles Table 5 gross turnover, Table 7 taxable turnover, Table 12 ITC against audited financials,
-- manages discrepancy reason codes, auditor verification certification, and DRC-03 additional tax liability tracking.

create table if not exists gstr9_returns (
  id                            uuid primary key,
  firm_id                       uuid not null references firms(id),
  client_id                     uuid not null references clients(id) on delete cascade,
  financial_year                text not null, -- e.g. 2023-24
  gstin                         text not null,
  legal_name                    text not null,
  trade_name                    text,
  aggregate_turnover            numeric(14,2) not null default 0,
  is_gstr9_mandatory            boolean not null default false, -- Aggregate turnover > 2 Cr
  is_gstr9c_mandatory           boolean not null default false, -- Aggregate turnover > 5 Cr
  status                        text not null default 'Draft',  -- Draft, Reconciled, Certified, Filed
  
  -- Part II: Outward & Inward Supplies
  outward_taxable_supplies      numeric(14,2) not null default 0, -- Table 4N
  outward_exempted_supplies     numeric(14,2) not null default 0, -- Table 5M
  total_turnover                numeric(14,2) not null default 0, -- Table 5N = 4N + 5M - 4G
  
  -- Part III: Input Tax Credit (ITC)
  itc_availed_3b                numeric(14,2) not null default 0, -- Table 6A
  itc_categorized_b2b           numeric(14,2) not null default 0, -- Table 6B
  itc_reversed                  numeric(14,2) not null default 0, -- Table 7I
  net_itc_availed               numeric(14,2) not null default 0, -- Table 7J
  itc_as_per_2a                 numeric(14,2) not null default 0, -- Table 8A
  itc_reconciled_variance       numeric(14,2) not null default 0, -- Table 8D = 8A - (8B + 8C)
  
  -- Part IV: Tax Details
  total_tax_payable             numeric(14,2) not null default 0, -- Table 9
  tax_paid_cash                 numeric(14,2) not null default 0,
  tax_paid_itc                  numeric(14,2) not null default 0,
  shortfall_tax                 numeric(14,2) not null default 0,
  
  -- Part V: Subsequent FY Declarations
  previous_fy_amendments_tax    numeric(14,2) not null default 0, -- Table 14
  
  -- Filing metadata
  arn                           text,
  filed_at                      timestamptz,
  notes                         text,
  created_at                    timestamptz not null default now(),
  created_by                    uuid,
  updated_at                    timestamptz,
  updated_by                    uuid,
  constraint ux_gstr9_returns_client_fy unique (firm_id, client_id, financial_year)
);

create index if not exists ix_gstr9_returns_client on gstr9_returns (firm_id, client_id);
create index if not exists ix_gstr9_returns_fy on gstr9_returns (firm_id, financial_year);

alter table gstr9_returns enable row level security;
drop policy if exists tenant_isolation on gstr9_returns;
create policy tenant_isolation on gstr9_returns
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on gstr9_returns to cadesk_app;

-- Detailed Table Entries (Tables 4, 5, 6, 7, 8, 9, 10-14)
create table if not exists gstr9_table_entries (
  id                            uuid primary key,
  firm_id                       uuid not null references firms(id),
  gstr9_return_id               uuid not null references gstr9_returns(id) on delete cascade,
  table_section                 text not null, -- Table4, Table5, Table6, Table7, Table8, Table9, Table10_14
  row_code                      text not null, -- 4A, 4B, 5A, 6B, 7A, 8A, etc.
  description                   text not null,
  taxable_value                 numeric(14,2) not null default 0,
  igst_amount                   numeric(14,2) not null default 0,
  cgst_amount                   numeric(14,2) not null default 0,
  sgst_amount                   numeric(14,2) not null default 0,
  cess_amount                   numeric(14,2) not null default 0,
  created_at                    timestamptz not null default now(),
  created_by                    uuid,
  updated_at                    timestamptz,
  updated_by                    uuid
);

create index if not exists ix_gstr9_table_entries_return on gstr9_table_entries (firm_id, gstr9_return_id);

alter table gstr9_table_entries enable row level security;
drop policy if exists tenant_isolation on gstr9_table_entries;
create policy tenant_isolation on gstr9_table_entries
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on gstr9_table_entries to cadesk_app;

-- Form GSTR-9C Reconciliation Statements (Turnover > 5 Cr)
create table if not exists gstr9c_reconciliations (
  id                            uuid primary key,
  firm_id                       uuid not null references firms(id),
  gstr9_return_id               uuid not null references gstr9_returns(id) on delete cascade,
  client_id                     uuid not null references clients(id) on delete cascade,
  financial_year                text not null,
  
  -- Table 5: Gross Turnover Reconciliation
  audited_gross_turnover        numeric(14,2) not null default 0, -- Table 5A
  unbilled_revenue_start        numeric(14,2) not null default 0, -- Table 5B (+)
  unadjusted_advances_end       numeric(14,2) not null default 0, -- Table 5C (+)
  deemed_supplies               numeric(14,2) not null default 0, -- Table 5D (+)
  credit_notes_after_fy         numeric(14,2) not null default 0, -- Table 5E (+)
  trade_discounts_disallowed    numeric(14,2) not null default 0, -- Table 5F (+)
  unbilled_revenue_end          numeric(14,2) not null default 0, -- Table 5H (-)
  unadjusted_advances_start     numeric(14,2) not null default 0, -- Table 5I (-)
  other_turnover_adjustments    numeric(14,2) not null default 0, -- Table 5O (+/-)
  reconciled_turnover           numeric(14,2) not null default 0, -- Table 5P
  turnover_as_per_gstr9         numeric(14,2) not null default 0, -- Table 5Q
  unreconciled_turnover_variance numeric(14,2) not null default 0, -- Table 5R = 5P - 5Q
  
  -- Table 7: Taxable Turnover Reconciliation
  exempted_nil_turnover         numeric(14,2) not null default 0, -- Table 7B (-)
  zero_rated_wop_turnover       numeric(14,2) not null default 0, -- Table 7C (-)
  rcm_outward_turnover          numeric(14,2) not null default 0, -- Table 7D (-)
  reconciled_taxable_turnover   numeric(14,2) not null default 0, -- Table 7E = 5P - 7B - 7C - 7D
  taxable_turnover_gstr9        numeric(14,2) not null default 0, -- Table 7F
  unreconciled_taxable_variance numeric(14,2) not null default 0, -- Table 7G = 7E - 7F
  
  -- Table 9: Additional Tax Liability
  additional_tax_payable        numeric(14,2) not null default 0,
  additional_interest_payable   numeric(14,2) not null default 0,
  additional_penalty_payable    numeric(14,2) not null default 0,
  
  -- Table 12 & 14: ITC Reconciliation
  book_itc_total                numeric(14,2) not null default 0, -- Table 12A
  gstr9_itc_total               numeric(14,2) not null default 0, -- Table 12E
  unreconciled_itc_variance     numeric(14,2) not null default 0, -- Table 12F = 12A - 12E
  
  -- Certification & Verification
  certification_status          text not null default 'Draft',    -- Draft, UnderReview, Certified
  auditor_name                  text,
  auditor_membership_no         text,
  auditor_firm_reg_no           text,
  udin                          text,
  certified_at                  timestamptz,
  auditor_remarks               text,
  
  -- DRC-03 Payment Tracking
  drc03_arn                     text,
  drc03_date                    date,
  drc03_paid_amount             numeric(14,2) not null default 0,
  drc03_payment_mode            text default 'CashLedger',
  is_drc03_completed            boolean not null default false,
  
  created_at                    timestamptz not null default now(),
  created_by                    uuid,
  updated_at                    timestamptz,
  updated_by                    uuid,
  constraint ux_gstr9c_returns unique (firm_id, gstr9_return_id)
);

create index if not exists ix_gstr9c_reconciliations_client on gstr9c_reconciliations (firm_id, client_id);
create index if not exists ix_gstr9c_reconciliations_fy on gstr9c_reconciliations (firm_id, financial_year);

alter table gstr9c_reconciliations enable row level security;
drop policy if exists tenant_isolation on gstr9c_reconciliations;
create policy tenant_isolation on gstr9c_reconciliations
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on gstr9c_reconciliations to cadesk_app;

-- Discrepancies Register with Statutory Reason Codes
create table if not exists gstr9c_discrepancies (
  id                            uuid primary key,
  firm_id                       uuid not null references firms(id),
  gstr9c_id                     uuid not null references gstr9c_reconciliations(id) on delete cascade,
  category                      text not null, -- TurnoverTimingDifference, UnbilledRevenue, AdvanceAdjustment, CreditNoteAdjustment, RateMismatch, ItcBookVsReturnMismatch, ExpenseItcUnclaimed, NonGstSupplyDifference, Other
  table_ref                     text not null, -- Table5, Table7, Table9, Table12, Table14
  description                   text not null,
  discrepancy_amount            numeric(14,2) not null default 0,
  tax_impact                    numeric(14,2) not null default 0,
  auditor_recommendation        text,
  is_resolved                   boolean not null default false,
  resolution_notes              text,
  created_at                    timestamptz not null default now(),
  created_by                    uuid,
  updated_at                    timestamptz,
  updated_by                    uuid
);

create index if not exists ix_gstr9c_discrepancies_rec on gstr9c_discrepancies (firm_id, gstr9c_id);

alter table gstr9c_discrepancies enable row level security;
drop policy if exists tenant_isolation on gstr9c_discrepancies;
create policy tenant_isolation on gstr9c_discrepancies
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on gstr9c_discrepancies to cadesk_app;
