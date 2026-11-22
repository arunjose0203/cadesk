-- 0042_schema_drift_fixes: bring the database in line with the EF model (found by SchemaDriftTests).

-- 1. Audit stamping columns the entities write but the Sprint 35-37 migrations never created.
--    No foreign key to users: like the other created_by / updated_by columns, they record who acted
--    (which may be a portal user or a service identity), not a relationship to enforce.
do $$
declare t text;
begin
  foreach t in array array[
    'foreign_remittance_assessments', 'foreign_remittance_checklists',
    'internal_audit_engagements', 'internal_audit_findings', 'rcm_control_items',
    'related_party_entities', 'related_party_omnibus_approvals', 'related_party_transactions']
  loop
    execute format('alter table %I add column if not exists created_by uuid', t);
    execute format('alter table %I add column if not exists updated_by uuid', t);
  end loop;
end $$;

-- 2. Company and business identifiers for Indian clients. Values are stored normalised
--    (upper case, no spaces or hyphens), so patterns have no separators.
insert into tax_id_types (country_code, code, label, pattern, checksum, is_unique, example, sort_order) values
  ('IN', 'CIN',   'Corporate Identification Number (CIN)', '^[LU][0-9]{5}[A-Z]{2}[0-9]{4}[A-Z]{3}[0-9]{6}$', 'none', true,  'U72200MH2020PTC123456', 40),
  ('IN', 'LLPIN', 'LLP Identification Number (LLPIN)',     '^[A-Z]{3}[0-9]{4}$',                             'none', true,  'AAB-1234',              50),
  ('IN', 'UDYAM', 'Udyam Registration Number (MSME)',      '^UDYAM[A-Z]{2}[0-9]{9}$',                        'none', true,  'UDYAM-MH-01-0000001',   60),
  ('IN', 'IEC',   'Importer-Exporter Code (IEC)',          '^[A-Z0-9]{10}$',                                 'none', false, 'AABCT1234F',            70)
on conflict do nothing;
