-- 0066_plans_and_legal.sql: Sprint 71, plan tiers that match the subscription engine, and legal acceptances.

-- firms.plan_code references plans: add the tiers the subscription engine and feature flags use
insert into plans (code, name, sort_order) values
  ('ENTERPRISE', 'Enterprise', 4),
  ('DEMO', 'Demo (all desks)', 90),
  ('DEV', 'Development (all desks)', 91),
  ('INTERNAL', 'Internal (all desks)', 92)
on conflict (code) do nothing;

-- who accepted which version of the terms, privacy notice and data processing terms (DPDP Act 2023)
create table if not exists legal_acceptances (
    id uuid primary key default gen_random_uuid(),
    firm_id uuid not null references firms(id) on delete cascade,
    user_id uuid not null references users(id) on delete cascade,
    document text not null check (document in ('terms', 'privacy', 'dpa')),
    version text not null check (length(version) between 1 and 20),
    accepted_at timestamptz not null default now(),
    ip_address text check (ip_address is null or length(ip_address) <= 64),
    user_agent text check (user_agent is null or length(user_agent) <= 300),
    constraint uq_legal_acceptances unique (firm_id, user_id, document, version)
);

alter table legal_acceptances enable row level security;

create policy legal_acceptances_tenant_isolation on legal_acceptances
    for all using (firm_id = app_current_firm());

create index if not exists idx_legal_acceptances_firm_document on legal_acceptances (firm_id, document, version);
