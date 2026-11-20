-- 0033_audit_committee_governance.sql: Audit Committee & Board Governance Desk (ICAI SA 260 / SA 265 / Companies Act Sec 177)

create table if not exists audit_committee_engagements (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  client_id                       uuid not null references clients(id) on delete cascade,
  audit_engagement_id             uuid references audit_engagements(id) on delete set null,
  financial_year                  text not null, -- e.g. "2024-25"
  title                           text not null,
  meeting_type                    text not null default 'Pre-Issuance', -- Annual, Quarterly, Pre-Issuance, Special
  meeting_date                    timestamptz,
  meeting_location                text,
  committee_chair_name            text not null default '',
  lead_partner_name               text not null default '',
  status                          text not null default 'Draft', -- Draft, PackGenerated, Presented, SignedOff
  meeting_minutes                 text,
  generated_governance_pack_text  text,
  generated_sa265_letter_text     text,
  signed_off_by_user_id           uuid references users(id) on delete set null,
  signed_off_at                   timestamptz,
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_audit_committee_engagements_client_fy
  on audit_committee_engagements (firm_id, client_id, financial_year);

alter table audit_committee_engagements enable row level security;
alter table audit_committee_engagements force row level security;
drop policy if exists tenant_isolation on audit_committee_engagements;
create policy tenant_isolation on audit_committee_engagements
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on audit_committee_engagements to cadesk_app;

-- SA 265 Internal Control Deficiencies
create table if not exists internal_control_deficiencies (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  committee_engagement_id         uuid not null references audit_committee_engagements(id) on delete cascade,
  deficiency_code                 text not null, -- e.g. "ICD-01"
  title                           text not null,
  process_area                    text not null default 'Revenue Recognition',
  deficiency_type                 text not null default 'DesignDeficiency', -- DesignDeficiency, OperatingDeficiency
  severity                        text not null default 'SignificantDeficiency', -- SignificantDeficiency, OtherDeficiency
  description                     text not null default '',
  potential_financial_effect      text not null default '',
  estimated_risk_amount           numeric(16,2),
  auditor_recommendation          text not null default '',
  management_response             text,
  remediation_target_date         timestamptz,
  status                          text not null default 'Identified', -- Identified, CommunicatedToManagement, Remediated, CarriedForward
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_internal_control_deficiencies_engagement
  on internal_control_deficiencies (firm_id, committee_engagement_id);

alter table internal_control_deficiencies enable row level security;
alter table internal_control_deficiencies force row level security;
drop policy if exists tenant_isolation on internal_control_deficiencies;
create policy tenant_isolation on internal_control_deficiencies
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on internal_control_deficiencies to cadesk_app;

-- SA 260 Significant Audit Matters
create table if not exists significant_audit_matters (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  committee_engagement_id         uuid not null references audit_committee_engagements(id) on delete cascade,
  matter_code                     text not null, -- e.g. "SAM-01"
  subject                         text not null,
  category                        text not null default 'AccountingPractices', -- AccountingPractices, AuditDifficulties, ManagementConsultation, WrittenRepresentations, KeyAuditMatter
  details                         text not null default '',
  auditor_assessment              text not null default '',
  audit_committee_feedback        text,
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_significant_audit_matters_engagement
  on significant_audit_matters (firm_id, committee_engagement_id);

alter table significant_audit_matters enable row level security;
alter table significant_audit_matters force row level security;
drop policy if exists tenant_isolation on significant_audit_matters;
create policy tenant_isolation on significant_audit_matters
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on significant_audit_matters to cadesk_app;

-- SA 450 Uncorrected Misstatements
create table if not exists uncorrected_misstatements (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  committee_engagement_id         uuid not null references audit_committee_engagements(id) on delete cascade,
  account_description             text not null,
  misstatement_amount             numeric(16,2) not null default 0,
  nature                          text not null default 'Factual', -- Factual, Judgmental, Projected
  is_material                     boolean not null default false,
  management_justification_for_non_adjustment text,
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_uncorrected_misstatements_engagement
  on uncorrected_misstatements (firm_id, committee_engagement_id);

alter table uncorrected_misstatements enable row level security;
alter table uncorrected_misstatements force row level security;
drop policy if exists tenant_isolation on uncorrected_misstatements;
create policy tenant_isolation on uncorrected_misstatements
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on uncorrected_misstatements to cadesk_app;

-- Seed permissions
insert into permissions (code, area, description) values
  ('auditcommittee.read',     'audit', 'View audit committee engagements, governance packs, and SA 265 deficiency communications'),
  ('auditcommittee.manage',   'audit', 'Manage audit committee governance dockets, record internal control deficiencies, and draft packs'),
  ('auditcommittee.signoff',  'audit', 'Formally sign off on Audit Committee presentation and SA 265 communication to TCWG')
on conflict (code) do nothing;

-- Grant to existing Partner roles
insert into role_permissions (role_id, permission_code, firm_id)
select r.id, p.code, r.firm_id
from roles r
cross join (values
  ('auditcommittee.read'),
  ('auditcommittee.manage'),
  ('auditcommittee.signoff')
) as p(code)
where r.name = 'Partner'
on conflict do nothing;
