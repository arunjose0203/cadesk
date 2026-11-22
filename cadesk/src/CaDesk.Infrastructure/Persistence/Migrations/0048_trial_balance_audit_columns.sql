-- 0048_trial_balance_audit_columns.sql
-- Add TenantEntity audit columns (created_by, updated_by) to trial balance tables

ALTER TABLE trial_balance_imports ADD COLUMN IF NOT EXISTS created_by UUID;
ALTER TABLE trial_balance_imports ADD COLUMN IF NOT EXISTS updated_by UUID;

ALTER TABLE trial_balance_ledgers ADD COLUMN IF NOT EXISTS created_by UUID;
ALTER TABLE trial_balance_ledgers ADD COLUMN IF NOT EXISTS updated_by UUID;

ALTER TABLE client_ledger_mappings ADD COLUMN IF NOT EXISTS created_by UUID;
ALTER TABLE client_ledger_mappings ADD COLUMN IF NOT EXISTS updated_by UUID;
