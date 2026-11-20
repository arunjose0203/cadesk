-- 0037_anti_profiteering_engine.sql
-- CGST Act, 2017 Section 171 Anti-Profiteering & Rate Notification Engine

CREATE TABLE IF NOT EXISTS gst_rate_notifications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    notification_number VARCHAR(100) NOT NULL,
    notification_date TIMESTAMPTZ NOT NULL,
    effective_date TIMESTAMPTZ NOT NULL,
    hsn_or_sac_code VARCHAR(20) NOT NULL,
    description VARCHAR(500) NOT NULL,
    previous_rate_percentage NUMERIC(6,2) NOT NULL,
    revised_rate_percentage NUMERIC(6,2) NOT NULL,
    rate_reduction_percentage NUMERIC(6,2) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    notes TEXT NOT NULL DEFAULT '',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_gst_rate_notifications_firm_hsn ON gst_rate_notifications (firm_id, hsn_or_sac_code);
CREATE INDEX IF NOT EXISTS ix_gst_rate_notifications_firm_date ON gst_rate_notifications (firm_id, effective_date);

ALTER TABLE gst_rate_notifications ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS gst_rate_notifications_isolation ON gst_rate_notifications;
CREATE POLICY gst_rate_notifications_isolation ON gst_rate_notifications
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON gst_rate_notifications TO cadesk_app;


CREATE TABLE IF NOT EXISTS anti_profiteering_assessments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    client_id UUID NOT NULL REFERENCES clients(id) ON DELETE CASCADE,
    assessment_reference VARCHAR(50) NOT NULL,
    title VARCHAR(255) NOT NULL,
    financial_year VARCHAR(20) NOT NULL,
    audit_period_from TIMESTAMPTZ NOT NULL,
    audit_period_to TIMESTAMPTZ NOT NULL,
    gst_notification_id UUID REFERENCES gst_rate_notifications(id) ON DELETE SET NULL,
    notification_reference VARCHAR(100) NOT NULL DEFAULT '',
    rate_cut_effective_date TIMESTAMPTZ,
    audit_status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    lead_auditor_name VARCHAR(150) NOT NULL DEFAULT '',
    udin VARCHAR(50),
    certification_date TIMESTAMPTZ,
    total_skus_audited INT NOT NULL DEFAULT 0,
    profiteered_skus_count INT NOT NULL DEFAULT 0,
    total_turnover_post_cut NUMERIC(15,2) NOT NULL DEFAULT 0,
    total_undue_profiteered_amount NUMERIC(15,2) NOT NULL DEFAULT 0,
    total_customer_refunded_amount NUMERIC(15,2) NOT NULL DEFAULT 0,
    total_cwf_deposited_amount NUMERIC(15,2) NOT NULL DEFAULT 0,
    cwf_interest_payable_amount NUMERIC(15,2) NOT NULL DEFAULT 0,
    statutory_report_text TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_apa_firm_client_fy ON anti_profiteering_assessments (firm_id, client_id, financial_year);

ALTER TABLE anti_profiteering_assessments ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS anti_profiteering_assessments_isolation ON anti_profiteering_assessments;
CREATE POLICY anti_profiteering_assessments_isolation ON anti_profiteering_assessments
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON anti_profiteering_assessments TO cadesk_app;


CREATE TABLE IF NOT EXISTS sku_profiteering_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    assessment_id UUID NOT NULL REFERENCES anti_profiteering_assessments(id) ON DELETE CASCADE,
    product_code VARCHAR(100) NOT NULL,
    hsn_or_sac_code VARCHAR(20) NOT NULL,
    product_name VARCHAR(255) NOT NULL,
    unit_of_measurement VARCHAR(20) NOT NULL DEFAULT 'PCS',
    pre_cut_base_price NUMERIC(12,2) NOT NULL DEFAULT 0,
    old_gst_rate NUMERIC(6,2) NOT NULL DEFAULT 0,
    pre_cut_selling_price NUMERIC(12,2) NOT NULL DEFAULT 0,
    new_gst_rate NUMERIC(6,2) NOT NULL DEFAULT 0,
    commensurate_price_required NUMERIC(12,2) NOT NULL DEFAULT 0,
    post_cut_actual_base_price NUMERIC(12,2) NOT NULL DEFAULT 0,
    post_cut_actual_selling_price NUMERIC(12,2) NOT NULL DEFAULT 0,
    base_price_increase_percentage NUMERIC(6,2) NOT NULL DEFAULT 0,
    quantity_sold_post_cut NUMERIC(12,2) NOT NULL DEFAULT 0,
    profiteering_per_unit NUMERIC(12,2) NOT NULL DEFAULT 0,
    total_profiteered_amount NUMERIC(15,2) NOT NULL DEFAULT 0,
    compliance_status VARCHAR(50) NOT NULL DEFAULT 'Compliant',
    has_raw_material_cost_justification BOOLEAN NOT NULL DEFAULT FALSE,
    justification_remarks TEXT NOT NULL DEFAULT '',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_sku_items_firm_assessment ON sku_profiteering_items (firm_id, assessment_id);

ALTER TABLE sku_profiteering_items ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS sku_profiteering_items_isolation ON sku_profiteering_items;
CREATE POLICY sku_profiteering_items_isolation ON sku_profiteering_items
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON sku_profiteering_items TO cadesk_app;


CREATE TABLE IF NOT EXISTS profiteering_refund_records (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    assessment_id UUID NOT NULL REFERENCES anti_profiteering_assessments(id) ON DELETE CASCADE,
    recipient_type VARCHAR(50) NOT NULL DEFAULT 'IdentifiedCustomer',
    customer_name_or_entity VARCHAR(255) NOT NULL DEFAULT '',
    customer_gstin VARCHAR(20) NOT NULL DEFAULT '',
    invoice_reference VARCHAR(100) NOT NULL DEFAULT '',
    refund_amount NUMERIC(15,2) NOT NULL DEFAULT 0,
    refund_mode VARCHAR(50) NOT NULL DEFAULT 'CreditNote',
    refund_or_deposit_date TIMESTAMPTZ NOT NULL,
    challan_or_cin_number VARCHAR(100) NOT NULL DEFAULT '',
    interest_amount_paid NUMERIC(15,2) NOT NULL DEFAULT 0,
    notes TEXT NOT NULL DEFAULT '',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_refund_records_firm_assessment ON profiteering_refund_records (firm_id, assessment_id);

ALTER TABLE profiteering_refund_records ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS profiteering_refund_records_isolation ON profiteering_refund_records;
CREATE POLICY profiteering_refund_records_isolation ON profiteering_refund_records
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON profiteering_refund_records TO cadesk_app;
