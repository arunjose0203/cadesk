-- 0070_backup_no_force_rls: Migrations 0028..0036 enabled FORCE ROW LEVEL SECURITY, which also binds
-- table owner cadesk_owner and causes pg_dump (without --enable-row-security, or dumping 0 rows with it)
-- to fail during automated encrypted backups.
-- Clearing FORCE keeps ENABLE ROW LEVEL SECURITY intact on every table so cadesk_app (non-owner runtime
-- role used by all HTTP requests) continues to have strict PostgreSQL RLS isolation, while cadesk_owner
-- can back up and migrate all firm data reliably.

alter table if exists audit_committee_engagements no force row level security;
alter table if exists branch_audit_engagements no force row level security;
alter table if exists branch_audit_offices no force row level security;
alter table if exists caro_assessments no force row level security;
alter table if exists caro_clause_responses no force row level security;
alter table if exists foreign_remittance_assessments no force row level security;
alter table if exists foreign_remittance_checklists no force row level security;
alter table if exists form3cd_assessments no force row level security;
alter table if exists form3cd_clause_responses no force row level security;
alter table if exists internal_audit_engagements no force row level security;
alter table if exists internal_audit_findings no force row level security;
alter table if exists internal_control_deficiencies no force row level security;
alter table if exists presumptive_44ae_vehicles no force row level security;
alter table if exists presumptive_tax_computations no force row level security;
alter table if exists rcm_control_items no force row level security;
alter table if exists related_party_entities no force row level security;
alter table if exists related_party_omnibus_approvals no force row level security;
alter table if exists related_party_transactions no force row level security;
alter table if exists schedule3_ageing_items no force row level security;
alter table if exists schedule3_assessments no force row level security;
alter table if exists schedule3_ratio_items no force row level security;
alter table if exists significant_audit_matters no force row level security;
alter table if exists uncorrected_misstatements no force row level security;
