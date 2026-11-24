-- 0063_gst_b2b_tax_invoicing.sql: Rule 46 & Section 31 CGST Act B2B Tax Invoicing
-- Supplier and recipient GSTIN details, state codes, place of supply, reverse charge, and tax breakups

alter table invoices add column if not exists supplier_name text not null default '';
alter table invoices add column if not exists supplier_address text not null default '';
alter table invoices add column if not exists supplier_gstin text not null default '';
alter table invoices add column if not exists supplier_state text not null default 'Kerala';
alter table invoices add column if not exists supplier_state_code text not null default '32';

alter table invoices add column if not exists recipient_legal_name text not null default '';
alter table invoices add column if not exists recipient_trade_name text not null default '';
alter table invoices add column if not exists recipient_address text not null default '';
alter table invoices add column if not exists recipient_gstin text not null default '';
alter table invoices add column if not exists recipient_state text not null default '';
alter table invoices add column if not exists recipient_state_code text not null default '';

alter table invoices add column if not exists place_of_supply text not null default '';
alter table invoices add column if not exists place_of_supply_state_code text not null default '';
alter table invoices add column if not exists is_reverse_charge boolean not null default false;
alter table invoices add column if not exists is_inter_state boolean not null default false;

alter table invoices add column if not exists cgst_amount numeric(12,2) not null default 0;
alter table invoices add column if not exists sgst_amount numeric(12,2) not null default 0;
alter table invoices add column if not exists igst_amount numeric(12,2) not null default 0;
alter table invoices add column if not exists cess_amount numeric(12,2) not null default 0;

alter table invoice_items add column if not exists unit text not null default 'NOS';
alter table invoice_items add column if not exists cgst_rate numeric(5,2) not null default 0;
alter table invoice_items add column if not exists cgst_amount numeric(12,2) not null default 0;
alter table invoice_items add column if not exists sgst_rate numeric(5,2) not null default 0;
alter table invoice_items add column if not exists sgst_amount numeric(12,2) not null default 0;
alter table invoice_items add column if not exists igst_rate numeric(5,2) not null default 0;
alter table invoice_items add column if not exists igst_amount numeric(12,2) not null default 0;
alter table invoice_items add column if not exists cess_rate numeric(5,2) not null default 0;
alter table invoice_items add column if not exists cess_amount numeric(12,2) not null default 0;
