-- 0047_trial_balance_data_in.sql
-- Phase 1 Data In: Trial Balance & Ledgers Import (Tally XML / CSV / Excel)
-- Mapped once per client, cascading into Statutory Audit, Form 3CD, Schedule III, CARO, and Fixed Assets desks.

CREATE TABLE IF NOT EXISTS trial_balance_imports (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    client_id UUID NOT NULL REFERENCES clients(id) ON DELETE CASCADE,
    financial_year VARCHAR(20) NOT NULL, -- e.g. "2024-25"
    source_type VARCHAR(50) NOT NULL DEFAULT 'TallyXml', -- TallyXml, TallyCsv, Excel
    file_name VARCHAR(255) NOT NULL DEFAULT 'tally_import.xml',
    total_debit NUMERIC(18,2) NOT NULL DEFAULT 0,
    total_credit NUMERIC(18,2) NOT NULL DEFAULT 0,
    is_balanced BOOLEAN NOT NULL DEFAULT FALSE,
    total_ledgers_count INT NOT NULL DEFAULT 0,
    mapped_ledgers_count INT NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Draft', -- Draft, Mapped, Synced
    synced_at TIMESTAMPTZ,
    synced_desks TEXT, -- Comma-separated: "Audit,Form3Cd,Schedule3,Caro,FixedAssets"
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID,
    updated_at TIMESTAMPTZ,
    updated_by UUID
);

CREATE INDEX IF NOT EXISTS ix_tb_imports_firm_client_fy ON trial_balance_imports (firm_id, client_id, financial_year);

ALTER TABLE trial_balance_imports ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS trial_balance_imports_isolation ON trial_balance_imports;
CREATE POLICY trial_balance_imports_isolation ON trial_balance_imports
    FOR ALL
    USING (firm_id = app_current_firm())
    WITH CHECK (firm_id = app_current_firm());

GRANT SELECT, INSERT, UPDATE, DELETE ON trial_balance_imports TO cadesk_app;


CREATE TABLE IF NOT EXISTS trial_balance_ledgers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    import_id UUID NOT NULL REFERENCES trial_balance_imports(id) ON DELETE CASCADE,
    ledger_name VARCHAR(255) NOT NULL,
    tally_group VARCHAR(255) NOT NULL DEFAULT '',
    opening_balance NUMERIC(18,2) NOT NULL DEFAULT 0,
    debit_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    credit_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    closing_balance NUMERIC(18,2) NOT NULL DEFAULT 0,
    schedule3_group VARCHAR(100) NOT NULL DEFAULT 'OtherExpenses',
    form3_cd_category VARCHAR(100),
    caro_category VARCHAR(100),
    fixed_asset_block VARCHAR(100),
    is_mapped BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID,
    updated_at TIMESTAMPTZ,
    updated_by UUID
);

CREATE INDEX IF NOT EXISTS ix_tb_ledgers_firm_import ON trial_balance_ledgers (firm_id, import_id);
CREATE INDEX IF NOT EXISTS ix_tb_ledgers_name ON trial_balance_ledgers (firm_id, ledger_name);

ALTER TABLE trial_balance_ledgers ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS trial_balance_ledgers_isolation ON trial_balance_ledgers;
CREATE POLICY trial_balance_ledgers_isolation ON trial_balance_ledgers
    FOR ALL
    USING (firm_id = app_current_firm())
    WITH CHECK (firm_id = app_current_firm());

GRANT SELECT, INSERT, UPDATE, DELETE ON trial_balance_ledgers TO cadesk_app;


-- Client-level ledger mapping rules (Mapped once per client, reused across financial years)
CREATE TABLE IF NOT EXISTS client_ledger_mappings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firm_id UUID NOT NULL REFERENCES firms(id) ON DELETE CASCADE,
    client_id UUID NOT NULL REFERENCES clients(id) ON DELETE CASCADE,
    ledger_pattern VARCHAR(255) NOT NULL, -- Exact ledger name or prefix
    tally_group VARCHAR(255) NOT NULL DEFAULT '',
    schedule3_group VARCHAR(100) NOT NULL DEFAULT 'OtherExpenses',
    form3_cd_category VARCHAR(100),
    caro_category VARCHAR(100),
    fixed_asset_block VARCHAR(100),
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID,
    updated_at TIMESTAMPTZ,
    updated_by UUID,
    CONSTRAINT ux_client_ledger_mapping UNIQUE (firm_id, client_id, ledger_pattern)
);

CREATE INDEX IF NOT EXISTS ix_client_ledger_mappings_firm_client ON client_ledger_mappings (firm_id, client_id);

ALTER TABLE client_ledger_mappings ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS client_ledger_mappings_isolation ON client_ledger_mappings;
CREATE POLICY client_ledger_mappings_isolation ON client_ledger_mappings
    FOR ALL
    USING (firm_id = app_current_firm())
    WITH CHECK (firm_id = app_current_firm());

GRANT SELECT, INSERT, UPDATE, DELETE ON client_ledger_mappings TO cadesk_app;


-- Register Permissions for Trial Balance / Data In
INSERT INTO permissions (code, area, description) VALUES
  ('trialbalance.read',   'compliance', 'View imported trial balances, Tally ledgers, and desk mapping rules'),
  ('trialbalance.manage', 'compliance', 'Import Tally trial balances, map chart of accounts, and manage rules'),
  ('trialbalance.sync',   'compliance', 'Synchronize imported trial balances into Audit, Form 3CD, Schedule III, CARO, and Fixed Assets')
ON CONFLICT (code) DO NOTHING;

-- Grant permissions to Partner role for existing firms
INSERT INTO role_permissions (role_id, permission_code, firm_id)
SELECT r.id, p.code, r.firm_id
FROM roles r
CROSS JOIN permissions p
WHERE r.name IN ('Partner', 'Manager') AND r.is_system AND p.code IN ('trialbalance.read', 'trialbalance.manage', 'trialbalance.sync')
ON CONFLICT DO NOTHING;
