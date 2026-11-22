-- 0045_branch_portal_token_policy: branch_audit_offices uses FORCE ROW LEVEL SECURITY, so even the security
-- definer function from 0043 sees no rows before the firm is known. Rather than weaken that, one extra
-- read-only policy shows a row only to a session that presents that row's exact token hash, which is
-- the same as proving it holds the token. The lookup function sets the hash for its own transaction only.

drop policy if exists branch_portal_token_lookup on branch_audit_offices;
create policy branch_portal_token_lookup on branch_audit_offices
  for select
  using (access_token_hash is not null
         and access_token_hash = nullif(current_setting('app.branch_token_hash', true), ''));

create or replace function branch_portal_find_office(p_hash text)
returns table (id uuid, firm_id uuid, token_expires_at timestamptz)
language plpgsql volatile security definer set search_path = public, pg_temp as $$
begin
  perform set_config('app.branch_token_hash', coalesce(p_hash, ''), true);
  return query
    select o.id, o.firm_id, o.token_expires_at from branch_audit_offices o where o.access_token_hash = p_hash;
  perform set_config('app.branch_token_hash', '', true);
end
$$;
revoke all on function branch_portal_find_office(text) from public;
grant execute on function branch_portal_find_office(text) to cadesk_app;
