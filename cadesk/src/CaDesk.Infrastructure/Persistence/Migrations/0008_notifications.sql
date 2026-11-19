-- 0008_notifications: message templates (platform defaults + firm versions) and the outbox of every reminder sent.

-- dialling code per country, for WhatsApp numbers typed without it
alter table countries add column calling_code text;
update countries set calling_code = v.cc from (values ('IN','91'),('AE','971'),('SA','966'),('OM','968'),('BH','973'),('QA','974'),('KW','965')) v(code, cc)
 where countries.code = v.code;

-- ---------------------------------------------------------------- templates

-- firm_id null = platform default; the firm's own row for the same key + channel replaces it.
create table notification_templates (
  id          uuid primary key,
  firm_id     uuid references firms(id),
  key         text not null,              -- client.deadline_reminder, staff.task_sent_back ...
  channel     text not null check (channel in ('email','whatsapp')),
  subject     text check (length(subject) <= 200),
  body        text not null check (length(body) between 1 and 4000),
  wa_template text check (wa_template ~ '^[a-z0-9_]{1,255}$'),   -- approved WhatsApp template name
  wa_params   jsonb,                      -- placeholder names in the WhatsApp template's {{1}}, {{2}} ... order
  created_at  timestamptz not null default now()
);
create unique index ux_notification_templates_platform on notification_templates (key, channel) where firm_id is null;
create unique index ux_notification_templates_firm on notification_templates (firm_id, key, channel) where firm_id is not null;

-- ---------------------------------------------------------------- outbox

create table notifications (
  id                  uuid primary key,
  firm_id             uuid not null references firms(id),
  recipient_type      text not null check (recipient_type in ('client_contact','client','user')),
  recipient_id        uuid,
  recipient_name      text,
  client_id           uuid,
  channel             text not null check (channel in ('email','whatsapp','sms')),
  to_address          text not null check (length(to_address) <= 320),
  template_key        text not null,
  subject             text,
  body                text not null,
  params              jsonb not null default '{}',
  related_type        text check (related_type in ('deadline','task')),
  related_id          uuid,
  dedupe_key          text not null,       -- the same reminder is never queued twice
  status              text not null default 'queued'
                      check (status in ('queued','sending','sent','failed','skipped','cancelled')),
  scheduled_at        timestamptz not null,
  attempts            int not null default 0,
  locked_at           timestamptz,
  last_error          text,
  provider_message_id text,
  sent_at             timestamptz,
  created_at          timestamptz not null default now(),
  constraint ux_notifications_dedupe unique (firm_id, dedupe_key),
  foreign key (client_id, firm_id) references clients(id, firm_id) on delete cascade
);
create index ix_notifications_due on notifications (firm_id, scheduled_at) where status = 'queued';
create index ix_notifications_client on notifications (client_id, created_at desc);
create index ix_notifications_firm_created on notifications (firm_id, created_at desc);

-- ---------------------------------------------------------------- row-level security

alter table notifications enable row level security;
create policy tenant_isolation on notifications using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());

alter table notification_templates enable row level security;
create policy templates_read on notification_templates for select using (firm_id is null or firm_id = app_current_firm());
create policy templates_insert on notification_templates for insert with check (firm_id = app_current_firm());
create policy templates_update on notification_templates for update using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());
create policy templates_delete on notification_templates for delete using (firm_id = app_current_firm());

grant select, insert, update, delete on notification_templates to cadesk_app;
grant select, insert, update on notifications to cadesk_app;     -- the outbox is a record: never deleted by the app

-- ---------------------------------------------------------------- settings

insert into config_definitions (key, area, value_type, json_schema, default_value, lowest_scope, plan_value_is_cap, labels, sort_order) values
('reminder.client_enabled',     'reminders', 'bool',   '{"type":"boolean"}', 'true', 4, false, '{"en":"Send deadline reminders to clients"}', 5),
('reminder.staff_digest_time',  'reminders', 'string', '{"type":"string","pattern":"^([01][0-9]|2[0-3]):[0-5][0-9]$"}', '"09:00"', 3, false, '{"en":"Daily task summary to staff at"}', 60),
('notify.staff_daily_digest',   'reminders', 'bool',   '{"type":"boolean"}', 'true', 5, false, '{"en":"Email me a daily task summary"}', 70),
('email.from_name',             'integrations', 'string', '{"type":"string","maxLength":80}', '""', 3, false, '{"en":"Email sender name (empty = firm name)"}', 20),
('email.reply_to',              'integrations', 'string', '{"type":"string","maxLength":200}', '""', 3, false, '{"en":"Reply-to address for client emails"}', 30),
('whatsapp.phone_number_id',    'integrations', 'string', '{"type":"string","pattern":"^[0-9]{0,30}$"}', '""', 3, false, '{"en":"WhatsApp Business phone number ID"}', 15),
('whatsapp.language',           'integrations', 'string', '{"type":"string","enum":["en","en_US","en_GB","ar","hi","ml","ta"]}', '"en"', 3, false, '{"en":"WhatsApp template language"}', 16);

-- notify.staff_daily_digest has lowest level 5 (user), so each user can switch their own digest off in My account.

-- ---------------------------------------------------------------- default templates
-- Placeholders: {{firm}} {{client}} {{contact}} {{compliance}} {{period}} {{due_date}} {{days_left}} {{days_overdue}}
--               {{user}} {{task}} {{comment}} {{overdue_count}} {{today_count}} {{week_count}} {{review_count}} {{task_list}}

insert into notification_templates (id, firm_id, key, channel, subject, body, wa_template, wa_params) values
(gen_random_uuid(), null, 'client.deadline_reminder', 'email',
 '{{compliance}} for {{period}} is due on {{due_date}}',
 'Dear {{contact}},

This is a reminder from {{firm}}: {{compliance}} for {{period}} is due on {{due_date}} ({{days_left}}).

If you haven''t sent us the documents for this yet, please share them as soon as possible so we can file on time.

Regards,
{{firm}}', null, null),
(gen_random_uuid(), null, 'client.deadline_reminder', 'whatsapp', null,
 'Hello {{contact}}, reminder from {{firm}}: {{compliance}} for {{period}} is due on {{due_date}}. Please share pending documents.',
 'deadline_reminder', '["contact","firm","compliance","period","due_date"]'),
(gen_random_uuid(), null, 'client.deadline_overdue', 'email',
 '{{compliance}} for {{period}} is overdue',
 'Dear {{contact}},

{{compliance}} for {{period}} was due on {{due_date}} and has not been filed yet. Late filing can attract late fees and interest.

Please send us the pending documents or contact us today.

Regards,
{{firm}}', null, null),
(gen_random_uuid(), null, 'client.deadline_overdue', 'whatsapp', null,
 'Hello {{contact}}, {{compliance}} for {{period}} was due on {{due_date}} and is not filed yet. Please contact {{firm}} today.',
 'deadline_overdue', '["contact","compliance","period","due_date","firm"]'),
(gen_random_uuid(), null, 'staff.daily_digest', 'email',
 'Your tasks today: {{overdue_count}} overdue, {{today_count}} due today',
 'Hello {{user}},

Overdue: {{overdue_count}}
Due today: {{today_count}}
Due in the next 7 days: {{week_count}}
Waiting for your review: {{review_count}}

{{task_list}}

— CA Desk for {{firm}}', null, null),
(gen_random_uuid(), null, 'staff.task_sent_back', 'email',
 'Sent back: {{task}}',
 'Hello {{user}},

Your work on "{{task}}" ({{client}}) was sent back:

{{comment}}

— CA Desk for {{firm}}', null, null),
(gen_random_uuid(), null, 'staff.review_requested', 'email',
 'Please review: {{task}}',
 'Hello {{user}},

"{{task}}" ({{client}}) is waiting for your review.

— CA Desk for {{firm}}', null, null),
(gen_random_uuid(), null, 'staff.escalation', 'email',
 'Overdue {{days_overdue}} days: {{compliance}} for {{client}}',
 'Hello {{user}},

{{compliance}} for {{period}} ({{client}}) was due on {{due_date}} and is {{days_overdue}} days overdue.

— CA Desk for {{firm}}', null, null);
