-- 0034_foreign_remittance_15ca_15cb.sql: Foreign Remittance & Form 15CA / 15CB Certification Engine (Income Tax Rule 37BB / Section 195 / DTAA)

create table if not exists foreign_remittance_assessments (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  client_id                       uuid not null references clients(id) on delete cascade,
  financial_year                  text not null, -- e.g. "2024-25"
  remittance_reference            text not null, -- e.g. "REM-2024-001"

  -- Remitter Details
  remitter_pan                    text not null default '',
  remitter_tan                    text not null default '',
  remitter_name                   text not null default '',
  remitter_status                 text not null default 'Company',
  remitter_address                text not null default '',
  remitter_email                  text not null default '',
  remitter_phone                  text not null default '',

  -- Beneficiary Details
  beneficiary_name                text not null default '',
  beneficiary_country             text not null default '',
  beneficiary_country_code        text not null default '',
  beneficiary_address             text not null default '',
  beneficiary_tax_id              text not null default '',
  beneficiary_entity_type         text not null default 'Company',

  -- Remittance Particulars
  remittance_currency             text not null default 'USD',
  foreign_amount                  numeric(16,2) not null default 0,
  exchange_rate                   numeric(12,4) not null default 0,
  inr_equivalent                  numeric(16,2) not null default 0,
  proposed_remittance_date        timestamptz,
  rbi_purpose_code                text not null default 'P0802',
  purpose_description             text not null default '',
  nature_of_remittance            text not null default 'FeesForTechnicalServices',
  contract_description            text not null default '',
  invoice_number                  text not null default '',
  invoice_date                    timestamptz,

  -- Domestic Taxability
  is_chargeable_to_tax            boolean not null default true,
  chargeability_reason            text not null default '',
  domestic_section                text not null default 'Section 195 read with Section 115A',
  domestic_tax_rate               numeric(8,4) not null default 20.00,
  surcharge_rate                  numeric(8,4) not null default 0.00,
  cess_rate                       numeric(8,4) not null default 4.00,
  effective_domestic_rate         numeric(8,4) not null default 20.80,

  -- DTAA / Treaty Relief
  treaty_benefit_claimed          boolean not null default true,
  treaty_country                  text not null default '',
  treaty_article                  text not null default 'Article 12 - Royalties and Fees for Technical Services',
  treaty_tax_rate                 numeric(8,4) not null default 10.00,
  has_trc                         boolean not null default true,
  trc_period_from                 timestamptz,
  trc_period_to                   timestamptz,
  has_form10f                     boolean not null default true,
  no_pe_declaration               boolean not null default true,
  beneficial_ownership_confirmed  boolean not null default true,
  mli_ppt_passed                  boolean not null default true,
  applicable_rate_type            text not null default 'TreatyRate',
  final_withholding_rate          numeric(8,4) not null default 10.00,

  -- Grossing up & Calculations
  tax_borne_by_remitter           boolean not null default false,
  grossed_up_amount_inr           numeric(16,2) not null default 0,
  tds_amount_inr                  numeric(16,2) not null default 0,
  tds_amount_fc                   numeric(16,2) not null default 0,
  net_remittance_fc               numeric(16,2) not null default 0,
  net_remittance_inr              numeric(16,2) not null default 0,

  -- Form 15CA Routing
  form15ca_part                   text not null default 'PartC',
  form15ca_routing_reason         text not null default '',
  ao_order_section                text,
  ao_order_number                 text,
  ao_order_date                   timestamptz,
  ao_certificate_rate             numeric(8,4),

  -- Form 15CB CA Certification
  requires_form15cb               boolean not null default true,
  ca_membership_number            text not null default '',
  ca_name                         text not null default '',
  ca_firm_name                    text not null default '',
  ca_frn                          text not null default '',
  udin                            text not null default '',
  certified_at                    timestamptz,
  certifier_user_id               uuid references users(id) on delete set null,

  -- Generated Artifacts
  generated_15cb_certificate_text text,
  generated_15ca_xml_payload      text,
  acknowledgement_number          text,

  status                          text not null default 'Draft',
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_foreign_remittance_assessments_client_fy
  on foreign_remittance_assessments (firm_id, client_id, financial_year);

alter table foreign_remittance_assessments enable row level security;
alter table foreign_remittance_assessments force row level security;
drop policy if exists tenant_isolation on foreign_remittance_assessments;
create policy tenant_isolation on foreign_remittance_assessments
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on foreign_remittance_assessments to cadesk_app;

-- Foreign Remittance Documentation Checklist
create table if not exists foreign_remittance_checklists (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  assessment_id                   uuid not null references foreign_remittance_assessments(id) on delete cascade,
  item_code                       text not null, -- TRC, FORM_10F, NO_PE, INVOICE, CONTRACT, SEC_197_LDC
  item_description                text not null,
  is_mandatory                    boolean not null default true,
  status                          text not null default 'Pending', -- Pending, Verified, Rejected, Waived
  verification_notes              text not null default '',
  verified_at                     timestamptz,
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_foreign_remittance_checklists_assessment
  on foreign_remittance_checklists (firm_id, assessment_id);

alter table foreign_remittance_checklists enable row level security;
alter table foreign_remittance_checklists force row level security;
drop policy if exists tenant_isolation on foreign_remittance_checklists;
create policy tenant_isolation on foreign_remittance_checklists
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on foreign_remittance_checklists to cadesk_app;
