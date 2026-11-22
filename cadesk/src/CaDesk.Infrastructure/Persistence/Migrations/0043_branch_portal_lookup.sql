-- 0043_branch_portal_lookup: token lookup for the Sprint 33 external branch auditor portal.

-- An external branch auditor opens the portal before we know the firm. Like portal_find_upload_link, this
--    finds exactly one branch office by its token hash and returns only what is needed to set the tenant;
--    everything after that runs under the normal firm filter and row-level security.
create or replace function branch_portal_find_office(p_hash text)
returns table (id uuid, firm_id uuid, token_expires_at timestamptz)
language sql stable security definer set search_path = public, pg_temp as $$
  select id, firm_id, token_expires_at from branch_audit_offices where access_token_hash = p_hash
$$;
revoke all on function branch_portal_find_office(text) from public;
grant execute on function branch_portal_find_office(text) to cadesk_app;
