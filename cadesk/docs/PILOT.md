# Design-partner pilot (Sprint 72)

**Goal:** three CA firms in Kerala run one full GST cycle in CaDesk, from the November 2026 returns to the
December filings, with no blocker bug left open.

## Who

| | Firm | Size we want | Partner contact | Status |
| --- | --- | --- | --- | --- |
| 1 | [firm] | Sole practitioner, 40–80 GST clients | [name, phone] | [ ] agreed |
| 2 | [firm] | 2–4 partners, audits and GST | [name, phone] | [ ] agreed |
| 3 | [firm] | Branch or 10+ staff | [name, phone] | [ ] agreed |

Offer: CaDesk free on the Growth plan until 31 March 2027, then 50% off the first year, in return for one
30-minute call a week and honest reports.

Set the pilot firms' plan (owner database role):

```sql
update firm_subscriptions set plan_tier = 'GROWTH', status = 'Active'
 where firm_id in (select id from firms where name in ('[firm 1]', '[firm 2]', '[firm 3]'));
update firms set plan_code = 'GROWTH' where name in ('[firm 1]', '[firm 2]', '[firm 3]');
```

## Before the first firm signs up

- [ ] `dotnet test`, `npm test`, and `npm run e2e` green against staging (local verified 25 Nov 2026: 896 unit, 173 integration, 54 web tests + Vite build, 15 Playwright E2E)
- [ ] Load test passed on the production-size server (`deploy/load/README.md`)
- [ ] Legal pages: [brackets] filled, lawyer has read them
- [ ] Help centre: support email and number filled in `/help/report-a-problem.html`
- [ ] Backups run nightly and **one restore has been tested** on a spare machine (local encrypted `backup-db.ps1` → `restore-db.ps1` drill verified with migration `0070`; repeat on production/spare host)
- [ ] Uptime check on `https://<site>/health/ready` every minute, alerting your phone
- [ ] Someone watches problem reports daily (query below)
- [ ] One GSTR-1 JSON from CaDesk imported into the GST offline tool without errors

## Timeline (November 2026 returns)

| When | What |
| --- | --- |
| 30 Nov – 5 Dec | Onboarding call per firm (1 hour): sign up, two-step sign-in, import clients, invite staff, check deadlines |
| by 11 Dec | GSTR-1 for November (monthly filers); IFF by 13 Dec for QRMP clients |
| by 20 Dec | GSTR-3B for November, with the CaDesk 3B check done before filing |
| every Friday | 30-minute call: what was slow, what was wrong, what was missing |
| 28 Dec | Review: did every firm file through the cycle without a blocker? |

Firms keep filing as they do today; CaDesk's files are checked against their usual method for this cycle.

## What counts as a blocker

A **blocker** stops a firm finishing a filing on time with CaDesk:

- a wrong figure in a return file or the 3B check;
- a GSTR-1 JSON the offline tool or portal rejects;
- data from one firm visible to another (stop the pilot, fix, tell the firms);
- sign-in or data loss problems.

Blockers: answer within 2 hours, fix or give a workaround the same day. Everything else goes on the list for the
Friday call.

## Watching problem reports

Users report from the flag button in the sidebar (reference `PR-XXXXXXXX`); desk crashes are reported
automatically. Server errors show a reference `E-XXXXXXXXXX`, which is also in the API log line.

```sql
-- owner role: all firms, newest first
select f.name as firm, p.kind, p.occurrences, p.page, p.message, p.app_version,
       'PR-' || upper(right(replace(p.id::text, '-', ''), 8)) as reference, p.last_seen_at
  from problem_reports p join firms f on f.id = p.firm_id
 where p.status = 'open'
 order by p.last_seen_at desc
 limit 50;
```

```bash
# server errors in the API log
docker compose -f docker-compose.prod.yml logs api --since 24h | grep -E "Unhandled|Problem report"
```

Mark a report resolved after the fix ships: `POST /api/problem-reports/{id}/status {"status":"resolved"}` as
a partner of that firm, or `update problem_reports set status = 'resolved' where id = ...`.

## What to measure

| Measure | Target |
| --- | --- |
| Returns filed with CaDesk files | every November GSTR-1 in the three firms |
| Blockers open at 28 Dec | 0 |
| Time to prepare one client's GSTR-1, from sales register to JSON | under 10 minutes |
| Firms who would pay at the list price | 2 of 3 |

Keep notes from each Friday call in `docs/pilot-notes/` (one file per firm).
