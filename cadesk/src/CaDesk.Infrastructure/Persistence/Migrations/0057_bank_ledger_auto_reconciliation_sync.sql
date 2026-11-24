-- 0057_bank_ledger_auto_reconciliation_sync.sql
-- Bank & Ledger Auto-Reconciliation Engine with Tally Prime / Busy Live Sync Agent

-- 1. ERP Sync Agent Configurations
create table if not exists erp_sync_agent_configs (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  client_id                                 uuid not null references clients(id) on delete cascade,
  agent_name                                text not null default 'Office ERP Sync Agent',
  agent_token                               text not null,
  erp_type                                  text not null default 'TallyPrime',
  host_url                                  text not null default 'http://localhost:9000',
  company_name                              text not null default '',
  sync_status                               text not null default 'Offline',
  last_heartbeat_at                         timestamptz,
  last_sync_at                              timestamptz,
  sync_frequency_minutes                    integer not null default 15,
  sync_daybook                              boolean not null default true,
  sync_masters                              boolean not null default true,
  sync_bank_feeds                           boolean not null default true,
  total_vouchers_synced                     integer not null default 0,
  total_masters_synced                      integer not null default 0,
  is_active                                 boolean not null default true,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_erp_sync_agent_firm_client on erp_sync_agent_configs (firm_id, client_id);
create index if not exists ix_erp_sync_agent_token on erp_sync_agent_configs (firm_id, agent_token);
alter table erp_sync_agent_configs enable row level security;
drop policy if exists tenant_isolation on erp_sync_agent_configs;
create policy tenant_isolation on erp_sync_agent_configs
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on erp_sync_agent_configs to cadesk_app;

-- 2. Synced ERP Ledger Vouchers
create table if not exists erp_ledger_vouchers (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  client_id                                 uuid not null references clients(id) on delete cascade,
  agent_config_id                           uuid not null references erp_sync_agent_configs(id) on delete cascade,
  erp_type                                  text not null default 'TallyPrime',
  voucher_number                            text not null,
  voucher_guid                              text not null default '',
  voucher_type                              text not null default 'Receipt',
  voucher_date                              date not null,
  effective_date                            date not null,
  amount                                    numeric(15,2) not null default 0,
  debit_ledger                              text not null default '',
  credit_ledger                             text not null default '',
  narration                                 text not null default '',
  cheque_number                             text,
  instrument_date                           date,
  bank_party_name                           text,
  bank_clearance_date                       date,
  reconciliation_status                     text not null default 'Unreconciled',
  reconciliation_match_id                   uuid,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_erp_ledger_vouchers_firm_client on erp_ledger_vouchers (firm_id, client_id);
create index if not exists ix_erp_ledger_vouchers_recon on erp_ledger_vouchers (firm_id, reconciliation_status);
create index if not exists ix_erp_ledger_vouchers_date on erp_ledger_vouchers (firm_id, voucher_date);
alter table erp_ledger_vouchers enable row level security;
drop policy if exists tenant_isolation on erp_ledger_vouchers;
create policy tenant_isolation on erp_ledger_vouchers
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on erp_ledger_vouchers to cadesk_app;

-- 3. 2-Way Outgoing Vouchers Queue
create table if not exists erp_outgoing_vouchers (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  client_id                                 uuid not null references clients(id) on delete cascade,
  agent_config_id                           uuid not null references erp_sync_agent_configs(id) on delete cascade,
  voucher_type                              text not null default 'Journal',
  voucher_date                              date not null,
  reference_number                          text not null default '',
  debit_ledger                              text not null default '',
  credit_ledger                             text not null default '',
  amount                                    numeric(15,2) not null default 0,
  narration                                 text not null default '',
  audit_reason                              text not null default 'StatutoryAuditAdjustmentSA700',
  posting_status                            text not null default 'PendingApproval',
  generated_xml_payload                     text,
  erp_voucher_number                        text,
  erp_response_xml                          text,
  error_message                             text,
  authorized_by_user_id                     uuid references users(id) on delete set null,
  authorized_by_name                        text,
  authorized_at                             timestamptz,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_erp_outgoing_vouchers_firm_client on erp_outgoing_vouchers (firm_id, client_id);
create index if not exists ix_erp_outgoing_vouchers_status on erp_outgoing_vouchers (firm_id, posting_status);
alter table erp_outgoing_vouchers enable row level security;
drop policy if exists tenant_isolation on erp_outgoing_vouchers;
create policy tenant_isolation on erp_outgoing_vouchers
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on erp_outgoing_vouchers to cadesk_app;

-- 4. Bank Ledger Auto-Reconciliation Matches
create table if not exists bank_ledger_reconciliation_matches (
  id                                        uuid primary key,
  firm_id                                   uuid not null references firms(id),
  client_id                                 uuid not null references clients(id) on delete cascade,
  bank_account_id                           uuid not null,
  bank_transaction_id                       uuid not null,
  erp_ledger_voucher_id                     uuid references erp_ledger_vouchers(id) on delete set null,
  match_rule                                text not null default 'ExactAmountAndDate',
  confidence_score                          numeric(5,2) not null default 95.00,
  bank_amount                               numeric(15,2) not null default 0,
  ledger_amount                             numeric(15,2) not null default 0,
  variance_amount                           numeric(15,2) not null default 0,
  variance_explanation                      text,
  matched_voucher_numbers                   text not null default '',
  status                                    text not null default 'Suggested',
  reconciled_by_user_id                     uuid references users(id) on delete set null,
  reconciled_by_name                        text,
  reconciled_at                             timestamptz,
  created_at                                timestamptz not null default now(),
  updated_at                                timestamptz
);

create index if not exists ix_bank_ledger_recon_firm_client on bank_ledger_reconciliation_matches (firm_id, client_id);
create index if not exists ix_bank_ledger_recon_tx on bank_ledger_reconciliation_matches (firm_id, bank_transaction_id);
create index if not exists ix_bank_ledger_recon_vch on bank_ledger_reconciliation_matches (firm_id, erp_ledger_voucher_id);
alter table bank_ledger_reconciliation_matches enable row level security;
drop policy if exists tenant_isolation on bank_ledger_reconciliation_matches;
create policy tenant_isolation on bank_ledger_reconciliation_matches
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());
grant select, insert, update, delete on bank_ledger_reconciliation_matches to cadesk_app;
