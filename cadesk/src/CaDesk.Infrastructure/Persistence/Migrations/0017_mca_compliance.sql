-- 0017_mca_compliance: MCA 21 / ROC Corporate Governance, Company Law Compliance (Companies Act 2013).
-- Supports Company Master (CIN 21-char), Director DIN management (8-digit DIN, DIR-3 KYC tracking),
-- MCA statutory annual filings (AOC-4, MGT-7, ADT-1, DPT-3 etc. with Section 403 late fee engine),
-- and Board & Committee meeting resolutions register.

-- ---------------------------------------------------------------- company_master

create table if not exists company_master (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  client_id                   uuid not null,
  cin                         text not null check (cin ~ '^[LU][0-9]{5}[A-Z]{2}[0-9]{4}[A-Z]{3}[0-9]{6}$'),
  company_name                text not null check (length(company_name) between 3 and 250),
  company_class               text not null default 'Private' check (company_class in ('Private','Public','OnePersonCompany','Section8','LLP')),
  authorized_capital          numeric(15,2) not null default 100000 check (authorized_capital >= 0),
  paid_up_capital             numeric(15,2) not null default 100000 check (paid_up_capital >= 0),
  registered_office_address   text not null check (length(registered_office_address) between 5 and 500),
  roc_jurisdiction            text not null check (length(roc_jurisdiction) between 3 and 100),
  incorporation_date          date not null,
  company_status              text not null default 'Active' check (company_status in ('Active','StrikeOff','Amalgamated','UnderLiquidation','Dormant')),
  last_agm_date               date,
  financial_year_end_month    int not null default 3 check (financial_year_end_month between 1 and 12),
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_company_master_id_firm unique (id, firm_id),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade
);

create index if not exists ix_company_master_firm_client on company_master (firm_id, client_id);
create index if not exists ix_company_master_firm_cin on company_master (firm_id, cin);

-- ---------------------------------------------------------------- director_din_records

create table if not exists director_din_records (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  company_id                  uuid not null,
  din                         text not null check (din ~ '^[0-9]{8}$'),
  director_name               text not null check (length(director_name) between 2 and 150),
  pan                         text check (pan is null or pan ~ '^[A-Z]{5}[0-9]{4}[A-Z]{1}$'),
  designation                 text not null default 'Director' check (designation in ('Director','ManagingDirector','WholeTimeDirector','IndependentDirector','NomineeDirector','AdditionalDirector','DesignatedPartner')),
  appointment_date            date not null,
  cessation_date              date,
  dsc_expiry_date             date,
  dir3_kyc_status             text not null default 'Complied' check (dir3_kyc_status in ('Complied','Pending','DeactivatedDueToNonFiling')),
  dir3_kyc_last_filed_date    date,
  is_signatory                boolean not null default true,
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_director_din_id_firm unique (id, firm_id),
  foreign key (company_id, firm_id) references company_master(id, firm_id) on delete cascade
);

create index if not exists ix_director_din_firm_company on director_din_records (firm_id, company_id);
create index if not exists ix_director_din_firm_din on director_din_records (firm_id, din);

-- ---------------------------------------------------------------- mca_annual_filings

create table if not exists mca_annual_filings (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  company_id                  uuid not null,
  financial_year              text not null check (financial_year ~ '^[0-9]{4}-[0-9]{2}$'),
  form_type                   text not null check (form_type in ('AOC-4','AOC-4_XBRL','AOC-4_CFS','MGT-7','MGT-7A','ADT-1','DPT-3','MSME-1','DIR-12','MGT-14','CHG-1')),
  due_date                    date not null,
  filing_date                 date,
  srn                         text check (srn is null or length(srn) between 9 and 15),
  status                      text not null default 'Pending' check (status in ('Pending','ChallanGenerated','Uploaded','Approved','Defective')),
  normal_fee                  numeric(10,2) not null default 0,
  late_fee                    numeric(10,2) not null default 0,
  total_fee                   numeric(10,2) not null default 0,
  remarks                     text,
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_mca_filing_id_firm unique (id, firm_id),
  foreign key (company_id, firm_id) references company_master(id, firm_id) on delete cascade
);

create index if not exists ix_mca_filings_firm_company on mca_annual_filings (firm_id, company_id, financial_year);

-- ---------------------------------------------------------------- board_resolutions

create table if not exists board_resolutions (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  company_id                  uuid not null,
  resolution_number           text not null check (length(resolution_number) between 3 and 50),
  meeting_type                text not null check (meeting_type in ('BoardMeeting','AGM','EGM','CommitteeMeeting')),
  meeting_date                date not null,
  category                    text not null default 'Ordinary' check (category in ('Ordinary','Special','Unanimous')),
  subject                     text not null check (length(subject) between 5 and 250),
  verbiage                    text not null,
  passed_unanimously          boolean not null default true,
  signatory_din               text,
  signatory_name              text,
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_board_resolution_id_firm unique (id, firm_id),
  foreign key (company_id, firm_id) references company_master(id, firm_id) on delete cascade
);

create index if not exists ix_board_resolutions_firm_company on board_resolutions (firm_id, company_id, meeting_date);

-- ---------------------------------------------------------------- row-level security

alter table company_master enable row level security;
drop policy if exists tenant_isolation on company_master;
create policy tenant_isolation on company_master
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

alter table director_din_records enable row level security;
drop policy if exists tenant_isolation on director_din_records;
create policy tenant_isolation on director_din_records
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

alter table mca_annual_filings enable row level security;
drop policy if exists tenant_isolation on mca_annual_filings;
create policy tenant_isolation on mca_annual_filings
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

alter table board_resolutions enable row level security;
drop policy if exists tenant_isolation on board_resolutions;
create policy tenant_isolation on board_resolutions
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on company_master to cadesk_app;
grant select, insert, update, delete on director_din_records to cadesk_app;
grant select, insert, update, delete on mca_annual_filings to cadesk_app;
grant select, insert, update, delete on board_resolutions to cadesk_app;

-- ---------------------------------------------------------------- permissions

insert into permissions (code, area, description) values
  ('mca.read',   'mca', 'View MCA company master, secretarial filings, DIN records, and resolutions'),
  ('mca.manage', 'mca', 'Manage MCA companies, record annual filings, DIN KYC and draft resolutions')
on conflict (code) do nothing;
