-- 0011_dsc_management: Digital Signature Certificate (DSC) and physical USB token management.
-- Tracks client & partner DSC validity, expiry alerts, safe custody locations, and check-out logs.

-- ---------------------------------------------------------------- DSC master register

create table if not exists dsc_registers (
  id                uuid primary key,
  firm_id           uuid not null references firms(id),
  client_id         uuid,
  holder_name       text not null check (length(holder_name) between 1 and 200),
  pan               text not null check (pan ~ '^[A-Z]{5}[0-9]{4}[A-Z]$'),
  dsc_type          text not null default 'class_3' check (dsc_type in ('class_3','dgft','other')),
  issuing_authority text not null default 'eMudhra' check (length(issuing_authority) <= 100),
  valid_from        date not null,
  valid_to          date not null,
  serial_number     text check (serial_number is null or length(serial_number) <= 100),
  usb_token_id      text check (usb_token_id is null or length(usb_token_id) <= 100),
  location          text not null default 'vault' check (location in ('vault','with_staff','with_client','in_transit')),
  custodian_user_id uuid,
  pin_protected     text check (pin_protected is null or length(pin_protected) <= 500),
  status            text not null default 'active' check (status in ('active','expiring_soon','expired','revoked','handed_over')),
  notes             text check (notes is null or length(notes) <= 2000),
  created_at        timestamptz not null default now(),
  created_by        uuid,
  updated_at        timestamptz,
  updated_by        uuid,
  constraint ux_dsc_registers_id_firm unique (id, firm_id),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete set null (client_id),
  foreign key (custodian_user_id, firm_id) references users(id, firm_id) on delete set null (custodian_user_id),
  foreign key (created_by, firm_id) references users(id, firm_id),
  foreign key (updated_by, firm_id) references users(id, firm_id)
);

create index if not exists ix_dsc_registers_firm_client on dsc_registers (firm_id, client_id);
create index if not exists ix_dsc_registers_valid_to on dsc_registers (firm_id, valid_to);
create index if not exists ix_dsc_registers_pan on dsc_registers (firm_id, pan);

-- ---------------------------------------------------------------- DSC movement / custody history

create table if not exists dsc_movement_logs (
  id              uuid primary key,
  firm_id         uuid not null references firms(id),
  dsc_id          uuid not null,
  moved_at        timestamptz not null default now(),
  from_location   text not null,
  to_location     text not null,
  handler_user_id uuid,
  remarks         text check (remarks is null or length(remarks) <= 1000),
  created_by      uuid,
  constraint ux_dsc_movement_logs_id_firm unique (id, firm_id),
  foreign key (dsc_id, firm_id) references dsc_registers(id, firm_id) on delete cascade,
  foreign key (handler_user_id, firm_id) references users(id, firm_id),
  foreign key (created_by, firm_id) references users(id, firm_id)
);

create index if not exists ix_dsc_movement_logs_dsc on dsc_movement_logs (dsc_id, moved_at desc);

-- ---------------------------------------------------------------- row-level security

do $$
declare t text;
begin
  foreach t in array array['dsc_registers','dsc_movement_logs']
  loop
    execute format('alter table %I enable row level security', t);
    execute format('drop policy if exists tenant_isolation on %I', t);
    execute format(
      'create policy tenant_isolation on %I using (firm_id = app_current_firm()) with check (firm_id = app_current_firm())', t);
  end loop;
end $$;

grant select, insert, update, delete on dsc_registers, dsc_movement_logs to cadesk_app;

-- ---------------------------------------------------------------- permissions

insert into permissions (code, area, description) values
  ('dsc.read',   'dsc', 'View digital signature certificates and custody'),
  ('dsc.manage', 'dsc', 'Register, update and transfer custody of digital signatures')
on conflict (code) do nothing;

-- ---------------------------------------------------------------- settings

insert into config_definitions (key, area, value_type, json_schema, default_value, lowest_scope, plan_value_is_cap, labels, sort_order) values
('dsc.expiry_warning_days', 'dsc', 'int', '{"type":"integer","minimum":7,"maximum":90}', '30', 3, false, '{"en":"Warn about expiring DSCs (days before)"}', 10)
on conflict (key) do nothing;
