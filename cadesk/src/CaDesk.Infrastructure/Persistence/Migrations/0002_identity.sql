-- 0002_identity: permissions, roles, users, sessions (refresh tokens), MFA, invitations,
-- login audit, firm setup templates and the sign-up function.
--
-- Login happens BEFORE we know the firm, so a few narrow SECURITY DEFINER functions
-- (auth_*) let the restricted app role look up exactly what login needs, and nothing more.
-- Everything after login runs with app.firm_id set, under row-level security.

-- ---------------------------------------------------------------- permission catalogue (global)

create table permissions (
  code        text primary key,
  area        text not null,
  description text not null
);

insert into permissions (code, area, description) values
  ('clients.read',          'clients',     'View clients'),
  ('clients.write',         'clients',     'Create and edit clients'),
  ('clients.delete',        'clients',     'Delete clients'),
  ('users.read',            'users',       'View users'),
  ('users.manage',          'users',       'Invite, change and deactivate users'),
  ('roles.manage',          'users',       'Create and change roles'),
  ('settings.manage',       'settings',    'Change firm settings'),
  ('documents.read',        'documents',   'View documents'),
  ('documents.upload',      'documents',   'Upload documents'),
  ('documents.delete',      'documents',   'Delete documents'),
  ('extractions.approve',   'documents',   'Approve extracted data'),
  ('compliance.read',       'compliance',  'View deadlines'),
  ('compliance.manage',     'compliance',  'Change deadlines and filing status'),
  ('tasks.read',            'tasks',       'View tasks'),
  ('tasks.manage',          'tasks',       'Create, assign and move tasks'),
  ('computations.run',      'tax',         'Prepare tax computations'),
  ('computations.approve',  'tax',         'Approve tax computations'),
  ('billing.manage',        'billing',     'Manage client invoices and payments'),
  ('reports.read',          'reports',     'View reports'),
  ('audit.read',            'audit',       'View audit and login history');

-- ---------------------------------------------------------------- firm setup templates (global)

create table firm_setup_templates (
  code         text primary key,
  country_code char(2) not null references countries(code),
  name         text not null,
  payload      jsonb not null
);

insert into firm_setup_templates (code, country_code, name, payload) values
('IN_CA_FIRM', 'IN', 'Indian CA firm', '{
  "roles": [
    { "name": "Partner", "system": true, "permissions": ["*"] },
    { "name": "Manager", "system": true, "permissions": [
        "clients.read","clients.write","users.read","documents.read","documents.upload","documents.delete",
        "extractions.approve","compliance.read","compliance.manage","tasks.read","tasks.manage",
        "computations.run","billing.manage","reports.read"] },
    { "name": "Staff", "system": true, "permissions": [
        "clients.read","clients.write","documents.read","documents.upload","extractions.approve",
        "compliance.read","compliance.manage","tasks.read","tasks.manage","computations.run"] },
    { "name": "Article", "system": true, "permissions": [
        "clients.read","documents.read","documents.upload","compliance.read","tasks.read"] }
  ]
}');

-- ---------------------------------------------------------------- roles and users

create table roles (
  id         uuid primary key,
  firm_id    uuid not null references firms(id),
  name       text not null,
  is_system  boolean not null default false,
  created_at timestamptz not null default now(),
  constraint ux_roles_firm_name unique (firm_id, name),
  constraint ux_roles_id_firm unique (id, firm_id)      -- target for same-firm foreign keys
);

create table role_permissions (
  role_id         uuid not null,
  permission_code text not null references permissions(code),
  firm_id         uuid not null references firms(id),   -- copied for row-level security
  primary key (role_id, permission_code),
  foreign key (role_id, firm_id) references roles(id, firm_id) on delete cascade
);

create table users (
  id                 uuid primary key,
  firm_id            uuid not null references firms(id),
  name               text not null,
  email              citext not null,
  phone              text,
  password_hash      text,
  language           text,
  is_active          boolean not null default true,
  failed_login_count int not null default 0,
  lockout_until      timestamptz,
  last_login_at      timestamptz,
  created_at         timestamptz not null default now(),
  created_by         uuid,
  updated_at         timestamptz,
  updated_by         uuid,
  deleted_at         timestamptz,
  constraint ux_users_firm_email unique (firm_id, email),
  constraint ux_users_id_firm unique (id, firm_id)
);
create index ix_users_email on users (email);

-- composite keys: a link can only point at a user/role of the SAME firm
-- (foreign-key checks ignore row-level security, so this matters)
create table user_roles (
  user_id uuid not null,
  role_id uuid not null,
  firm_id uuid not null references firms(id),
  primary key (user_id, role_id),
  foreign key (user_id, firm_id) references users(id, firm_id) on delete cascade,
  foreign key (role_id, firm_id) references roles(id, firm_id) on delete cascade
);

alter table clients
  add constraint fk_clients_assigned_user
  foreign key (assigned_user_id, firm_id) references users(id, firm_id);

-- ---------------------------------------------------------------- sessions, MFA, invitations

create table refresh_tokens (
  id             uuid primary key,
  firm_id        uuid not null references firms(id),
  user_id        uuid not null,
  family_id      uuid not null,          -- all tokens from one login; reuse of an old one revokes the family
  token_hash     text not null,
  expires_at     timestamptz not null,
  revoked_at     timestamptz,
  revoked_reason text,
  replaced_by    uuid,
  created_ip     inet,
  user_agent     text,
  created_at     timestamptz not null default now(),
  constraint ux_refresh_tokens_hash unique (token_hash),
  foreign key (user_id, firm_id) references users(id, firm_id) on delete cascade
);
create index ix_refresh_tokens_family on refresh_tokens (family_id);

create table user_mfa (
  user_id             uuid primary key,
  firm_id             uuid not null references firms(id),
  secret_protected    text not null,     -- TOTP secret encrypted with ASP.NET Data Protection
  enabled_at          timestamptz,       -- null = set up started but not confirmed
  recovery_codes_hash text[] not null default '{}',
  last_used_step      bigint not null default 0,  -- stops a code being used twice
  created_at          timestamptz not null default now(),
  foreign key (user_id, firm_id) references users(id, firm_id) on delete cascade
);

create table invitations (
  id          uuid primary key,
  firm_id     uuid not null references firms(id),
  email       citext not null,
  name        text,
  role_id     uuid not null,
  token_hash  text not null,
  expires_at  timestamptz not null,
  accepted_at timestamptz,
  invited_by  uuid not null,
  created_at  timestamptz not null default now(),
  constraint ux_invitations_hash unique (token_hash),
  foreign key (role_id, firm_id) references roles(id, firm_id) on delete cascade,
  foreign key (invited_by, firm_id) references users(id, firm_id)
);

create table login_events (
  id         uuid primary key,
  firm_id    uuid references firms(id),   -- null when the email matched no user
  user_id    uuid,
  email      citext,
  succeeded  boolean not null,
  reason     text,                        -- ok, bad_password, locked, inactive, unknown_email, mfa_failed
  ip         inet,
  user_agent text,
  at         timestamptz not null default now()
);
create index ix_login_events_firm_at on login_events (firm_id, at desc);

-- ---------------------------------------------------------------- row-level security

do $$
declare t text;
begin
  foreach t in array array['roles','role_permissions','users','user_roles','refresh_tokens','user_mfa','invitations']
  loop
    execute format('alter table %I enable row level security', t);
    execute format(
      'create policy tenant_isolation on %I using (firm_id = app_current_firm()) with check (firm_id = app_current_firm())', t);
  end loop;
end $$;

alter table login_events enable row level security;
create policy tenant_read on login_events for select using (firm_id = app_current_firm());

grant select on permissions, firm_setup_templates to cadesk_app;
grant select, insert, update, delete on roles, role_permissions, users, user_roles,
  refresh_tokens, user_mfa, invitations to cadesk_app;
grant select on login_events to cadesk_app;   -- inserts only through auth_log_login()

-- ---------------------------------------------------------------- login helpers (SECURITY DEFINER)
-- Each returns only what its step needs. Owned by the owner role, so they bypass RLS;
-- search_path is pinned so they can't be hijacked.

create function auth_find_users(p_email citext)
returns table (user_id uuid, firm_id uuid, firm_name text, firm_status text, password_hash text,
               is_active boolean, lockout_until timestamptz, mfa_enabled boolean)
language sql stable security definer set search_path = public, pg_temp as $$
  select u.id, u.firm_id, f.name, f.status, u.password_hash, u.is_active, u.lockout_until,
         exists (select 1 from user_mfa m where m.user_id = u.id and m.enabled_at is not null)
  from users u
  join firms f on f.id = u.firm_id
  where u.email = p_email and u.deleted_at is null
  order by f.name
$$;

create function auth_find_refresh_token(p_hash text)
returns table (id uuid, firm_id uuid, user_id uuid, family_id uuid, expires_at timestamptz, revoked_at timestamptz)
language sql stable security definer set search_path = public, pg_temp as $$
  select id, firm_id, user_id, family_id, expires_at, revoked_at
  from refresh_tokens where token_hash = p_hash
$$;

create function auth_find_invitation(p_hash text)
returns table (id uuid, firm_id uuid, email citext, expires_at timestamptz, accepted_at timestamptz)
language sql stable security definer set search_path = public, pg_temp as $$
  select id, firm_id, email, expires_at, accepted_at
  from invitations where token_hash = p_hash
$$;

create function auth_log_login(p_firm_id uuid, p_user_id uuid, p_email citext, p_succeeded boolean,
                               p_reason text, p_ip inet, p_user_agent text)
returns void
language sql volatile security definer set search_path = public, pg_temp as $$
  insert into login_events (id, firm_id, user_id, email, succeeded, reason, ip, user_agent)
  values (gen_random_uuid(), p_firm_id, p_user_id, p_email, p_succeeded, p_reason, p_ip, left(p_user_agent, 300))
$$;

revoke all on function auth_find_users(citext), auth_find_refresh_token(text), auth_find_invitation(text),
  auth_log_login(uuid, uuid, citext, boolean, text, inet, text) from public;
grant execute on function auth_find_users(citext), auth_find_refresh_token(text), auth_find_invitation(text),
  auth_log_login(uuid, uuid, citext, boolean, text, inet, text) to cadesk_app;

-- ---------------------------------------------------------------- sign-up (owner connection only)

create function platform_create_firm(
  p_firm_id uuid, p_firm_name text, p_country char(2), p_template text,
  p_user_id uuid, p_user_name text, p_email citext, p_password_hash text)
returns void
language plpgsql as $$
declare
  c        countries%rowtype;
  tpl      jsonb;
  r        jsonb;
  v_role   uuid;
  v_owner  uuid;
begin
  select * into c from countries where code = upper(p_country);
  if not found then raise exception 'Unknown country %', p_country using errcode = '22023'; end if;

  select payload into tpl from firm_setup_templates where code = p_template and country_code = c.code;
  if tpl is null then raise exception 'No setup template % for %', p_template, c.code using errcode = '22023'; end if;

  insert into firms (id, name, country_code, data_region, base_currency, time_zone, plan_code, status, trial_ends_at)
  values (p_firm_id, p_firm_name, c.code, c.data_region, c.currency, c.default_time_zone, 'STARTER', 'trial',
          now() + interval '30 days');

  for r in select * from jsonb_array_elements(tpl -> 'roles') loop
    v_role := gen_random_uuid();
    insert into roles (id, firm_id, name, is_system)
    values (v_role, p_firm_id, r ->> 'name', coalesce((r ->> 'system')::boolean, false));

    if r -> 'permissions' ? '*' then
      insert into role_permissions (role_id, permission_code, firm_id)
      select v_role, code, p_firm_id from permissions;
      v_owner := coalesce(v_owner, v_role);
    else
      insert into role_permissions (role_id, permission_code, firm_id)
      select v_role, p.code, p_firm_id
      from jsonb_array_elements_text(r -> 'permissions') as x(code)
      join permissions p on p.code = x.code;
    end if;
  end loop;

  if v_owner is null then raise exception 'Template % has no full-access role', p_template; end if;

  insert into users (id, firm_id, name, email, password_hash)
  values (p_user_id, p_firm_id, p_user_name, p_email, p_password_hash);
  insert into user_roles (user_id, role_id, firm_id) values (p_user_id, v_owner, p_firm_id);
end $$;

revoke all on function platform_create_firm(uuid, text, char, text, uuid, text, citext, text) from public;
