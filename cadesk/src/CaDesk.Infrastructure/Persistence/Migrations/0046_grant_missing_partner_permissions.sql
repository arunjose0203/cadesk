-- 0046_grant_missing_partner_permissions: 0041 registered 24 permissions (GSTR-9, 15CA/15CB, related party,
-- internal audit, anti-profiteering, secretarial audit, transfer pricing, cost audit) but granted them to no role.
-- New firms get every permission at sign-up; firms created earlier had Partners locked out of those desks.
-- The built-in Partner role is the full-access role, so it gets any permission it is missing.
insert into role_permissions (role_id, permission_code, firm_id)
select r.id, p.code, r.firm_id
from roles r
cross join permissions p
where r.name = 'Partner' and r.is_system
on conflict do nothing;
