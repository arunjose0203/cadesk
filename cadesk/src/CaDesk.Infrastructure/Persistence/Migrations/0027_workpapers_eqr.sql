-- 0027_workpapers_eqr: Digital Workpapers & Engagement Quality Review (EQR) Desk (ICAI SQC 1 / SA 220).
-- Supports multi-tier review workflow (Preparer -> Manager -> EQR -> Lead Partner),
-- Hot/Cold EQR reviews, pre-issuance quality checklist, review notes & resolution tracking,
-- and the statutory 60-day immutable archival lock (SQC 1 Para 74).

-- 1. Extend audit_engagements with EQR & Archival tracking
alter table audit_engagements
  add column if not exists audit_report_date date,
  add column if not exists archival_deadline date,
  add column if not exists is_archived boolean not null default false,
  add column if not exists archived_at timestamptz,
  add column if not exists archived_by uuid references users(id),
  add column if not exists eqr_required boolean not null default false,
  add column if not exists eqr_partner_id uuid references users(id),
  add column if not exists eqr_status text not null default 'not_applicable';

-- 2. Engagement Quality Review (EQR) Table
create table if not exists audit_eqr_reviews (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  engagement_id               uuid not null,
  review_type                 text not null default 'hot_review' check (review_type in ('hot_review', 'cold_review')),
  eqr_partner_id              uuid not null references users(id),
  review_status               text not null default 'assigned' check (review_status in ('assigned', 'in_progress', 'clearance_granted', 'qualifications_noted')),
  evaluation_materiality      text,
  evaluation_significant_risks text,
  evaluation_going_concern    text,
  evaluation_audit_opinion    text,
  pre_issuance_checklist      jsonb not null default '[]'::jsonb,
  eqr_signoff_date            timestamptz,
  eqr_conclusion_remarks      text,
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_audit_eqr_reviews_id_firm unique (id, firm_id),
  foreign key (engagement_id, firm_id) references audit_engagements(id, firm_id) on delete cascade
);

create index if not exists ix_audit_eqr_reviews_engagement on audit_eqr_reviews (firm_id, engagement_id);

alter table audit_eqr_reviews enable row level security;
drop policy if exists tenant_isolation on audit_eqr_reviews;
create policy tenant_isolation on audit_eqr_reviews
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on audit_eqr_reviews to cadesk_app;

-- 3. Audit Review Notes & Query Resolution Table
create table if not exists audit_review_notes (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  engagement_id               uuid not null,
  working_paper_id            uuid,
  raised_by_user_id           uuid not null references users(id),
  raised_by_role              text not null check (raised_by_role in ('manager', 'eqr', 'lead_partner')),
  category                    text not null check (category in ('documentation_gap', 'insufficient_testing', 'assertion_unverified', 'accounting_policy_mismatch', 'caro_clause_omission', 'other')),
  priority                    text not null default 'medium' check (priority in ('low', 'medium', 'high', 'critical')),
  query                       text not null,
  status                      text not null default 'open' check (status in ('open', 'response_provided', 'cleared', 'reopened')),
  response                    text,
  response_by_user_id         uuid references users(id),
  response_at                 timestamptz,
  cleared_by_user_id          uuid references users(id),
  cleared_at                  timestamptz,
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_audit_review_notes_id_firm unique (id, firm_id),
  foreign key (engagement_id, firm_id) references audit_engagements(id, firm_id) on delete cascade
);

create index if not exists ix_audit_review_notes_engagement on audit_review_notes (firm_id, engagement_id);
create index if not exists ix_audit_review_notes_wp on audit_review_notes (firm_id, working_paper_id);

alter table audit_review_notes enable row level security;
drop policy if exists tenant_isolation on audit_review_notes;
create policy tenant_isolation on audit_review_notes
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on audit_review_notes to cadesk_app;

-- 4. Permissions
insert into permissions (code, area, description) values
  ('workpapers.read',    'audit', 'View audit workpapers, EQR reviews, and review notes'),
  ('workpapers.manage',  'audit', 'Prepare and update digital workpapers, respond to review queries'),
  ('eqr.review',         'audit', 'Perform Engagement Quality Control Reviews (Hot/Cold) under ICAI SA 220'),
  ('audit.archive',      'audit', 'Lock and archive statutory audit engagement files under ICAI SQC 1')
on conflict (code) do nothing;
