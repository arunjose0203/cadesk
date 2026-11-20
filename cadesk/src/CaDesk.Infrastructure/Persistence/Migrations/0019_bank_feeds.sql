-- 0019_bank_feeds: Cash & Bank Feed Integration, Multi-Bank Statement Parser & BRS Auto-Matcher.
-- Supports Bank Account master (Current, Savings, OD, CC), Bank Statement imports (CSV/OFX),
-- Bank Transactions with categorization & ledger matching, and BRS reconciliation calculation engine.

-- ---------------------------------------------------------------- bank_accounts

create table if not exists bank_accounts (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  client_id                   uuid not null,
  account_name                text not null check (length(account_name) between 2 and 150),
  bank_name                   text not null check (length(bank_name) between 2 and 100),
  branch_name                 text,
  account_number              text not null check (length(account_number) between 5 and 50),
  ifsc_code                   text not null check (ifsc_code ~ '^[A-Z]{4}0[A-Z0-9]{6}$'),
  account_type                text not null default 'Current' check (account_type in ('Current','Savings','Overdraft','CashCredit')),
  opening_balance             numeric(14,2) not null default 0,
  current_balance             numeric(14,2) not null default 0,
  currency                    text not null default 'INR' check (length(currency) = 3),
  is_active                   boolean not null default true,
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid,
  constraint ux_bank_account_id_firm unique (id, firm_id),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade
);

create index if not exists ix_bank_accounts_firm_client on bank_accounts (firm_id, client_id);
create index if not exists ix_bank_accounts_firm_acc_num on bank_accounts (firm_id, account_number);

-- ---------------------------------------------------------------- bank_statements

create table if not exists bank_statements (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  bank_account_id             uuid not null,
  statement_date              date not null,
  start_date                  date not null,
  end_date                    date not null,
  file_name                   text not null check (length(file_name) between 1 and 255),
  format                      text not null default 'CSV' check (format in ('CSV','OFX','PDF_PARSED','MANUAL')),
  opening_balance             numeric(14,2) not null default 0,
  closing_balance             numeric(14,2) not null default 0,
  total_debits                numeric(14,2) not null default 0,
  total_credits               numeric(14,2) not null default 0,
  total_transactions          int not null default 0,
  status                      text not null default 'Parsed' check (status in ('Parsed','PartiallyReconciled','Reconciled','Error')),
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  constraint ux_bank_statement_id_firm unique (id, firm_id),
  foreign key (bank_account_id, firm_id) references bank_accounts(id, firm_id) on delete cascade
);

create index if not exists ix_bank_statements_firm_account on bank_statements (firm_id, bank_account_id);

-- ---------------------------------------------------------------- bank_transactions

create table if not exists bank_transactions (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  statement_id                uuid not null,
  bank_account_id             uuid not null,
  transaction_date            date not null,
  value_date                  date,
  narration                   text not null,
  reference_number            text,
  transaction_type            text not null check (transaction_type in ('Debit','Credit')),
  amount                      numeric(14,2) not null check (amount > 0),
  running_balance             numeric(14,2),
  category                    text,
  match_status                text not null default 'Unmatched' check (match_status in ('Unmatched','Matched','AutoCategorized','Ignored')),
  matched_entity_type         text check (matched_entity_type is null or matched_entity_type in ('Cheque','Invoice','Bill','Expense','Journal')),
  matched_entity_id           uuid,
  notes                       text,
  created_at                  timestamptz not null default now(),
  constraint ux_bank_transaction_id_firm unique (id, firm_id),
  foreign key (statement_id, firm_id) references bank_statements(id, firm_id) on delete cascade,
  foreign key (bank_account_id, firm_id) references bank_accounts(id, firm_id) on delete cascade
);

create index if not exists ix_bank_transactions_firm_account on bank_transactions (firm_id, bank_account_id);
create index if not exists ix_bank_transactions_firm_statement on bank_transactions (firm_id, statement_id);
create index if not exists ix_bank_transactions_firm_date on bank_transactions (firm_id, transaction_date);
create index if not exists ix_bank_transactions_firm_status on bank_transactions (firm_id, match_status);

-- ---------------------------------------------------------------- bank_reconciliation_rules

create table if not exists bank_reconciliation_rules (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  rule_name                   text not null check (length(rule_name) between 2 and 150),
  match_keyword               text not null check (length(match_keyword) between 1 and 100),
  transaction_type            text not null default 'Any' check (transaction_type in ('Any','Debit','Credit')),
  assign_category             text not null check (length(assign_category) between 2 and 100),
  priority                    int not null default 0,
  is_active                   boolean not null default true,
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  constraint ux_bank_recon_rule_id_firm unique (id, firm_id)
);

create index if not exists ix_bank_rules_firm_priority on bank_reconciliation_rules (firm_id, priority desc);

-- ---------------------------------------------------------------- row-level security

alter table bank_accounts enable row level security;
drop policy if exists tenant_isolation on bank_accounts;
create policy tenant_isolation on bank_accounts
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

alter table bank_statements enable row level security;
drop policy if exists tenant_isolation on bank_statements;
create policy tenant_isolation on bank_statements
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

alter table bank_transactions enable row level security;
drop policy if exists tenant_isolation on bank_transactions;
create policy tenant_isolation on bank_transactions
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

alter table bank_reconciliation_rules enable row level security;
drop policy if exists tenant_isolation on bank_reconciliation_rules;
create policy tenant_isolation on bank_reconciliation_rules
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on bank_accounts to cadesk_app;
grant select, insert, update, delete on bank_statements to cadesk_app;
grant select, insert, update, delete on bank_transactions to cadesk_app;
grant select, insert, update, delete on bank_reconciliation_rules to cadesk_app;

-- ---------------------------------------------------------------- permissions

insert into permissions (code, area, description) values
  ('bank.read',      'bank', 'View bank accounts, uploaded statements, transactions, and BRS reconciliation sheets'),
  ('bank.manage',    'bank', 'Create bank accounts, upload statements, configure reconciliation rules'),
  ('bank.reconcile', 'bank', 'Perform bank reconciliation, match ledger transactions, and finalize BRS reports')
on conflict (code) do nothing;
