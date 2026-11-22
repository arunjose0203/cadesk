-- 0044_practice_targets_audit_columns: PracticeTarget is a TenantEntity (created_by / updated_by) but
-- 0020 never created those columns on databases that already ran it. Applied migrations are never
-- edited, so they are added here; "if not exists" makes this safe where they already exist.
alter table practice_targets add column if not exists created_by uuid;
alter table practice_targets add column if not exists updated_by uuid;
