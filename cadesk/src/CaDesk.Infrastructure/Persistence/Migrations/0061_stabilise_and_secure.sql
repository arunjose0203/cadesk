-- Migration 0061: Stabilise & Secure (Sprint 62)
-- 1. Add dedicated UDIN and partner sign-off columns to audit_engagements
alter table audit_engagements add column if not exists udin text;
alter table audit_engagements add column if not exists partner_membership_no text;
alter table audit_engagements add column if not exists partner_signoff_remarks text;

-- 2. Sequential subscription invoice sequence per financial year (CD/2026-27/0001)
create sequence if not exists seq_subscription_invoices start with 1;

-- 3. Firm feature flags table (checked by API & menu; unfinished desks disabled by default)
create table if not exists firm_features (
    id uuid primary key default gen_random_uuid(),
    firm_id uuid not null references firms(id) on delete cascade,
    feature_key text not null,
    is_enabled boolean not null default false,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint uq_firm_features unique (firm_id, feature_key)
);

create index if not exists ix_firm_features_firm_key on firm_features (firm_id, feature_key);

alter table firm_features enable row level security;
create policy tenant_isolation on firm_features
    using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());
grant select, insert, update, delete on firm_features to cadesk_app;

-- 4. Permissions for partner sign-off, banking payouts, and desks
insert into permissions (code, area, description) values
    ('audit.signoff', 'audit', 'Partner Sign-off Audit Report'),
    ('banking.payout.create', 'banking', 'Create Connected Banking Payout Batch'),
    ('banking.payout.approve', 'banking', 'Authorize Connected Banking Payout Batch'),
    ('notices.copilot', 'notices', 'Tax Notice Copilot & Precedent Search'),
    ('uaecorporatetax.read', 'uae-tax', 'View UAE Corporate Tax Returns & Profiles'),
    ('uaecorporatetax.manage', 'uae-tax', 'Manage UAE Corporate Tax Profiles & Reliefs'),
    ('uaecorporatetax.file', 'uae-tax', 'File UAE Corporate Tax Returns'),
    ('practicehealth.read', 'practice-health', 'View Practice Health Telemetry & Benchmarks'),
    ('practicehealth.manage', 'practice-health', 'Manage Practice Succession & Benchmarking Data'),
    ('practicehealth.valuation', 'practice-health', 'Run Firm DCF & Multiple Valuation Calculator'),
    ('ledgersync.read', 'ledger-sync', 'View Synced Vouchers & Reconciliation Status'),
    ('ledgersync.manage', 'ledger-sync', 'Manage Tally / Busy Sync Agents & Mappings'),
    ('ledgersync.post', 'ledger-sync', 'Authorize & Post 2-Way Audit Vouchers to ERP'),
    ('taxlitigation.read', 'tax-litigation', 'View ITAT & High Court Appeal Registry'),
    ('taxlitigation.manage', 'tax-litigation', 'Manage Grounds of Appeal & Legal Citations'),
    ('taxlitigation.certify', 'tax-litigation', 'Compile & Certify Paginated Appeal Paper Books'),
    ('esgcsr.read', 'esg-csr', 'View BRSR Core & CSR Assessments'),
    ('esgcsr.manage', 'esg-csr', 'Manage CSR Projects & BRSR Telemetry'),
    ('esgcsr.certify', 'esg-csr', 'Certify CSR Form CSR-2 & BRSR Core Report'),
    ('ibccirp.read', 'ibc-cirp', 'View IBC CIRP Cases & CoC Registry'),
    ('ibccirp.manage', 'ibc-cirp', 'Manage CoC Resolutions, Voting & PUFE Trans'),
    ('ibccirp.certify', 'ibc-cirp', 'Compile Information Memorandum & Liquidation Reports')
on conflict (code) do nothing;

-- Grant new permissions to all Partner roles across existing firms
insert into role_permissions (role_id, permission_code, firm_id)
select r.id, p.code, r.firm_id
from roles r
cross join permissions p
where r.name = 'Partner' and r.is_system
  and p.code in (
    'ledgersync.read', 'ledgersync.manage', 'ledgersync.post',
    'taxlitigation.read', 'taxlitigation.manage', 'taxlitigation.certify',
    'esgcsr.read', 'esgcsr.manage', 'esgcsr.certify',
    'ibccirp.read', 'ibccirp.manage', 'ibccirp.certify'
  )
on conflict do nothing;

-- Grant banking.payout.create to Manager roles
insert into role_permissions (role_id, permission_code, firm_id)
select r.id, 'banking.payout.create', r.firm_id
from roles r
where r.name = 'Manager' and r.is_system
on conflict do nothing;
