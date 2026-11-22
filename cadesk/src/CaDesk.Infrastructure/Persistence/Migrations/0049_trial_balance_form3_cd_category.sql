-- 0049_trial_balance_form3_cd_category.sql: Rename form3cd_category to form3_cd_category for EF Core convention

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'trial_balance_ledgers' AND column_name = 'form3cd_category'
    ) THEN
        ALTER TABLE trial_balance_ledgers RENAME COLUMN form3cd_category TO form3_cd_category;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'client_ledger_mappings' AND column_name = 'form3cd_category'
    ) THEN
        ALTER TABLE client_ledger_mappings RENAME COLUMN form3cd_category TO form3_cd_category;
    END IF;
END $$;
