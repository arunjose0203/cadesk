-- 0072_ledger_sync_agent_token_lookup: Allows anonymous local Windows Tally/Busy bridge agents to
-- authenticate solely by presenting an active AgentToken, resolving FirmId + ClientId safely under RLS.

drop policy if exists erp_sync_agent_token_lookup on erp_sync_agent_configs;
create policy erp_sync_agent_token_lookup on erp_sync_agent_configs
  for select
  using (agent_token is not null
         and is_active = true
         and agent_token = nullif(current_setting('app.ledger_sync_token', true), ''));

create or replace function ledger_sync_find_agent_by_token(p_token text)
returns table (id uuid, firm_id uuid, client_id uuid)
language plpgsql volatile security definer set search_path = public, pg_temp as $$
begin
  perform set_config('app.ledger_sync_token', coalesce(p_token, ''), true);
  return query
    select a.id, a.firm_id, a.client_id
    from erp_sync_agent_configs a
    where a.agent_token = p_token
      and a.is_active = true
    limit 1;
  perform set_config('app.ledger_sync_token', '', true);
end
$$;

revoke all on function ledger_sync_find_agent_by_token(text) from public;
grant execute on function ledger_sync_find_agent_by_token(text) to cadesk_app;
