-- 0050_subscriptions_and_aqmm.sql
-- Subscriptions & Commercial Licensing (Sprint 47) and ICAI AQMM v1.0 Self-Assessment Desk (Sprint 49)

CREATE TABLE IF NOT EXISTS firm_subscriptions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    plan_tier VARCHAR(50) NOT NULL DEFAULT 'STARTER',
    status VARCHAR(50) NOT NULL DEFAULT 'Trialing',
    billing_interval VARCHAR(20) NOT NULL DEFAULT 'Monthly',
    trial_starts_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    trial_ends_at TIMESTAMPTZ NOT NULL DEFAULT NOW() + INTERVAL '14 days',
    current_period_start TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    current_period_end TIMESTAMPTZ NOT NULL DEFAULT NOW() + INTERVAL '14 days',
    cancel_at_period_end BOOLEAN NOT NULL DEFAULT FALSE,
    razorpay_subscription_id VARCHAR(100),
    razorpay_customer_id VARCHAR(100),
    last_payment_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    last_payment_date TIMESTAMPTZ,
    last_payment_invoice_number VARCHAR(100),
    is_demo_data_seeded BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID,
    updated_at TIMESTAMPTZ,
    updated_by UUID
);

CREATE INDEX IF NOT EXISTS ix_firm_subscriptions_firm ON firm_subscriptions (firm_id);

ALTER TABLE firm_subscriptions ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS firm_subscriptions_isolation ON firm_subscriptions;
CREATE POLICY firm_subscriptions_isolation ON firm_subscriptions
    FOR ALL
    USING (firm_id = app_current_firm())
    WITH CHECK (firm_id = app_current_firm());

GRANT SELECT, INSERT, UPDATE, DELETE ON firm_subscriptions TO cadesk_app;


CREATE TABLE IF NOT EXISTS subscription_invoices (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    invoice_number VARCHAR(100) NOT NULL,
    invoice_date DATE NOT NULL,
    plan_tier VARCHAR(50) NOT NULL DEFAULT 'STARTER',
    billing_interval VARCHAR(20) NOT NULL DEFAULT 'Monthly',
    base_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    cgst_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    sgst_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    igst_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    total_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    currency VARCHAR(10) NOT NULL DEFAULT 'INR',
    sac_code VARCHAR(20) NOT NULL DEFAULT '998311',
    payment_status VARCHAR(50) NOT NULL DEFAULT 'Paid',
    payment_method VARCHAR(50) NOT NULL DEFAULT 'UPI AutoPay',
    transaction_reference VARCHAR(100) NOT NULL DEFAULT '',
    firm_gstin VARCHAR(50),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID,
    updated_at TIMESTAMPTZ,
    updated_by UUID
);

CREATE INDEX IF NOT EXISTS ix_sub_invoices_firm_inv ON subscription_invoices (firm_id, invoice_number);

ALTER TABLE subscription_invoices ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS subscription_invoices_isolation ON subscription_invoices;
CREATE POLICY subscription_invoices_isolation ON subscription_invoices
    FOR ALL
    USING (firm_id = app_current_firm())
    WITH CHECK (firm_id = app_current_firm());

GRANT SELECT, INSERT, UPDATE, DELETE ON subscription_invoices TO cadesk_app;


-- ICAI AQMM v1.0 Assessments (600-Point Framework)
CREATE TABLE IF NOT EXISTS aqmm_assessments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    financial_year VARCHAR(20) NOT NULL DEFAULT '2024-25',
    status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    total_score INT NOT NULL DEFAULT 0,
    max_possible_score INT NOT NULL DEFAULT 600,
    overall_percentage NUMERIC(6,2) NOT NULL DEFAULT 0,
    maturity_level VARCHAR(50) NOT NULL DEFAULT 'Level 1',
    section_a_score INT NOT NULL DEFAULT 0,
    section_b_score INT NOT NULL DEFAULT 0,
    section_c_score INT NOT NULL DEFAULT 0,
    lead_partner_name VARCHAR(255),
    partner_membership_no VARCHAR(50),
    firm_registration_no VARCHAR(50),
    udin VARCHAR(50),
    certified_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID,
    updated_at TIMESTAMPTZ,
    updated_by UUID
);

CREATE INDEX IF NOT EXISTS ix_aqmm_assessments_firm_fy ON aqmm_assessments (firm_id, financial_year);

ALTER TABLE aqmm_assessments ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS aqmm_assessments_isolation ON aqmm_assessments;
CREATE POLICY aqmm_assessments_isolation ON aqmm_assessments
    FOR ALL
    USING (firm_id = app_current_firm())
    WITH CHECK (firm_id = app_current_firm());

GRANT SELECT, INSERT, UPDATE, DELETE ON aqmm_assessments TO cadesk_app;


CREATE TABLE IF NOT EXISTS aqmm_responses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    assessment_id UUID NOT NULL REFERENCES aqmm_assessments(id) ON DELETE CASCADE,
    question_code VARCHAR(50) NOT NULL,
    section VARCHAR(50) NOT NULL,
    max_points INT NOT NULL,
    awarded_points INT NOT NULL DEFAULT 0,
    partner_remarks TEXT,
    evidence_reference TEXT,
    is_auto_evaluated BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID,
    updated_at TIMESTAMPTZ,
    updated_by UUID
);

CREATE INDEX IF NOT EXISTS ix_aqmm_responses_firm_ass_q ON aqmm_responses (firm_id, assessment_id, question_code);

ALTER TABLE aqmm_responses ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS aqmm_responses_isolation ON aqmm_responses;
CREATE POLICY aqmm_responses_isolation ON aqmm_responses
    FOR ALL
    USING (firm_id = app_current_firm())
    WITH CHECK (firm_id = app_current_firm());

GRANT SELECT, INSERT, UPDATE, DELETE ON aqmm_responses TO cadesk_app;


-- Register AQMM permissions
INSERT INTO permissions (code, area, description)
VALUES
    ('aqmm.read', 'aqmm', 'Can view the ICAI AQMM v1.0 maturity scores and self-assessments'),
    ('aqmm.manage', 'aqmm', 'Can score, update responses, and certify AQMM assessments')
ON CONFLICT (code) DO NOTHING;

-- Grant to Partner and Manager roles
INSERT INTO role_permissions (role_id, permission_code, firm_id)
SELECT r.id, p.code, r.firm_id
FROM roles r
CROSS JOIN (VALUES ('aqmm.read'), ('aqmm.manage')) AS p(code)
WHERE r.name IN ('Partner', 'Admin')
ON CONFLICT (role_id, permission_code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_code, firm_id)
SELECT r.id, 'aqmm.read', r.firm_id
FROM roles r
WHERE r.name = 'Manager'
ON CONFLICT (role_id, permission_code) DO NOTHING;
