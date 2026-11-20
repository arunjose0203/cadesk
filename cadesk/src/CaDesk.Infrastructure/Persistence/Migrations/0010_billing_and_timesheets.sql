-- 0010_billing_and_timesheets: CA fee billing, GST invoices, payment receipts and staff timesheets.
-- Supports multi-currency (INR, AED, SAR), SAC codes (9982 for accounting/auditing),
-- GST rate calculation, atomic sequence numbering, and time entries linked to clients/tasks.

-- ---------------------------------------------------------------- time entries (staff timesheets)

create table if not exists time_entries (
  id              uuid primary key,
  firm_id         uuid not null references firms(id),
  client_id       uuid not null,
  task_id         uuid,
  user_id         uuid not null,
  date            date not null,
  hours           numeric(5,2) not null check (hours > 0 and hours <= 24),
  hourly_rate     numeric(10,2) not null default 0 check (hourly_rate >= 0),
  description     text not null check (length(description) between 1 and 2000),
  billable        boolean not null default true,
  is_billed       boolean not null default false,
  invoice_item_id uuid,
  created_at      timestamptz not null default now(),
  created_by      uuid,
  updated_at      timestamptz,
  updated_by      uuid,
  constraint ux_time_entries_id_firm unique (id, firm_id),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade,
  foreign key (task_id, firm_id) references tasks(id, firm_id) on delete set null (task_id),
  foreign key (user_id, firm_id) references users(id, firm_id),
  foreign key (created_by, firm_id) references users(id, firm_id),
  foreign key (updated_by, firm_id) references users(id, firm_id)
);

create index if not exists ix_time_entries_client on time_entries (firm_id, client_id, date desc);
create index if not exists ix_time_entries_user on time_entries (firm_id, user_id, date desc);
create index if not exists ix_time_entries_unbilled on time_entries (firm_id, client_id) where billable and not is_billed;

-- ---------------------------------------------------------------- invoices

create table if not exists invoices (
  id              uuid primary key,
  firm_id         uuid not null references firms(id),
  client_id       uuid not null,
  invoice_number  text not null,
  issue_date      date not null,
  due_date        date not null,
  status          text not null default 'draft' check (status in ('draft','sent','partially_paid','paid','cancelled')),
  currency        char(3) not null default 'INR',
  subtotal        numeric(12,2) not null default 0 check (subtotal >= 0),
  tax_amount      numeric(12,2) not null default 0 check (tax_amount >= 0),
  total_amount    numeric(12,2) not null default 0 check (total_amount >= 0),
  paid_amount     numeric(12,2) not null default 0 check (paid_amount >= 0 and paid_amount <= total_amount),
  notes           text check (notes is null or length(notes) <= 2000),
  terms           text check (terms is null or length(terms) <= 2000),
  created_at      timestamptz not null default now(),
  created_by      uuid,
  updated_at      timestamptz,
  updated_by      uuid,
  constraint ux_invoices_id_firm unique (id, firm_id),
  constraint ux_invoices_firm_number unique (firm_id, invoice_number),
  foreign key (client_id, firm_id) references clients(id, firm_id),
  foreign key (created_by, firm_id) references users(id, firm_id),
  foreign key (updated_by, firm_id) references users(id, firm_id)
);

create index if not exists ix_invoices_client on invoices (firm_id, client_id, issue_date desc);
create index if not exists ix_invoices_status on invoices (firm_id, status);

-- ---------------------------------------------------------------- invoice items

create table if not exists invoice_items (
  id              uuid primary key,
  firm_id         uuid not null references firms(id),
  invoice_id      uuid not null,
  description     text not null check (length(description) between 1 and 1000),
  sac_code        text check (sac_code is null or length(sac_code) <= 10),
  quantity        numeric(8,2) not null default 1 check (quantity > 0),
  unit_price      numeric(12,2) not null default 0 check (unit_price >= 0),
  gst_rate        numeric(5,2) not null default 18.00 check (gst_rate >= 0 and gst_rate <= 100),
  amount          numeric(12,2) not null default 0 check (amount >= 0),
  tax_amount      numeric(12,2) not null default 0 check (tax_amount >= 0),
  total_amount    numeric(12,2) not null default 0 check (total_amount >= 0),
  time_entry_id   uuid,
  constraint ux_invoice_items_id_firm unique (id, firm_id),
  foreign key (invoice_id, firm_id) references invoices(id, firm_id) on delete cascade,
  foreign key (time_entry_id, firm_id) references time_entries(id, firm_id) on delete set null (time_entry_id)
);

create index if not exists ix_invoice_items_invoice on invoice_items (invoice_id);

-- link back from time_entries to invoice_items if not already added
do $$
begin
  if not exists (select 1 from pg_constraint where conname = 'fk_time_entries_invoice_item') then
    alter table time_entries
      add constraint fk_time_entries_invoice_item foreign key (invoice_item_id, firm_id)
      references invoice_items(id, firm_id) on delete set null (invoice_item_id);
  end if;
end $$;

-- ---------------------------------------------------------------- payments

create table if not exists payments (
  id               uuid primary key,
  firm_id          uuid not null references firms(id),
  invoice_id       uuid not null,
  client_id        uuid not null,
  payment_date     date not null,
  amount           numeric(12,2) not null check (amount > 0),
  payment_mode     text not null check (payment_mode in ('bank_transfer','upi','cheque','cash','card','other')),
  reference_number text check (reference_number is null or length(reference_number) <= 100),
  notes            text check (notes is null or length(notes) <= 1000),
  created_at       timestamptz not null default now(),
  created_by       uuid,
  constraint ux_payments_id_firm unique (id, firm_id),
  foreign key (invoice_id, firm_id) references invoices(id, firm_id) on delete cascade,
  foreign key (client_id, firm_id) references clients(id, firm_id),
  foreign key (created_by, firm_id) references users(id, firm_id)
);

create index if not exists ix_payments_invoice on payments (firm_id, invoice_id);
create index if not exists ix_payments_client on payments (firm_id, client_id, payment_date desc);

-- ---------------------------------------------------------------- row-level security

do $$
declare t text;
begin
  foreach t in array array['time_entries','invoices','invoice_items','payments']
  loop
    execute format('alter table %I enable row level security', t);
    execute format('drop policy if exists tenant_isolation on %I', t);
    execute format(
      'create policy tenant_isolation on %I using (firm_id = app_current_firm()) with check (firm_id = app_current_firm())', t);
  end loop;
end $$;

grant select, insert, update, delete on time_entries, invoices, invoice_items, payments to cadesk_app;

-- ---------------------------------------------------------------- permissions & role defaults

insert into permissions (code, area, description) values
  ('billing.read',      'billing',    'View invoices and payments'),
  ('timesheets.read',   'timesheets', 'View team timesheets'),
  ('timesheets.log',    'timesheets', 'Log own timesheet entries'),
  ('timesheets.manage', 'timesheets', 'Manage all team timesheets')
on conflict (code) do nothing;

-- ---------------------------------------------------------------- settings

insert into config_definitions (key, area, value_type, json_schema, default_value, lowest_scope, plan_value_is_cap, labels, sort_order) values
('billing.invoice_prefix',     'billing', 'string',  '{"type":"string","maxLength":40}', '"INV/{FY}/{SEQ:4}"', 3, false, '{"en":"Invoice numbering pattern"}', 10),
('billing.default_sac_code',   'billing', 'string',  '{"type":"string","maxLength":10}', '"9982"',             3, false, '{"en":"Default HSN/SAC code"}',     20),
('billing.default_gst_rate',   'billing', 'decimal', '{"type":"number","minimum":0,"maximum":28}', '18',      3, false, '{"en":"Default GST rate (%)"}',      30),
('billing.default_due_days',   'billing', 'int',     '{"type":"integer","minimum":0,"maximum":90}', '15',     3, false, '{"en":"Default payment due days"}', 40),
('billing.default_hourly_rate', 'billing', 'decimal', '{"type":"number","minimum":0,"maximum":100000}', '1500', 3, false, '{"en":"Default hourly billing rate"}', 50)
on conflict (key) do nothing;
