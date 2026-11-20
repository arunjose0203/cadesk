-- 0020_practice_reports: Practice Performance MIS, Benchmarking & Executive Goals.
-- Stores annual firm goals (Revenue targets, billable hours, target recovery %, compliance SLA %)
-- and grants reporting permissions.

create table if not exists practice_targets (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  financial_year              text not null check (financial_year ~ '^[0-9]{4}-[0-9]{2}$'),
  target_annual_revenue       numeric(14,2) not null default 0,
  target_billable_hours       numeric(10,2) not null default 0,
  target_recovery_percentage  numeric(5,2) not null default 85.0 check (target_recovery_percentage between 0 and 100),
  target_compliance_sla_pct   numeric(5,2) not null default 95.0 check (target_compliance_sla_pct between 0 and 100),
  created_at                  timestamptz not null default now(),
  updated_at                  timestamptz,
  constraint ux_practice_targets_firm_fy unique (firm_id, financial_year)
);

create index if not exists ix_practice_targets_firm on practice_targets (firm_id);

alter table practice_targets enable row level security;
drop policy if exists tenant_isolation on practice_targets;
create policy tenant_isolation on practice_targets
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on practice_targets to cadesk_app;

insert into permissions (code, area, description) values
  ('reports.manage', 'reports', 'Manage practice annual targets, KPI benchmarks, and report presets')
on conflict (code) do nothing;
