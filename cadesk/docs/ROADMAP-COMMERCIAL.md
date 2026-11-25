# CaDesk — Road to a sellable product (Sprints 65–72)

Written 24 Nov 2026, after checking the code at commit `ae07f43` (Sprint 64) plus uncommitted UI work.

## 1. Is the Gemini gap list right?

Partly. It describes a 33-sprint plan, but the repo is already at Sprint 64, and three of its five "missing" items are mostly built.

| Gemini says missing | What the code actually has | Real remaining gap |
| --- | --- | --- |
| Government e-filing JSON/XML | **True gap.** Only UAE EmaraTax has an export. Form 3CD, GSTR-1/3B/9, TDS 24Q/26Q, ITR, MCA have none. | The biggest gap. Without upload files, staff still re-key everything into the portals. |
| Document vault / cloud storage | `IFileStore` + `LocalFileStore`, ClamAV scan, folders, trash, retention purge, upload links. | Only an S3-compatible adapter (AWS S3 / MinIO / Cloudflare R2). About 2 days of work. |
| SA 230 digital dossier | Sprint 43: lead schedules A–Q, SHA-256 seal, SQC 1 lock, HTML export. | It's HTML only. Needs a real indexed PDF (bookmarks, page numbers). |
| WhatsApp, email, payments | WhatsApp Cloud API channel + webhook, SendGrid / SES / SMTP channels, outbox. | Razorpay isn't built (only two ID fields exist). No payment links on client invoices, no SaaS checkout, no webhooks. |
| Client self-service portal | Portal login, dashboard, filings, invoices, DSC, service requests, upload links, SA 505 public confirmation page. | Client document download, "pay this invoice" button, consent capture. |

## 2. Gaps Gemini did not mention (these matter more for selling)

1. **Scope sprawl.** There are 50+ desks (UAE tax, IBC, ESG/BRSR, cost audit…). A 3-partner firm in Kerala uses about 10. Desks need to be gated by plan, and the default navigation should show only what the firm switched on.
2. **Correctness sign-off.** 706 unit tests prove the code does what it was written to do, not that the tax law is right. A practising CA (a design partner) has to check every computation desk against real past returns before it's sold.
3. **DPDP Act 2023 / Rules 2025.** CaDesk processes client PAN, Aadhaar and bank data. It needs consent notices, a data processing agreement for firms, breach notification, a data-principal request flow, and a published privacy policy and terms.
4. **Migration in.** Firms switching from other software need a bulk import of clients, compliance calendars and DSC registers from Excel, with a dry-run preview.
5. **Performance and UX.** All 50 desks load in one bundle, the URL never changes (no back button, no deep links), and the FY badge was hard-coded to 2024-25.
6. **Help and support.** No in-app help, no status page, no support ticket path.
7. **Real-world verification.** No end-to-end browser tests. Integration tests need PostgreSQL and haven't been run in CI with real data.

## 3. Status (updated 24 Nov 2026)

| Sprint | Status | Done | Still to do |
| --- | --- | --- | --- |
| 65 Look & feel | **Done** | "Clear" design (white sidebar, teal), light/dark, Today screen, Ctrl+K desk search, pinned/recent desks, URL per desk, lazy-loaded desks (first load 465 → 124 KB gzip), desk error boundary, self-hosted fonts | Restyle the public landing, sign-up and sign-in pages (they still use hard-coded navy/blue) |
| 66 GST exports | **Done (pending your checks)** | One desk, "GST returns". **GSTR-1:** CSV sales register → checks → JSON with B2B, B2CL (₹1 lakh), B2CS, CDNR, CDNUR (B2CL and export notes), exports, nil/exempt, HSN B2B/B2C, Table 13 documents issued (cancelled rows and numbering gaps), amendments B2BA/B2CLA/CDNRA/EXPA. **GSTR-3B check:** Table 3.1/3.2 from sales, Table 4 from the 2B match, set-off per s.49(5)/Rule 88A, RCM in cash, s.50 interest, s.47 late fee with Notification 19/2021 caps, monthly and QRMP (22nd/24th by state) due dates. **GSTR-9 working:** Tables 4, 5 and 17 for the year. Every output can be downloaded or saved to the client's document vault (new `POST /api/clients/{id}/documents/generated`, .json/.csv only). Fixed: the GSTR-9 desk invented table rows with fixed percentages (B2C = 20% etc.); now only rows backed by entered figures are stored. 37 web tests + 1 new C# test | You: `dotnet build` + `dotnet test`; import one GSTR-1 JSON into the GSTN offline tool (keys marked `SCHEMA`: version, HSN split, doc_issue, amendments); have a CA check the 3B Table 4 mapping and one real GSTR-9 year. Later: B2CSA/CDNURA amendments, GSTR-9 upload file (needs the offline-tool schema), GSTR-9 Tables 6–19 |
| 67 Income-tax exports | Waiting for schemas | | You: download the AY 2026-27 ITR-1/ITR-4 JSON schemas and validation rules, and the Form 3CD schema, from incometax.gov.in into `docs/schemas/` |
| 68 TDS returns | Not started | | |
| 69 Payments (Razorpay) | **Done (pending your checks)** | "Collect online" on sent invoices: Razorpay payment link for the balance due (copy, WhatsApp, open). Webhook `POST /api/webhooks/razorpay/{firmId}` checks the signature and records the payment once (unique gateway payment id), marks the invoice paid or part-paid. Settings: `payments.razorpay.key_id` + secrets for the key secret and webhook secret. Migration 0065. C# rule tests | You: `dotnet build` + `dotnet test`; create a Razorpay test-mode link and send a test webhook (events `payment_link.*`) |
| 70 Storage & PDFs | **Done (pending your checks)** | S3-compatible document storage (AWS S3, MinIO, Wasabi; SigV4 checked against AWS test vectors), chosen by `Documents:Storage=S3`. In-browser PDF writer (A4, bookmarks, tables that repeat their header across pages, page X of Y); printable HTML → PDF. Audit dossier and auditor's report: Download PDF / Save PDF to client documents (generated-documents route now takes PDFs as base64). 11 new web tests | You: build/test; try S3 with a test bucket; open a dossier PDF in Acrobat. Later: PDF for invoices and the other desks |
| 71 Sellable packaging | **Done (pending your checks)** | Desks by plan (Starter / Growth / Enterprise; `PlanDesks.cs`), enforced on the API (403 with the plan needed) and shown in the app (plan badge, upgrade screen). Client and user limits enforced on create, import and invite. Plans table gains ENTERPRISE/DEMO/DEV/INTERNAL (the upgrade code set ENTERPRISE, which the table did not have). DPDP: terms, privacy notice and data processing terms (drafts at `/legal/*.html`), acceptance recorded per user and version (`legal_acceptances`, migration 0066), sign-up checkbox, acceptance screen on version change; privacy link on the client upload page. Bulk Excel/CSV client import with preview already existed (now checks the plan limit). Integration test firms run as INTERNAL; `PlanAndLegalTests` covers gating | You: build/test; fill the [brackets] in the legal pages and have a lawyer review them; confirm the desk-to-plan split |
| 72 Launch hardening | **Built; pilot to run** | **End-to-end:** 13 Playwright tests over the top flows (sign-up with terms, sign-in, add and import clients, tasks, invoice to payment, document upload, GSTR-1 JSON, plan locks, user limit, problem reports, help and legal pages), `npm run e2e` against a running stack. **Load test:** k6 scripts in `deploy/load/` for 50 firms × 500 clients and 150 concurrent users, with pass marks. **Error monitoring:** browser crashes and desk failures sent to `problem_reports` (migration 0067), "Report a problem" with a PR- reference, server errors carry an E- reference in the response and the log. **Help centre:** 8 guides at `/help/index.html`, linked from the sidebar. **Pilot:** `docs/PILOT.md` (3 Kerala firms, November GST cycle, blocker rules, monitoring queries). Also: plan in force is now the higher of the subscription tier and the firm plan code | You: `dotnet build/test`; `npm install`, then `npx playwright install chromium` and `npm run e2e` against a local stack; run the load test on a production-size server; recruit the 3 firms and work through the PILOT.md checklist |

Backend note: the .NET 10 SDK is not available in the build sandbox, so C# changes must be built and tested on a developer machine (`dotnet build`, `dotnet test`).

## 4. Sprint plan

Each sprint is about one week for one developer. Sprints marked **(FE)** can be built and checked without the .NET SDK.

| Sprint | Theme | Deliverables | Done when |
| --- | --- | --- | --- |
| **65** (FE) | Look & feel, speed | New design system (ledger palette, IBM Plex Sans, tabular figures), light/dark/system theme, oxblood sidebar with SVG icons, Ctrl+K command palette with recents, a "Today" home screen, URL routing with back/forward, lazy-loaded desks, computed FY | `tsc` and `vite build` pass; first-load JS is much smaller; deep links work |
| **66** | Filing exports I — GST | GSTR-1 and GSTR-3B offline-tool JSON, GSTR-9 JSON from the existing desk, schema validation before download | Files open in the GSTN offline tool for 3 real test GSTINs |
| **67** | Filing exports II — Income tax | Form 3CD JSON from the Tax Audit desk, ITR-1/ITR-4 JSON from the Tax / Presumptive desks (schema per AY) | The e-filing utility's "Import JSON" accepts them with no schema errors |
| **68** | Filing exports III — TDS | 24Q / 26Q / 27Q text file in NSDL RPU format, FVU validation step, Form 16/16A import | The FVU utility generates a .fvu with no errors |
| **69** | Payments | Razorpay payment links on client invoices, webhook (signature-verified) that marks invoices paid, SaaS subscription checkout, GST invoice for the subscription | Test-mode payment marks an invoice paid end to end |
| **70** | Storage & documents | S3-compatible file store (env switch), real PDF output for the SA 230 dossier, auditor's report, computations and invoices (QuestPDF) | Dossier PDF has bookmarks, page numbers and the SHA-256 seal |
| **71** | Sellable packaging | Plan-based desk gating (Starter / Practice / Audit+), Excel bulk import with dry-run, DPDP consent + privacy/terms pages, client portal document download + pay button | A new firm can sign up, import 200 clients and see only its plan's desks |
| **72** | Launch hardening | Playwright end-to-end tests for the top 10 flows, load test (50 firms × 500 clients), help centre, error monitoring, design-partner pilot with 3 Kerala firms | 3 firms use it for one GST cycle with no blocker bugs |

### Things to decide

- **Schemas:** the official GST offline-tool and e-filing JSON schemas change every year. Download the current ones from the GST and Income Tax portals into `docs/schemas/<form>/<year>/` and build against them, not from memory.
- **Plan tiers:** which desks go in which plan (Sprint 71).
- **Design partners:** recruit 2–3 Kerala CA firms now, so they can check Sprints 66–68 output against their real filings.
