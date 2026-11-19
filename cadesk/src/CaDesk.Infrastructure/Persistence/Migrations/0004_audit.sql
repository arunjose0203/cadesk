-- 0004_audit: append-only audit log of every create / update / delete, partitioned by month.
-- The app can add rows for its own firm and read them; nobody but the owner can change or remove them.

create table audit_logs (
  id          uuid not null,
  firm_id     uuid,
  actor_id    uuid,
  actor_type  text not null check (actor_type in ('user','client_user','system','platform_admin')),
  entity      text not null,               -- clients, users, roles, config_values ...
  entity_id   text,                        -- id, or composite key as text
  action      text not null check (action in ('create','update','delete','approve','export','download','revoke')),
  old_values  jsonb,                       -- only changed fields on update; sensitive fields masked
  new_values  jsonb,
  ip          inet,
  at          timestamptz not null default now(),
  primary key (id, at)
) partition by range (at);

create table audit_logs_default partition of audit_logs default;
create index ix_audit_logs_firm_at on audit_logs (firm_id, at desc);
create index ix_audit_logs_entity on audit_logs (firm_id, entity, entity_id, at desc);

-- Creates monthly partitions (UTC months) from this month up to N months ahead. Safe to call repeatedly and
-- concurrently. If rows already fell into the default partition for a month (e.g. the daily job didn't run),
-- they are moved into the new month's partition before it is attached, instead of failing.
create function audit_ensure_partitions(months_ahead int default 3) returns int
language plpgsql security definer set search_path = public, pg_temp as $$
declare
  m     date := date_trunc('month', now() at time zone 'UTC')::date;
  lo    timestamptz;
  hi    timestamptz;
  made  int := 0;
  name  text;
begin
  perform pg_advisory_xact_lock(7270002);          -- one caller at a time
  for i in 0 .. months_ahead loop
    name := format('audit_logs_%s', to_char(m, 'YYYY_MM'));
    lo := m::timestamp at time zone 'UTC';
    hi := (m + interval '1 month')::timestamp at time zone 'UTC';
    if to_regclass(name) is null then
      execute format('create table %I (like audit_logs including defaults including constraints)', name);
      execute format(
        'with moved as (delete from audit_logs_default where at >= %L and at < %L returning *) insert into %I select * from moved',
        lo, hi, name);
      execute format('alter table audit_logs attach partition %I for values from (%L) to (%L)', name, lo, hi);
      made := made + 1;
    end if;
    m := (m + interval '1 month')::date;
  end loop;
  return made;
end $$;
revoke all on function audit_ensure_partitions(int) from public;

select audit_ensure_partitions(3);

alter table audit_logs enable row level security;
create policy tenant_read on audit_logs for select using (firm_id = app_current_firm());
create policy tenant_insert on audit_logs for insert with check (firm_id = app_current_firm());
grant select, insert on audit_logs to cadesk_app;    -- no update, no delete: append-only

-- sessions: when each refresh token was last used, for the idle sign-out setting
alter table refresh_tokens add column last_used_at timestamptz;
