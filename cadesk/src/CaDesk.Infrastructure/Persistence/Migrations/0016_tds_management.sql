-- 0016_tds_management: TDS/TCS Computation, Lower Deduction Certificates & Form 26Q/27Q Preparation.
-- Supports Chapter XVII-B sections (194C/J/I/Q/A/H, 195), PAN 206AA / 206AB rates, LDC limits,
-- monthly ITNS 281 Challan mapping, and 201(1A) interest / 234E late fee calculations.

create table if not exists tds_lower_deduction_certificates (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  client_id                   uuid not null,
  deductee_pan                text not null check (deductee_pan ~ '^[A-Z]{5}[0-9]{4}[A-Z]{1}$'),
  deductee_name               text not null check (length(deductee_name) between 2 and 150),
  section                     text not null,
  certificate_number          text not null check (length(certificate_number) between 8 and 30),
  financial_year              text not null check (financial_year ~ '^[0-9]{4}-[0-9]{2}$'),
  valid_from                  date not null,
  valid_to                    date not null,
  specified_rate              numeric(5,2) not null check (specified_rate >= 0),
  threshold_amount            numeric(14,2) not null check (threshold_amount > 0),
  utilized_amount             numeric(14,2) not null default 0,
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_tds_ldc_id_firm unique (id, firm_id),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade
);

create index if not exists ix_tds_ldc_firm_client on tds_lower_deduction_certificates (firm_id, client_id, deductee_pan);

create table if not exists tds_challans (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  client_id                   uuid not null,
  quarter                     text not null check (quarter in ('Q1','Q2','Q3','Q4')),
  financial_year              text not null check (financial_year ~ '^[0-9]{4}-[0-9]{2}$'),
  bsr_code                    text not null check (bsr_code ~ '^[0-9]{7}$'),
  challan_date                date not null,
  challan_serial_number       text not null check (challan_serial_number ~ '^[0-9]{5}$'),
  section_code                text not null,
  minor_head                  text not null default '200_regular' check (minor_head in ('200_regular','400_assessment')),
  tds_amount                  numeric(14,2) not null default 0,
  surcharge                   numeric(14,2) not null default 0,
  cess                        numeric(14,2) not null default 0,
  interest                    numeric(14,2) not null default 0,
  late_fee234_e               numeric(14,2) not null default 0,
  total_challan_amount        numeric(14,2) not null default 0,
  allocated_amount            numeric(14,2) not null default 0,
  notes                       text,
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_tds_challans_id_firm unique (id, firm_id),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade
);

create index if not exists ix_tds_challans_firm_client on tds_challans (firm_id, client_id, financial_year, quarter);

create table if not exists tds_deductions (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  client_id                   uuid not null,
  challan_id                  uuid,
  form_type                   text not null default '26Q' check (form_type in ('26Q','27Q','27EQ')),
  quarter                     text not null check (quarter in ('Q1','Q2','Q3','Q4')),
  financial_year              text not null check (financial_year ~ '^[0-9]{4}-[0-9]{2}$'),
  deductee_pan                text not null,
  deductee_name               text not null,
  section                     text not null,
  payment_date                date not null,
  amount_paid                 numeric(14,2) not null check (amount_paid > 0),
  tds_rate                    numeric(5,2) not null check (tds_rate >= 0),
  tds_amount                  numeric(14,2) not null default 0,
  surcharge                   numeric(14,2) not null default 0,
  cess                        numeric(14,2) not null default 0,
  total_tax_deducted          numeric(14,2) not null default 0,
  deduction_date              date not null,
  certificate_id              uuid,
  higher_rate_reason          text not null default 'none' check (higher_rate_reason in ('none','no_pan_206aa','non_filer_206ab')),
  booking_status              text not null default 'pending_deposit' check (booking_status in ('pending_deposit','deposited','mapped_to_challan')),
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_tds_deductions_id_firm unique (id, firm_id),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade,
  foreign key (challan_id, firm_id) references tds_challans(id, firm_id) on delete set null,
  foreign key (certificate_id, firm_id) references tds_lower_deduction_certificates(id, firm_id) on delete set null
);

create index if not exists ix_tds_deductions_firm_client on tds_deductions (firm_id, client_id, financial_year, quarter, form_type);

-- ---------------------------------------------------------------- row-level security

alter table tds_lower_deduction_certificates enable row level security;
drop policy if exists tenant_isolation on tds_lower_deduction_certificates;
create policy tenant_isolation on tds_lower_deduction_certificates
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

alter table tds_challans enable row level security;
drop policy if exists tenant_isolation on tds_challans;
create policy tenant_isolation on tds_challans
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

alter table tds_deductions enable row level security;
drop policy if exists tenant_isolation on tds_deductions;
create policy tenant_isolation on tds_deductions
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on tds_lower_deduction_certificates to cadesk_app;
grant select, insert, update, delete on tds_challans to cadesk_app;
grant select, insert, update, delete on tds_deductions to cadesk_app;

-- ---------------------------------------------------------------- permissions

insert into permissions (code, area, description) values
  ('tds.read',   'tds', 'View TDS registers, challans, lower deduction certificates, and 26Q/27Q preparation'),
  ('tds.manage', 'tds', 'Record TDS deductions, apply lower rate certificates, and allocate challans')
on conflict (code) do nothing;
