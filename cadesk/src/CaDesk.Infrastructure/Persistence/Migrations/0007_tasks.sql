-- 0007_tasks: work items (from deadlines or ad hoc), checklists, review, comments.

-- ---------------------------------------------------------------- new permission: approve or send back work

insert into permissions (code, area, description) values ('tasks.review', 'tasks', 'Approve work or send it back');

-- existing firms: Partners and Managers review
insert into role_permissions (role_id, permission_code, firm_id)
select r.id, 'tasks.review', r.firm_id from roles r
 where r.is_system and r.name in ('Partner', 'Manager')
on conflict do nothing;

-- new firms: Manager gets it from the template (Partner has "*")
update firm_setup_templates
   set payload = jsonb_set(payload, '{roles,1,permissions}', (payload #> '{roles,1,permissions}') || '["tasks.review"]'::jsonb)
 where code = 'IN_CA_FIRM' and payload #>> '{roles,1,name}' = 'Manager';

-- ---------------------------------------------------------------- checklist templates

-- firm_id null = platform default for a compliance type; a firm's own row for the same type replaces it.
create table task_templates (
  id         uuid primary key,
  firm_id    uuid references firms(id),
  type_code  text references compliance_types(code),     -- null = a general template (ad-hoc work)
  name       text not null check (length(name) between 1 and 120),
  items      jsonb not null default '[]' check (jsonb_typeof(items) = 'array' and jsonb_array_length(items) <= 40),
  created_at timestamptz not null default now()
);
create unique index ux_task_templates_platform on task_templates (type_code) where firm_id is null and type_code is not null;
create unique index ux_task_templates_firm on task_templates (firm_id, type_code) where firm_id is not null and type_code is not null;

-- ---------------------------------------------------------------- tasks

-- target for the same-firm foreign key from tasks
alter table deadlines add constraint ux_deadlines_id_firm unique (id, firm_id);

create table tasks (
  id                uuid primary key,
  firm_id           uuid not null references firms(id),
  client_id         uuid,
  deadline_id       uuid,
  title             text not null check (length(title) between 1 and 200),
  description       text check (length(description) <= 4000),
  status            text not null default 'open'
                    check (status in ('open','in_progress','in_review','changes_requested','done','cancelled')),
  priority          text not null default 'normal' check (priority in ('low','normal','high')),
  due_date          date,
  assigned_user_id  uuid,
  reviewer_user_id  uuid,
  review_required   boolean not null default false,
  submitted_by      uuid,
  filed_on          date,                        -- for deadline tasks: what to record on the deadline when approved
  ack_no            text check (length(ack_no) <= 100),
  completed_at      timestamptz,
  completed_by      uuid,
  created_at        timestamptz not null default now(),
  created_by        uuid,
  updated_at        timestamptz,
  updated_by        uuid,
  constraint ux_tasks_deadline unique (deadline_id),
  constraint ux_tasks_id_firm unique (id, firm_id),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade,
  foreign key (deadline_id, firm_id) references deadlines(id, firm_id) on delete cascade,
  foreign key (assigned_user_id, firm_id) references users(id, firm_id),
  foreign key (reviewer_user_id, firm_id) references users(id, firm_id),
  foreign key (submitted_by, firm_id) references users(id, firm_id),
  foreign key (completed_by, firm_id) references users(id, firm_id)
);
create index ix_tasks_assignee_open on tasks (firm_id, assigned_user_id, due_date) where status not in ('done','cancelled');
create index ix_tasks_reviewer on tasks (firm_id, reviewer_user_id) where status = 'in_review';
create index ix_tasks_client on tasks (client_id);

create table task_checklist_items (
  id         uuid primary key,
  firm_id    uuid not null references firms(id),
  task_id    uuid not null,
  position   int not null,
  text       text not null check (length(text) between 1 and 300),
  is_done    boolean not null default false,
  done_by    uuid,
  done_at    timestamptz,
  foreign key (task_id, firm_id) references tasks(id, firm_id) on delete cascade,
  foreign key (done_by, firm_id) references users(id, firm_id)
);
create index ix_task_checklist_task on task_checklist_items (task_id, position);

-- comments, plus the task's own history lines (kind <> 'comment')
create table task_comments (
  id         uuid primary key,
  firm_id    uuid not null references firms(id),
  task_id    uuid not null,
  kind       text not null default 'comment' check (kind in ('comment','status','review')),
  body       text not null check (length(body) between 1 and 4000),
  created_by uuid,                                   -- null = the system (generator)
  created_at timestamptz not null default now(),
  foreign key (task_id, firm_id) references tasks(id, firm_id) on delete cascade,
  foreign key (created_by, firm_id) references users(id, firm_id)
);
create index ix_task_comments_task on task_comments (task_id, created_at);

-- ---------------------------------------------------------------- row-level security

do $$
declare t text;
begin
  foreach t in array array['tasks','task_checklist_items','task_comments']
  loop
    execute format('alter table %I enable row level security', t);
    execute format(
      'create policy tenant_isolation on %I using (firm_id = app_current_firm()) with check (firm_id = app_current_firm())', t);
  end loop;
end $$;

alter table task_templates enable row level security;
create policy templates_read on task_templates for select using (firm_id is null or firm_id = app_current_firm());
create policy templates_insert on task_templates for insert with check (firm_id = app_current_firm());
create policy templates_update on task_templates for update using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());
create policy templates_delete on task_templates for delete using (firm_id = app_current_firm());

grant select, insert, update, delete on task_templates, tasks, task_checklist_items, task_comments to cadesk_app;

-- ---------------------------------------------------------------- settings

insert into config_definitions (key, area, value_type, json_schema, default_value, lowest_scope, plan_value_is_cap, labels, sort_order) values
('task.create_ahead_days', 'tasks', 'int', '{"type":"integer","minimum":3,"maximum":90}', '30', 3, false,
 '{"en":"Create tasks this many days before the due date"}', 30);

-- ---------------------------------------------------------------- default checklists (platform)

insert into task_templates (id, firm_id, type_code, name, items) values
(gen_random_uuid(), null, 'GSTR1_M',   'GSTR-1', '["Get sales register / invoices from client","Check B2B GSTINs and HSN summary","Record credit and debit notes","Upload and verify on the GST portal","File with EVC/DSC and save the ARN"]'),
(gen_random_uuid(), null, 'GSTR1_Q',   'GSTR-1 (QRMP)', '["Get sales register for the quarter","Check IFF uploads for months 1 and 2","Check B2B GSTINs and HSN summary","File and save the ARN"]'),
(gen_random_uuid(), null, 'GSTR3B_M',  'GSTR-3B', '["Get purchase register","Reconcile ITC with GSTR-2B","Compute output tax and ITC","Send tax payable to client for approval","Pay tax (challan) and file","Save the ARN and challan"]'),
(gen_random_uuid(), null, 'GSTR3B_Q',  'GSTR-3B (QRMP)', '["Check PMT-06 payments for months 1 and 2","Reconcile ITC with GSTR-2B","Compute tax for the quarter","Client approval","Pay balance and file","Save the ARN"]'),
(gen_random_uuid(), null, 'GSTR9',     'GSTR-9', '["Collect books for the year","Reconcile GSTR-1 / 3B with books","Reconcile ITC with 2B for the year","Prepare tables and get client sign-off","File and save the ARN"]'),
(gen_random_uuid(), null, 'TDS_PAY',   'TDS payment', '["Get payments subject to TDS for the month","Compute TDS section-wise","Send challan amounts to client","Confirm payment and save challan details"]'),
(gen_random_uuid(), null, 'TDS_RETURN','TDS/TCS returns', '["Collect challans for the quarter","Verify PAN of deductees","Prepare statements","Validate with FVU","File and save the token number","Download and send certificates"]'),
(gen_random_uuid(), null, 'ADV_TAX',   'Advance tax', '["Estimate income for the year","Compute instalment due","Send amount to client","Confirm payment and save challan"]'),
(gen_random_uuid(), null, 'ITR',       'Income tax return', '["Collect Form 16 / 16A, AIS and TIS","Collect bank interest and capital gains statements","Compute income and tax","Client approval","File and e-verify","Save acknowledgement"]'),
(gen_random_uuid(), null, 'ITR_BUS',   'Income tax return (business)', '["Collect books and bank statements","Reconcile with AIS / 26AS","Prepare computation","Client approval","File and e-verify","Save acknowledgement"]'),
(gen_random_uuid(), null, 'TAX_AUDIT', 'Tax audit', '["Get audited financial statements","Prepare Form 3CD clauses","Partner review of audit report","Upload and get client acceptance","Save acknowledgement"]'),
(gen_random_uuid(), null, 'ITR_AUDIT', 'Income tax return (audit)', '["Tax audit report filed","Prepare computation from audited accounts","Client approval","File and e-verify / DSC","Save acknowledgement"]'),
(gen_random_uuid(), null, 'AE_VAT',    'UAE VAT return', '["Get sales and purchase listings","Check tax invoices and reverse charge","Prepare VAT 201","Client approval","Submit on EmaraTax and save reference","Confirm payment"]'),
(gen_random_uuid(), null, 'AE_CT',     'UAE corporate tax return', '["Get audited financial statements","Compute taxable income and adjustments","Check small business relief / free zone status","Client approval","Submit on EmaraTax","Confirm payment"]'),
(gen_random_uuid(), null, 'SA_VAT',    'Saudi VAT return', '["Get sales and purchase listings","Check e-invoices (FATOORA)","Prepare return","Client approval","Submit on ZATCA portal","Confirm payment"]'),
(gen_random_uuid(), null, 'SA_ZAKAT',  'Zakat return', '["Get audited financial statements","Compute zakat base","Client approval","Submit on ZATCA portal","Confirm payment and certificate"]');
