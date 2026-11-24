-- 0058_tax_appeal_litigation_paperbook.sql
-- ITAT & High Court Appeal Brief Builder & Rule 18 Paper Book Generator

-- 1. Tax Appeals Master Registry
create table if not exists tax_appeals (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  client_id                                 uuid not null references clients(id) on delete cascade,
  tax_notice_id                             uuid references tax_notices(id) on delete set null,
  notice_reference_number                   text,
  forum                                     text not null default 'Itat',
  appeal_number                             text,
  assessment_year                           text not null default '2024-25',
  financial_year                            text not null default '2023-24',
  assessing_officer_ward_or_circle          text not null default '',
  impugned_order_section                    text not null default 'Sec 143(3)',
  impugned_order_number                     text not null default '',
  impugned_order_date                       date not null,
  date_of_service_of_order                  date not null,
  statutory_filing_deadline                 date not null,
  filing_fee_amount                         numeric(12,2) not null default 0,
  assessed_total_income                     numeric(15,2) not null default 0,
  returned_total_income                     numeric(15,2) not null default 0,
  total_disputed_additions                  numeric(15,2) not null default 0,
  disputed_tax_demand                       numeric(15,2) not null default 0,
  filing_date                               date,
  din_or_ack_number                         text,
  status                                    text not null default 'Drafting',
  delay_condonation_required                boolean not null default false,
  days_of_delay                             integer not null default 0,
  condonation_reason                        text,
  statement_of_facts                        text not null default '',
  relief_sought                             text not null default '',
  partner_membership_number                 text,
  partner_certified_by_name                 text,
  partner_certified_at                      timestamptz,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_tax_appeals_firm_client on tax_appeals (firm_id, client_id);
create index if not exists ix_tax_appeals_status on tax_appeals (firm_id, status);
create index if not exists ix_tax_appeals_deadline on tax_appeals (firm_id, statutory_filing_deadline);
alter table tax_appeals enable row level security;
drop policy if exists tenant_isolation on tax_appeals;
create policy tenant_isolation on tax_appeals
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on tax_appeals to cadesk_app;

-- 2. Structured Grounds of Appeal
create table if not exists appeal_grounds (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  tax_appeal_id                             uuid not null references tax_appeals(id) on delete cascade,
  ground_number                             integer not null default 1,
  ground_type                               text not null default 'MeritsAddition',
  title                                     text not null,
  ground_text                               text not null,
  statutory_section                         text not null default '',
  disputed_addition_amount                  numeric(15,2) not null default 0,
  is_without_prejudice                      boolean not null default false,
  is_alternative_ground                     boolean not null default false,
  order_index                               integer not null default 0,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_appeal_grounds_firm_appeal on appeal_grounds (firm_id, tax_appeal_id);
alter table appeal_grounds enable row level security;
drop policy if exists tenant_isolation on appeal_grounds;
create policy tenant_isolation on appeal_grounds
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on appeal_grounds to cadesk_app;

-- 3. ITAT Rule 18 Paginated Paper Book Documents
create table if not exists paper_book_documents (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  tax_appeal_id                             uuid not null references tax_appeals(id) on delete cascade,
  item_number                               integer not null default 1,
  document_date                             date not null,
  description_of_document                   text not null,
  relevance                                 text not null default '',
  evidence_type                             text not null default 'PartLowerAuthoritiesRecord',
  lower_authority_filing_date               date,
  page_number_from                          integer not null default 1,
  page_number_to                            integer not null default 1,
  is_certified                              boolean not null default true,
  order_index                               integer not null default 0,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_paper_book_docs_firm_appeal on paper_book_documents (firm_id, tax_appeal_id);
alter table paper_book_documents enable row level security;
drop policy if exists tenant_isolation on paper_book_documents;
create policy tenant_isolation on paper_book_documents
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on paper_book_documents to cadesk_app;

-- 4. Judicial Authority Landmark Citations
create table if not exists appeal_judicial_citations (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  tax_appeal_id                             uuid not null references tax_appeals(id) on delete cascade,
  precedent_id                              uuid references statutory_precedents(id) on delete set null,
  case_title                                text not null,
  citation                                  text not null,
  court_or_tribunal                         text not null default 'Supreme Court',
  judgment_year                             integer not null default 2024,
  statutory_section                         text not null default '',
  key_ratio_proposition                     text not null,
  paragraph_reference                       text not null default '',
  relevance_to_ground                       text not null default '',
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_appeal_judicial_citations_firm_appeal on appeal_judicial_citations (firm_id, tax_appeal_id);
alter table appeal_judicial_citations enable row level security;
drop policy if exists tenant_isolation on appeal_judicial_citations;
create policy tenant_isolation on appeal_judicial_citations
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on appeal_judicial_citations to cadesk_app;
