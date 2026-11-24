-- Sprint 54: Direct Connected Corporate Banking API & 1-Click Vendor Payout Desk
-- Tables, RLS policies, indexes, and permissions for Open Banking feeds & Maker-Checker payouts

CREATE TABLE IF NOT EXISTS connected_bank_credentials (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    bank_account_id UUID NOT NULL REFERENCES bank_accounts(id) ON DELETE CASCADE,
    client_id UUID NOT NULL REFERENCES clients(id) ON DELETE CASCADE,
    provider TEXT NOT NULL,
    corporate_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    encrypted_api_key TEXT NOT NULL,
    encrypted_api_secret TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'Connected',
    consent_expires_at TIMESTAMPTZ,
    last_sync_at TIMESTAMPTZ,
    last_sync_balance NUMERIC(18, 2),
    sync_frequency_minutes INT NOT NULL DEFAULT 60,
    webhook_secret TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    CONSTRAINT uq_connected_bank_account UNIQUE (firm_id, bank_account_id)
);

ALTER TABLE connected_bank_credentials ENABLE ROW LEVEL SECURITY;

CREATE POLICY connected_bank_credentials_firm_isolation ON connected_bank_credentials
    FOR ALL
    USING (firm_id = current_setting('app.current_firm', true)::uuid);

CREATE INDEX IF NOT EXISTS idx_connected_bank_account_id
    ON connected_bank_credentials (firm_id, bank_account_id);

CREATE TABLE IF NOT EXISTS payout_batches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    batch_number TEXT NOT NULL,
    bank_account_id UUID NOT NULL REFERENCES bank_accounts(id) ON DELETE RESTRICT,
    client_id UUID NOT NULL REFERENCES clients(id) ON DELETE CASCADE,
    title TEXT NOT NULL,
    total_amount NUMERIC(18, 2) NOT NULL DEFAULT 0,
    total_count INT NOT NULL DEFAULT 0,
    status TEXT NOT NULL DEFAULT 'Draft',
    created_by_user_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    maker_remarks TEXT,
    authorized_by_user_id UUID REFERENCES users(id) ON DELETE SET NULL,
    authorized_at TIMESTAMPTZ,
    auth_method TEXT,
    auth_reference TEXT,
    rejection_reason TEXT,
    scheduled_execution_date DATE,
    executed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    CONSTRAINT uq_payout_batch_number UNIQUE (firm_id, batch_number)
);

ALTER TABLE payout_batches ENABLE ROW LEVEL SECURITY;

CREATE POLICY payout_batches_firm_isolation ON payout_batches
    FOR ALL
    USING (firm_id = current_setting('app.current_firm', true)::uuid);

CREATE INDEX IF NOT EXISTS idx_payout_batches_client_status
    ON payout_batches (firm_id, client_id, status);

CREATE TABLE IF NOT EXISTS payout_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    batch_id UUID NOT NULL REFERENCES payout_batches(id) ON DELETE CASCADE,
    payout_type TEXT NOT NULL DEFAULT 'VendorInvoice',
    beneficiary_name TEXT NOT NULL,
    beneficiary_account_number TEXT NOT NULL,
    beneficiary_ifsc TEXT NOT NULL,
    beneficiary_pan TEXT,
    amount NUMERIC(18, 2) NOT NULL,
    payment_mode TEXT NOT NULL DEFAULT 'NEFT',
    narration TEXT NOT NULL,
    invoice_or_challan_ref TEXT,
    status TEXT NOT NULL DEFAULT 'Pending',
    utr_number TEXT,
    failure_reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

ALTER TABLE payout_items ENABLE ROW LEVEL SECURITY;

CREATE POLICY payout_items_firm_isolation ON payout_items
    FOR ALL
    USING (firm_id = current_setting('app.current_firm', true)::uuid);

CREATE INDEX IF NOT EXISTS idx_payout_items_batch_id
    ON payout_items (firm_id, batch_id);

-- Register Banking Payout Permissions
INSERT INTO permissions (code, area, description)
VALUES 
    ('banking.connected.manage', 'banking', 'Connect ICICI, HDFC, Axis, and Open API corporate bank accounts and trigger live balance syncs'),
    ('banking.payout.maker', 'banking', 'Create vendor invoice, salary, and statutory tax challan payout batches'),
    ('banking.payout.checker', 'banking', 'Authorize bank disbursements with OTP, DSC or Biometrics and release payments')
ON CONFLICT (code) DO NOTHING;

-- Grant permissions to default roles
INSERT INTO role_permissions (role_id, permission_code, firm_id)
SELECT r.id, p.code, r.firm_id
FROM roles r
CROSS JOIN permissions p
WHERE p.code IN ('banking.connected.manage', 'banking.payout.maker', 'banking.payout.checker')
  AND r.name IN ('admin', 'partner', 'lead_partner', 'managing_partner', 'Partner', 'Admin')
ON CONFLICT DO NOTHING;

INSERT INTO role_permissions (role_id, permission_code, firm_id)
SELECT r.id, 'banking.payout.maker', r.firm_id
FROM roles r
WHERE r.name IN ('manager', 'staff', 'article_trainee', 'Manager', 'Staff')
ON CONFLICT DO NOTHING;
