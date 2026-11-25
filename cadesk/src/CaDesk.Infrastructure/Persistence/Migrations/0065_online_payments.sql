-- 0065_online_payments.sql: Sprint 69, online fee collection through Razorpay payment links.

-- the current payment link on an invoice (for its balance due)
alter table invoices add column if not exists payment_link_id text check (payment_link_id is null or length(payment_link_id) <= 64);
alter table invoices add column if not exists payment_link_url text check (payment_link_url is null or length(payment_link_url) <= 300);
alter table invoices add column if not exists payment_link_status text check (payment_link_status is null or payment_link_status in ('created','partially_paid','expired','cancelled','paid'));
alter table invoices add column if not exists payment_link_amount numeric(12,2);
create index if not exists ix_invoices_firm_payment_link on invoices (firm_id, payment_link_id) where payment_link_id is not null;

-- payments recorded by the gateway webhook
alter table payments drop constraint if exists payments_payment_mode_check;
alter table payments add constraint payments_payment_mode_check
  check (payment_mode in ('bank_transfer','upi','cheque','cash','card','other','online'));
alter table payments add column if not exists gateway_payment_id text check (gateway_payment_id is null or length(gateway_payment_id) <= 64);
-- the same gateway payment can never be recorded twice (webhooks are delivered at least once)
create unique index if not exists ux_payments_firm_gateway_payment on payments (firm_id, gateway_payment_id) where gateway_payment_id is not null;

-- settings: a partner enters the key id here and stores the key secret and webhook secret in Secrets
insert into config_definitions (key, area, value_type, json_schema, default_value, lowest_scope, plan_value_is_cap, labels, sort_order) values
('payments.razorpay.key_id',             'payments', 'string',     '{"type":"string","pattern":"^(rzp_(test|live)_[A-Za-z0-9]{6,40})?$"}', '""', 3, false, '{"en":"Razorpay key ID"}', 10),
('payments.razorpay.credentials_ref',    'payments', 'secret_ref', '{"type":"string","pattern":"^[a-z0-9_.-]{1,60}$"}', '""', 3, false, '{"en":"Razorpay key secret (secret name)"}', 20),
('payments.razorpay.webhook_secret_ref', 'payments', 'secret_ref', '{"type":"string","pattern":"^[a-z0-9_.-]{1,60}$"}', '""', 3, false, '{"en":"Razorpay webhook secret (secret name)"}', 30)
on conflict (key) do nothing;
