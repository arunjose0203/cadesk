-- 0024_fixed_assets: Fixed Asset Register & Dual Depreciation Engine (Companies Act Sch II vs. Income Tax Sec 32).
-- Tracks individual fixed assets, capitalization dates, gross block, dual depreciation runs,
-- Section 32 180-day 50% rule, additional depreciation, AS 22 / Ind AS 12 deferred tax, and physical verification audits.

create table if not exists fixed_assets (
  id                                      uuid primary key,
  firm_id                                 uuid not null references firms(id),
  client_id                               uuid not null references clients(id) on delete cascade,
  asset_tag                               text not null, -- Unique per firm & client, e.g. FA-2024-001
  name                                    text not null,
  asset_class                             text not null default 'PlantAndMachinery', -- Building, PlantAndMachinery, ComputersAndSoftware, FurnitureAndFixtures, OfficeEquipment, Vehicles, Intangibles
  purchase_date                           date not null,
  capitalization_date                     date not null, -- Put to use date
  purchase_cost                           numeric(14,2) not null default 0,
  inward_freight                          numeric(14,2) not null default 0,
  installation_cost                       numeric(14,2) not null default 0,
  pre_operating_cost                      numeric(14,2) not null default 0,
  gst_itc_claimed                         numeric(14,2) not null default 0,
  gross_cost                              numeric(14,2) not null default 0,
  salvage_value                           numeric(14,2) not null default 0,
  useful_life_years                       numeric(6,2) not null default 5,
  company_depreciation_method             text not null default 'SLM',             -- SLM, WDV
  shift_type                              text not null default 'SingleShift',     -- SingleShift, DoubleShift, TripleShift
  income_tax_block_rate_percent           numeric(5,2) not null default 15,
  is_eligible_for_additional_depreciation boolean not null default false,
  location                                text,
  department                              text,
  custodian_name                          text,
  status                                  text not null default 'Active',          -- Active, Disposed, Scrapped, WrittenOff
  disposal_date                           date,
  disposal_proceeds                       numeric(14,2),
  disposal_remarks                        text,
  created_at                              timestamptz not null default now(),
  created_by                              uuid,
  updated_at                              timestamptz,
  updated_by                              uuid,
  constraint ux_fixed_assets_tag unique (firm_id, client_id, asset_tag)
);

create index if not exists ix_fixed_assets_firm_client on fixed_assets (firm_id, client_id);
create index if not exists ix_fixed_assets_class on fixed_assets (firm_id, asset_class);
create index if not exists ix_fixed_assets_status on fixed_assets (firm_id, status);

alter table fixed_assets enable row level security;
drop policy if exists tenant_isolation on fixed_assets;
create policy tenant_isolation on fixed_assets
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on fixed_assets to cadesk_app;

-- Dual Depreciation Annual Entries
create table if not exists asset_depreciation_entries (
  id                        uuid primary key,
  firm_id                   uuid not null references firms(id),
  fixed_asset_id            uuid not null references fixed_assets(id) on delete cascade,
  financial_year            text not null, -- e.g. 2024-25
  opening_book_value        numeric(14,2) not null default 0,
  book_depreciation         numeric(14,2) not null default 0,
  closing_book_value        numeric(14,2) not null default 0,
  opening_tax_wdv           numeric(14,2) not null default 0,
  tax_depreciation          numeric(14,2) not null default 0,
  closing_tax_wdv           numeric(14,2) not null default 0,
  timing_difference         numeric(14,2) not null default 0, -- TaxDepr - BookDepr
  deferred_tax_impact       numeric(14,2) not null default 0, -- TimingDiff * TaxRate (positive = DTL, negative = DTA)
  days_put_to_use_in_year   integer not null default 0,
  is_under180_days          boolean not null default false,
  calculated_at             timestamptz not null default now(),
  created_at                timestamptz not null default now(),
  created_by                uuid,
  updated_at                timestamptz,
  updated_by                uuid,
  constraint ux_asset_depr_entries_asset_fy unique (firm_id, fixed_asset_id, financial_year)
);

create index if not exists ix_asset_depr_entries_asset on asset_depreciation_entries (firm_id, fixed_asset_id);
create index if not exists ix_asset_depr_entries_fy on asset_depreciation_entries (firm_id, financial_year);

alter table asset_depreciation_entries enable row level security;
drop policy if exists tenant_isolation on asset_depreciation_entries;
create policy tenant_isolation on asset_depreciation_entries
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on asset_depreciation_entries to cadesk_app;

-- Physical Verification Logs
create table if not exists asset_physical_verifications (
  id                        uuid primary key,
  firm_id                   uuid not null references firms(id),
  fixed_asset_id            uuid not null references fixed_assets(id) on delete cascade,
  verification_date         date not null,
  verified_by               text not null,
  status                    text not null default 'FoundAndGood', -- FoundAndGood, Damaged, Missing, NeedsMaintenance
  remarks                   text,
  location_verified         text,
  created_at                timestamptz not null default now(),
  created_by                uuid,
  updated_at                timestamptz,
  updated_by                uuid
);

create index if not exists ix_asset_verifications_asset on asset_physical_verifications (firm_id, fixed_asset_id);

alter table asset_physical_verifications enable row level security;
drop policy if exists tenant_isolation on asset_physical_verifications;
create policy tenant_isolation on asset_physical_verifications
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on asset_physical_verifications to cadesk_app;

-- Permissions Catalogue
insert into permissions (code, area, description) values
  ('assets.read', 'assets', 'View fixed asset register, depreciation schedules, and physical verification audits'),
  ('assets.manage', 'assets', 'Create, update, tag, verify, and dispose fixed assets'),
  ('assets.depreciate', 'assets', 'Run dual depreciation engine (Schedule II & Sec 32) and compute deferred tax')
on conflict (code) do nothing;
