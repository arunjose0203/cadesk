-- 0039_transfer_pricing_3ceb.sql
-- Income Tax Act, 1961 Section 92E / Rule 10D & 10E Form 3CEB Certification Desk

CREATE TABLE IF NOT EXISTS transfer_pricing_studies (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    client_id UUID NOT NULL REFERENCES clients(id) ON DELETE CASCADE,
    study_reference VARCHAR(50) NOT NULL,
    title VARCHAR(255) NOT NULL,
    financial_year VARCHAR(20) NOT NULL,
    assessment_year VARCHAR(20) NOT NULL,
    audit_period_start TIMESTAMPTZ NOT NULL,
    audit_period_end TIMESTAMPTZ NOT NULL,
    rule_10d_documentation_maintained BOOLEAN NOT NULL DEFAULT TRUE,
    rule_10d_documentation_path VARCHAR(500) NOT NULL DEFAULT '',
    study_status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    accountant_name VARCHAR(150) NOT NULL DEFAULT '',
    accountant_firm_name VARCHAR(255) NOT NULL DEFAULT '',
    membership_number VARCHAR(50) NOT NULL DEFAULT '',
    firm_registration_number VARCHAR(50) NOT NULL DEFAULT '',
    udin VARCHAR(50),
    certified_at TIMESTAMPTZ,
    certified_by VARCHAR(150),
    total_associated_enterprises_count INT NOT NULL DEFAULT 0,
    total_international_transactions_count INT NOT NULL DEFAULT 0,
    total_international_inr_value NUMERIC(18,2) NOT NULL DEFAULT 0,
    total_sdt_transactions_count INT NOT NULL DEFAULT 0,
    total_sdt_inr_value NUMERIC(18,2) NOT NULL DEFAULT 0,
    total_alp_adjustments_count INT NOT NULL DEFAULT 0,
    total_alp_adjustment_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    transactions_within_tolerance_count INT NOT NULL DEFAULT 0,
    transactions_outside_tolerance_count INT NOT NULL DEFAULT 0,
    form_3ceb_report_text TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_tp_studies_firm_client_fy ON transfer_pricing_studies (firm_id, client_id, financial_year);

ALTER TABLE transfer_pricing_studies ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS transfer_pricing_studies_isolation ON transfer_pricing_studies;
CREATE POLICY transfer_pricing_studies_isolation ON transfer_pricing_studies
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON transfer_pricing_studies TO cadesk_app;


CREATE TABLE IF NOT EXISTS associated_enterprises (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    study_id UUID NOT NULL REFERENCES transfer_pricing_studies(id) ON DELETE CASCADE,
    enterprise_name VARCHAR(255) NOT NULL,
    country_of_residence VARCHAR(100) NOT NULL DEFAULT 'USA',
    country_code VARCHAR(10) NOT NULL DEFAULT 'US',
    tax_identification_number VARCHAR(100) NOT NULL DEFAULT '',
    registered_address TEXT NOT NULL DEFAULT '',
    relationship_section VARCHAR(255) NOT NULL,
    ownership_percentage NUMERIC(6,2) NOT NULL DEFAULT 0,
    brief_activity_description TEXT NOT NULL DEFAULT '',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_ae_firm_study ON associated_enterprises (firm_id, study_id);

ALTER TABLE associated_enterprises ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS associated_enterprises_isolation ON associated_enterprises;
CREATE POLICY associated_enterprises_isolation ON associated_enterprises
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON associated_enterprises TO cadesk_app;


CREATE TABLE IF NOT EXISTS transfer_pricing_transactions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    study_id UUID NOT NULL REFERENCES transfer_pricing_studies(id) ON DELETE CASCADE,
    associated_enterprise_id UUID REFERENCES associated_enterprises(id) ON DELETE SET NULL,
    transaction_category VARCHAR(100) NOT NULL DEFAULT 'InternationalTransaction',
    clause_number INT NOT NULL DEFAULT 11,
    description_of_transaction TEXT NOT NULL,
    currency_code VARCHAR(10) NOT NULL DEFAULT 'USD',
    foreign_currency_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    exchange_rate NUMERIC(12,4) NOT NULL DEFAULT 1.0000,
    inr_value NUMERIC(18,2) NOT NULL DEFAULT 0,
    most_appropriate_method VARCHAR(50) NOT NULL DEFAULT 'TNMM',
    profit_level_indicator VARCHAR(50) NOT NULL DEFAULT 'OP_TC',
    tested_party_cost_base NUMERIC(18,2) NOT NULL DEFAULT 0,
    tested_party_margin NUMERIC(8,4) NOT NULL DEFAULT 0,
    comparables_count INT NOT NULL DEFAULT 0,
    benchmark_range_min NUMERIC(8,4) NOT NULL DEFAULT 0,
    benchmark_range_max NUMERIC(8,4) NOT NULL DEFAULT 0,
    benchmark_median_or_mean NUMERIC(8,4) NOT NULL DEFAULT 0,
    tolerance_band_percentage NUMERIC(6,2) NOT NULL DEFAULT 3.00,
    is_within_arm_length BOOLEAN NOT NULL DEFAULT TRUE,
    alp_adjustment_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    method_selection_rationale TEXT NOT NULL DEFAULT '',
    documentation_reference VARCHAR(255) NOT NULL DEFAULT '',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_tp_txs_firm_study ON transfer_pricing_transactions (firm_id, study_id);

ALTER TABLE transfer_pricing_transactions ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS transfer_pricing_transactions_isolation ON transfer_pricing_transactions;
CREATE POLICY transfer_pricing_transactions_isolation ON transfer_pricing_transactions
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON transfer_pricing_transactions TO cadesk_app;


CREATE TABLE IF NOT EXISTS comparable_benchmarks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    study_id UUID NOT NULL REFERENCES transfer_pricing_studies(id) ON DELETE CASCADE,
    transaction_id UUID REFERENCES transfer_pricing_transactions(id) ON DELETE SET NULL,
    comparable_company_name VARCHAR(255) NOT NULL,
    cin_or_identifier VARCHAR(50) NOT NULL DEFAULT '',
    financial_year VARCHAR(20) NOT NULL DEFAULT '',
    operating_revenue NUMERIC(18,2) NOT NULL DEFAULT 0,
    operating_cost NUMERIC(18,2) NOT NULL DEFAULT 0,
    operating_profit NUMERIC(18,2) NOT NULL DEFAULT 0,
    calculated_margin_percentage NUMERIC(8,4) NOT NULL DEFAULT 0,
    weight NUMERIC(6,2) NOT NULL DEFAULT 1.00,
    is_accepted BOOLEAN NOT NULL DEFAULT TRUE,
    rejection_reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_comparables_firm_study ON comparable_benchmarks (firm_id, study_id);

ALTER TABLE comparable_benchmarks ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS comparable_benchmarks_isolation ON comparable_benchmarks;
CREATE POLICY comparable_benchmarks_isolation ON comparable_benchmarks
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON comparable_benchmarks TO cadesk_app;
