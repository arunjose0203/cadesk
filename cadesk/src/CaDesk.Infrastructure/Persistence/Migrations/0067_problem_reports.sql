-- 0067_problem_reports.sql: Sprint 72, crash reports from the browser and "Report a problem".

create table if not exists problem_reports (
    id uuid primary key default gen_random_uuid(),
    firm_id uuid not null references firms(id) on delete cascade,
    user_id uuid references users(id) on delete set null,
    kind text not null check (kind in ('crash', 'feedback')),
    message text not null check (length(message) between 1 and 2000),
    page text check (page is null or length(page) <= 300),
    detail text check (detail is null or length(detail) <= 8000),
    user_agent text check (user_agent is null or length(user_agent) <= 300),
    app_version text check (app_version is null or length(app_version) <= 40),
    occurrences int not null default 1 check (occurrences >= 1),
    status text not null default 'open' check (status in ('open', 'resolved')),
    created_at timestamptz not null default now(),
    last_seen_at timestamptz not null default now()
);

alter table problem_reports enable row level security;

create policy problem_reports_tenant_isolation on problem_reports
    for all using (firm_id = app_current_firm());

create index if not exists idx_problem_reports_firm_seen on problem_reports (firm_id, last_seen_at desc);
