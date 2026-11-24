-- Sprint 53: Partner Mobile Companion Desk & PWA
-- Tables, RLS policies, and permissions for Managing Partner on-the-go action center

CREATE TABLE IF NOT EXISTS partner_mobile_preferences (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    quick_pin_hash TEXT,
    quick_pin_salt TEXT,
    biometric_credential_id TEXT,
    biometric_enabled BOOLEAN NOT NULL DEFAULT FALSE,
    auto_lock_minutes INT NOT NULL DEFAULT 5,
    push_notification_endpoint TEXT,
    last_active_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    CONSTRAINT uq_partner_mobile_preferences_user UNIQUE (firm_id, user_id)
);

ALTER TABLE partner_mobile_preferences ENABLE ROW LEVEL SECURITY;

CREATE POLICY partner_mobile_preferences_firm_isolation ON partner_mobile_preferences
    FOR ALL
    USING (firm_id = current_setting('app.current_firm', true)::uuid);

CREATE INDEX IF NOT EXISTS idx_partner_mobile_preferences_user
    ON partner_mobile_preferences (firm_id, user_id);

-- Register Partner Companion permissions
INSERT INTO permissions (code, area, description)
VALUES 
    ('partner.companion', 'partner', 'Access mobile PWA companion desk, action center pulse, 1-tap audit sign-offs, and quick approvals')
ON CONFLICT (code) DO NOTHING;

-- Grant to admin / partner roles
INSERT INTO role_permissions (role_id, permission_code, firm_id)
SELECT r.id, 'partner.companion', r.firm_id
FROM roles r
WHERE r.name IN ('admin', 'partner', 'lead_partner', 'managing_partner', 'Partner', 'Admin')
ON CONFLICT DO NOTHING;
