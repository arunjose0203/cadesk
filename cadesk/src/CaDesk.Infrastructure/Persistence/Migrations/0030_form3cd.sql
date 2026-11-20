-- 0030_form3cd.sql: Form 3CD Tax Audit Statement & Clause-by-Clause Disclosure Desk (Sec 44AB / 40(a) / 43B / 43B(h) / 269SS/ST/T)

create table if not exists form3cd_assessments (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  client_id                       uuid not null references clients(id) on delete cascade,
  audit_engagement_id             uuid references audit_engagements(id) on delete set null,
  financial_year                  text not null, -- e.g. "2024-25"
  assessment_year                 text not null, -- e.g. "2025-26"
  audit_type                      text not null default 'Form3CA', -- Form3CA, Form3CB
  section44ab_clause              text not null,
  total_turnover_or_gross_receipts numeric(14,2) not null default 0,
  total_cash_receipts             numeric(14,2) not null default 0,
  total_payments                  numeric(14,2) not null default 0,
  total_cash_payments             numeric(14,2) not null default 0,
  cash_receipt_percentage         numeric(5,2) not null default 0,
  cash_payment_percentage         numeric(5,2) not null default 0,
  is_tax_audit_mandatory          boolean not null default true,
  applicability_reason            text,
  assessee_name                   text not null default '',
  pan                             text not null default '',
  address                         text not null default '',
  gstin                           text,
  status                          text not null default 'Company', -- Company, Firm, LLP, Individual, HUF
  method_of_accounting            text not null default 'Mercantile',
  opted_special_tax_regime        text not null default 'None',
  total_disallowance_sec40a       numeric(14,2) not null default 0,
  total_disallowance_sec40a3      numeric(14,2) not null default 0,
  total_disallowance_sec43b       numeric(14,2) not null default 0,
  total_disallowance_sec43bh      numeric(14,2) not null default 0,
  total_disallowance_sec361va     numeric(14,2) not null default 0,
  total_sec269_violations         numeric(14,2) not null default 0,
  total_icds_net_adjustment       numeric(14,2) not null default 0,
  total_net_income_adjustment     numeric(14,2) not null default 0,
  assessment_status               text not null default 'draft', -- draft, in_review, completed, finalized
  qualifications_count            integer not null default 0,
  discrepancies_count             integer not null default 0,
  auditor_observations            text,
  generated_report_text           text,
  finalized_by_user_id            uuid references users(id) on delete set null,
  finalized_at                    timestamptz,
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_form3cd_assessments_client_fy
  on form3cd_assessments (firm_id, client_id, financial_year);

alter table form3cd_assessments enable row level security;
alter table form3cd_assessments force row level security;
drop policy if exists tenant_isolation on form3cd_assessments;
create policy tenant_isolation on form3cd_assessments
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on form3cd_assessments to cadesk_app;

-- Form 3CD Clause Responses (Clauses 1 to 44)
create table if not exists form3cd_clause_responses (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  assessment_id                   uuid not null references form3cd_assessments(id) on delete cascade,
  clause_number                   text not null, -- "Clause 13", "Clause 21(b)", "Clause 26/43B(h)", etc.
  clause_title                    text not null,
  category                        text not null default 'Part A - General Assessee Details',
  status                          text not null default 'Clean', -- Clean, Disallowed, Qualified, NotApplicable
  reported_amount                 numeric(14,2) not null default 0,
  auditor_remarks                 text not null default '',
  has_discrepancy                 boolean not null default false,
  particulars_json                text,
  workpaper_ref                   text,
  verified_by_user_id             uuid references users(id) on delete set null,
  verified_at                     timestamptz,
  updated_at                      timestamptz
);

create index if not exists ix_form3cd_clause_responses_assessment
  on form3cd_clause_responses (firm_id, assessment_id);

alter table form3cd_clause_responses enable row level security;
alter table form3cd_clause_responses force row level security;
drop policy if exists tenant_isolation on form3cd_clause_responses;
create policy tenant_isolation on form3cd_clause_responses
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on form3cd_clause_responses to cadesk_app;

-- Seed permissions
insert into permissions (code, area, description) values
  ('taxaudit.read',     'audit', 'View Form 3CD tax audit assessments, applicability, and clause responses'),
  ('taxaudit.manage',   'audit', 'Create and edit Form 3CD tax audit particulars, verify clauses, and calculate disallowances'),
  ('taxaudit.finalize', 'audit', 'Finalize Form 3CA / 3CB Tax Audit Report with partner observations')
on conflict (code) do nothing;

-- Grant to existing Partner roles
insert into role_permissions (role_id, permission_code, firm_id)
select r.id, p.code, r.firm_id
from roles r
cross join (values
  ('taxaudit.read'),
  ('taxaudit.manage'),
  ('taxaudit.finalize')
) as p(code)
where r.name = 'Partner'
on conflict do nothing;
