-- 0068_fix_grants_and_rls.sql: grant cadesk_app access on tables from Sprints 53, 54, 71 and 72,
-- and align Sprint 53/54 RLS policies with TenantConnectionInterceptor's app_current_firm() (app.firm_id).

grant select, insert, update, delete on partner_mobile_preferences to cadesk_app;
grant select, insert, update, delete on connected_bank_credentials to cadesk_app;
grant select, insert, update, delete on payout_batches to cadesk_app;
grant select, insert, update, delete on payout_items to cadesk_app;
grant select, insert, update, delete on legal_acceptances to cadesk_app;
grant select, insert, update, delete on problem_reports to cadesk_app;

do $$
begin
    if to_regclass('seq_subscription_invoices') is not null then
        grant usage, select on sequence seq_subscription_invoices to cadesk_app;
    end if;
end $$;

drop policy if exists partner_mobile_preferences_firm_isolation on partner_mobile_preferences;
create policy partner_mobile_preferences_firm_isolation on partner_mobile_preferences
    for all using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());

drop policy if exists connected_bank_credentials_firm_isolation on connected_bank_credentials;
create policy connected_bank_credentials_firm_isolation on connected_bank_credentials
    for all using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());

drop policy if exists payout_batches_firm_isolation on payout_batches;
create policy payout_batches_firm_isolation on payout_batches
    for all using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());

drop policy if exists payout_items_firm_isolation on payout_items;
create policy payout_items_firm_isolation on payout_items
    for all using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());
