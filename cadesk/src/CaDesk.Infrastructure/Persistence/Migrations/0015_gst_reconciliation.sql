-- 0015_gst_reconciliation: GST 2A / 2B vs Purchase Register Reconciliation Engine.
-- Matches client book purchases against GST portal GSTR-2B files per Section 16(2)(aa) & Rule 36(4).
-- Detects exact matches, round-off tolerances, tax value discrepancies, missing in 2B, and unbooked purchases.

create table if not exists gst_reconciliation_batches (
  id                              uuid primary key,
  firm_id                         uuid not null references firms(id),
  client_id                       uuid not null,
  return_period                   text not null check (return_period ~ '^[0-9]{2}[0-9]{4}$|^[0-9]{4}-[0-9]{2}$'),
  financial_year                  text not null check (financial_year ~ '^[0-9]{4}-[0-9]{2}$'),
  status                          text not null default 'completed' check (status in ('pending','processing','completed','failed')),
  tolerance_amount                numeric(6,2) not null default 1.00,
  purchase_register_count         integer not null default 0,
  gstr2_b_count                   integer not null default 0,
  matched_count                   integer not null default 0,
  matched_with_tolerance_count    integer not null default 0,
  missing_in2_b_count             integer not null default 0,
  missing_in_books_count          integer not null default 0,
  mismatched_tax_count            integer not null default 0,
  total_pr_taxable                numeric(14,2) not null default 0,
  total_pr_tax                    numeric(14,2) not null default 0,
  total2_b_taxable                numeric(14,2) not null default 0,
  total2_b_tax                    numeric(14,2) not null default 0,
  total_itc_eligible              numeric(14,2) not null default 0,
  total_itc_ineligible            numeric(14,2) not null default 0,
  notes                           text check (notes is null or length(notes) <= 4000),
  created_at                      timestamptz not null default now(),
  created_by                      uuid,
  updated_at                      timestamptz,
  updated_by                      uuid,
  constraint ux_gst_reconciliation_batches_id_firm unique (id, firm_id),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade
);

create index if not exists ix_gst_reconciliation_batches_firm_client on gst_reconciliation_batches (firm_id, client_id, return_period);

create table if not exists gst_reconciliation_items (
  id                              uuid primary key,
  firm_id                         uuid not null references firms(id),
  batch_id                        uuid not null,
  supplier_gstin                  text not null check (supplier_gstin ~ '^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$'),
  supplier_name                   text,
  invoice_number                  text not null check (length(invoice_number) between 1 and 50),
  normalized_invoice_number       text not null,
  invoice_date                    date,
  invoice_type                    text not null default 'regular' check (invoice_type in ('regular','debit_note','credit_note','sez')),
  
  -- Purchase Register (Books)
  pr_taxable_value                numeric(14,2) not null default 0,
  pr_cgst                         numeric(14,2) not null default 0,
  pr_sgst                         numeric(14,2) not null default 0,
  pr_igst                         numeric(14,2) not null default 0,
  pr_cess                         numeric(14,2) not null default 0,

  -- GSTR-2B (Portal)
  gstr2_b_taxable_value           numeric(14,2) not null default 0,
  gstr2_b_cgst                    numeric(14,2) not null default 0,
  gstr2_b_sgst                    numeric(14,2) not null default 0,
  gstr2_b_igst                    numeric(14,2) not null default 0,
  gstr2_b_cess                    numeric(14,2) not null default 0,

  match_status                    text not null default 'exact_match' check (match_status in ('exact_match','match_with_tolerance','missing_in_2b','missing_in_books','tax_mismatch','date_mismatch')),
  itc_eligibility                 text not null default 'eligible' check (itc_eligibility in ('eligible','ineligible_17_5','pos_mismatch','supplier_cancelled')),
  tax_difference                  numeric(14,2) not null default 0,
  notes                           text,
  created_at                      timestamptz not null default now(),
  constraint ux_gst_reconciliation_items_id_firm unique (id, firm_id),
  foreign key (batch_id, firm_id) references gst_reconciliation_batches(id, firm_id) on delete cascade
);

create index if not exists ix_gst_reconciliation_items_batch on gst_reconciliation_items (firm_id, batch_id, match_status);

-- ---------------------------------------------------------------- row-level security

alter table gst_reconciliation_batches enable row level security;
drop policy if exists tenant_isolation on gst_reconciliation_batches;
create policy tenant_isolation on gst_reconciliation_batches
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

alter table gst_reconciliation_items enable row level security;
drop policy if exists tenant_isolation on gst_reconciliation_items;
create policy tenant_isolation on gst_reconciliation_items
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on gst_reconciliation_batches to cadesk_app;
grant select, insert, update, delete on gst_reconciliation_items to cadesk_app;

-- ---------------------------------------------------------------- permissions

insert into permissions (code, area, description) values
  ('gst.read',      'gst', 'View GST 2A/2B reconciliation batches and reports'),
  ('gst.reconcile', 'gst', 'Run GST purchase register matching and ITC reconciliation')
on conflict (code) do nothing;
