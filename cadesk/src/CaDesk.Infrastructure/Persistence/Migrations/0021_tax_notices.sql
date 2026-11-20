-- 0021_tax_notices: Tax Notice & Faceless Assessment Management Desk.
-- Tracks Income Tax & GST statutory notices, CBDT/CBIC DIN verification,
-- faceless assessment hearings & VC, written submission drafts & partner approvals,
-- and tax demand / CIT(A) appeal 30-day countdowns.

create table if not exists tax_notices (
  id                    uuid primary key,
  firm_id               uuid not null references firms(id),
  client_id             uuid not null references clients(id) on delete cascade,
  authority             text not null, -- IncomeTax, Gst, Customs, Other
  category              text not null, -- Intimation143_1, Inquiry142_1, Reassessment148_148A, GstDrc01A, etc.
  section               text not null, -- e.g. "Sec 143(1)", "Sec 142(1)", "DRC-01A"
  notice_number         text not null,
  din                   text,          -- Document Identification Number
  din_status            text not null default 'Unverified', -- Valid, Invalid, Unverified, NotApplicable
  assessment_year       text,
  financial_year        text not null,
  issue_date            date not null,
  due_date              date not null,
  demand_amount         numeric(14,2) not null default 0,
  refund_disallowed     numeric(14,2) not null default 0,
  status                text not null default 'Received', -- Received, InReview, DraftPrepared, PartnerApproved, Submitted, DisposedOrClosed, Appealed
  subject               text not null,
  description           text,
  assigned_to_user_id   uuid references users(id) on delete set null,
  assigned_to_name      text,
  officer_name          text,
  officer_jurisdiction  text,
  deleted_at            timestamptz,
  created_at            timestamptz not null default now(),
  created_by            uuid,
  updated_at            timestamptz,
  updated_by            uuid
);

create index if not exists ix_tax_notices_firm_client on tax_notices (firm_id, client_id);
create index if not exists ix_tax_notices_due_date on tax_notices (firm_id, due_date);
create index if not exists ix_tax_notices_status on tax_notices (firm_id, status);

alter table tax_notices enable row level security;
drop policy if exists tenant_isolation on tax_notices;
create policy tenant_isolation on tax_notices
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on tax_notices to cadesk_app;

-- Faceless Video Conference & Hearing Appointments
create table if not exists notice_hearings (
  id                    uuid primary key,
  firm_id               uuid not null references firms(id),
  notice_id             uuid not null references tax_notices(id) on delete cascade,
  hearing_date          timestamptz not null,
  hearing_type          text not null default 'WrittenSubmission', -- WrittenSubmission, VideoConference, PhysicalHearing
  officer_name          text,
  officer_designation   text,
  meeting_link          text,
  status                text not null default 'Scheduled', -- Scheduled, Attended, AdjournmentRequested, Concluded, Cancelled
  adjournment_reason    text,
  summary_notes         text,
  attended_by_user_id   uuid references users(id) on delete set null,
  attended_by_name      text,
  created_at            timestamptz not null default now(),
  created_by            uuid,
  updated_at            timestamptz,
  updated_by            uuid
);

create index if not exists ix_notice_hearings_notice on notice_hearings (firm_id, notice_id);
create index if not exists ix_notice_hearings_date on notice_hearings (firm_id, hearing_date);

alter table notice_hearings enable row level security;
drop policy if exists tenant_isolation on notice_hearings;
create policy tenant_isolation on notice_hearings
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on notice_hearings to cadesk_app;

-- Written Submissions & Maker-Checker Approvals
create table if not exists notice_responses (
  id                    uuid primary key,
  firm_id               uuid not null references firms(id),
  notice_id             uuid not null references tax_notices(id) on delete cascade,
  response_version      integer not null default 1,
  submission_title      text not null,
  summary_of_submissions text not null,
  legal_grounds         text,
  status                text not null default 'Draft', -- Draft, UnderReview, PartnerApproved, UploadedToPortal
  prepared_by_user_id   uuid references users(id) on delete set null,
  prepared_by_name      text,
  approved_by_user_id   uuid references users(id) on delete set null,
  approved_by_name      text,
  approved_at           timestamptz,
  portal_ack_number     text,
  submitted_at          timestamptz,
  attachment_names      text,
  created_at            timestamptz not null default now(),
  created_by            uuid,
  updated_at            timestamptz,
  updated_by            uuid
);

create index if not exists ix_notice_responses_notice on notice_responses (firm_id, notice_id);

alter table notice_responses enable row level security;
drop policy if exists tenant_isolation on notice_responses;
create policy tenant_isolation on notice_responses
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on notice_responses to cadesk_app;

-- Outstanding Tax Demands & CIT(A) / Appellate Countdown Ledger
create table if not exists tax_demands (
  id                    uuid primary key,
  firm_id               uuid not null references firms(id),
  client_id             uuid not null references clients(id) on delete cascade,
  notice_id             uuid references tax_notices(id) on delete set null,
  assessment_year       text,
  financial_year        text not null,
  section               text not null, -- Sec 156, DRC-07, etc.
  original_demand       numeric(14,2) not null default 0,
  stayed_amount         numeric(14,2) not null default 0,
  paid_amount           numeric(14,2) not null default 0,
  adjusted_refund       numeric(14,2) not null default 0,
  demand_date           date not null,
  appeal_due_date       date not null,
  appeal_status         text not null default 'NotAppealed', -- NotAppealed, AppealDrafted, FiledCitA, StayGranted, DemandDeleted, Settled
  appeal_ack_number     text,
  remarks               text,
  created_at            timestamptz not null default now(),
  created_by            uuid,
  updated_at            timestamptz,
  updated_by            uuid
);

create index if not exists ix_tax_demands_firm_client on tax_demands (firm_id, client_id);
create index if not exists ix_tax_demands_appeal_due on tax_demands (firm_id, appeal_due_date);

alter table tax_demands enable row level security;
drop policy if exists tenant_isolation on tax_demands;
create policy tenant_isolation on tax_demands
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on tax_demands to cadesk_app;

-- Permissions Catalogue
insert into permissions (code, area, description) values
  ('notices.read', 'tax_notices', 'View statutory tax notices, hearings, submissions, and demand ledgers'),
  ('notices.manage', 'tax_notices', 'Create and manage tax notices, hearings, submission drafts, and appeals'),
  ('notices.approve', 'tax_notices', 'Partner sign-off and approval on faceless assessment submissions')
on conflict (code) do nothing;
