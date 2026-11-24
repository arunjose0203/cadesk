-- Migration 0062: Schema Drift and Missing Audit Tables (Sprint 62)
-- 1. Add audit stamping columns (created_by, updated_by) to tables where EF model expects them
do $$
declare t text;
begin
  foreach t in array array[
    'bank_ledger_reconciliation_matches',
    'client_succession_transitions',
    'connected_bank_credentials',
    'erp_ledger_vouchers',
    'erp_outgoing_vouchers',
    'erp_sync_agent_configs',
    'fema_firms_filings',
    'partner_equities',
    'partner_mobile_preferences',
    'partner_retirement_plans',
    'payout_batches',
    'payout_items',
    'practice_benchmarks',
    'practice_valuations',
    'rbi_annual_fla_returns',
    'uae_corporate_tax_returns',
    'uae_tax_profiles'
  ]
  loop
    execute format('alter table if exists %I add column if not exists created_by uuid', t);
    execute format('alter table if exists %I add column if not exists updated_by uuid', t);
  end loop;
end $$;

-- 2. Ensure columns in csr_assessments and cirp_cases match expected conventions
alter table if exists csr_assessments add column if not exists net_profit_fy_minus_1 numeric(15,2) not null default 0;
alter table if exists csr_assessments add column if not exists net_profit_fy_minus_2 numeric(15,2) not null default 0;
alter table if exists csr_assessments add column if not exists net_profit_fy_minus_3 numeric(15,2) not null default 0;
alter table if exists csr_assessments add column if not exists mandatory_csr_obligation_2_pct numeric(15,2) not null default 0;

alter table if exists cirp_cases add column if not exists moratorium_section_14_declared boolean not null default true;
alter table if exists cirp_cases add column if not exists initial_180_days_deadline date;
alter table if exists cirp_cases add column if not exists extended_270_days_deadline date;
alter table if exists cirp_cases add column if not exists final_330_days_limit date;

-- 3. Audit Lead Schedules
create table if not exists audit_lead_schedules (
    id uuid primary key,
    firm_id uuid not null references firms(id),
    engagement_id uuid not null references audit_engagements(id) on delete cascade,
    lead_code text not null default 'A',
    lead_title text not null default '',
    schedule_category text not null default 'Asset',
    unadjusted_book_balance numeric(18,2) not null default 0,
    audit_adjustments_debit numeric(18,2) not null default 0,
    audit_adjustments_credit numeric(18,2) not null default 0,
    audited_balance numeric(18,2) not null default 0,
    prior_year_balance numeric(18,2) not null default 0,
    variance_amount numeric(18,2) not null default 0,
    variance_percentage numeric(8,2) not null default 0,
    is_material_variance boolean not null default false,
    audit_procedures_summary text,
    exceptions_noted text,
    status text not null default 'Draft',
    signed_off_by_user_id uuid references users(id) on delete set null,
    signed_off_at timestamptz,
    created_at timestamptz not null default now(),
    created_by uuid,
    updated_at timestamptz,
    updated_by uuid
);
alter table audit_lead_schedules enable row level security;
drop policy if exists tenant_isolation on audit_lead_schedules;
create policy tenant_isolation on audit_lead_schedules using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());
grant select, insert, update, delete on audit_lead_schedules to cadesk_app;

-- 4. Audit Dossiers
create table if not exists audit_dossiers (
    id uuid primary key,
    firm_id uuid not null references firms(id),
    engagement_id uuid not null references audit_engagements(id) on delete cascade,
    dossier_type text not null default 'COMBINED_PEER_REVIEW',
    version int not null default 1,
    status text not null default 'draft',
    integrity_sha256 text not null default '',
    nfra_inspection_ready boolean not null default false,
    total_working_papers_included int not null default 0,
    lead_schedules_count int not null default 0,
    total_exceptions_noted int not null default 0,
    total_open_notes int not null default 0,
    partner_signoff_remarks text,
    dossier_manifest_json jsonb,
    compiled_at timestamptz not null default now(),
    compiled_by uuid references users(id) on delete set null,
    sealed_at timestamptz,
    sealed_by uuid references users(id) on delete set null,
    created_at timestamptz not null default now(),
    updated_at timestamptz
);
alter table audit_dossiers enable row level security;
drop policy if exists tenant_isolation on audit_dossiers;
create policy tenant_isolation on audit_dossiers using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());
grant select, insert, update, delete on audit_dossiers to cadesk_app;

-- 5. Confirmation Campaigns
create table if not exists audit_confirmation_campaigns (
    id uuid primary key,
    firm_id uuid not null references firms(id),
    audit_engagement_id uuid not null references audit_engagements(id) on delete cascade,
    title text not null default '',
    description text,
    as_of_date date not null,
    cutoff_date date not null,
    status text not null default 'Active',
    total_requests_count int not null default 0,
    confirmed_agreed_count int not null default 0,
    disputed_count int not null default 0,
    pending_count int not null default 0,
    total_book_balance_amount numeric(18,2) not null default 0,
    total_confirmed_amount numeric(18,2) not null default 0,
    created_at timestamptz not null default now(),
    created_by uuid,
    updated_at timestamptz,
    updated_by uuid
);
alter table audit_confirmation_campaigns enable row level security;
drop policy if exists tenant_isolation on audit_confirmation_campaigns;
create policy tenant_isolation on audit_confirmation_campaigns using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());
grant select, insert, update, delete on audit_confirmation_campaigns to cadesk_app;

-- 6. External Confirmations
create table if not exists audit_external_confirmations (
    id uuid primary key,
    firm_id uuid not null references firms(id),
    campaign_id uuid not null references audit_confirmation_campaigns(id) on delete cascade,
    audit_engagement_id uuid not null references audit_engagements(id) on delete cascade,
    lead_schedule_ref text not null default 'B',
    party_type text not null default 'TradeDebtor',
    party_name text not null default '',
    party_contact_email text not null default '',
    party_contact_phone text,
    party_pan_gstin text,
    confirmation_type text not null default 'PositiveStatedBalance',
    book_balance_amount numeric(18,2) not null default 0,
    currency text not null default 'INR',
    as_of_date date not null,
    secure_access_token text not null,
    token_expires_at timestamptz not null,
    status text not null default 'Draft',
    sent_at timestamptz,
    responded_at timestamptz,
    confirmed_balance_amount numeric(18,2),
    variance_amount numeric(18,2),
    dispute_reason text not null default 'None',
    third_party_remarks text,
    third_party_signatory_name text,
    third_party_designation text,
    attachment_doc_id uuid,
    alternative_procedures_performed text,
    auditor_conclusion text not null default 'PendingVerification',
    proposed_adjustment_amount numeric(18,2),
    auditor_notes text,
    reviewed_by uuid,
    reviewed_at timestamptz,
    created_at timestamptz not null default now(),
    created_by uuid,
    updated_at timestamptz,
    updated_by uuid
);
create unique index if not exists ix_audit_external_confirmations_token on audit_external_confirmations (secure_access_token);
alter table audit_external_confirmations enable row level security;
drop policy if exists tenant_isolation on audit_external_confirmations;
create policy tenant_isolation on audit_external_confirmations using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());
grant select, insert, update, delete on audit_external_confirmations to cadesk_app;

-- 7. Independent Auditor Reports
create table if not exists audit_independent_auditor_reports (
    id uuid primary key,
    firm_id uuid not null references firms(id),
    audit_engagement_id uuid not null references audit_engagements(id) on delete cascade,
    report_title text not null default 'INDEPENDENT AUDITOR''S REPORT',
    opinion_type text not null default 'Unmodified',
    report_date date not null,
    place_of_signature text not null default 'Mumbai',
    auditor_firm_name text not null default '',
    firm_registration_number text not null default '',
    partner_name text not null default '',
    partner_membership_number text not null default '',
    udin text,
    opinion_paragraph text not null default '',
    basis_for_opinion_paragraph text not null default '',
    material_uncertainty_going_concern_paragraph text,
    emphasis_of_matter_paragraph text,
    other_matter_paragraph text,
    include_key_audit_matters boolean not null default true,
    key_audit_matters_intro text,
    responsibilities_of_management_paragraph text not null default '',
    auditor_responsibilities_paragraph text not null default '',
    clause143_3_a_information_obtained boolean not null default true,
    clause143_3_b_proper_books_kept boolean not null default true,
    clause143_3_c_agreement_with_books boolean not null default true,
    clause143_3_d_compliance_with_as boolean not null default true,
    clause143_3_e_adverse_functioning_remarks text,
    clause143_3_f_directors_disqualified boolean not null default false,
    clause143_3_g_maintenance_accounts_qualifications text,
    clause143_3_h_internal_financial_controls_annexure boolean not null default true,
    rule11_a_pending_litigations text not null default '',
    rule11_b_foreseeable_losses text not null default '',
    rule11_c_iepf_transfers text not null default '',
    rule11_e_intermediary_funds text not null default '',
    rule11_f_dividend_compliance text not null default '',
    rule11_g_audit_trail_status text not null default '',
    rule11_g_audit_trail_remarks text not null default '',
    has_caro2020_annexure boolean not null default true,
    status text not null default 'Draft',
    created_at timestamptz not null default now(),
    created_by uuid,
    updated_at timestamptz,
    updated_by uuid
);
alter table audit_independent_auditor_reports enable row level security;
drop policy if exists tenant_isolation on audit_independent_auditor_reports;
create policy tenant_isolation on audit_independent_auditor_reports using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());
grant select, insert, update, delete on audit_independent_auditor_reports to cadesk_app;

-- 8. Key Audit Matters
create table if not exists audit_key_audit_matters (
    id uuid primary key,
    firm_id uuid not null references firms(id),
    report_id uuid not null references audit_independent_auditor_reports(id) on delete cascade,
    category text not null default 'RevenueRecognition',
    title text not null default '',
    description_of_matter text not null default '',
    why_considered_significant text not null default '',
    how_addressed_in_audit text not null default '',
    key_documentation_ref text
);
alter table audit_key_audit_matters enable row level security;
drop policy if exists tenant_isolation on audit_key_audit_matters;
create policy tenant_isolation on audit_key_audit_matters using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());
grant select, insert, update, delete on audit_key_audit_matters to cadesk_app;

-- 9. Notes to Accounts Master
create table if not exists audit_notes_to_accounts (
    id uuid primary key,
    firm_id uuid not null references firms(id),
    audit_engagement_id uuid not null references audit_engagements(id) on delete cascade,
    financial_year text not null default '2024-25',
    note1_corporate_information text not null default '',
    note2_accounting_policies text not null default '',
    balance_sheet_notes_json jsonb not null default '{}'::jsonb,
    profit_and_loss_notes_json jsonb not null default '{}'::jsonb,
    regulatory_ratios_json jsonb not null default '[]'::jsonb,
    additional_regulatory_disclosures text not null default '',
    status text not null default 'Generated',
    created_at timestamptz not null default now(),
    created_by uuid,
    updated_at timestamptz,
    updated_by uuid
);
alter table audit_notes_to_accounts enable row level security;
drop policy if exists tenant_isolation on audit_notes_to_accounts;
create policy tenant_isolation on audit_notes_to_accounts using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());
grant select, insert, update, delete on audit_notes_to_accounts to cadesk_app;

-- 10. Forensic Anomalies
create table if not exists bank_forensic_anomalies (
    id uuid primary key,
    firm_id uuid not null references firms(id),
    bank_account_id uuid not null references bank_accounts(id) on delete cascade,
    transaction_id uuid references bank_transactions(id) on delete set null,
    transaction_date date not null,
    anomaly_type text not null default 'Sec40A3CashPayment',
    risk_level text not null default 'High',
    amount numeric(18,2) not null default 0,
    exposure_amount numeric(18,2) not null default 0,
    counterparty text not null default '',
    narration text not null default '',
    statutory_section text not null default 'Section 40A(3)',
    recommended_action text not null default '',
    auditor_notes text,
    status text not null default 'Flagged',
    created_at timestamptz not null default now(),
    created_by uuid,
    updated_at timestamptz,
    updated_by uuid
);
alter table bank_forensic_anomalies enable row level security;
drop policy if exists tenant_isolation on bank_forensic_anomalies;
create policy tenant_isolation on bank_forensic_anomalies using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());
grant select, insert, update, delete on bank_forensic_anomalies to cadesk_app;
