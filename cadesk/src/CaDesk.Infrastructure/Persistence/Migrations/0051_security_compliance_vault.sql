-- 0051_security_compliance_vault.sql
-- ISO 27001:2022 & SOC 2 Type II Compliance Evidence Vault & Security Whitepaper Center (Sprint 52)

CREATE TABLE IF NOT EXISTS security_assessments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    assessment_period VARCHAR(50) NOT NULL DEFAULT '2024-25',
    status VARCHAR(50) NOT NULL DEFAULT 'Active',
    overall_score_percentage INT NOT NULL DEFAULT 96,
    total_controls_count INT NOT NULL DEFAULT 18,
    compliant_controls_count INT NOT NULL DEFAULT 17,
    partially_compliant_count INT NOT NULL DEFAULT 1,
    access_control_score INT NOT NULL DEFAULT 100,
    cryptography_score INT NOT NULL DEFAULT 100,
    operations_score INT NOT NULL DEFAULT 92,
    physical_score INT NOT NULL DEFAULT 95,
    privacy_score INT NOT NULL DEFAULT 95,
    certified_by_partner VARCHAR(200),
    partner_membership_no VARCHAR(50),
    last_evaluated_at TIMESTAMPTZ,
    certified_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID,
    updated_at TIMESTAMPTZ,
    updated_by UUID
);

CREATE INDEX IF NOT EXISTS ix_security_assessments_firm_period ON security_assessments (firm_id, assessment_period);

ALTER TABLE security_assessments ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS security_assessments_isolation ON security_assessments;
CREATE POLICY security_assessments_isolation ON security_assessments
    FOR ALL
    USING (firm_id = app_current_firm())
    WITH CHECK (firm_id = app_current_firm());

GRANT SELECT, INSERT, UPDATE, DELETE ON security_assessments TO cadesk_app;


CREATE TABLE IF NOT EXISTS security_control_evidences (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    assessment_id UUID NOT NULL REFERENCES security_assessments(id) ON DELETE CASCADE,
    control_code VARCHAR(50) NOT NULL,
    title VARCHAR(255) NOT NULL,
    category VARCHAR(100) NOT NULL,
    framework_mapping VARCHAR(255) NOT NULL,
    description TEXT NOT NULL,
    verification_method TEXT NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Compliant',
    is_automated_telemetry BOOLEAN NOT NULL DEFAULT TRUE,
    telemetry_signal TEXT,
    evidence_notes TEXT,
    last_tested_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID,
    updated_at TIMESTAMPTZ,
    updated_by UUID
);

CREATE INDEX IF NOT EXISTS ix_security_evidences_firm_ass_c ON security_control_evidences (firm_id, assessment_id, control_code);

ALTER TABLE security_control_evidences ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS security_control_evidences_isolation ON security_control_evidences;
CREATE POLICY security_control_evidences_isolation ON security_control_evidences
    FOR ALL
    USING (firm_id = app_current_firm())
    WITH CHECK (firm_id = app_current_firm());

GRANT SELECT, INSERT, UPDATE, DELETE ON security_control_evidences TO cadesk_app;


-- Register Security Compliance permissions
INSERT INTO permissions (code, area, description)
VALUES
    ('security.read', 'security', 'Can view the ISO 27001 & SOC 2 compliance evidence vault and security whitepapers'),
    ('security.manage', 'security', 'Can run continuous audit telemetry scans and update control evidence')
ON CONFLICT (code) DO NOTHING;

-- Grant to Partner and Admin
INSERT INTO role_permissions (role_id, permission_code, firm_id)
SELECT r.id, p.code, r.firm_id
FROM roles r
CROSS JOIN (VALUES ('security.read'), ('security.manage')) AS p(code)
WHERE r.name IN ('Partner', 'Admin')
ON CONFLICT (role_id, permission_code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_code, firm_id)
SELECT r.id, 'security.read', r.firm_id
FROM roles r
WHERE r.name = 'Manager'
ON CONFLICT (role_id, permission_code) DO NOTHING;
