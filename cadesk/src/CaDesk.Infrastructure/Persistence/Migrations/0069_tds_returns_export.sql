-- 0069_tds_returns_export: Sprint 68 NSDL/Protean 24Q/26Q/27Q returns export, FVU metadata & deductor profiles.

alter table tds_deductions drop constraint if exists tds_deductions_form_type_check;
alter table tds_deductions add constraint tds_deductions_form_type_check
  check (form_type in ('24Q','26Q','27Q','27EQ'));

alter table tds_challans
  add column if not exists is_book_entry boolean not null default false,
  add column if not exists is_nil boolean not null default false;

alter table tds_deductions
  add column if not exists deductee_code text,
  add column if not exists remark_code text,
  add column if not exists remittance_code text,
  add column if not exists country_code text,
  add column if not exists tax_rate_source text,
  add column if not exists ack_number text,
  add column if not exists opted_out_new_regime boolean;

create table if not exists tds_deductor_profiles (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  client_id                   uuid not null,
  tan                         text not null default '',
  pan                         text not null default '',
  deductor_type               text not null default 'K',
  deductor_name               text not null default '',
  branch_division             text not null default 'NA',
  gstin                       text,
  flat_block                  text not null default '',
  premises_name               text,
  street_road                 text,
  area_locality               text,
  city_district               text,
  state_code                  text not null default '16',
  pincode                     text not null default '',
  country_name                text not null default 'INDIA',
  email                       text not null default '',
  std_code                    text,
  phone                       text not null default '',
  mobile                      text not null default '',
  country_calling_code        text not null default '91',
  resp_name                   text not null default '',
  resp_designation            text not null default '',
  resp_pan                    text not null default '',
  resp_flat_block             text not null default '',
  resp_premises_name          text,
  resp_street_road            text,
  resp_area_locality          text,
  resp_city_district          text,
  resp_state_code             text not null default '16',
  resp_pincode                text not null default '',
  resp_country_name           text not null default 'INDIA',
  resp_email                  text not null default '',
  resp_mobile                 text not null default '',
  resp_country_calling_code   text not null default '91',
  filed_previous_regular      boolean not null default false,
  previous_token_no           text,
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_tds_deductor_profiles_firm_client unique (firm_id, client_id),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade
);

alter table tds_deductor_profiles enable row level security;
drop policy if exists tenant_isolation on tds_deductor_profiles;
create policy tenant_isolation on tds_deductor_profiles
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on tds_deductor_profiles to cadesk_app;
