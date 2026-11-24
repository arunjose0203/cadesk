-- 0064_office_essentials.sql: Attendance, Leave Management, Office Expenses & Reimbursables, Inward/Outward Registers

-- 1. Attendance Records
create table if not exists attendance_records (
    id uuid primary key default gen_random_uuid(),
    firm_id uuid not null references firms(id) on delete cascade,
    user_id uuid not null references users(id) on delete cascade,
    date date not null,
    check_in_time timestamptz not null,
    check_out_time timestamptz,
    status text not null default 'Present',
    notes text,
    ip_address text,
    created_at timestamptz not null default now(),
    created_by uuid references users(id),
    updated_at timestamptz,
    updated_by uuid references users(id),
    constraint uq_attendance_firm_user_date unique (firm_id, user_id, date)
);

alter table attendance_records enable row level security;

create policy attendance_records_tenant_isolation on attendance_records
    for all using (firm_id = app_current_firm());

create index if not exists idx_attendance_records_firm_user_date on attendance_records (firm_id, user_id, date);
create index if not exists idx_attendance_records_firm_date on attendance_records (firm_id, date);

-- 2. Leave Balances
create table if not exists leave_balances (
    id uuid primary key default gen_random_uuid(),
    firm_id uuid not null references firms(id) on delete cascade,
    user_id uuid not null references users(id) on delete cascade,
    year int not null,
    leave_type text not null,
    allocated_days numeric(5,2) not null default 12.00,
    used_days numeric(5,2) not null default 0.00,
    created_at timestamptz not null default now(),
    updated_at timestamptz,
    constraint uq_leave_balance_user_year_type unique (firm_id, user_id, year, leave_type)
);

alter table leave_balances enable row level security;

create policy leave_balances_tenant_isolation on leave_balances
    for all using (firm_id = app_current_firm());

create index if not exists idx_leave_balances_firm_user_year on leave_balances (firm_id, user_id, year);

-- 3. Leave Requests
create table if not exists leave_requests (
    id uuid primary key default gen_random_uuid(),
    firm_id uuid not null references firms(id) on delete cascade,
    user_id uuid not null references users(id) on delete cascade,
    leave_type text not null,
    start_date date not null,
    end_date date not null,
    days_count numeric(5,2) not null,
    reason text not null,
    status text not null default 'Pending',
    approver_id uuid references users(id),
    approved_at timestamptz,
    rejection_reason text,
    created_at timestamptz not null default now(),
    created_by uuid references users(id),
    updated_at timestamptz,
    updated_by uuid references users(id)
);

alter table leave_requests enable row level security;

create policy leave_requests_tenant_isolation on leave_requests
    for all using (firm_id = app_current_firm());

create index if not exists idx_leave_requests_firm_user on leave_requests (firm_id, user_id);
create index if not exists idx_leave_requests_firm_dates on leave_requests (firm_id, start_date, end_date);
create index if not exists idx_leave_requests_firm_status on leave_requests (firm_id, status);

-- 4. Office Expenses & Client Reimbursables
create table if not exists office_expenses (
    id uuid primary key default gen_random_uuid(),
    firm_id uuid not null references firms(id) on delete cascade,
    user_id uuid not null references users(id) on delete cascade,
    client_id uuid references clients(id) on delete set null,
    task_id uuid references tasks(id) on delete set null,
    date date not null,
    category text not null,
    amount numeric(12,2) not null,
    description text not null,
    receipt_document_id uuid references documents(id) on delete set null,
    is_reimbursable_by_client boolean not null default false,
    is_client_billed boolean not null default false,
    billed_invoice_id uuid references invoices(id) on delete set null,
    status text not null default 'Submitted',
    approved_by uuid references users(id),
    approved_at timestamptz,
    rejection_reason text,
    created_at timestamptz not null default now(),
    created_by uuid references users(id),
    updated_at timestamptz,
    updated_by uuid references users(id)
);

alter table office_expenses enable row level security;

create policy office_expenses_tenant_isolation on office_expenses
    for all using (firm_id = app_current_firm());

create index if not exists idx_office_expenses_firm_user on office_expenses (firm_id, user_id);
create index if not exists idx_office_expenses_firm_client on office_expenses (firm_id, client_id);
create index if not exists idx_office_expenses_firm_status on office_expenses (firm_id, status);

-- 5. Inward / Outward Physical Registers
create table if not exists inward_outward_registers (
    id uuid primary key default gen_random_uuid(),
    firm_id uuid not null references firms(id) on delete cascade,
    entry_type text not null,
    entry_number text not null,
    date date not null,
    client_id uuid not null references clients(id) on delete cascade,
    description text not null,
    delivery_mode text not null,
    courier_name text,
    tracking_number text,
    sender_recipient_name text,
    handled_by_user_id uuid not null references users(id),
    dsc_token_ref text,
    status text not null default 'Received',
    acknowledgement_file_id uuid references documents(id) on delete set null,
    created_at timestamptz not null default now(),
    created_by uuid references users(id),
    updated_at timestamptz,
    updated_by uuid references users(id)
);

alter table inward_outward_registers enable row level security;

create policy inward_outward_registers_tenant_isolation on inward_outward_registers
    for all using (firm_id = app_current_firm());

create index if not exists idx_inward_outward_registers_firm_type on inward_outward_registers (firm_id, entry_type);
create index if not exists idx_inward_outward_registers_firm_client on inward_outward_registers (firm_id, client_id);
create index if not exists idx_inward_outward_registers_firm_date on inward_outward_registers (firm_id, date);

grant select, insert, update, delete on attendance_records to cadesk_app;
grant select, insert, update, delete on leave_balances to cadesk_app;
grant select, insert, update, delete on leave_requests to cadesk_app;
grant select, insert, update, delete on office_expenses to cadesk_app;
grant select, insert, update, delete on inward_outward_registers to cadesk_app;

-- 6. Permissions Registration & Grants
insert into permissions (code, area, description) values
    ('attendance.manage', 'attendance', 'Manage and view firm-wide attendance, export attendance registers'),
    ('leave.approve', 'leave', 'Approve or reject staff leave applications and manage balances'),
    ('expense.approve', 'expenses', 'Approve employee reimbursement claims and mark for client invoicing'),
    ('register.manage', 'registers', 'Create, manage, and print physical inward and outward registers')
on conflict (code) do nothing;

-- Grant to system Partner and Manager roles across all firms
insert into role_permissions (role_id, permission_code, firm_id)
select r.id, p.code, r.firm_id
from roles r
cross join permissions p
where r.name in ('Partner', 'Manager') and r.is_system
  and p.code in ('attendance.manage', 'leave.approve', 'expense.approve', 'register.manage')
on conflict do nothing;
