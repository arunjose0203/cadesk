-- 0036_internal_audit_rcm_ifc.sql: Internal Audit Risk & Control Matrix (RCM) & IFC Testing Desk (ICAI SIA / Sec 138 / Sec 143(3)(i))

create table if not exists internal_audit_engagements (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  client_id                       uuid not null references clients(id) on delete cascade,
  financial_year                  text not null,
  audit_period                    text not null default 'Annual', -- Q1, Q2, Q3, Q4, H1, H2, Annual
  title                           text not null,
  engagement_lead_name            text not null default '',
  audit_scope_description         text not null default '',
  status                          text not null default 'Planning', -- Planning, Fieldwork, Review, Reporting, Closed
  report_date                     timestamptz,
  health_score_percentage         numeric(8,2) not null default 0,
  generated_report_text           text,
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_internal_audit_engagements_client_fy
  on internal_audit_engagements (firm_id, client_id, financial_year);

alter table internal_audit_engagements enable row level security;
alter table internal_audit_engagements force row level security;
drop policy if exists tenant_isolation on internal_audit_engagements;
create policy tenant_isolation on internal_audit_engagements
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on internal_audit_engagements to cadesk_app;

-- Risk & Control Matrix (RCM) Controls
create table if not exists rcm_control_items (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  engagement_id                   uuid not null references internal_audit_engagements(id) on delete cascade,
  control_code                    text not null,
  process_cycle                   text not null default 'P2P', -- P2P, O2C, H2R, R2R, Treasury, ITGC, InventoryFixedAssets
  sub_process                     text not null default '',
  risk_description                text not null default '',
  risk_category                   text not null default 'FinancialReporting',
  inherent_risk_rating            text not null default 'High', -- High, Medium, Low
  control_activity                text not null default '',
  control_nature                  text not null default 'Preventive', -- Preventive, Detective, Corrective
  control_frequency               text not null default 'Monthly',
  control_type                    text not null default 'Automated', -- Automated, SemiAutomated, Manual
  control_owner                   text not null default '',
  assertions                      text not null default '',

  -- Design Evaluation
  walkthrough_performed           boolean not null default false,
  walkthrough_date                timestamptz,
  walkthrough_reference           text not null default '',
  design_effectiveness           text not null default 'NotTested', -- Effective, Deficient, NotTested
  design_notes                    text not null default '',

  -- Test of Operating Effectiveness
  testing_method                  text not null default 'Inspection', -- Inspection, Reperformance, Observation, Inquiry
  sample_size                     integer not null default 0,
  exceptions_count                integer not null default 0,
  operating_effectiveness         text not null default 'NotTested', -- OperatingEffectively, Ineffective, NotTested
  testing_notes                   text not null default '',
  tested_at                       timestamptz,
  tested_by                       text not null default '',

  residual_risk_rating            text not null default 'Medium',
  has_finding                     boolean not null default false,
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_rcm_control_items_engagement
  on rcm_control_items (firm_id, engagement_id);

alter table rcm_control_items enable row level security;
alter table rcm_control_items force row level security;
drop policy if exists tenant_isolation on rcm_control_items;
create policy tenant_isolation on rcm_control_items
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on rcm_control_items to cadesk_app;

-- Internal Audit Findings & Remediation Tracker
create table if not exists internal_audit_findings (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  engagement_id                   uuid not null references internal_audit_engagements(id) on delete cascade,
  control_item_id                 uuid references rcm_control_items(id) on delete set null,
  control_code                    text not null default '',
  finding_reference               text not null,
  title                           text not null,
  process_cycle                   text not null default 'P2P',
  severity                        text not null default 'High', -- Critical, High, Medium, Low
  detailed_observation           text not null default '',
  root_cause                      text not null default '',
  estimated_risk_amount           numeric(16,2),
  ifc_classification              text not null default 'SignificantDeficiency', -- MaterialWeakness, SignificantDeficiency, OperationalDeficiency, ProcessImprovement
  auditor_recommendation          text not null default '',
  management_action_plan          text not null default '',
  action_owner                    text not null default '',
  target_completion_date          timestamptz,
  implementation_status           text not null default 'Open', -- Open, InProgress, Implemented, Overdue
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_internal_audit_findings_engagement
  on internal_audit_findings (firm_id, engagement_id);

alter table internal_audit_findings enable row level security;
alter table internal_audit_findings force row level security;
drop policy if exists tenant_isolation on internal_audit_findings;
create policy tenant_isolation on internal_audit_findings
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on internal_audit_findings to cadesk_app;
