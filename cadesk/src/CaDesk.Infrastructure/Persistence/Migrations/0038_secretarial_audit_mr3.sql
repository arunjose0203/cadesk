-- 0038_secretarial_audit_mr3.sql
-- Companies Act, 2013 Section 204 & Rule 9 Secretarial Audit & Form MR-3 Compliance Engine

CREATE TABLE IF NOT EXISTS secretarial_audit_engagements (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    client_id UUID NOT NULL REFERENCES clients(id) ON DELETE CASCADE,
    engagement_reference VARCHAR(50) NOT NULL,
    title VARCHAR(255) NOT NULL,
    financial_year VARCHAR(20) NOT NULL,
    audit_period_start TIMESTAMPTZ NOT NULL,
    audit_period_end TIMESTAMPTZ NOT NULL,
    cin VARCHAR(50) NOT NULL DEFAULT '',
    company_type VARCHAR(50) NOT NULL DEFAULT 'UnlistedPublic',
    paid_up_share_capital NUMERIC(18,2) NOT NULL DEFAULT 0,
    turnover NUMERIC(18,2) NOT NULL DEFAULT 0,
    outstanding_borrowings NUMERIC(18,2) NOT NULL DEFAULT 0,
    is_applicable BOOLEAN NOT NULL DEFAULT TRUE,
    applicability_reason TEXT NOT NULL DEFAULT '',
    audit_status VARCHAR(50) NOT NULL DEFAULT 'Draft',
    auditor_name VARCHAR(150) NOT NULL DEFAULT '',
    auditor_firm_name VARCHAR(255) NOT NULL DEFAULT '',
    membership_type VARCHAR(20) NOT NULL DEFAULT 'FCS',
    membership_number VARCHAR(50) NOT NULL DEFAULT '',
    certificate_of_practice_number VARCHAR(50) NOT NULL DEFAULT '',
    peer_review_certificate_number VARCHAR(50) NOT NULL DEFAULT '',
    udin VARCHAR(50),
    certified_at TIMESTAMPTZ,
    certified_by VARCHAR(150),
    total_checklist_count INT NOT NULL DEFAULT 0,
    complied_count INT NOT NULL DEFAULT 0,
    not_complied_count INT NOT NULL DEFAULT 0,
    partially_complied_count INT NOT NULL DEFAULT 0,
    not_applicable_count INT NOT NULL DEFAULT 0,
    qualifications_count INT NOT NULL DEFAULT 0,
    overall_compliance_score NUMERIC(6,2) NOT NULL DEFAULT 100,
    total_filings_audited INT NOT NULL DEFAULT 0,
    filings_delayed_count INT NOT NULL DEFAULT 0,
    total_meetings_audited INT NOT NULL DEFAULT 0,
    meeting_deficiencies_count INT NOT NULL DEFAULT 0,
    form_mr3_report_text TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_sa_engagements_firm_client_fy ON secretarial_audit_engagements (firm_id, client_id, financial_year);

ALTER TABLE secretarial_audit_engagements ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS secretarial_audit_engagements_isolation ON secretarial_audit_engagements;
CREATE POLICY secretarial_audit_engagements_isolation ON secretarial_audit_engagements
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON secretarial_audit_engagements TO cadesk_app;


CREATE TABLE IF NOT EXISTS secretarial_checklist_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    engagement_id UUID NOT NULL REFERENCES secretarial_audit_engagements(id) ON DELETE CASCADE,
    statute_category VARCHAR(100) NOT NULL,
    clause_code VARCHAR(50) NOT NULL,
    subject_area VARCHAR(255) NOT NULL,
    statutory_requirement TEXT NOT NULL,
    compliance_status VARCHAR(50) NOT NULL DEFAULT 'Complied',
    auditor_remarks TEXT NOT NULL DEFAULT '',
    is_qualification BOOLEAN NOT NULL DEFAULT FALSE,
    deviation_risk VARCHAR(50) NOT NULL DEFAULT 'None',
    target_completion_date TIMESTAMPTZ,
    action_owner VARCHAR(150),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_sa_checklist_firm_engagement ON secretarial_checklist_items (firm_id, engagement_id);

ALTER TABLE secretarial_checklist_items ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS secretarial_checklist_items_isolation ON secretarial_checklist_items;
CREATE POLICY secretarial_checklist_items_isolation ON secretarial_checklist_items
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON secretarial_checklist_items TO cadesk_app;


CREATE TABLE IF NOT EXISTS mca_form_filing_audits (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    engagement_id UUID NOT NULL REFERENCES secretarial_audit_engagements(id) ON DELETE CASCADE,
    form_name VARCHAR(50) NOT NULL,
    description VARCHAR(255) NOT NULL,
    event_date TIMESTAMPTZ NOT NULL,
    statutory_days_allowed INT NOT NULL,
    statutory_due_date TIMESTAMPTZ NOT NULL,
    actual_filing_date TIMESTAMPTZ,
    srn VARCHAR(50) NOT NULL DEFAULT '',
    filing_status VARCHAR(50) NOT NULL DEFAULT 'FiledOnTime',
    delay_days INT NOT NULL DEFAULT 0,
    additional_fee_paid NUMERIC(12,2) NOT NULL DEFAULT 0,
    auditor_remarks TEXT NOT NULL DEFAULT '',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_mca_filings_firm_engagement ON mca_form_filing_audits (firm_id, engagement_id);

ALTER TABLE mca_form_filing_audits ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS mca_form_filing_audits_isolation ON mca_form_filing_audits;
CREATE POLICY mca_form_filing_audits_isolation ON mca_form_filing_audits
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON mca_form_filing_audits TO cadesk_app;


CREATE TABLE IF NOT EXISTS board_meeting_audit_records (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    engagement_id UUID NOT NULL REFERENCES secretarial_audit_engagements(id) ON DELETE CASCADE,
    meeting_type VARCHAR(100) NOT NULL,
    meeting_date TIMESTAMPTZ NOT NULL,
    previous_meeting_date TIMESTAMPTZ,
    gap_from_previous_meeting_days INT,
    is_gap_compliant BOOLEAN NOT NULL DEFAULT TRUE,
    notice_dispatch_date TIMESTAMPTZ,
    notice_days_given INT,
    is_notice_compliant BOOLEAN NOT NULL DEFAULT TRUE,
    total_members_count INT NOT NULL DEFAULT 0,
    attended_members_count INT NOT NULL DEFAULT 0,
    quorum_required_count INT NOT NULL DEFAULT 0,
    is_quorum_compliant BOOLEAN NOT NULL DEFAULT TRUE,
    independent_directors_attended INT NOT NULL DEFAULT 0,
    secretarial_standard_complied BOOLEAN NOT NULL DEFAULT TRUE,
    auditor_remarks TEXT NOT NULL DEFAULT '',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_meeting_audits_firm_engagement ON board_meeting_audit_records (firm_id, engagement_id);

ALTER TABLE board_meeting_audit_records ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS board_meeting_audit_records_isolation ON board_meeting_audit_records;
CREATE POLICY board_meeting_audit_records_isolation ON board_meeting_audit_records
    FOR ALL
    USING (firm_id = current_setting('app.firm_id', true)::uuid)
    WITH CHECK (firm_id = current_setting('app.firm_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON board_meeting_audit_records TO cadesk_app;
