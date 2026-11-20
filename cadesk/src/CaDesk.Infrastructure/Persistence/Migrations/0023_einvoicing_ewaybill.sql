-- 0023_einvoicing_ewaybill: Automated E-Invoicing & E-Way Bill Integration Desk.
-- Tracks IRP standard JSON generation, 64-char SHA-256 IRN generation, signed QR codes,
-- E-Way Bill Part A & Part B, transporter details, distance estimation, and Rule 138(10) validity countdowns.

create table if not exists einvoice_records (
  id                    uuid primary key,
  firm_id               uuid not null references firms(id),
  client_id             uuid not null references clients(id) on delete cascade,
  invoice_id            uuid references invoices(id) on delete set null,
  doc_type              text not null default 'INV', -- INV, CRN, DBN
  doc_number            text not null,
  doc_date              date not null,
  financial_year        text not null,
  supplier_gstin        text not null,
  supplier_trade_name   text not null,
  recipient_gstin       text not null,
  recipient_legal_name  text not null,
  supply_type           text not null default 'B2B', -- B2B, SEZWP, SEZWOP, EXPWP, EXPWOP, DEXP
  total_taxable_value   numeric(14,2) not null default 0,
  cgst_amount           numeric(14,2) not null default 0,
  sgst_amount           numeric(14,2) not null default 0,
  igst_amount           numeric(14,2) not null default 0,
  cess_amount           numeric(14,2) not null default 0,
  total_invoice_value   numeric(14,2) not null default 0,
  irn                   text,                        -- 64-character hexadecimal SHA-256 hash
  signed_qr_code        text,
  ack_no                text,
  ack_date              timestamptz,
  status                text not null default 'Draft', -- Draft, Generated, Cancelled, Failed
  cancel_reason         text,
  cancel_remarks        text,
  cancelled_at          timestamptz,
  created_at            timestamptz not null default now(),
  created_by            uuid,
  updated_at            timestamptz,
  updated_by            uuid,
  constraint ux_einvoice_records_supplier_doc unique (firm_id, supplier_gstin, financial_year, doc_type, doc_number)
);

create index if not exists ix_einvoice_records_firm_client on einvoice_records (firm_id, client_id);
create index if not exists ix_einvoice_records_irn on einvoice_records (firm_id, irn);

alter table einvoice_records enable row level security;
drop policy if exists tenant_isolation on einvoice_records;
create policy tenant_isolation on einvoice_records
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on einvoice_records to cadesk_app;

-- E-Way Bills (Part A & Part B)
create table if not exists eway_bill_records (
  id                    uuid primary key,
  firm_id               uuid not null references firms(id),
  client_id             uuid not null references clients(id) on delete cascade,
  einvoice_id           uuid references einvoice_records(id) on delete set null,
  ewb_number            text not null, -- 12-digit E-Way Bill Number
  ewb_date              timestamptz not null default now(),
  valid_until           timestamptz not null,
  supply_type           text not null default 'Outward', -- Outward, Inward
  sub_supply_type       text not null default 'Supply',  -- Supply, Export, JobWork, SKD_CKD, LineSale, Exhibition, Others
  doc_type              text not null default 'INV',     -- INV, CHL, BIL
  doc_number            text not null,
  doc_date              date not null,
  from_gstin            text not null,
  from_trade_name       text not null,
  from_pincode          text not null,
  from_state_code       integer not null,
  to_gstin              text not null,
  to_trade_name         text not null,
  to_pincode            text not null,
  to_state_code         integer not null,
  total_value           numeric(14,2) not null default 0,
  hsn_code              text not null,
  distance_km           integer not null default 0,
  transport_mode        text not null default 'Road',    -- Road, Rail, Air, Ship
  vehicle_type          text not null default 'Regular', -- Regular, OverDimensionalCargo
  vehicle_number        text,
  transporter_id        text,
  transporter_name      text,
  status                text not null default 'Active',  -- Active, Extended, Cancelled, Expired
  cancel_reason         text,
  cancelled_at          timestamptz,
  created_at            timestamptz not null default now(),
  created_by            uuid,
  updated_at            timestamptz,
  updated_by            uuid,
  constraint ux_eway_bill_records_ewb_num unique (firm_id, ewb_number)
);

create index if not exists ix_eway_bill_records_firm_client on eway_bill_records (firm_id, client_id);
create index if not exists ix_eway_bill_records_valid on eway_bill_records (firm_id, valid_until);

alter table eway_bill_records enable row level security;
drop policy if exists tenant_isolation on eway_bill_records;
create policy tenant_isolation on eway_bill_records
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on eway_bill_records to cadesk_app;

-- Permissions Catalogue
insert into permissions (code, area, description) values
  ('einvoice.read', 'einvoicing', 'View E-Invoices, IRN details, QR codes, and E-Way Bills'),
  ('einvoice.generate', 'einvoicing', 'Generate E-Invoices, IRN hashes, and Part A/B E-Way Bills'),
  ('einvoice.cancel', 'einvoicing', 'Cancel IRN and E-Way Bills within statutory window')
on conflict (code) do nothing;
