# CA Desk

Multi-tenant SaaS for CA and accounting firms (India first, GCC later).

| Sprint | Delivered | Done when |
| --- | --- | --- |
| 1 | Solution skeleton, PostgreSQL, firm isolation (two locks), Clients API, Docker, CI | Tenant isolation tests pass |
| 2 | Sign-up, login, lockout, refresh-token rotation, TOTP two-step sign-in, roles and permissions, invitations, login history | Auth tests pass; a firm signs up and invites staff |
| 3 | Settings engine: catalogue of 38 settings, six levels with locks and plan ceilings, validation, cache, change log + undo, encrypted secrets, Settings screen, personal preferences | A partner changes reminder days and sees inherited values |
| 4 | Audit log (every create/update/delete, append-only, monthly partitions) + viewer; two-step sign-in required by role; sign-out after inactivity; role editor | Every change is traceable; required roles can't work without two-step sign-in |
| 5 | Client module: auto client codes from settings, rules per country (entity types, required fields, PAN/GSTIN/TRN/VAT/CR formats + GSTIN check digit), custom fields, contacts, groups, notes, Excel/CSV import with error report | A firm imports its client list from Excel and fixes the rows that failed |
| 6 | Compliance calendar: due-date rules per country as data (GST, TDS, advance tax, ITR, tax audit; UAE VAT/CT; Saudi VAT/zakat), government extensions, holidays and weekends, per-client choices (QRMP, VAT frequency, quarter pattern), daily generation, filing status with acknowledgement | A client with a GSTIN shows its GST deadlines for the year, with holidays handled |
| 7 | Tasks: one per deadline (made 30 days ahead, assigned by the firm's rule, checklist from templates), ad-hoc tasks, review with separation of duties (approve / send back), deadline filed only on approval, comments and history, My tasks / To review / Workload screens, firm checklists | Managers see each person's load; staff work through checklists; nothing is filed until reviewed |
| 8 | Reminders: outbox with dedupe, client deadline reminders + overdue notice (email, WhatsApp templates), staff daily summary, review / sent-back emails, partner escalation, quiet hours, retries, firm message texts with preview, log screen | Clients get timely reminders, staff a daily summary; nothing is sent twice or at night |
| 9 | Document vault: per-client folders by year and filing, staff upload and client upload links (no account), allowed types and size from settings with content checks, ClamAV virus scan, trash, retention review and legal hold, documents linked to tasks, downloads audited | A client sends its bank statements with a link; staff find them under the right year and filing, attached to the task |
| 11 | Billing, Invoicing & Timesheets: CA fee billing, GST invoices, SAC codes, payment receipts, staff billable time, unbilled timesheets imported to invoices, multi-tenant isolation | Staff log billable hours; invoices calculate 18% GST; payments update balances to paid; Firm B cannot read or pay Firm A invoices |
| 12 | Digital Signature Certificates (DSC) & Token Management: Hardware USB token tracking, signer PAN validation, expiry countdowns & alerts, custody transfer audit trail (vault, staff, client, in-transit) | Register Class 3 token; status auto-calculated (active/expiring soon/expired); custody movement logged with handler user and remarks; strict firm isolation |
| 13 | Client Self-Service Portal & External Auth: Client portal user login, customer service requests / helpdesk tickets, filing status & ARN viewer, invoice balances, DSC tracking, staff ticket desk | Client users log in securely, view filings and invoices, submit inquiries with 2-way messaging; firm staff resolve tickets; strict multi-firm isolation |
| 14 | Direct Tax Computation & Regime Comparison: 5 heads of income, Old vs New 115BAC comparison, revised AY 2025-26 slab rates, Sec 87A rebate, surcharge, and Chapter VI-A deductions | Dual-regime side-by-side computation with recommended tax regime, automated savings quantification, and draft/approved lifecycle |
| 15 | Statutory Audit Workspace & Audit Working Papers: ICAI SA 200/300/320/500/530/700, automated materiality benchmarks, standard working paper templates, multi-tier review workflow | Preparer draft -> Manager review -> Partner sign-off with audit opinions; strict multi-tenant isolation |
| 16 | GST 2A / 2B Purchase Register Reconciliation: Section 16(2)(aa) & Rule 36(4) compliance, smart invoice number normalization, discrepancy categorization (exact, tolerance, tax mismatch, missing in 2B) | Auto-matching supplier invoices against GSTR-2B; ITC at risk flagged; multi-firm isolation |
| 17 | TDS Computation, Lower Deduction Certificates & Form 26Q/27Q: Chapter XVII-B withholding, Sec 206AA/206AB penalty rates, Sec 197 LDC tracking, ITNS 281 challans, Sec 201(1A) interest calculator | Monthly TDS calculation with challan FIFO mapping, quarterly 26Q return prep, and firm isolation |
| 18 | ROC / MCA Corporate Governance & Secretarial Compliance: CIN/DIN registry, DIR-3 KYC tracking, Section 403 statutory late fee engine (₹100/day AOC-4/MGT-7, ₹5,000 DIR-3 KYC), board resolutions repository | Company registry with DIN compliance countdown, automated late fee calculator, and secretarial resolutions register |
| 19 | Digital Engagement Letters & KYC Compliance: ICAI SA 210 standard audit terms generator, digital acceptance audit trail, PMLA risk profiling (Low/Medium/High/PEP), UBO (≥10%/≥15%) beneficial ownership | Engagement letter sent and digitally accepted by client; periodic KYC review cycle; strict multi-tenant isolation |
| 20 | Cash & Bank Feeds, Multi-Bank Statement Parser & BRS Auto-Matcher: Bank accounts master (Current, Savings, OD, CC), CSV/OFX statement parsing, auto-categorization heuristics & custom rules, transaction-to-ledger matching, statutory Bank Reconciliation Statement (BRS) calculation engine | Bank accounts registered; CSV statements parsed into debit/credit streams; rules auto-classify UPI/salary/charges; BRS report computes timing differences; strict multi-firm isolation |
| 21 | Practice MIS & Executive Reporting Desk: Executive KPI summary, Client profitability & margins, Staff billable utilization, Aging of receivables (0-30, 31-60, 61-90, 90+ days), Statutory compliance SLAs, FY target setting | Real-time practice financial KPIs, overdue recovery insights, staff capacity analytics, and client margin ranking; two-lock multi-tenancy |
| 22 | Tax Notice & Faceless Assessment Management Desk: CBDT/CBIC DIN verification (non-est notices), Faceless Video Conference & hearing appointments, Maker-checker written submission workflows with partner sign-off, Outstanding tax demand ledger & CIT(A) 30-day appeal countdowns | Statutory notice tracking, faceless hearing room management, partner-approved portal submissions, and stay/demand tracking; two-lock multi-tenancy |
| 23 | Payroll, Salary TDS & Statutory PF / ESIC Engine: Client employee master, Automated EPF (12% + EPS 8.33% / EPF 3.67% split), ESIC (0.75% / 3.25%), State Professional Tax (PT), Section 192 Salary TDS, Monthly salary register & EPFO ECR text export | Automated statutory wage ceilings, payslip generation, and official ECR file formatting; two-lock multi-tenancy |
| 24 | Automated E-Invoicing & E-Way Bill Integration Desk: Direct Invoice Registration Portal (IRP) simulation, 64-character SHA-256 IRN generation, digitally signed QR codes, Rule 138(10) E-Way Bill validity calculator, Part-B multi-modal vehicle updater, 24-hour statutory cancellation engine | Direct IRP schema generation, dynamic distance-based validity countdowns, vehicle transshipment updates, and multi-tenant firm isolation |
| 25 | Fixed Asset Register & Dual Depreciation Engine: Companies Act 2013 Schedule II (useful life, SLM/WDV, single/double/triple shift) vs. Income Tax Act Section 32 (block of assets, 180-day 50% rule, additional depr @ 20%), AS 22 / Ind AS 12 deferred tax engine, and physical verification audit trail | Side-by-side dual depreciation schedule, automated 180-day cutoff, DTA/DTL quantification, physical verification logs; two-lock multi-tenancy |
| 26 | Advance Tax Planner, Self-Assessment & Section 234A/B/C Interest Engine: Section 208 statutory ₹10,000 liability threshold, Section 211 quarterly milestones (15%, 45%, 75%, 100%) and Sec 44AD/ADA presumptive schedule, Section 234C installment deferment interest with safe harbors (12%, 36%), Section 234B shortfall (< 90%) interest from April 1, Section 234A late filing interest, ITNS 280 challan BSR tracking | Quarterly advance tax schedule with automatic deferment interest, safe harbor rules, 234A/B/C interest breakdowns, and ITNS 280 challan recording; two-lock multi-tenancy |
| 27 | GSTR-9 / 9C Annual Return & Audit Reconciliation Desk: Section 44 annual return compilation (Tables 4, 5, 6, 7, 8, 9, 10-14), turnover threshold gating (Exempt < ₹2 Cr, Mandatory GSTR-9 > ₹2 Cr, Mandatory GSTR-9 & 9C > ₹5 Cr), Table 5 gross turnover & Table 7 taxable turnover reconciliation, Table 8 ITC 2A/2B vs 3B difference detection, audit discrepancy manager with statutory reason codes, CA certification with UDIN, and Form GST DRC-03 voluntary payment tracking | Full-stack GSTR-9 annual return tables, interactive GSTR-9C gross & taxable reconciliation workbench, discrepancy log with auditor recommendations, UDIN certification, and DRC-03 electronic cash ledger payment recording; two-lock multi-tenancy |
| 28 | Digital Workpapers & Engagement Quality Review (EQR) Desk (ICAI SQC 1 / SA 220): 60-day archival lock, immutable workpaper freeze, 10-point SA pre-issuance quality checklist, EQR hot/cold reviews, multi-tier review notes (Manager, EQR, Lead Partner) with gate-checked sign-off | Full-stack digital workpapers with ICAI SQC 1 60-day archival deadline countdown, mandatory EQR clearance gating, review query resolution workflow, and two-lock multi-tenancy |
| 29 | Presumptive Taxation & Minimal Financials Generator (Section 44AD / 44ADA / 44AE): Enhanced 5% cash turnover gating (₹3 Cr / ₹75 L), 6%/8% and 50% deemed profit split, Section 44AE heavy/light vehicle fleet register, Section 44AD(4) 5-year lockout penalty engine, Section 44AB mandatory tax audit warnings, and Section 44AA minimal balance sheet particulars | Full-stack presumptive tax desk with digital/cash ratio checks, vehicle fleet manager, ITR-4 balance sheet coherence analyzer, and two-lock multi-tenancy |

## What's inside

```
CaDesk.slnx
├─ src/
│  ├─ CaDesk.Domain/          Entities: Firm, Client (+ contacts, groups, notes, custom fields), base types
│  ├─ CaDesk.Application/     ITenantContext, IAppDbContext, ClientService (use cases)
│  ├─ CaDesk.Infrastructure/  AppDbContext (tenant filter + stamping), RLS interceptor,
│  │                          SQL migrations + DbMigrator, PlatformDb
│  └─ CaDesk.Api/             Minimal API, JWT auth, TenantMiddleware, endpoints, Dockerfile
├─ tests/
│  ├─ CaDesk.UnitTests/         Tenant context, naming
│  └─ CaDesk.IntegrationTests/  Real API + real PostgreSQL: Firm A vs Firm B
├─ web/                       React + Vite + TypeScript (Sprint 1 test screen)
├─ deploy/                    db-init.sql (roles), Caddyfile
├─ docker-compose.yml
└─ .github/workflows/ci.yml
```

## How firm data is kept apart (two locks)

1. **EF Core global query filter** — every `ITenantOwned` table is filtered to the firm in the
   token; new rows get the firm stamped; changing another firm's row throws.
2. **PostgreSQL row-level security** — the API connects as `cadesk_app`, which is not a table owner.
   On every connection open, `TenantConnectionInterceptor` runs
   `set_config('app.firm_id', <firm>)`. Policies only show rows for that firm. With no firm set,
   nothing is visible. Even hand-written SQL cannot cross firms.

Migrations and firm sign-up use the owner role `cadesk_owner` (via `DbMigrator` and `PlatformDb`) — keep
that connection string away from anything firm users can reach.

## Prerequisites

- .NET 10 SDK
- Node.js 22
- PostgreSQL 16 or 17 (local install or Docker)

## Run locally (without Docker for the API)

```bash
# 1. Database roles + databases (once). With Docker:
docker compose up -d db          # runs deploy/db-init.sql on first start
#    Or with a local PostgreSQL:
psql -U postgres -f deploy/db-init.sql

# 2. API (applies migrations on start) -> http://localhost:5080
dotnet run --project src/CaDesk.Api

# 3. Web -> http://localhost:5173
cd web && npm install && npm run dev
```

Open http://localhost:5173 → **Create a firm** → you're the Partner. Then:
- **Users** → invite a team member → open the invite link in a private window → they join as Staff.
- **My security** → set up two-step sign-in with an authenticator app.
- Create a second firm in another browser: each sees only its own clients and users.

## Run the tests

```bash
dotnet test CaDesk.slnx
```

Integration tests use the `cadesk_test` database created by `deploy/db-init.sql`.
To point them elsewhere set `CADESK_TEST_APP_CS` and `CADESK_TEST_OWNER_CS`.

## Run everything in Docker

```bash
docker compose up --build
# open https://localhost  (Caddy uses a local certificate; accept it in the browser)
```

On a server: set `SITE_ADDRESS=app.yourdomain.com`, `API_ENVIRONMENT=Production` and real passwords /
`JWT_SIGNING_KEY` in a `.env` file. Caddy then gets the HTTPS certificate automatically.
Change the role passwords in `deploy/db-init.sql` before the first start on a server, and remove the
`5432` port mapping.

## API (Sprint 1)

| Method | Route | Notes |
| --- | --- | --- |
| GET | /api/health | Anonymous |
| GET | /api/health/db | Anonymous |
| POST | /api/auth/signup | New firm + first user (Partner). Rate limited |
| POST | /api/auth/login | Returns `ok`, `mfa_required` (+ `mfaToken`) or `choose_firm` |
| POST | /api/auth/mfa/verify | Code from authenticator app or a recovery code |
| POST | /api/auth/refresh | Uses the `cadesk_rt` HttpOnly cookie; rotates it |
| POST | /api/auth/logout | Ends the session family |
| POST | /api/auth/accept-invite | Join a firm from an invitation |
| GET | /api/auth/me | Profile, roles, permissions |
| POST | /api/auth/password | Change password; signs out other sessions, returns a new one |
| POST | /api/auth/mfa/setup · /enable · /disable | Two-step sign-in |
| GET / PUT | /api/users · /api/users/{id} | `users.read` / `users.manage` |
| GET / POST / PUT / DELETE | /api/roles | `users.read` / `roles.manage` (built-in roles are fixed) |
| GET | /api/permissions | `roles.manage` |
| GET / POST / DELETE | /api/invitations | `users.manage` |
| GET | /api/login-events | `audit.read` |
| GET / POST / PUT / DELETE | /api/clients | `clients.read` / `clients.write` / `clients.delete` |

## Adding a migration

Add the next file in `src/CaDesk.Infrastructure/Persistence/Migrations/`, e.g. `0002_users.sql`.
Files run once each, in name order, as `cadesk_owner`. For every new firm-owned table:

```sql
alter table <t> enable row level security;
create policy tenant_isolation on <t>
  using (firm_id = app_current_firm()) with check (firm_id = app_current_firm());
grant select, insert, update, delete on <t> to cadesk_app;
```

…and make the entity implement `ITenantOwned` (usually by inheriting `TenantEntity`).

## How sign-in works (Sprint 2)

- **Passwords:** ASP.NET Core Identity's `PasswordHasher` (PBKDF2), 10+ characters, rehashed when the algorithm improves.
- **Lockout:** 5 wrong attempts → 15 minutes. Counted with one atomic SQL update, so parallel guesses can't skip it.
  Every failure (unknown email, wrong password, locked) returns the same 401, so emails can't be discovered.
- **Sessions:** 15-minute access token (JWT, kept in memory by the web app) + 14-day refresh token in an
  HttpOnly, Secure, SameSite=Strict cookie scoped to `/api/auth`. Each refresh rotates the token inside a
  transaction; replaying an old token ends the whole session family (theft detection).
- **Two-step sign-in:** TOTP (RFC 6238) with replay protection + 8 single-use recovery codes. Secrets are encrypted
  with ASP.NET Data Protection; **set `DataProtection:KeysPath` and back up that folder** (Docker: `dpkeys` volume).
- **Roles:** permissions are fixed in code; roles are firm data, seeded from the `IN_CA_FIRM` template
  (Partner, Manager, Staff, Article). Permission changes reach users at their next refresh (≤ 15 minutes).
- **No escalation:** you can only grant, assign or invite with permissions you hold, and can't change a user who has
  more access than you. A firm always keeps at least one active Partner.
- **Login before the firm is known:** narrow `SECURITY DEFINER` SQL functions (`auth_*`) let the restricted app role look up
  only what sign-in needs. Composite foreign keys stop any row linking to another firm's user or role.
- **Rate limits:** sign-in endpoints, `/password` and `/mfa/disable`: 20 per minute per IP (`RateLimits:AuthPerMinute`).
  Behind Caddy, set `ForwardedHeaders:KnownNetworks` so real client IPs are used (Docker Compose does this).

Invitation emails arrive with notifications (Sprint 8); until then the Partner copies the invite link.

## How settings work (Sprint 3)

- **Catalogue** (`config_definitions`): each setting has a type, validation rules (a small JSON Schema subset), a default,
  and the most specific level it may be set at. 38 settings today; each later module adds its own in its migration.
- **Six levels**: platform → country → plan → firm → client → user. The most specific value wins. A **locked** value stops
  every level below it (e.g. "staff review AI output" is locked on; data region is fixed per country). A firm can lock its
  own value so clients can't differ. Plan values can act as **ceilings** (AI pages per month).
- **Government rules live here as data**: country defaults (India: GST 18% on fees, April financial year; UAE: VAT 5%,
  Mon–Fri; Saudi: VAT 15%, Sun–Thu) are rows, not code. Changing them is a data change by the platform team.
- **Use in code**: `await settings.GetAsync(Keys.ReminderDays, clientId)`. Values are cached per firm; a database trigger bumps
  `config_version` on every change, so every server sees it on the next request.
- **Change log**: every change records old/new value, lock state, who and why; any change can be undone.
- **Secrets**: API keys are encrypted (Data Protection, per-firm purpose) and write-only; settings refer to them by name.
- **Security**: firms can only write firm/client/user levels for themselves (enforced in code and by row-level security);
  users can only change their own personal preferences.

API: `GET /api/settings`, `PUT /api/settings/{key}` (body: `value`, `scope` firm|client, `scopeId`, `lockForLowerLevels`, `reason`),
`DELETE /api/settings/{key}`, `GET /api/settings/{key}/explain`, `GET /api/settings/changes`, `POST /api/settings/changes/{id}/revert`,
`/api/settings/secrets`, `/api/me/settings` (personal preferences). All need `settings.manage` except `/api/me/settings`.

Not yet done: a platform console for platform/country/plan values (today they're seeded by migrations), enforcing
`security.session_timeout_min` and `security.mfa_required_roles` in the web app (planned with Sprint 4).

## Audit log and security policies (Sprint 4)

- **What's recorded:** every create, update and delete saved through EF, written in the same transaction as the change
  (`AuditInterceptor`). Updates keep only the fields that changed. Soft deletes are shown as deletes. Bulk deletes
  (revoking an invitation, deleting a secret) are recorded explicitly in the same transaction.
- **Never recorded in clear:** password hashes, token hashes, TOTP secrets, recovery codes, secret ciphertext (shown as `***`).
  Sign-in bookkeeping (last login, failed counts) is left out; sign-ins are in `login_events`.
- **Append-only:** the app role can insert and read its own firm's rows, never update or delete them.
- **Partitions:** one per UTC month, created up to 3 months ahead at start-up and daily by `AuditPartitionJob`.
  If rows ever land in the default partition, they're moved when that month's partition is created.
- **Viewer:** `GET /api/audit-logs?entity=&entityId=&actorId=&action=&from=&to=&before=&take=` (`audit.read`), newest first.
- **Two-step sign-in required by role** (`security.mfa_required_roles`): the user's token only allows setting it up until
  they do; the web app shows just the set-up screen. Already-issued tokens follow the rule within 15 minutes.
- **Sign-out after inactivity** (`security.session_timeout_min`): the web app signs out after that many idle minutes; the
  server refuses to refresh an idle session (never less than 20 minutes server-side, counted from the last refresh).
- **Role editor:** create and change custom roles; you can only grant permissions you hold.

## Client module (Sprint 5)

- **Client codes**: leave Code empty and the next number comes from `client.code_format` (e.g. `C{SEQ:4}` → C0001,
  `KL/{FY}/{SEQ:3}` → KL/2026-27/001). `{FY}`, `{YYYY}`/`{YY}` or `{MM}` in the format restart numbering each year or month.
  Numbers come from `next_number()` in PostgreSQL, so parallel saves never share one; hand-typed codes are skipped.
- **Rules per country, as data**: entity types and required fields are settings (India: PAN required; UAE: TRN; Saudi: CR).
  Tax ID formats live in `tax_id_types` (PAN, GSTIN with its check digit, TAN, TRN, VAT, CR). A GSTIN must contain the
  client's PAN. IDs marked unique (GSTIN, CR) can't repeat within a firm; a repeated PAN only warns (proprietor + business).
- **Custom fields**: Settings → Clients. Text (optional format rule), number, date, dropdown, yes/no; can be required.
  Key and type are fixed once created; switch a field off instead of deleting it (values are kept).
- **Contacts** (one primary per client), **groups** (family / business group; only empty groups can be deleted) and **notes**
  (only the author can delete).
- **Import**: Clients → Import from Excel. Download the template (Excel or CSV, includes your tax IDs and custom fields and a
  Help sheet), upload, and check: every row is validated with the same rules as the form, plus duplicates inside the file.
  Nothing is saved until you press Import; then all good rows are saved in one transaction (all or nothing), missing groups
  are created, and "Assigned to" is matched by email. Up to 2,000 rows / 5 MB per file. Download the error report, fix, re-import.

API: `GET /api/clients?search=&groupId=&entityType=&assignedUserId=&active=&taxId=&skip=&take=` (returns `{items,total}`),
`GET /api/clients/form`, `/api/clients/{id}/contacts`, `/api/clients/{id}/notes`, `/api/client-groups`,
`/api/custom-fields` (changes need `settings.manage`), `/api/tax-id-types`,
`GET /api/clients/import/template?format=csv|xlsx`, `POST /api/clients/import/preview`, `POST /api/clients/import/commit?skipInvalid=true`
(multipart field `file`).

## Compliance calendar (Sprint 6)

- **Rules are data** (`compliance_types`, platform-managed): frequency, due day (e.g. GSTR-3B: 20th of next month; TDS: 7th,
  March by 30 April; TDS returns: 31st after the quarter, Q4 by 31 May; ITR: 31 July / 31 August / 31 October; UAE VAT: 28th;
  Saudi zakat: 120 days), whether a due date moves off weekends/public holidays, and who it applies to (has GSTIN / TAN / TRN,
  entity types). **The seeded dates are as known in Nov 2026 — the platform team must keep them current** with CBIC/CBDT/FTA/ZATCA
  notifications. A government extension is one row in `compliance_extensions`; open deadlines follow it on the next run.
- **Annual returns use the law's tax year** per country (India April–March), not the firm's books; a client can override where
  the law allows (UAE corporate tax year).
- **Per client**: Clients → a client → Compliance. Automatic filings come from the rules; tick/untick to override (e.g. switch to
  QRMP quarterly GST), choose VAT frequency and the quarter pattern (UAE staggered periods). "Use rule" removes the override.
- **Dates on each deadline**: statutory date, the due date after weekend/holiday moves, and the firm's internal target
  (`compliance.internal_due_offset_days` before, moved to an office working day; office holidays in Settings → Deadlines).
- **Generation**: when a client is saved or imported, when choices or office holidays change, from `POST /api/deadlines/generate`,
  and daily for every firm (`DeadlineJob`). Window: 31 days back to `compliance.generate_ahead_days` ahead (default 365).
  Filed deadlines are never changed; untouched future ones are removed when they stop applying. One run per firm at a time
  (advisory lock). Generated deadlines aren't audited row by row; people's changes to them are.
- **Deadlines screen**: overdue / due this week / open / filed this month, filters, start, mark filed (date + ARN), not needed.

API: `GET /api/deadlines?from=&to=&clientId=&type=&status=open|overdue|pending|in_progress|filed|not_applicable&assignedUserId=`,
`PUT /api/deadlines/{id}`, `POST /api/deadlines/generate`, `GET|PUT|DELETE /api/clients/{id}/compliances[/{code}]`,
`GET /api/compliance-types`, `GET|POST|DELETE /api/holidays`. Read needs `compliance.read`; changes `compliance.manage`;
office holidays `settings.manage`.

Known limits: only fixed-date public holidays are seeded (festival dates must be added per year); turnover-based rules (e.g. GSTR-9
optional below ₹2 crore, QRMP eligibility) are left to the firm's per-client choice; holiday changes regenerate in the request.

## Tasks and review (Sprint 7)

- **From deadlines**: when a deadline comes within `task.create_ahead_days` (default 30), it gets one task: title, client,
  aim date = the deadline's internal target, checklist from the firm's own template for that filing (or the platform default),
  assignee by `task.auto_assign_rule` (client's staff / least-loaded person / unassigned). Dates and the review rule follow
  the deadline and settings on every run. "Work on it as a task" on a deadline makes its task early.
- **Flow**: To do → In progress → (In review → Done or Sent back) — or straight to Done when review isn't required.
  Every checklist step must be ticked (remove steps that don't apply). Deadline tasks record the filing date and ARN, which
  are written to the deadline **only when the work is approved**.
- **Review rules** (`task.review_required`, firm or client level): only people with the new `tasks.review` permission
  (Partner, Manager) approve; nobody approves their own work while another reviewer exists (a sole reviewer can); sending back
  needs a note; a filing can't be marked filed on the deadline while review is required; review can't be switched off on a
  filing's task. Changing a deadline through its task also needs `compliance.manage`.
- **Ad-hoc tasks** (notices, follow-ups) with their own checklist, priority and optional review.
- **Screens**: Tasks → My tasks / To review / All tasks / Workload (open, overdue, due in 7 days, waiting for review, can review
  per person). Settings → Tasks: edit checklists per filing type.

API: `GET /api/tasks?assignee=me|unassigned|all|<id>&toReview=&clientId=&status=&dueTo=&search=`, `GET /api/tasks/workload`,
`GET|POST|PUT /api/tasks[/{id}]`, `POST /api/tasks/{id}/start|complete|review|reopen|cancel`, `/api/tasks/{id}/checklist[/{itemId}]`,
`POST /api/tasks/{id}/comments`, `GET|PUT|DELETE /api/task-templates[/{typeCode}]`, `POST /api/deadlines/{id}/task`.

## Reminders (Sprint 8)

- **Outbox**: every message is a row in `notifications` with a unique dedupe key, rendered when queued, so the log shows exactly what
  was sent and planning can run any number of times without sending twice. `NotificationJob` plans and sends every 5 minutes;
  Settings → Reminders → "Send due reminders now" does the same at once. Sending claims rows with `FOR UPDATE SKIP LOCKED`
  (safe with several servers), saves after each message, retries temporary failures (2, 4, 8, 16 min; 5 attempts).
- **Clients**: for each open deadline, the latest reached offset of `reminder.schedule_days` (e.g. 7/3/0 days before) — missed days
  are not all sent at once; one "overdue" notice within a week of the due date. Sent to contacts with "receives reminders" (or the
  client's own email/phone when it has no contacts) on `reminder.channels`; held during `reminder.quiet_start`–`quiet_end`
  (client time zone settings). Not sent when the client is switched off (`reminder.client_enabled`), the work is already in review,
  or — checked again just before sending — the deadline was filed meanwhile.
- **Staff**: daily task summary at `reminder.staff_digest_time` (each user can switch it off in My account); "please review" to a
  named reviewer; "sent back" to the person who did the work; partners get an escalation when a deadline is
  `reminder.escalate_after_days` overdue.
- **Message texts**: platform defaults; a firm can edit subject/body per message with allowed placeholders only, and preview.
- **Channels** (configuration `Notifications:*`; Docker env `EMAIL_MODE`, `SMTP_HOST`, `SMTP_USER`, `SMTP_PASSWORD`, `EMAIL_FROM`, `WHATSAPP_MODE`):
  - Email: `Smtp` (any SMTP relay, e.g. Amazon SES, Zoho) or `Log` (default — nothing is sent, only logged). The firm's name is the
    sender name; `email.reply_to` sends client replies to the firm. Set up SPF/DKIM for the sending domain.
  - WhatsApp: `Cloud` (WhatsApp Business Cloud API) or `Log`. Each firm adds its phone number ID (`whatsapp.phone_number_id`), its token
    as a secret (`whatsapp.credentials_ref`), and the names of its Meta-approved templates. Without that, WhatsApp messages are
    "skipped" with the reason shown.
  - SMS: not available yet.

API: `GET /api/notifications` (log, `settings.manage`), `POST /api/notifications/{id}/retry|cancel`, `POST /api/notifications/test-email`,
`POST /api/reminders/run`, `GET /api/clients/{id}/notifications` (what the client received; `clients.read`),
`GET|PUT|DELETE /api/notification-templates[/{key}/{channel}]`, `POST /api/notification-templates/{key}/preview`.

## Document vault (Sprint 9)

- **Folders**: each client has a **Permanent** folder (KYC, deeds, registrations; no year) and, per financial year (firm's
  `firm.fiscal_year_start_month`, e.g. 2026-27), general folders (`document_folders`: bank statements, sales, purchases,
  books, notices, letters, other) plus one folder per filing (`compliance_types`, e.g. GSTR-3B). Clients → a client → Documents.
- **Checks on every file** (staff and client uploads alike): extension in `doc.allowed_types` **and** content matching it
  (PDF/JPEG/PNG/XLS signatures, real .xlsx/.docx packages, CSV that is text); Office files with macros are refused; size up to
  `doc.max_size_mb`; the same file twice for one client is refused. File names lose any folder part and unsafe characters.
- **Virus scan**: `Documents:Scan:Mode = ClamAv` streams each file to clamd (`Host`, `Port`) before it's stored; a virus → refused.
  If clamd doesn't answer, the file is stored as **waiting for virus scan** and can't be downloaded until `DocumentJob`
  (every 10 minutes) or "Scan now" scans it; a virus found then removes the file and shows why. `None` stores files as
  "not scanned" (a warning is printed outside Development). Keep clamd's `StreamMaxLength` (25 MB by default) ≥ `doc.max_size_mb`.
- **Storage**: bytes go to `Documents:StoragePath` (Docker: the `documents` volume) under `firm/yyyy/MM/id` — never a name a user
  typed. **Back this folder up together with the database.** Downloads are always attachments with `nosniff` and a sandbox CSP,
  and each one is written to the audit log.
- **Trash and retention**: Delete (`documents.delete`) moves a file to the trash; it can be restored for `doc.trash_days` (30), then
  the job removes the file. Files older than `doc.retention_years` after the end of their year (permanent folders: after the client
  is deleted) are **listed** in Settings → Documents → Past retention for a person to remove — never removed automatically.
  **Legal hold** stops deletion and retention removal. Removed files keep their row (name, hash, who, when) as a record.
- **Client upload links**: on a client's Documents (or a task: "Ask client for files"), make a link for a folder/year or a task;
  it works for `doc.upload_link_days` (14) or the days chosen, up to a number of files, until revoked, and only while
  `doc.client_upload_enabled` is on for that client. The token is shown once and only its hash is stored; the client opens
  `/upload/<token>`, sees the firm, what's asked and the limits, and uploads without an account. The link is looked up with
  `portal_find_upload_link()` (SECURITY DEFINER, one row by hash); everything after runs inside that firm under both isolation
  locks, and the audit log records the actor as `client_user`. Rate limited (`RateLimits:PortalPerMinute`, 60 per IP).
- **Tasks**: a task's Documents section uploads into its client's folder — a deadline's task files into that filing, period and year —
  or links files already in the vault (same client only), and records it in the task's history. What a client sends with a task's link
  is linked automatically. Documents can be added while the task is open or in review and removed only while it's being worked on.

API: `GET /api/clients/{id}/documents/vault` (tree, folder choices, limits), `GET /api/clients/{id}/documents?fy=|permanent&folder=&typeCode=&search=&trash=`,
`POST /api/clients/{id}/documents` (multipart `file` + `folder` | `typeCode`, `fy`, `name`, `notes`, `taskId`), `GET|PUT /api/documents/{id}`,
`GET /api/documents/{id}/download`, `DELETE /api/documents/{id}`, `POST /api/documents/{id}/restore|rescan`, `PUT /api/documents/{id}/hold`,
`GET /api/documents/retention`, `POST /api/documents/retention/purge`, `POST /api/documents/maintenance` (`settings.manage`),
`GET|POST /api/tasks/{id}/documents`, `DELETE /api/tasks/{id}/documents/{documentId}`, `POST /api/tasks/{id}/documents/upload`,
`GET|POST /api/clients/{id}/upload-links`, `POST /api/upload-links/{id}/revoke`, anonymous `GET|POST /api/portal/uploads/{token}`.
Read needs `documents.read`; upload, move and links `documents.upload`; delete, restore, hold and retention `documents.delete`.

Not yet done: emailing an upload link to the client's contacts from the app (copy it into email/WhatsApp for now), previews in the
browser, object storage (S3-compatible) instead of a disk folder, encrypting files at rest beyond the disk's own encryption.

## Billing, Invoicing & Timesheets (Sprint 11)

- **Timesheets**: Staff track billable and non-billable client and compliance work (hours 0.01–24, hourly rates, client and task links).
  Unbilled entries can be imported directly into invoice line items. Once billed, entries are locked from modification or deletion.
- **Invoices**: Statutory CA fee invoices with sequential numbering (`billing.invoice_prefix`, default `INV/{FY}/{SEQ:4}` via atomic `next_number`),
  HSN/SAC codes (`9982` default), multi-rate GST computation (18% CGST/SGST/IGST), issue and due dates, draft/sent/partially_paid/paid/cancelled lifecycle.
- **Payments**: Track payments received against invoices (Bank Transfer, UPI, Cheque, Cash, Card, Other with UTR/cheque references).
  Automatically maintains `paid_amount`, computes real-time `balance_due`, and updates invoice status to `partially_paid` or `paid`.
- **Practice Analytics**: Real-time KPI tiles for outstanding receivables, overdue balances, monthly billed amounts, and unbilled work-in-progress (WIP).
- **Two Locks**: Full multi-tenant isolation enforced by EF Core global query filters and PostgreSQL Row Level Security (`0010_billing_and_timesheets.sql`).

API:
- `GET /api/invoices?status=&clientId=&from=&to=&skip=&take=`, `GET /api/invoices/{id}`, `GET /api/invoices/summary` (`billing.read`)
- `POST /api/invoices`, `PUT /api/invoices/{id}`, `PUT /api/invoices/{id}/status`, `DELETE /api/invoices/{id}`, `POST /api/invoices/{id}/payments` (`billing.manage`)
- `GET /api/timesheets?userId=&clientId=&taskId=&from=&to=&unbilledOnly=`, `GET /api/timesheets/summary` (`FirmMember`)
- `POST /api/timesheets`, `PUT /api/timesheets/{id}`, `DELETE /api/timesheets/{id}` (`FirmMember` / `timesheets.manage`)

## Digital Signature Certificates & Token Custody (Sprint 12)

- **DSC Register**: Custody tracking for physical USB e-Pass/mToken dongles (Class 3 Signing & Encryption).
  Tracks Certifying Authorities (eMudhra, Capricorn, VSign, Pantasign, Sify), issuing dates, expiry alerts, and hardware serials.
- **Custody Chain**: Movement logs on every handover between client, staff custodian, and safe vault storage.
- **Expiry Radar**: Automated classification into Active, Expiring Soon (configurable warning threshold, default 30 days), and Expired.
- **Two Locks**: Row Level Security and EF Core query filtering (`0011_dsc_management.sql`).

API:
- `GET /api/dsc`, `GET /api/dsc/{id}`, `GET /api/dsc/summary` (`dsc.read`)
- `POST /api/dsc`, `PUT /api/dsc/{id}`, `POST /api/dsc/{id}/custody`, `DELETE /api/dsc/{id}` (`dsc.manage`)

## Client Self-Service Portal & Service Desk (Sprint 13)

- **External Client Auth**: Secure external client logins via PBKDF2 salted passwords and `auth_find_portal_user` security definer.
- **Service Desk Tickets**: Client issue submissions, categorized priorities (Urgent, High, Medium, Low), sequential numbering (`REQ/{YYYY}/{SEQ:4}`).
- **Interactive Ticket Threads**: Two-way client/staff discussions with document attachments and resolution workflow.
- **Client 360 View**: Direct client visibility into statutory filings, invoices, and in-custody DSC tokens.
- **Two Locks**: Isolation on client portal entities with RLS policies (`0012_client_portal.sql`).

API:
- `POST /api/portal/auth/login`, `GET /api/portal/auth/me`, `GET /api/portal/dashboard`
- `GET /api/portal/filings`, `GET /api/portal/invoices`, `GET /api/portal/dsc`, `GET|POST /api/portal/requests`
- `GET /api/service-requests`, `PUT /api/service-requests/{id}/status` (`service_requests.manage`)

## Direct Tax Computation & Regime Comparison (Sprint 14)

- **5 Heads of Income**: Salary (Standard deduction ₹75k New vs ₹50k Old, HRA Sec 10(13A), Professional Tax), House Property (Sec 24(b) loan interest), Business/PGBP, Capital Gains (STCG 111A, LTCG 112A), and Other Sources (Savings/FD interest, Dividends).
- **Comparative Regime Simulator**: Side-by-side Old vs New Regime (Sec 115BAC) comparative evaluation with automatic recommendation and quantified tax savings.
- **Statutory Computations**: Revised AY 2025-26 slab rates, Sec 87A rebate (₹7L threshold New vs ₹5L Old), Surcharge slabs, 4% Health & Education Cess, and Chapter VI-A deductions (80C, 80CCD(1B), 80D, 80E, 80G, 80TTA).
- **Computation History**: Draft, Computed, Approved, and Filed lifecycle with full audit trail and multi-tenant RLS (`0013_tax_computations.sql`).

API:
- `POST /api/tax/preview`, `GET /api/tax/computations`, `GET /api/tax/computations/{id}`, `POST /api/tax/computations`, `PUT /api/tax/computations/{id}/status`, `DELETE /api/tax/computations/{id}` (`computations.run`, `computations.approve`)

## Statutory Audit Workspace & Audit Working Papers (Sprint 15)

- **ICAI Standards on Auditing**: Conforms to SA 200 (Overall Objectives), SA 300 (Audit Planning), SA 315 (Risk Assessment), SA 320 (Materiality), SA 500 (Audit Evidence), SA 505 (External Confirmations), SA 530 (Audit Sampling), and SA 700/705 (Audit Opinion).
- **Automated Materiality Benchmarking (SA 320 / SA 450)**: Live calculation of Overall Materiality (Revenue 1%, PBT 5%, Total Assets 1%, Equity 2%), Performance Materiality (75%), and Clearly Trivial Threshold (5%).
- **Standard Working Paper Provisioning**: Automated seeding of standard working paper templates for every new engagement (`WP/BNK/01` Bank BRS, `WP/REV/01` Sales Cut-off, `WP/PUR/01` Purchase vs GSTR-2B, `WP/AST/01` Fixed Assets Additions, `WP/TAX/01` Statutory Dues).
- **Multi-Tier Review Workflow**: Preparer draft -> Ready for Review -> Manager Review & Notes -> Partner Sign-off & Audit Opinion issuance (`unmodified`, `qualified`, `adverse`, `disclaimer`).
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security (`0014_statutory_audit.sql`).

API:
- `GET /api/audit/summary`, `GET /api/audit/engagements`, `GET /api/audit/engagements/{id}`, `POST /api/audit/engagements`, `PUT /api/audit/engagements/{id}/materiality`, `PUT /api/audit/engagements/{id}/status`, `DELETE /api/audit/engagements/{id}` (`audit.read`, `audit.manage`, `audit.review`)
- `GET /api/audit/engagements/{id}/working-papers`, `GET /api/audit/working-papers/{id}`, `POST /api/audit/engagements/{id}/working-papers`, `PUT /api/audit/working-papers/{id}`, `PUT /api/audit/working-papers/{id}/review`, `DELETE /api/audit/working-papers/{id}` (`audit.read`, `audit.manage`, `audit.review`)

## GST 2A / 2B Purchase Register Reconciliation (Sprint 16)

- **Statutory ITC Matching**: Automates Section 16(2)(aa) and Rule 36(4) compliance, preventing wrongful credit claims and penalty notices.
- **Smart Invoice Number Normalization**: Strips separators, hyphens, slashes, and leading zeroes from numeric groups (e.g. `SIEM/24-25/001` matches `SIEM-24-25-1`, `0042` matches `42`).
- **Discrepancy Categorization**:
  - `Exact Match`: 0 difference in invoice number, taxable amount, and GST values.
  - `Match With Tolerance`: Tax or taxable difference within configurable rounding threshold (default ±₹1.00).
  - `Tax Mismatch`: Same GSTIN & invoice, but tax values exceed tolerance.
  - `Missing in 2B (ITC at Risk)`: Present in books but unfiled by supplier in GSTR-1/IFF.
  - `Missing in Books`: Present in GSTR-2B but unbooked by client accountant.
- **Two Locks**: Full tenant isolation via RLS and EF Core global query filters (`0015_gst_reconciliation.sql`).

API:
- `GET /api/gst/summary`, `GET /api/gst/batches`, `GET /api/gst/batches/{id}`, `POST /api/gst/reconcile`, `GET /api/gst/batches/{id}/items`, `DELETE /api/gst/batches/{id}` (`gst.read`, `gst.reconcile`)

## TDS Computation, Lower Deduction Certificates & Form 26Q/27Q Preparation (Sprint 17)

- **Chapter XVII-B Statutory Tax Deduction Engine**: Supports sections 194C (1% individual, 2% company), 194J (2% technical, 10% professional), 194I (10% land/building, 2% plant/machinery), 194Q (0.1%), 194A (10%), 194H (5%), and 195 (payments to non-residents).
- **Anti-Tax Evasion Penalty Rates**:
  - **Section 206AA**: Automatic 20% flat withholding when deductee PAN is invalid or missing.
  - **Section 206AB**: Higher rate (double of standard rate or 5%) for specified non-filers.
- **Section 197 / 197A Lower Deduction Certificates (LDC)**: Tracks approved certificate numbers, validity windows, specified rates, and cumulative threshold utilization with automatic balance exhaustion warnings.
- **ITNS 281 Challan Management & Deposit Allocation**: Monthly Challan BSR code, serial number, and tender date registration; FIFO or manual allocation of deductions against deposited tax with unallocated balance accounting.
- **Section 201(1A) Statutory Interest Calculator**:
  - `Delay in Deduction`: 1% per month or part of a month from transaction date to deduction date u/s 201(1A)(i).
  - `Delay in Deposit`: 1.5% per month or part of a month from deduction date to actual bank deposit date u/s 201(1A)(ii).
- **Two Locks**: EF Core tenant filtering and PostgreSQL Row Level Security (`0016_tds_management.sql`).

API:
- `GET /api/tds/summary`, `GET /api/tds/ldc`, `POST /api/tds/ldc`, `GET /api/tds/challans`, `POST /api/tds/challans`, `GET /api/tds/deductions`, `POST /api/tds/deductions`, `POST /api/tds/challans/map`, `POST /api/tds/interest/calculate` (`tds.read`, `tds.manage`)

## ROC / MCA Corporate Governance & Secretarial Compliance (Sprint 18)

- **Company Master & Entity Registry**: Validates 21-character Corporate Identification Numbers (CIN regex `^[LU][0-9]{5}[A-Z]{2}[0-9]{4}[A-Z]{3}[0-9]{6}$`), tracks company class (Private, Public, OPC, Section 8, LLP), authorized & paid-up capital, RoC jurisdiction, and AGM calendar.
- **Director DIN & DIR-3 KYC Management**: 8-digit Director Identification Number (DIN) tracking, appointment/cessation dates, DSC expiry dates, and annual DIR-3 KYC status (`Complied`, `Pending`, `DeactivatedDueToNonFiling`).
- **Section 403 Statutory Late Fee Engine**:
  - **Annual Returns (AOC-4 & MGT-7)**: Section 403(1) second proviso calculation of ₹100 per day of delay without upper limit.
  - **DIR-3 KYC**: Rule 12A flat statutory additional fee of ₹5,000 per DIN for post-due-date filing.
  - **Event Forms (DIR-12, MGT-14, ADT-1, DPT-3, MSME-1)**: Slab-based multiplier of normal filing fee (2x up to 30 days, 4x up to 60 days, 6x up to 90 days, 10x up to 180 days, 12x beyond 180 days).
  - **Capital-based Normal Filing Fees**: Graded fee tiers based on authorized / paid-up capital.
- **Board & Committee Resolutions Register**: Complete secretarial repository for Board Meetings, AGMs, EGMs, and Audit Committees with resolution numbering, unanimous/special classification, verbiage preview, and signatory DIN tracking.
- **Two Locks**: Database schema and RLS policies on `company_master`, `director_din_records`, `mca_annual_filings`, and `board_resolutions` (`0017_mca_compliance.sql`).

API:
- `GET /api/mca/summary`, `GET /api/mca/companies`, `GET /api/mca/companies/{id}`, `POST /api/mca/companies`, `GET /api/mca/directors`, `POST /api/mca/directors`, `GET /api/mca/filings`, `POST /api/mca/filings`, `GET /api/mca/resolutions`, `POST /api/mca/resolutions`, `POST /api/mca/late-fee/calculate` (`mca.read`, `mca.manage`)

## Digital Engagement Letters & KYC Compliance (Sprint 19)

- **ICAI SA 210 Standard on Auditing Compliance**:
  - Prepares formal engagement terms agreeing on the objective and scope of the engagement, the auditor's responsibilities, management's responsibilities, applicable financial reporting framework, and expected form of reports.
  - Built-in standardized clauses covering Statutory Audits (Companies Act 2013 Sec 139), Tax Audits (Income Tax Sec 44AB), GST Audits (Rule 80(3)), and Internal Audits (Sec 138).
  - Lifecycle states: `Draft`, `IssuedToClient`, `AcceptedByClient`, `Declined`, `Superseded`.
- **PMLA (Prevention of Money Laundering Act) & Beneficial Ownership KYC**:
  - Risk classification tiers: `Low`, `Medium`, `High`, and `PEP` (Politically Exposed Persons).
  - Dynamic risk review windows: 1-year periodic re-verification for High Risk/PEP, 2 years for Medium Risk, and 3 years for Low Risk.
  - Beneficial Ownership (Rule 9 of PMLA Rules): Natural persons holding ≥ 10% (companies) or ≥ 15% (partnerships/trusts) control or ownership.
  - Mandatory verification tracking: PAN verification, Aadhaar/Offline XML, C-KYC 14-digit identifier, and Board Resolution identity proofs.
- **Two Locks**: EF Core tenant filtering and PostgreSQL Row Level Security on `engagement_letters` and `client_kyc_records` (`0018_engagement_kyc.sql`).

API:
- `GET /api/engagements/summary`, `GET /api/engagements`, `GET /api/engagements/{id}`, `POST /api/engagements`, `PUT /api/engagements/{id}/status`, `POST /api/engagements/preview-template` (`engagement.read`, `engagement.manage`)
- `GET /api/kyc`, `GET /api/kyc/{id}`, `POST /api/kyc`, `PUT /api/kyc/{id}/status` (`kyc.read`, `kyc.manage`)

## Cash & Bank Feed Integration & BRS Auto-Matcher (Sprint 20)

- **Bank Account Master**: Current, Savings, Overdraft, and Cash Credit accounts tracking with IFSC code validation (`^[A-Z]{4}0[A-Z0-9]{6}$`), live balance monitoring, and client linkage.
- **Multi-Bank Statement Parser**: Automatic detection of standard bank formats (Date, Narration, Reference, Debit, Credit, Balance or Amount/Type columns); supports varying date conventions (`dd/MM/yyyy`, `yyyy-MM-dd`, `dd-MMM-yyyy`).
- **Smart Categorization Engine**: High-priority user-defined keyword rules combined with built-in heuristics (SALARY -> Payroll, GST -> Tax, UPI -> Collections, CHARGES -> Bank Fees, ITAX -> Advance Tax).
- **Statutory Bank Reconciliation Statement (BRS)**:
  - Balance as per Bank Statement
  - (+) Cheques issued / Outward debits not yet presented in bank (Uncleared Debits)
  - (-) Cheques deposited / Inward credits not yet cleared by bank (Uncredited Credits)
  - Direct bank charges and interest adjustments
  - (=) Balance as per Books of Account (Adjusted Ledger Balance)
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `bank_accounts`, `bank_statements`, `bank_transactions`, and `bank_reconciliation_rules` (`0019_bank_feeds.sql`).

API:
- `GET /api/bank/accounts`, `GET /api/bank/accounts/{id}`, `POST /api/bank/accounts`, `PUT /api/bank/accounts/{id}`, `DELETE /api/bank/accounts/{id}` (`bank.read`, `bank.manage`)
- `GET /api/bank/accounts/{id}/statements`, `POST /api/bank/accounts/{id}/statements` (`bank.read`, `bank.manage`)
- `GET /api/bank/accounts/{id}/transactions`, `POST /api/bank/transactions/{id}/match` (`bank.read`, `bank.reconcile`)
- `GET /api/bank/accounts/{id}/brs` (`bank.read`)
- `GET /api/bank/rules`, `POST /api/bank/rules`, `DELETE /api/bank/rules/{id}` (`bank.read`, `bank.manage`)

## Practice MIS & Executive Reporting Desk (Sprint 21)

- **Executive KPI Summary**: Consolidated real-time snapshot of practice health:
  - Billed vs. collected revenue, outstanding dues, and cash recovery rate (%).
  - Total tracked hours vs. billable hours and firm-wide billable realization percentage.
  - Statutory compliance SLA on-time filing index and count of overdue client deadlines.
  - Annual practice targets tracking with progress against FY goals.
- **Debtors Aging Analysis (Receivables Aging)**:
  - Industry-standard aging buckets: Current (0–30 days), 31–60 days, 61–90 days, and Overdue (> 90 days).
  - Client-wise breakdown of unpaid invoices, days overdue, and risk weighting.
- **Client Profitability & Realization Matrix**:
  - Revenue generated per client vs. professional staff cost (calculated from hourly rates and logged time).
  - Net client margin (₹) and operating margin percentage (%), ranking top-tier vs. loss-making engagements.
- **Staff Utilization & Capacity Analytics**:
  - Member-level billable vs. non-billable ratio (utilization rate %).
  - Individual billing realization value generated for the practice.
- **Statutory Compliance SLA Performance**:
  - Category-wise analysis across GST, Income Tax (TDS/ITR), MCA/ROC, and Audit deadlines.
  - On-time vs. delayed filing ratio, active overdue liabilities, and compliance SLA benchmark compliance.
- **Annual Practice Target Settings**:
  - Configurable annual targets for revenue, billable hours, recovery percentage, and SLA benchmarks stored in `practice_targets`.
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `practice_targets` (`0020_practice_reports.sql`).

API:
- `GET /api/reports/executive-summary?fy={fy}` (`reports.read`)
- `GET /api/reports/receivables-aging` (`reports.read`)
- `GET /api/reports/client-profitability?fy={fy}` (`reports.read`)
- `GET /api/reports/staff-utilization?fy={fy}` (`reports.read`)
- `GET /api/reports/compliance-sla?fy={fy}` (`reports.read`)
- `GET /api/reports/revenue-breakdown?fy={fy}` (`reports.read`)
- `POST /api/reports/targets` (`reports.manage`)

## Tax Notice & Faceless Assessment Management Desk (Sprint 22)

- **Statutory Notice & Inquiry Tracking**:
  - Unified register for Income Tax (Sec 143(1) intimation, Sec 139(9) defective return, Sec 142(1) inquiry, Sec 148/148A reassessment, Sec 156 demand, Sec 245 adjustment) and GST (DRC-01A, DRC-01, ASMT-10, REG-17, DRC-07) notices.
  - Due date tracking with automatic remaining days and overdue alerts.
- **CBDT & CBIC DIN Verification Engine**:
  - Evaluates Document Identification Numbers per CBDT Circular No. 19/2019 and CBIC Circular No. 122/41/2019.
  - Flags post-mandate communications lacking a valid DIN as `Invalid` (legally *non-est*), providing grounds for jurisdictional challenge.
- **Faceless E-Hearings & Video Conference Scheduler**:
  - Central calendar for Video Conference (VC) appointments with assessing officers and National Faceless Assessment Centre (NFAC).
  - Virtual meeting link storage, officer jurisdiction mapping, and adjournment request tracking with minutes of hearing.
- **Maker-Checker Written Submissions & Partner Sign-off**:
  - Multi-version rebuttal draft editor with legal grounds and attachments inventory.
  - Mandatory maker-checker review: Staff drafts response -> Partner signs off (`notices.approve`) -> Final submission logged with portal acknowledgement number.
- **Statutory Demand Ledger & CIT(A) 30-Day Appeal Countdown**:
  - Real-time ledger of original demand, stayed amounts u/s 220(6), direct tax payments, and Sec 245 refund adjustments.
  - Automatic 30-day appeal countdown meter under Section 249(2) (Form 35) and 3-month GST appeal deadline under Section 107 (Form GST APL-01).
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `tax_notices`, `notice_hearings`, `notice_responses`, and `tax_demands` (`0021_tax_notices.sql`).

API:
- `GET /api/tax-notices`, `GET /api/tax-notices/{id}`, `POST /api/tax-notices`, `PUT /api/tax-notices/{id}`, `POST /api/tax-notices/{id}/verify-din` (`notices.read`, `notices.manage`)
- `GET /api/tax-notices/hearings`, `POST /api/tax-notices/{id}/hearings`, `PUT /api/tax-notices/hearings/{hearingId}/status` (`notices.read`, `notices.manage`)
- `GET /api/tax-notices/{id}/responses`, `POST /api/tax-notices/{id}/responses`, `POST /api/tax-notices/responses/{id}/approve`, `POST /api/tax-notices/responses/{id}/submit` (`notices.read`, `notices.manage`, `notices.approve`)
- `GET /api/tax-notices/demands`, `POST /api/tax-notices/demands`, `POST /api/tax-notices/demands/{id}/payment`, `PUT /api/tax-notices/demands/{id}/appeal` (`notices.read`, `notices.manage`)
- `GET /api/tax-notices/summary` (`notices.read`)

## Payroll, Salary TDS & Statutory PF / ESIC Engine (Sprint 23)

- **Client Employee Master & Salary Structures**:
  - Employee records linked to client firms: Employee code, PAN, UAN (Universal Account Number), ESIC insurance number, and designation.
  - Multi-component salary structure: Basic Salary, HRA, Special Allowance, Other Allowances, and Monthly CTC.
  - Dual tax regime preference: New Regime (Section 115BAC) vs. Old Regime.
- **Automated Statutory Deductions Engine**:
  - **EPF (Employees' Provident Fund)**: 12% employee contribution on PF wage (Basic) subject to the ₹15,000 statutory wage ceiling. Automated employer split: EPS (Pension) 8.33% (capped at ₹1,250/mo) and EPF 3.67%.
  - **ESIC (Employees' State Insurance)**: Automatic wage threshold evaluation (gross salary &le; ₹21,000/mo). Calculates employee contribution (0.75%) and employer contribution (3.25%).
  - **State Professional Tax (PT)**: Built-in state slab rules (e.g. Maharashtra ₹175/₹200 and ₹300 February balance, Karnataka ₹200 for gross > ₹15,000).
  - **Salary TDS (Section 192)**: Projected annual tax with standard deduction (₹75,000 in New Regime, ₹50,000 in Old Regime), Section 87A rebate (&le; ₹7,00,000 taxable), and monthly withholding.
- **Monthly Payroll Processing & Register**:
  - Executes batch monthly salary runs with pro-rata attendance calculations (`DaysWorked / DaysInMonth`) and bonus/incentive overrides.
  - Full itemized payslips storing earnings, statutory deductions, employer contributions, and net take-home salary.
- **EPFO ECR Text Export**:
  - One-click generation of the standard `#~#` delimited Electronic Challan cum Return (ECR) text file ready for direct upload to the EPFO unified employer portal.
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `payroll_employees`, `payroll_runs`, and `payroll_payslips` (`0022_payroll_statutory.sql`).

API:
- `GET /api/payroll/employees`, `GET /api/payroll/employees/{id}`, `POST /api/payroll/employees`, `PUT /api/payroll/employees/{id}`, `DELETE /api/payroll/employees/{id}` (`payroll.read`, `payroll.manage`)
- `GET /api/payroll/runs`, `GET /api/payroll/runs/{id}`, `POST /api/payroll/runs`, `PUT /api/payroll/runs/{id}/status` (`payroll.read`, `payroll.process`)
- `GET /api/payroll/runs/{id}/payslips`, `GET /api/payroll/runs/{id}/ecr` (`payroll.read`)
- `GET /api/payroll/summary` (`payroll.read`)

## Automated E-Invoicing & E-Way Bill Integration Desk (Sprint 24)

- **Direct IRP E-Invoice & IRN Generation**:
  - Full support for GST e-invoicing schema (INV tax invoice, CRN credit note, DBN debit note).
  - SHA-256 Invoice Reference Number (IRN) hash synthesis (`SupplierGSTIN + FY + DocType + DocNumber`) generating official 64-character lowercase hexadecimal IRNs.
  - Generates digitally signed QR code payloads carrying supplier GSTIN, recipient GSTIN, document number, document date, total invoice value, HSN, IRN, and IRP digital signature.
  - Real-time IRP acknowledgement number and timestamp assignment.
- **Rule 138(10) E-Way Bill Validity Engine**:
  - Automatically calculates statutory movement validity based on consignment distance:
    - **Regular Cargo**: 1 day for every 200 km (or part thereof); minimum 1 day.
    - **Over Dimensional Cargo (ODC)**: 1 day for every 20 km (or part thereof); minimum 1 day.
  - Live countdown meter calculating hours remaining before transit expiration, highlighting expiring-soon consignments (< 24h).
- **Part-B Vehicle & Multi-Modal Transit Updater**:
  - Update vehicle registration numbers dynamically for transshipment or vehicle breakdowns during transit.
  - Multi-modal support (Road, Rail, Air, Ship) with transporter assignment tracking.
- **Statutory 24-Hour IRN Cancellation Desk**:
  - Enforces the statutory 24-hour cancellation rule on the IRP with official cancellation codes (Duplicate, Data Entry Mistake, Order Cancelled, Others).
  - Flags and blocks attempts to cancel IRNs past the 24-hour threshold, prompting the user to issue a Credit Note (CRN) instead.
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `einvoice_records` and `eway_bill_records` (`0023_einvoicing_ewaybill.sql`).

API:
- `GET /api/einvoicing/invoices`, `GET /api/einvoicing/invoices/{id}`, `POST /api/einvoicing/invoices`, `POST /api/einvoicing/invoices/{id}/cancel` (`einvoice.read`, `einvoice.generate`, `einvoice.cancel`)
- `GET /api/einvoicing/ewaybills`, `GET /api/einvoicing/ewaybills/{id}`, `POST /api/einvoicing/ewaybills`, `PUT /api/einvoicing/ewaybills/{id}/part-b`, `POST /api/einvoicing/ewaybills/{id}/cancel` (`einvoice.read`, `einvoice.generate`, `einvoice.cancel`)
- `GET /api/einvoicing/summary` (`einvoice.read`)

## Fixed Asset Register & Dual Depreciation Engine (Sprint 25)

- **Comprehensive Fixed Asset Register**:
  - Asset tag, asset class (Building, Plant & Machinery, Computers & Software, Furniture, Office Equipment, Vehicles, Intangibles), location, department, custodian, and capitalization date.
  - Historical cost composition: Purchase cost, inward freight, installation, pre-operating expenses, less GST Input Tax Credit (ITC) availed.
- **Companies Act 2013 Schedule II Depreciation**:
  - Useful life-based straight-line (SLM) and written-down value (WDV) methods.
  - Automatic 5% salvage value ceiling.
  - Multi-shift depreciation adjustments: Single shift (1.0x), Double shift (1.5x / +50%), Triple shift (2.0x / +100%).
  - Pro-rata calculation on active days within the financial year.
- **Income Tax Act Section 32 Block of Assets & 180-Day Rule**:
  - Automatic classification into statutory Income Tax blocks (15% P&M, 40% Computers, 10% Furniture, etc.).
  - Statutory 180-day rule under the Proviso to Section 32(1): Assets put to use for < 180 days in the year of acquisition are restricted to 50% of the normal block rate.
  - Section 32(1)(iia) Additional Depreciation for manufacturing: 20% on actual cost if &ge; 180 days; 10% in Year 1 if < 180 days.
- **AS 22 / Ind AS 12 Deferred Tax Accounting**:
  - Quantifies timing differences (`TaxDepreciation - BookDepreciation`) on an asset-by-asset and aggregate client basis.
  - Calculates net Deferred Tax Liability (DTL) and Deferred Tax Asset (DTA) based on the client corporate tax rate (e.g. 25.17% u/s 115BAA).
- **Physical Verification Audit Trail**:
  - Logs periodic physical inspection events: Verification date, inspector/auditor, condition status (`FoundAndGood`, `Damaged`, `Missing`, `NeedsMaintenance`), verified location, and audit notes.
  - Automatic status updates (e.g. missing assets flagged for write-off).
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `fixed_assets`, `asset_depreciation_entries`, and `asset_physical_verifications` (`0024_fixed_assets.sql`).

API:
- `GET /api/fixed-assets`, `GET /api/fixed-assets/{id}`, `POST /api/fixed-assets`, `PUT /api/fixed-assets/{id}`, `POST /api/fixed-assets/{id}/dispose`, `DELETE /api/fixed-assets/{id}` (`assets.read`, `assets.manage`)
- `POST /api/fixed-assets/depreciation`, `GET /api/fixed-assets/depreciation` (`assets.depreciate`, `assets.read`)
- `POST /api/fixed-assets/{id}/verifications`, `GET /api/fixed-assets/verifications` (`assets.manage`, `assets.read`)
- `GET /api/fixed-assets/summary` (`assets.read`)

## Advance Tax Planner, Self-Assessment & Section 234A/B/C Interest Engine (Sprint 26)

- **Section 208 Mandatory Advance Tax Threshold**:
  - Automatically assesses net tax liability (`TotalEstimatedTax - TotalPrepaidTdsTcs`).
  - Flags mandatory advance tax obligation if net liability is &ge; ₹10,000; honors Section 207(2) resident senior citizen exemption (no business/profession income).
- **Section 211 Quarterly Installment Milestones**:
  - **Regular Assessees (Corporate & Non-Corporate)**:
    - 1st Installment (on or before June 15): 15% of advance tax.
    - 2nd Installment (on or before September 15): 45% of advance tax (cumulative).
    - 3rd Installment (on or before December 15): 75% of advance tax (cumulative).
    - 4th Installment (on or before March 15): 100% of advance tax (cumulative).
  - **Presumptive Tax Assessees (Section 44AD / 44ADA)**:
    - Single installment of 100% on or before March 15.
- **Section 234C Deferment Interest & Statutory Safe Harbors**:
  - Charges 1% per month for 3 months on quarterly shortfalls (1 month for March installment).
  - Evaluates statutory safe harbors under Section 234C(1):
    - June 15 safe harbor: No interest if cumulative paid &ge; 12% of assessed tax.
    - September 15 safe harbor: No interest if cumulative paid &ge; 36% of assessed tax.
- **Section 234B Default / Shortfall in Payment of Advance Tax**:
  - Triggered if cumulative advance tax paid on or before March 31 is < 90% of assessed tax.
  - Computes 1% per month (or part thereof) on shortfall from April 1 of the Assessment Year until assessment / self-assessment date.
- **Section 234A Interest for Late Filing of Income Tax Return**:
  - Evaluates filing date against Section 139(1) statutory due date (e.g. July 31 for non-audit, October 31 for tax audit, November 30 for transfer pricing).
  - Computes 1% per month (or part thereof) on balance tax remaining payable.
- **ITNS 280 Challan Tracking & Recording**:
  - Records BSR code (7 digits), challan date, challan serial number (5 digits), minor head code (100 for Advance Tax, 300 for Self-Assessment), and tax breakdown (Basic, Surcharge, Cess, Interest, Penalty).
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `advance_tax_plans`, `advance_tax_installments`, and `tax_interest_computations` (`0025_advance_tax.sql`).

API:
- `GET /api/advance-tax/plans`, `GET /api/advance-tax/plans/{id}`, `POST /api/advance-tax/plans`, `PUT /api/advance-tax/plans/{id}` (`advancetax.read`, `advancetax.manage`)
- `POST /api/advance-tax/plans/{id}/pay-installment` (`advancetax.manage`)
- `POST /api/advance-tax/plans/{id}/calculate-interest`, `GET /api/advance-tax/computations` (`advancetax.calculate`, `advancetax.read`)
- `GET /api/advance-tax/summary` (`advancetax.read`)

## GSTR-9 / 9C Annual Return & Audit Reconciliation Desk (Sprint 27)

- **Section 44 Statutory Turnover Threshold Gating**:
  - Automatically assesses aggregate turnover across pan-India operations.
  - Categorizes client statutory mandate:
    - **Aggregate Turnover &le; ₹2 Crores**: Exempt from mandatory filing (optional filing under CBIC notifications).
    - **Aggregate Turnover &gt; ₹2 Crores &le; ₹5 Crores**: Mandatory GSTR-9 Annual Return (exempt from GSTR-9C reconciliation statement).
    - **Aggregate Turnover &gt; ₹5 Crores**: Mandatory GSTR-9 and GSTR-9C Reconciliation Statement.
- **Form GSTR-9 Annual Return Compilation**:
  - **Part II Outward Supplies**: Table 4N (outward supplies on which tax is payable - B2C 4A, B2B 4B, RCM 4G) and Table 5M (supplies on which tax is not payable - Zero-rated 5A, Exempt 5C) to derive Table 5N Total Turnover (`4N + 5M - 4G`).
  - **Part III Input Tax Credit**: Table 6A (ITC in GSTR-3B), Table 6B (B2B inputs/services), Table 7I (Total ITC reversed under Rule 37/42/43 & Sec 17(5)), Table 7J (Net ITC availed), and Table 8A (auto-drafted 2A/2B).
  - **Table 8 ITC Variance Engine**: Automatically calculates `8D = 8A - (8B + 8C)`. Flags excess claims ($8D < 0$) with red alerts for recovery/DRC-03 demands.
  - **Part IV Tax Paid & Shortfall**: Table 9 tax payable vs. cash and ITC paid; quantifies net shortfall tax for voluntary DRC-03 recovery.
- **Form GSTR-9C Reconciliation Statement**:
  - **Table 5 Gross Turnover Reconciliation**: Reconciles audited annual gross turnover (5A) with unbilled revenue (5B/5H), unadjusted advances (5C/5I), deemed supplies (5D), credit notes after FY (5E), and trade discounts (5F) to derive Reconciled Turnover (5P) and Unreconciled Turnover Variance (5R).
  - **Table 7 Taxable Turnover Reconciliation**: Adjusts Reconciled Turnover (5P) for exempt/nil (7B), zero-rated (7C), and outward RCM (7D) to compute Reconciled Taxable Turnover (7E) against GSTR-9 Taxable Turnover (7F) and Variance (7G).
  - **Table 12 & 14 Net ITC Reconciliation**: Compares Book ITC (12A) against GSTR-9 Net ITC (12E) to isolate unreconciled credit differences (12F).
- **Audit Discrepancy Manager with Statutory Reason Codes**:
  - Categorizes reconciliation issues (`TurnoverTimingDifference`, `UnbilledRevenue`, `AdvanceAdjustment`, `CreditNoteAdjustment`, `RateMismatch`, `ItcBookVsReturnMismatch`, `ExpenseItcUnclaimed`, etc.).
  - Tracks discrepancy amounts, tax impact, and auditor recommendations with full resolution workflow.
- **Auditor Certification & Form GST DRC-03 Payment**:
  - Validates CA membership number, firm registration number (FRN), and ICAI UDIN for statutory certification.
  - Gated filing: Blocks filing of GSTR-9 returns with turnover &gt; ₹5 Crores if GSTR-9C is uncertified.
  - Records DRC-03 electronic cash ledger payments (ARN, date, amount, cash ledger).
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `gstr9_returns`, `gstr9_table_entries`, `gstr9c_reconciliations`, and `gstr9c_discrepancies` (`0026_gstr9_reconciliation.sql`).

API:
- `GET /api/gstr9/returns`, `GET /api/gstr9/returns/{id}`, `POST /api/gstr9/returns/compile`, `PUT /api/gstr9/returns/{id}`, `POST /api/gstr9/returns/{id}/file` (`gstr9.read`, `gstr9.manage`)
- `GET /api/gstr9/returns/{id}/gstr9c`, `POST /api/gstr9/returns/{id}/gstr9c/reconcile`, `POST /api/gstr9/returns/{id}/gstr9c/certify`, `POST /api/gstr9/returns/{id}/gstr9c/drc03` (`gstr9.read`, `gstr9.manage`, `gstr9.certify`)
- `POST /api/gstr9/returns/{id}/gstr9c/discrepancies`, `POST /api/gstr9/discrepancies/{id}/resolve` (`gstr9.manage`)
- `GET /api/gstr9/summary` (`gstr9.read`)

## Digital Workpapers & Engagement Quality Review (EQR) Desk (Sprint 28)

- **ICAI SQC 1 Engagement Archival Engine & 60-Day Lock**:
  - Implements ICAI SQC 1 (Paragraphs 74-75) statutory rule: Final engagement file assembly must be completed within 60 days of the auditor's report date.
  - Automatically calculates the exact 60-day deadline upon partner report sign-off.
  - Provides a real-time status monitor: In Progress &rarr; Due in X days &rarr; Overdue (SQC 1 Breach) &rarr; Archived & Locked.
  - **Permanent Immutability**: Once an engagement is sealed and archived, workpapers, review notes, and EQR reviews are locked. Any update, creation, or deletion throws an `InvalidOperationException`.
- **ICAI SA 220 Engagement Quality Control Review (EQR)**:
  - Supports Hot Reviews (pre-issuance of auditor's report) and Cold Reviews (post-issuance inspection / monitoring).
  - Embeds standard 10-point pre-issuance quality checklist (SA 210, SA 315, SA 320, SA 240, SA 505, SA 501, SA 570, SA 580, CARO 2020, and Trial Balance agreement).
  - Clearance gate: Evaluates all mandatory checklist items and records EQR partner concurrence remarks before clearance is granted.
- **Audit Review Notes & Query Resolution Workflow**:
  - Multi-tier query raising across roles (`manager`, `eqr`, `lead_partner`) and priority tiers (`low`, `medium`, `high`, `critical`).
  - Categorization into audit gap types (`documentation_gap`, `insufficient_testing`, `assertion_unverified`, `accounting_policy_mismatch`, `caro_clause_omission`, `other`).
  - Response, clearance, and reopening workflow with user audit stamps.
- **Partner Sign-Off Gate Checks**:
  - Automatically blocks report sign-off if any review notes remain open or reopened.
  - Blocks sign-off if EQR review is required (`EqrRequired = true`) and EQR clearance has not been formally granted.
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `audit_engagements`, `audit_working_papers`, `audit_eqr_reviews`, and `audit_review_notes` (`0027_workpapers_eqr.sql`).

API:
- `POST /api/audit/engagements/{id}/signoff`, `POST /api/audit/engagements/{id}/archive` (`audit.manage`, `audit.archive`)
- `GET /api/audit/engagements/{id}/eqr`, `POST /api/audit/engagements/{id}/eqr/assign`, `PUT /api/audit/eqr/{id}`, `POST /api/audit/eqr/{id}/clear` (`audit.read`, `audit.manage`, `eqr.review`)
- `GET /api/audit/engagements/{id}/review-notes`, `POST /api/audit/engagements/{id}/review-notes`, `POST /api/audit/review-notes/{id}/respond`, `POST /api/audit/review-notes/{id}/clear`, `POST /api/audit/review-notes/{id}/reopen` (`audit.read`, `audit.manage`)
- `GET /api/audit/checklist-template` (`audit.read`)

## Presumptive Taxation & Minimal Financials Generator (Sprint 29)

- **Section 44AD (Eligible Business for Resident Individuals, HUFs & Partnership Firms)**:
  - Evaluates statutory turnover ceilings: ₹2 Crores standard limit, or enhanced ₹3 Crores limit when cash receipts &le; 5% of aggregate turnover (Finance Act 2023 amendment).
  - Deemed profit split: 6% on turnover received through electronic clearing / banking channels and 8% on non-digital / cash receipts. Assessee can declare higher taxable profit.
  - Section 44AD(4) 5-Year Lockout Penalty Tracker: Tracks clients who opted out in an earlier Assessment Year by declaring profit below the statutory deemed rate. Flags active 5-year lockouts and triggers mandatory tax audit requirements under Section 44AB(e).
- **Section 44ADA (Specified Professionals)**:
  - Gross receipts ceiling: ₹50 Lakhs standard limit, or enhanced ₹75 Lakhs limit when cash receipts &le; 5%.
  - Minimum deemed professional profit: 50% of aggregate gross receipts.
- **Section 44AE (Goods Carriage Transporters Fleet Register)**:
  - Fleet size limit: Assessee cannot own more than 10 goods carriages at any time during the previous year.
  - Deemed profit computation per vehicle per month (or part thereof):
    - **Heavy Goods Vehicle** (Gross Vehicle Weight &gt; 12 Metric Tonnes): ₹1,000 per ton per month.
    - **Other Goods Vehicle** (&le; 12 MT): ₹7,500 per vehicle per month.
- **Section 44AB Mandatory Tax Audit Trigger Engine**:
  - Automatically raises red audit alerts with statutory citations when:
    - Declared profit is lower than the statutory minimum deemed profit (and total income exceeds basic exemption).
    - Turnover or gross receipts exceed statutory ceilings (₹2 Cr/₹3 Cr for 44AD, ₹50L/₹75L for 44ADA, &gt; 10 vehicles for 44AE).
    - Section 44AD(4) lockout penalty is active.
- **Section 44AA Minimal Financials Generator (ITR-4 Sugam Particulars)**:
  - Generates the 4 mandatory business balance sheet particulars required under Section 44AA for ITR-4:
    1. Amount of Sundry Debtors
    2. Amount of Sundry Creditors
    3. Amount of Stock-in-trade
    4. Amount of Cash balance
  - Real-time balance sheet sanity meter: Compares Total Assets against Total Liabilities & Capital, detects variances, and validates mathematical coherence.
  - Printable / Exportable official Statement of Presumptive Income for client records and sign-off.
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `presumptive_tax_computations` and `presumptive_44ae_vehicles` (`0028_presumptive_taxation.sql`).

API:
- `GET /api/presumptive/computations`, `GET /api/presumptive/computations/{id}`, `POST /api/presumptive/computations`, `PUT /api/presumptive/computations/{id}` (`presumptive.read`, `presumptive.manage`)
- `POST /api/presumptive/computations/{id}/recalculate`, `POST /api/presumptive/computations/{id}/approve` (`presumptive.manage`, `presumptive.approve`)
- `POST /api/presumptive/computations/{id}/vehicles`, `DELETE /api/presumptive/computations/{id}/vehicles/{vehicleId}` (`presumptive.manage`)
- `GET /api/presumptive/computations/{id}/statement`, `GET /api/presumptive/summary` (`presumptive.read`)




## Notes

- Package versions are in `Directory.Packages.props`. If restore can't find a version,
  update it there.
- If you already ran Sprint 1 locally, its database just gets migration `0002_identity.sql` on next start.
  Old test firms made with the Sprint 1 dev endpoint have no users; create new firms through sign-up.
