-- 0014_statutory_audit: Statutory Audit Workspace, Materiality & Working Papers (SA 200/300/320/500/530)
-- Provides structured audit engagement planning, risk & materiality calculators, sampling methodologies,
-- and multi-tier working paper review workflows (Preparer -> Reviewer -> Partner Sign-off).

create table if not exists audit_engagements (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  client_id                   uuid not null,
  financial_year              text not null check (financial_year ~ '^[0-9]{4}-[0-9]{2}$'),
  audit_type                  text not null default 'statutory' check (audit_type in ('statutory','tax_audit_44ab','internal','bank_concurrent','limited_review')),
  title                       text not null check (length(title) between 3 and 200),
  lead_partner_id             uuid references users(id),
  manager_id                  uuid references users(id),
  planned_start_date          date,
  planned_end_date            date,
  status                      text not null default 'planning' check (status in ('planning','fieldwork','review','partner_signoff','archived')),
  benchmark_type              text not null default 'revenue' check (benchmark_type in ('revenue','profit_before_tax','total_assets','equity')),
  benchmark_amount            numeric(14,2) not null default 0,
  overall_materiality         numeric(14,2) not null default 0,
  performance_materiality     numeric(14,2) not null default 0,
  trivial_threshold           numeric(14,2) not null default 0,
  audit_opinion               text check (audit_opinion is null or audit_opinion in ('unmodified','qualified','adverse','disclaimer')),
  notes                       text check (notes is null or length(notes) <= 4000),
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_audit_engagements_id_firm unique (id, firm_id),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade
);

create index if not exists ix_audit_engagements_firm_client on audit_engagements (firm_id, client_id, financial_year);

create table if not exists audit_working_papers (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  engagement_id               uuid not null,
  ref_code                    text not null check (length(ref_code) between 2 and 30),
  area                        text not null check (length(area) between 2 and 100),
  title                       text not null check (length(title) between 3 and 250),
  assertion                   text not null default 'existence' check (assertion in ('completeness','existence','accuracy','valuation','rights_obligations','presentation')),
  objective                   text not null,
  procedures_performed        text not null default '',
  sampling_method             text not null default 'haphazard' check (sampling_method in ('100_percent','monetary_unit_sampling','haphazard','stratified','risk_targeted')),
  population_value            numeric(14,2) not null default 0,
  sample_value                numeric(14,2) not null default 0,
  items_tested_count          integer not null default 0,
  exceptions_noted            text,
  conclusion                  text not null default 'pending_evidence' check (conclusion in ('satisfactory','qualified_finding','material_misstatement','pending_evidence')),
  status                      text not null default 'draft' check (status in ('draft','ready_for_review','reviewed','partner_approved')),
  preparer_id                 uuid references users(id),
  prepared_at                 timestamptz,
  reviewer_id                 uuid references users(id),
  reviewed_at                 timestamptz,
  review_notes                text,
  attachments                 jsonb not null default '[]'::jsonb,
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_audit_working_papers_id_firm unique (id, firm_id),
  foreign key (engagement_id, firm_id) references audit_engagements(id, firm_id) on delete cascade
);

create index if not exists ix_audit_working_papers_engagement on audit_working_papers (firm_id, engagement_id);

-- ---------------------------------------------------------------- row-level security

alter table audit_engagements enable row level security;
drop policy if exists tenant_isolation on audit_engagements;
create policy tenant_isolation on audit_engagements
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

alter table audit_working_papers enable row level security;
drop policy if exists tenant_isolation on audit_working_papers;
create policy tenant_isolation on audit_working_papers
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on audit_engagements to cadesk_app;
grant select, insert, update, delete on audit_working_papers to cadesk_app;

-- ---------------------------------------------------------------- permissions

insert into permissions (code, area, description) values
  ('audit.read',    'audit', 'View statutory audit engagements, materiality calculators, and working papers'),
  ('audit.manage',  'audit', 'Create and modify audit engagements, plan audit areas, and prepare working papers'),
  ('audit.review',  'audit', 'Review audit working papers, sign off audit files, and issue audit opinions')
on conflict (code) do nothing;
