-- 0040_cost_audit_cra.sql
-- Companies Act, 2013 Section 148 / Companies (Cost Records and Audit) Rules, 2014 Form CRA-1 to CRA-4 Desk

CREATE TABLE IF NOT EXISTS cost_audit_engagements (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    client_id UUID NOT NULL REFERENCES clients(id) ON DELETE CASCADE,
    engagement_reference VARCHAR(50) NOT NULL,
    financial_year VARCHAR(20) NOT NULL,
    period_start TIMESTAMPTZ NOT NULL,
    period_end TIMESTAMPTZ NOT NULL,
    sector_type VARCHAR(50) NOT NULL DEFAULT 'Regulated',
    sector_category VARCHAR(100) NOT NULL DEFAULT 'DrugsAndPharma',
    overall_turnover_inr NUMERIC(18,2) NOT NULL DEFAULT 0,
    product_turnover_inr NUMERIC(18,2) NOT NULL DEFAULT 0,
    export_turnover_inr NUMERIC(18,2) NOT NULL DEFAULT 0,
    is_export_exempt BOOLEAN NOT NULL DEFAULT FALSE,
    is_sez_exempt BOOLEAN NOT NULL DEFAULT FALSE,
    is_captive_power_exempt BOOLEAN NOT NULL DEFAULT FALSE,
    is_cost_records_applicable BOOLEAN NOT NULL DEFAULT TRUE,
    is_cost_audit_applicable BOOLEAN NOT NULL DEFAULT TRUE,
    applicability_reasoning TEXT NOT NULL DEFAULT '',
    board_resolution_date TIMESTAMPTZ,
    fy_commencement_date TIMESTAMPTZ,
    audit_committee_recommended BOOLEAN NOT NULL DEFAULT TRUE,
    cost_auditor_name VARCHAR(150) NOT NULL DEFAULT '',
    cost_auditor_firm_name VARCHAR(255) NOT NULL DEFAULT '',
    cost_auditor_membership_no VARCHAR(50) NOT NULL DEFAULT '',
    cost_auditor_firm_reg_no VARCHAR(50) NOT NULL DEFAULT '',
    cra2_filing_due_date TIMESTAMPTZ,
    cra2_filing_status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    cra2_srn VARCHAR(50),
    cra2_filed_date TIMESTAMPTZ,
    audit_status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    auditor_opinion VARCHAR(50) NOT NULL DEFAULT 'Unmodified',
    reservations_qualifications TEXT,
    observations_suggestions TEXT,
    udin VARCHAR(50),
    certified_at TIMESTAMPTZ,
    certified_by VARCHAR(150),
    cost_audit_report_text TEXT,
    cra3_handover_date TIMESTAMPTZ,
    cra4_filing_due_date TIMESTAMPTZ,
    cra4_filing_status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    cra4_srn VARCHAR(50),
    cra4_filed_date TIMESTAMPTZ,
    cra4_xbrl_validated BOOLEAN NOT NULL DEFAULT FALSE,
    total_products_count INT NOT NULL DEFAULT 0,
    total_production_value_inr NUMERIC(18,2) NOT NULL DEFAULT 0,
    total_cost_of_sales_inr NUMERIC(18,2) NOT NULL DEFAULT 0,
    total_sales_realization_inr NUMERIC(18,2) NOT NULL DEFAULT 0,
    total_cost_profit_inr NUMERIC(18,2) NOT NULL DEFAULT 0,
    audited_financial_profit_inr NUMERIC(18,2) NOT NULL DEFAULT 0,
    reconciled_financial_profit_inr NUMERIC(18,2) NOT NULL DEFAULT 0,
    net_reconciliation_difference_inr NUMERIC(18,2) NOT NULL DEFAULT 0,
    records_compliance_score_pct NUMERIC(5,2) NOT NULL DEFAULT 100.00,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_cost_audit_engagements_firm_client_fy ON cost_audit_engagements (firm_id, client_id, financial_year);

ALTER TABLE cost_audit_engagements ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS cost_audit_engagements_isolation ON cost_audit_engagements;
CREATE POLICY cost_audit_engagements_isolation ON cost_audit_engagements
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON cost_audit_engagements TO cadesk_app;


CREATE TABLE IF NOT EXISTS cost_record_heads (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    engagement_id UUID NOT NULL REFERENCES cost_audit_engagements(id) ON DELETE CASCADE,
    head_code VARCHAR(50) NOT NULL,
    head_name VARCHAR(255) NOT NULL,
    rule_reference VARCHAR(100) NOT NULL DEFAULT 'CRA-1 Clause',
    status VARCHAR(50) NOT NULL DEFAULT 'Compliant',
    record_maintenance_system VARCHAR(150) NOT NULL DEFAULT 'ERP Cost Ledger',
    verification_notes TEXT,
    deficiency_or_anomaly TEXT,
    corrective_action_required TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_cost_record_heads_firm_engagement ON cost_record_heads (firm_id, engagement_id);

ALTER TABLE cost_record_heads ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS cost_record_heads_isolation ON cost_record_heads;
CREATE POLICY cost_record_heads_isolation ON cost_record_heads
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON cost_record_heads TO cadesk_app;


CREATE TABLE IF NOT EXISTS product_cost_statements (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    engagement_id UUID NOT NULL REFERENCES cost_audit_engagements(id) ON DELETE CASCADE,
    product_code VARCHAR(50) NOT NULL,
    product_name VARCHAR(255) NOT NULL,
    cta_heading VARCHAR(50) NOT NULL DEFAULT '',
    unit_of_measurement VARCHAR(50) NOT NULL DEFAULT 'MT',
    installed_capacity NUMERIC(18,2) NOT NULL DEFAULT 0,
    available_capacity NUMERIC(18,2) NOT NULL DEFAULT 0,
    actual_production NUMERIC(18,2) NOT NULL DEFAULT 0,
    capacity_utilization_pct NUMERIC(5,2) NOT NULL DEFAULT 0,
    direct_materials_cost NUMERIC(18,2) NOT NULL DEFAULT 0,
    direct_labor_cost NUMERIC(18,2) NOT NULL DEFAULT 0,
    utilities_cost NUMERIC(18,2) NOT NULL DEFAULT 0,
    other_direct_expenses NUMERIC(18,2) NOT NULL DEFAULT 0,
    prime_cost NUMERIC(18,2) NOT NULL DEFAULT 0,
    works_overheads NUMERIC(18,2) NOT NULL DEFAULT 0,
    works_cost NUMERIC(18,2) NOT NULL DEFAULT 0,
    quality_control_cost NUMERIC(18,2) NOT NULL DEFAULT 0,
    rnd_expenses NUMERIC(18,2) NOT NULL DEFAULT 0,
    administration_overheads NUMERIC(18,2) NOT NULL DEFAULT 0,
    opening_wip_cost NUMERIC(18,2) NOT NULL DEFAULT 0,
    closing_wip_cost NUMERIC(18,2) NOT NULL DEFAULT 0,
    cost_of_production NUMERIC(18,2) NOT NULL DEFAULT 0,
    opening_finished_goods_cost NUMERIC(18,2) NOT NULL DEFAULT 0,
    closing_finished_goods_cost NUMERIC(18,2) NOT NULL DEFAULT 0,
    selling_and_distribution_overheads NUMERIC(18,2) NOT NULL DEFAULT 0,
    cost_of_sales NUMERIC(18,2) NOT NULL DEFAULT 0,
    net_sales_realization NUMERIC(18,2) NOT NULL DEFAULT 0,
    product_margin_inr NUMERIC(18,2) NOT NULL DEFAULT 0,
    product_margin_pct NUMERIC(5,2) NOT NULL DEFAULT 0,
    unit_cost_of_sales NUMERIC(18,2) NOT NULL DEFAULT 0,
    unit_sales_realization NUMERIC(18,2) NOT NULL DEFAULT 0,
    unit_margin_inr NUMERIC(18,2) NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_product_cost_statements_firm_engagement ON product_cost_statements (firm_id, engagement_id);

ALTER TABLE product_cost_statements ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS product_cost_statements_isolation ON product_cost_statements;
CREATE POLICY product_cost_statements_isolation ON product_cost_statements
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON product_cost_statements TO cadesk_app;


CREATE TABLE IF NOT EXISTS cost_audit_reconciliation_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    engagement_id UUID NOT NULL REFERENCES cost_audit_engagements(id) ON DELETE CASCADE,
    item_type VARCHAR(100) NOT NULL DEFAULT 'Add_IncomeNotCosted',
    description VARCHAR(255) NOT NULL,
    amount_inr NUMERIC(18,2) NOT NULL DEFAULT 0,
    impact VARCHAR(20) NOT NULL DEFAULT 'Positive',
    financial_schedule_ref VARCHAR(100),
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_cost_audit_rec_items_firm_engagement ON cost_audit_reconciliation_items (firm_id, engagement_id);

ALTER TABLE cost_audit_reconciliation_items ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS cost_audit_reconciliation_items_isolation ON cost_audit_reconciliation_items;
CREATE POLICY cost_audit_reconciliation_items_isolation ON cost_audit_reconciliation_items
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON cost_audit_reconciliation_items TO cadesk_app;
