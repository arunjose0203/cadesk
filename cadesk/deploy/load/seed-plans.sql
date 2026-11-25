-- Run between step 1 and step 2, as the database owner:
--   psql "$OWNER_CS" -f deploy/load/seed-plans.sql
-- Load firms get every desk and no client limit, and accept the legal documents so nothing blocks them.
update firms set plan_code = 'INTERNAL'
 where id in (select firm_id from users where email::text like 'load-firm-%@load.cadesk.test');

insert into legal_acceptances (firm_id, user_id, document, version)
select u.firm_id, u.id, d.document, '2026-11-24'
  from users u cross join (values ('terms'), ('privacy'), ('dpa')) d(document)
 where u.email::text like 'load-firm-%@load.cadesk.test'
on conflict do nothing;
