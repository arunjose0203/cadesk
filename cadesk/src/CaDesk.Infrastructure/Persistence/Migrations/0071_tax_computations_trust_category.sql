-- 0071_tax_computations_trust_category: Allow 'trust' taxpayer_category for ITR-7 Trust / Sec 8 / Institution computations.

do $$
declare
  v_conname text;
begin
  select c.conname into v_conname
  from pg_constraint c
  join pg_class t on t.oid = c.conrelid
  where t.relname = 'tax_computations'
    and c.contype = 'c'
    and pg_get_constraintdef(c.oid) like '%taxpayer_category%';

  if v_conname is not null then
    execute format('alter table tax_computations drop constraint if exists %I', v_conname);
  end if;

  alter table tax_computations
    add constraint tax_computations_taxpayer_category_check
    check (taxpayer_category in ('individual','senior_citizen','super_senior','huf','firm','company','trust'));
end $$;
