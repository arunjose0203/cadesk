-- 0032_branch_audit_consolidation.sql: Multi-Firm Collaborative Audit & Branch Audit Consolidation (Sec 143(8) / SA 600)

create table if not exists branch_audit_engagements (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  client_id                       uuid not null references clients(id) on delete cascade,
  audit_engagement_id             uuid references audit_engagements(id) on delete set null,
  financial_year                  text not null, -- e.g. "2024-25"
  title                           text not null,
  head_office_assets              numeric(16,2) not null default 0,
  head_office_liabilities         numeric(16,2) not null default 0,
  head_office_revenue             numeric(16,2) not null default 0,
  head_office_expenses            numeric(16,2) not null default 0,
  head_office_net_profit          numeric(16,2) not null default 0,
  total_consolidated_assets       numeric(16,2) not null default 0,
  total_consolidated_liabilities  numeric(16,2) not null default 0,
  total_consolidated_revenue      numeric(16,2) not null default 0,
  total_consolidated_expenses     numeric(16,2) not null default 0,
  total_consolidated_net_profit   numeric(16,2) not null default 0,
  branches_audited_count          integer not null default 0,
  branch_auditors_count           integer not null default 0,
  total_branch_assets             numeric(16,2) not null default 0,
  branch_assets_percentage        numeric(5,2) not null default 0,
  total_branch_revenue            numeric(16,2) not null default 0,
  branch_revenue_percentage       numeric(5,2) not null default 0,
  total_branch_net_profit         numeric(16,2) not null default 0,
  branch_net_profit_percentage    numeric(5,2) not null default 0,
  consolidation_status            text not null default 'draft', -- draft, branches_in_progress, review_ready, consolidated, finalized
  branch_qualifications_count     integer not null default 0,
  principal_auditor_notes         text,
  generated_sa600_report_text     text,
  finalized_by_user_id            uuid references users(id) on delete set null,
  finalized_at                    timestamptz,
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_branch_audit_engagements_client_fy
  on branch_audit_engagements (firm_id, client_id, financial_year);

alter table branch_audit_engagements enable row level security;
alter table branch_audit_engagements force row level security;
drop policy if exists tenant_isolation on branch_audit_engagements;
create policy tenant_isolation on branch_audit_engagements
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on branch_audit_engagements to cadesk_app;

-- Branch Offices (audited under Section 143(8))
create table if not exists branch_audit_offices (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  engagement_id                   uuid not null references branch_audit_engagements(id) on delete cascade,
  branch_code                     text not null, -- e.g. "BR-MUM"
  branch_name                     text not null,
  branch_location                 text not null default '',
  is_foreign_branch               boolean not null default false,
  auditor_firm_name               text not null default '',
  auditor_firm_registration_number text not null default '',
  auditor_name                    text not null default '',
  auditor_membership_number       text not null default '',
  auditor_email                   text not null default '',
  auditor_phone                   text,
  access_token_hash               text,
  token_expires_at                timestamptz,
  last_submitted_at               timestamptz,
  total_assets                    numeric(16,2) not null default 0,
  total_liabilities               numeric(16,2) not null default 0,
  total_revenue                   numeric(16,2) not null default 0,
  total_expenses                  numeric(16,2) not null default 0,
  net_profit                      numeric(16,2) not null default 0,
  branch_report_type              text not null default 'Clean', -- Clean, Qualified, Adverse, Disclaimer
  has_qualifications              boolean not null default false,
  qualifications_summary          text,
  branch_audit_report_ref         text,
  accounting_policies_consistent  boolean not null default true,
  related_party_transactions_disclosed boolean not null default true,
  contingent_liabilities_reported boolean not null default false,
  subsequent_events_reported      boolean not null default false,
  fraud_or_irregularities_reported boolean not null default false,
  internal_controls_commensurate  boolean not null default true,
  caro_applicable_to_branch       boolean not null default false,
  caro_report_summary             text,
  status                          text not null default 'Assigned', -- Assigned, PackageSent, SubmittedByBranch, UnderReview, Accepted, Rejected
  principal_auditor_review_notes  text,
  reviewed_by_user_id             uuid references users(id) on delete set null,
  reviewed_at                     timestamptz,
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_branch_audit_offices_engagement
  on branch_audit_offices (firm_id, engagement_id);

create index if not exists ix_branch_audit_offices_token
  on branch_audit_offices (access_token_hash);

alter table branch_audit_offices enable row level security;
alter table branch_audit_offices force row level security;
drop policy if exists tenant_isolation on branch_audit_offices;
create policy tenant_isolation on branch_audit_offices
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on branch_audit_offices to cadesk_app;

-- Seed permissions
insert into permissions (code, area, description) values
  ('branchaudit.read',        'audit', 'View branch audit engagements, branch auditor submissions, and SA 600 disclosures'),
  ('branchaudit.manage',      'audit', 'Create branch engagements, register branches, generate external auditor tokens, and review submissions'),
  ('branchaudit.consolidate', 'audit', 'Consolidate branch audit figures and finalize SA 600 Other Matter report paragraph')
on conflict (code) do nothing;

-- Grant to existing Partner roles
insert into role_permissions (role_id, permission_code, firm_id)
select r.id, p.code, r.firm_id
from roles r
cross join (values
  ('branchaudit.read'),
  ('branchaudit.manage'),
  ('branchaudit.consolidate')
) as p(code)
where r.name = 'Partner'
on conflict do nothing;
