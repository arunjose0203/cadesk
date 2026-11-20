-- 0018_engagement_kyc: Digital Engagement Letters (ICAI SA 210 / SQC 1) & Anti-Money Laundering / ICAI KYC Compliance.
-- Supports Engagement Letters with fee structures, management/auditor terms, digital acceptance workflows,
-- and Client KYC Records with PMLA risk rating (Low/Medium/High/PEP), UBO beneficial ownership tracking,
-- and review calendar automation.

-- ---------------------------------------------------------------- engagement_letters

create table if not exists engagement_letters (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  client_id                   uuid not null,
  letter_number               text not null check (length(letter_number) between 3 and 50),
  engagement_type             text not null check (engagement_type in ('StatutoryAudit','InternalAudit','TaxAudit','GstAudit','DirectTaxFiling','SecretarialCompliance','AccountingBookkeeping','DueDiligence','Advisory')),
  financial_year              text not null check (financial_year ~ '^[0-9]{4}-[0-9]{2}$'),
  scope_of_work               text not null check (length(scope_of_work) between 10 and 2000),
  management_responsibilities text not null check (length(management_responsibilities) between 10 and 2000),
  auditor_responsibilities    text not null check (length(auditor_responsibilities) between 10 and 2000),
  fee_structure               text not null default 'FixedFee' check (fee_structure in ('FixedFee','TimeBased','MonthlyRetainer','QuarterlyRetainer')),
  agreed_fee_amount           numeric(14,2) not null default 0 check (agreed_fee_amount >= 0),
  out_of_pocket_included      boolean not null default false,
  status                      text not null default 'Draft' check (status in ('Draft','SentToClient','Accepted','SignedDigitally','Declined','Superseded')),
  sent_date                   timestamptz,
  accepted_date               timestamptz,
  client_signatory_name       text,
  client_signatory_designation text,
  acceptance_ip               text,
  letter_content              text not null,
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_engagement_letter_id_firm unique (id, firm_id),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade
);

create index if not exists ix_engagement_letters_firm_client on engagement_letters (firm_id, client_id);
create index if not exists ix_engagement_letters_firm_type on engagement_letters (firm_id, engagement_type, financial_year);

-- ---------------------------------------------------------------- client_kyc_records

create table if not exists client_kyc_records (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  client_id                   uuid not null,
  risk_rating                 text not null default 'Low' check (risk_rating in ('Low','Medium','High','PoliticallyExposedPerson')),
  verification_status         text not null default 'PendingVerification' check (verification_status in ('Verified','PendingVerification','Rejected')),
  constitution_document_type  text not null check (length(constitution_document_type) between 2 and 100),
  constitution_document_number text check (constitution_document_number is null or length(constitution_document_number) between 3 and 100),
  pan_verified                boolean not null default false,
  gstin_verified              boolean not null default false,
  address_proof_type          text,
  address_proof_number        text,
  ubo_name                    text check (ubo_name is null or length(ubo_name) between 2 and 150),
  ubo_shareholding_percentage numeric(5,2) check (ubo_shareholding_percentage is null or (ubo_shareholding_percentage >= 0 and ubo_shareholding_percentage <= 100)),
  ubo_pan                     text check (ubo_pan is null or ubo_pan ~ '^[A-Z]{5}[0-9]{4}[A-Z]{1}$'),
  sanctions_check_cleared     boolean not null default true,
  adverse_media_check_cleared boolean not null default true,
  risk_rationale              text,
  last_reviewed_date          date not null,
  next_review_due_date        date not null,
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_client_kyc_id_firm unique (id, firm_id),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade
);

create index if not exists ix_client_kyc_firm_client on client_kyc_records (firm_id, client_id);
create index if not exists ix_client_kyc_firm_risk on client_kyc_records (firm_id, risk_rating);

-- ---------------------------------------------------------------- row-level security

alter table engagement_letters enable row level security;
drop policy if exists tenant_isolation on engagement_letters;
create policy tenant_isolation on engagement_letters
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

alter table client_kyc_records enable row level security;
drop policy if exists tenant_isolation on client_kyc_records;
create policy tenant_isolation on client_kyc_records
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on engagement_letters to cadesk_app;
grant select, insert, update, delete on client_kyc_records to cadesk_app;

-- ---------------------------------------------------------------- permissions

insert into permissions (code, area, description) values
  ('engagement.read',   'engagement', 'View digital engagement letters, contract terms, and fee structures'),
  ('engagement.manage', 'engagement', 'Issue engagement letters, configure audit/tax scope, and process digital acceptances'),
  ('kyc.read',          'kyc',        'View client AML/PMLA KYC records, risk classifications, and UBO beneficial ownership'),
  ('kyc.manage',        'kyc',        'Update client risk profiles, verify constitution documents, and clear sanctions/PEP checks')
on conflict (code) do nothing;
