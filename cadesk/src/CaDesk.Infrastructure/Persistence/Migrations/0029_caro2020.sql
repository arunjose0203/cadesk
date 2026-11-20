-- 0029_caro2020.sql: Companies (Auditor's Report) Order, 2020 (CARO 2020) Checklist & Disclosures Generator

create table if not exists caro_assessments (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  client_id                       uuid not null references clients(id) on delete cascade,
  engagement_id                   uuid references audit_engagements(id) on delete set null,
  financial_year                  text not null, -- e.g. "2024-25"
  company_type                    text not null default 'Private', -- Private, Public, Section8, SmallCompany, OPC, Banking, Insurance, NBFC
  is_holding_or_subsidiary_of_public boolean not null default false,
  paid_up_capital_and_reserves    numeric(14,2) not null default 0,
  total_borrowings                numeric(14,2) not null default 0,
  total_revenue                   numeric(14,2) not null default 0,
  is_caro_applicable              boolean not null default true,
  exemption_reason                text,
  status                          text not null default 'draft', -- draft, in_review, completed, finalized
  qualifications_count            integer not null default 0,
  adverse_remarks_count           integer not null default 0,
  generated_annexure_text         text,
  auditor_conclusion              text,
  finalized_by_user_id            uuid references users(id) on delete set null,
  finalized_at                    timestamptz,
  created_at                      timestamptz not null default now(),
  updated_at                      timestamptz
);

create index if not exists ix_caro_assessments_client_fy
  on caro_assessments (firm_id, client_id, financial_year);

alter table caro_assessments enable row level security;
alter table caro_assessments force row level security;
drop policy if exists tenant_isolation on caro_assessments;
create policy tenant_isolation on caro_assessments
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on caro_assessments to cadesk_app;

-- CARO Clause Responses (Clauses i to xxi)
create table if not exists caro_clause_responses (
  id                              uuid primary key default gen_random_uuid(),
  firm_id                         uuid not null references firms(id) on delete cascade,
  assessment_id                   uuid not null references caro_assessments(id) on delete cascade,
  clause_number                   text not null, -- "i(a)(A)", "i(b)", "ii(a)", ... "xxi"
  clause_title                    text not null,
  category                        text not null default 'General', -- PPE, Inventory, Loans, StatutoryDues, Fraud, Governance
  status                          text not null default 'Compliant', -- Compliant, Qualified, Adverse, NotApplicable
  auditor_remark                  text not null default '',
  has_discrepancy                 boolean not null default false,
  discrepancy_amount              numeric(14,2),
  supporting_wps_ref              text,
  verified_by_user_id             uuid references users(id) on delete set null,
  verified_at                     timestamptz,
  updated_at                      timestamptz
);

create index if not exists ix_caro_clause_responses_assessment
  on caro_clause_responses (firm_id, assessment_id);

alter table caro_clause_responses enable row level security;
alter table caro_clause_responses force row level security;
drop policy if exists tenant_isolation on caro_clause_responses;
create policy tenant_isolation on caro_clause_responses
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on caro_clause_responses to cadesk_app;

-- Seed permissions
insert into permissions (code, area, description) values
  ('caro.read',     'audit', 'View CARO 2020 applicability assessments and clause responses'),
  ('caro.manage',   'audit', 'Create and edit CARO 2020 assessments, verify clauses, and generate annexures'),
  ('caro.finalize', 'audit', 'Finalize CARO 2020 auditor reporting annexure for independent audit report')
on conflict (code) do nothing;

-- Grant to existing Partner roles
insert into role_permissions (role_id, permission_code, firm_id)
select r.id, p.code, r.firm_id
from roles r
cross join (values
  ('caro.read'),
  ('caro.manage'),
  ('caro.finalize')
) as p(code)
where r.name = 'Partner'
on conflict do nothing;
