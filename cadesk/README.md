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
| 30 | CARO 2020 Automated Audit Checklist & Disclosures Generator: Companies Act Sec 143(11) 3-pillar applicability test, 21 statutory clauses verification workbench, discrepancy tracker, and official Annexure 'A' to Auditor's Report auto-drafting | Full-stack CARO 2020 audit workbench with statutory limit evaluation, clause templates, qualification/adverse remarks drafting, and two-lock multi-tenancy |
| 31 | Form 3CD Tax Audit Statement & Clause-by-Clause Disclosure Desk: Section 44AB limits (₹1 Cr / ₹10 Cr 5% cash rule; ₹50L / ₹75L profession), Form 3CA vs 3CB selection, Clause 1-44 workbench (ICDS, Sec 40(a)(ia) 30%, Sec 40A(3), Sec 43B & 43B(h) MSME 45-day overdue, Sec 269SS/ST/T, Clause 44 GST), disallowance aggregator, and auditor observation generator | Full-stack Form 3CD tax audit workbench with 5% cash test, MSME overdue calculator, quantified disallowance schedules, Form 3CA/3CB legal text draft, and two-lock multi-tenancy |
| 32 | Schedule III Financial Ratios & Mandatory Corporate Disclosures Engine: Companies Act 2013 Schedule III 11 mandatory analytical ratios, mandatory variance explanations for changes &ge; 25%, statutory ageing schedules (Trade Receivables, Trade Payables, CWIP), and ₹100 Cr turnover rounding off compliance engine | Full-stack Schedule III corporate reporting desk with 11 financial ratios, variance threshold guards, interactive ageing buckets, audited disclosure note compiler, and two-lock multi-tenancy |
| 33 | Multi-Firm Collaborative Audit & Branch Audit Consolidation Desk (Companies Act Sec 143(8) / ICAI SA 600): Branch auditor tracking, delegated portal token access, Head Office vs Branch consolidation matrix, SA 600 magnitude calculations (% Assets/Revenues), and statutory Other Matter paragraph generator | Full-stack multi-firm branch audit workbench with Section 143(8) branch auditor registry, external token submissions, consolidation engine, and ICAI SA 600 Other Matter legal text draft; two-lock multi-tenancy |
| 34 | Audit Committee & Board Governance Desk (ICAI SA 260 / SA 265 / Companies Act Sec 177): Section 177 Audit Committee mandate, SA 260 communication with Those Charged with Governance (TCWG), SA 265 formal internal control deficiency letter generator, SA 450 schedule of uncorrected misstatements, Section 144 non-audit services independence affirmation | Full-stack Audit Committee and Board governance workbench with Section 177 compliance checks, SA 265 deficiency communications (design vs operating defect), SA 450 misstatement evaluation table, and SA 260 governance pack auto-generator; two-lock multi-tenancy |
| 35 | Foreign Remittance & Form 15CA / 15CB Certification Engine (Income Tax Rule 37BB / Section 195 / DTAA): Section 195 non-resident withholding evaluation, Section 90(2) DTAA treaty relief comparison, Rule 37BB(3) 33-item exempt list checker, Form 15CA automated Part A/B/C/D routing, Form 15CB CA certification generator with 18-digit UDIN, and Section 195A net-of-tax grossing up engine | Full-stack international tax and foreign remittance certification desk with Section 195/90(2) tax calculators, DTAA prerequisite documentation tracker (TRC, Form 10F, No-PE), official Form 15CB certificate generator, and Form 15CA e-Filing XML exporter; two-lock multi-tenancy |
| 36 | Related Party Transactions & Section 188 / AS 18 / Ind AS 24 Arm's Length Pricing Desk: Section 188 Rule 15 statutory threshold gates (10% turnover / 10% net worth), Section 188(1) 4th proviso ordinary course & arm's length exemption engine, Section 177 Audit Committee omnibus approvals with utilization tracking, Section 189 Form MBP-4 statutory register generator, and AS 18 / Ind AS 24 financial statement disclosure compiler | Full-stack related party compliance and transfer pricing desk with statutory threshold guards, arm's length price variance calculators, Section 40A(2)(b) risk flags, Form MBP-4 register drafting, and AS 18 / Ind AS 24 disclosure note generator; two-lock multi-tenancy |
| 37 | Internal Audit Risk & Control Matrix (RCM) & IFC Testing Desk (ICAI SIA Standards / Companies Act Sec 138 & 143(3)(i)): Section 138 statutory applicability criteria, 6 core business cycles (P2P, O2C, H2R, R2R, Treasury, ITGC), dual walkthrough/design effectiveness and Test of Operating Effectiveness (ToC) review, residual risk mitigation engine, deficiency and action plan remediation tracking, and ICAI SIA executive report generator | Full-stack internal audit and IFC testing desk with pre-populated baseline RCM, exception-driven residual risk scoring, material weakness classification, remediation tracking, executive reporting, and two-lock multi-tenancy |
| 38 | GST Annual Audit & Anti-Profiteering / Rate Notification Engine (CGST Act Section 171 / Rule 122–137): Landmark GST rate notification registry, SKU-level commensurate price reduction engine, base price hike tracking, customer credit note refund logger, 50:50 Central/State Consumer Welfare Fund (CWF) split with 18% p.a. statutory interest under Section 171(3A), and official CA Anti-Profiteering Audit Certificate with 18-digit UDIN | Full-stack GST anti-profiteering desk with automatic baseline rate notification seeding, SKU price reduction evaluator, customer refund and CWF tracking, statutory certificate generator, and two-lock multi-tenancy |
| 39 | Secretarial Audit & Form MR-3 Compliance Engine (Companies Act 2013 Sec 204 / Rule 9): Statutory applicability calculator (Listed, Public capital ≥ ₹50 Cr / turnover ≥ ₹250 Cr, Borrowings ≥ ₹100 Cr), multi-statute verification checklist (Companies Act, SEBI LODR/PIT/SAST, FEMA FDI/ODI/ECB, Depositories, SCRA), MCA e-filing audits (AOC-4, MGT-7, DIR-12, CHG-1/4, DPT-3) with delay penalty calculator, Board & Committee meetings audit (120-day interval gap, 7-day notice, quorum, SS-1/SS-2), and official Form MR-3 Secretarial Audit Report generator with 18-character UDIN | Full-stack secretarial audit desk with automated statutory applicability gating, baseline checklist seeding, MCA return delay tracking, meeting interval compliance, Form MR-3 drafting, and two-lock multi-tenancy |
| 40 | Transfer Pricing Study & Form 3CEB Certification Desk (Income Tax Act Sec 92E / Rule 10D & 10E): Section 92A Associated Enterprises (AEs) registry, Clauses 11–20 international transactions & Clauses 21–25 Specified Domestic Transactions (SDT), Most Appropriate Method (MAM) benchmarking engine (CUP, RPM, CPM, PSM, TNMM, Other Method), Rule 10CA 35th–65th percentile range calculator with +/- 3% tolerance band, automated Section 92CA primary TP adjustment quantification, and official Form No. 3CEB Accountant's Report generator with 18-digit ICAI UDIN | Full-stack transfer pricing desk with Rule 10CA percentile range engine, tolerance band checks, AE register, primary TP adjustment calculator, official Form 3CEB compilation, and two-lock multi-tenancy |
| 41 | Cost Audit & Form CRA-1 to CRA-4 Desk (Companies Act 2013 Sec 148 / Cost Records and Audit Rules 2014): Rule 3 cost records applicability (≥ ₹35 Cr) & Rule 4 cost audit thresholds (Table A Regulated: ≥ ₹50 Cr overall & ≥ ₹25 Cr product; Table B Non-Regulated: ≥ ₹100 Cr overall & ≥ ₹35 Cr product; > 75% export / SEZ / captive power exemptions), 30 standard Form CRA-1 cost record heads verification matrix, Annexure Part B manufacturing product-wise cost statements with capacity utilization, prime/works/production/sales cost breakdowns, Material Cost/Price/Usage variance calculator, Annexure Part D profit reconciliation between cost accounts and audited P&L, Form CRA-2 auditor appointment tracking (earlier of 30 days from Board meeting or 180 days of FY), Form CRA-4 MCA XBRL filing tracker, and official Form CRA-3 Cost Audit Report generator with 18-digit ICMAI / ICAI UDIN | Full-stack statutory cost audit and CRA-1 to CRA-4 workbench with automated applicability gating, baseline 30 heads seeding, product costing sheets, material variance analytics, P&L profit reconciliation, MCA filing timeline monitors, CRA-3 report auto-drafting, and two-lock multi-tenancy |
| 42 | Universal Trial Balance & Financial Data Importer (Tally, Busy, SAP, Excel Clipboard TSV): Multi-format intelligent ingestion hub, fuzzy column header heuristic matcher, Indian Chart of Accounts auto-classifier (Schedule III Division I/II, Form 3CD, CARO 2020, Fixed Assets, Cost Audit CRA-1/3), 60-second interactive CA test drive with 3 pre-loaded real-world datasets (Tally Prime Manufacturing ₹46.5 Cr, Busy Accounting Trading ₹18.2 Cr, SAP ERP Services ₹85.0 Cr), live preview diagnostics with financial performance KPIs (Revenue, EBITDA, Assets, Debt, Current Ratio), and 6-desk cascading synchronization | Full-stack financial data ingestion hub supporting Tally XML, Busy TSV/CSV, SAP GL, and Excel clipboard paste; 1-click test drives for prospective CA firms; 558 passing unit tests; zero frontend build errors; two-lock multi-tenancy |
| 43 | 1-Click SA 230 Digital Audit Workpaper & Peer Review Dossier Generator (ICAI / NFRA Inspection Vault): 17 substantive analytical lead schedules (A to Q), SHA-256 tamper-evident integrity hash, SQC 1 cold review sign-off lock, complete indexed peer review dossier in standalone HTML | Full-stack audit dossier compiler with lead schedules A-Q, partner lock, and tamper-evident SHA-256 seal |
| 44 | Bank Statement Analyzer & Forensic Fraud Detection Engine: Ingests Indian bank statements (PDF/CSV/Excel), Trial Balance bank ledger cross-reconciliation, and 7-rule forensic audit engine (Sec 40A(3), Sec 269ST, Sec 269SS/T, Round-Tripping, Smurfing, Dormancy surges, High-Value round sums) | Full-stack forensic audit engine with statutory disallowance quantification, risk scoring meter, and automated anomalies register |
| 45 | SA 505 Automated External Confirmations Desk: End-to-end third-party balance confirmation campaign manager with tokenized digital verification links, public party confirmation portal, ledger discrepancy auto-matching, and SA 505 paragraph 12 alternative audit procedures | Full-stack SA 505 circularization desk with public client portal, alternative procedure logs, and peer-review-ready HTML export |
| 46 | 1-Click ICAI Independent Auditor's Report (SA 700/701/705/706) & Schedule III Notes to Accounts Generator: Automated statutory opinion engine (Unmodified, Qualified, Adverse, Disclaimer), SA 701 Key Audit Matters (KAM) panel, Section 143(3) statutory matrix, Rule 11(g) software audit trail edit log verification, 18-character ICAI UDIN cryptographically bound attestations, and Schedule III Notes to Accounts with 11 mandatory financial ratios and >25% variance alerts | Full-stack ICAI Independent Auditor's Report & Notes generator with UDIN validator, Rule 11(g) compliance, Schedule III ratios, and print-ready HTML exports; 593 passing unit tests; two-lock multi-tenancy |
| 47 | Commercial SaaS Monetization: Self-Service Subscriptions (Starter/Growth/Enterprise), 14-Day Free Trial & 1-Click "Demo Firm" Seeder: 3-tier SaaS pricing catalog (Starter ₹1,499/mo, Growth ₹4,999/mo, Enterprise ₹14,999/mo with 20% annual discount), SAC 998311 statutory tax invoicing (18% GST with 9% CGST + 9% SGST or 18% IGST), payment simulator (UPI AutoPay, NetBanking, Credit Card, PO), 14-day free trial status and countdown engine, and 1-click workspace seeder populating 3 realistic demo clients (Apex Global ₹46.5 Cr turnover with active statutory audit & lead schedules, Bharat Steel ₹18.2 Cr with bank forensics & confirmations, Vanguard Healthcare with GSTR-2B purchase invoices) plus instant demo purge | Full-stack SaaS billing & monetization suite with live trial banner, capacity meters, multi-tier checkout simulator, SAC 998311 tax invoice history, 1-click demo firm seeder; 605 passing unit tests; zero frontend build errors; two-lock multi-tenancy |
| 48 | Production Cloud Deployment & DevOps Readiness Suite: Hardened multi-stage Dockerfiles (.NET 10 Web API with non-root security & React/Vite/Caddy SPA), automated daily encrypted PostgreSQL backup suite (`backup-db.sh`, `restore-db.sh`, PowerShell equivalents, GFS rotation & AES-256-CBC), Kubernetes production manifests (Namespace, ConfigMap, Secrets, Postgres StatefulSet 50Gi, API 2-replica Deployment with HPA/probes, Web Deployment, Caddy Ingress with cert-manager Let's Encrypt TLS, and daily Backup CronJob), Kubernetes liveness (`/health/live`) and readiness (`/health/ready`) probes, Prometheus metrics exporter (`/metrics`), structured JSON console logging with latency telemetry, and production Caddy reverse proxy with HTTP/3 & A+ security headers | Production-hardened containerization & Kubernetes deployment suite; automated AES-256 backup engine with S3/GCS sync; Prometheus metrics; 609 passing unit tests; zero frontend build errors; two-lock multi-tenancy |
| 49 | ICAI Audit Quality Maturity Model (AQMM v1.0) Self-Assessment & Scoring Desk: Mandatory 600-point peer-review evaluation model developed by ICAI Centre for Audit Quality (CAQ), Section A Practice Management (190 points), Section B Human Resource Management (110 points), and Section C Audit Execution & SQC 1 (300 points), automated 3-tier level classification (Level 1 < 450 pts, Level 2 450–509 pts, Level 3 510–600 pts Excellence for Tier-1 public audits), dynamic gap analysis with prioritized remediation roadmap, automated evaluation from CA Desk live telemetry (PMLA/KYC, SQC 1 60-day archival locks, EQR reviews, SA 230 Lead Schedules A–Q, bank forensics, and UDIN validation), partner certification with 18-character UDIN, and print-ready HTML Peer Review Compliance Dossier & Scorecard | Full-stack ICAI AQMM v1.0 self-assessment desk with telemetry auto-scorer, gap analysis roadmap, UDIN certification, and standalone HTML peer review dossier; 632 passing unit tests; zero frontend build errors; two-lock multi-tenancy |
| 50 | Public SaaS Marketing Landing Page, Interactive Product Tour & Self-Serve Firm Onboarding Wizard: High-converting marketing portal showcasing CA Desk's 6 core regulatory moats (Universal TB, Bank Forensics, SA 505 Confirmations, SA 230 Workpapers, 1-Click SA 700 Report, and ICAI AQMM v1.0 Desk), 5-step interactive product tour modal simulator for immediate test drives without signup, 3-step self-serve firm onboarding wizard with ICAI FRN (Firm Registration Number e.g. 104523W) and Membership Number (5-6 digits) verification, Firm PAN entity validation ('F'/'C'/'P'), 1-click sample demo client pre-loader integration, interactive pricing calculator with 20% annual discount toggle, and seamless routing in App.tsx | High-converting SaaS landing page, interactive product tour simulator, and guided ICAI firm onboarding wizard; 655 passing unit tests; zero frontend build errors; two-lock multi-tenancy |
| 51 | Real-Time Automation & Communication Moat: Meta WhatsApp Business Cloud API (v21.0) & Multi-Provider Transactional Email Gateway (SendGrid / AWS SES / SMTP): Automated WhatsApp template campaigns for statutory filing deadlines (GST GSTR-1/3B/9, Advance Tax Q1-Q4 Sec 211/234C, Income Tax Return AY 2025-26), instant WhatsApp delivery of SA 505 external balance confirmation tokenized signing links (`url_button:confirm?token=...`), client portal 6-digit cryptographic TOTP/OTP delivery over WhatsApp & Email, transactional email gateway with PDF Tax Invoice attachments (SAC 998311 with 18% GST itemized breakdown), bidirectional Meta Webhook processor for delivery receipts (`delivered`, `read`, `failed`), and interactive Communications Gateway Desk workbench in web frontend | Full-stack communications gateway with WhatsApp Cloud API, SendGrid/SES email, statutory deadline campaigns, SA 505 circularization, and portal OTP; 671 passing unit tests; zero frontend build errors; two-lock multi-tenancy |
| 52 | Corporate Enterprise Security & Compliance Moat: ISO/IEC 27001:2022 & SOC 2 Type II Compliance Evidence Vault & Security Whitepaper Center: 18 standard baseline controls across 5 core security pillars (Access Control & Tenant Isolation, Cryptography & Key Management, Operations & Vulnerability Defense, Physical & Cloud Infrastructure, Privacy, DPDP 2023 & Data Governance), continuous automated telemetry audit scanner (PostgreSQL Two-Lock RLS status, TOTP MFA enrollment, append-only audit trail partitions, USB DSC custody logs, ClamAV antivirus stream protection, GFS backup verification, India data sovereignty, zero AI model training), Partner Certification workflow with Membership Number attestation, and 1-Click exportable standalone HTML Security Whitepaper & Trust Center dossier for corporate clients, enterprise RFPs, and statutory peer reviews | Full-stack ISO 27001:2022 & SOC 2 Type II Compliance Evidence Vault with continuous telemetry auditor, interactive 5-pillar meters, partner certification, and standalone HTML Security Whitepaper dossier; 679 passing unit tests; zero frontend build errors; two-lock multi-tenancy |
| 53 | Managing Partner Mobile Companion & PWA Suite: Progressive Web App (PWA) with installable manifest (`manifest.webmanifest`), Service Worker offline caching (`sw.js`), Apple mobile meta optimizations, Biometric / Quick PIN Lock Screen with PBKDF2 cryptographic hashing & 10,000 iterations, Managing Partner On-the-Go Action Center: 1-Tap SA 230 audit sign-offs with ICAI UDIN previews, urgent staff timesheet & billing invoice approvals, real-time Practice Pulse metrics (today/month collections, unbilled WIP balance, billable velocity, active staff), Bank Statement Forensic Anomaly Stream (Sec 40A(3), round-tripping, high-value round sums) with 1-tap triage, and statutory deadlines countdown | Full-stack PWA and Managing Partner mobile companion desk with 1-tap audit sign-offs, timesheet/invoice approvals, forensic anomaly triage, biometric & quick PIN lock, and service worker caching; 688 passing unit tests; zero frontend build errors; two-lock multi-tenancy |
| 54 | Financial Infrastructure & Connected Corporate Banking Moat: Direct Connected Corporate Banking API & 1-Click Vendor Payout Desk (ICICI / HDFC / Axis / Open): Open Banking direct statement streaming and live balance fetch without manual file uploads, Maker-Checker disbursement workflow (CA staff maker -> Managing Partner checker with OTP/DSC/Biometric authorization & real-time bank UTR generation), RBI limit guards (RTGS ≥ ₹2L, IMPS ≤ ₹5L, UPI ≤ ₹1L), 1-Click Statutory Tax Challan e-Payment compiler (ITNS 280 Advance Tax, ITNS 281 TDS, and PMT-06 GST Electronic Cash Ledger) with BSR/CPIN routing, and integrated Connected Banking Desk workbench | Full-stack connected corporate banking suite with live API feeds, maker-checker payouts, tax challan direct debits, and bank UTR tracking; 711 passing unit tests; zero frontend build errors; two-lock multi-tenancy |
| 55 | AI Practice Intelligence Moat: CA Copilot: Statutory Citation & Tax Notice Defense Drafter (Income Tax & GST): Automated notice ingestion and OCR categorizer (Income Tax Sec 143(1), Sec 148/148A, Sec 142(1), Sec 143(2), Sec 270A, Sec 271AAC; GST Form DRC-01, DRC-01A, DRC-01B Rule 88C, DRC-01C Rule 88D, Sec 73/74) with CBDT/CBIC DIN verification, curated statutory legal corpus of 60+ landmark Supreme Court, High Court, and ITAT rulings (Price Waterhouse Coopers, Kelvinator, GKN Driveshafts, Rajeev Bansal, Suncraft Energies, Arise India, Bharti Airtel, Radha Krishan Industries), and 1-Click Legal Reply Generator compiling print-ready formal written submissions on auditor/advocate letterhead with structured preliminary objections, factual matrix table, merit grounds, judicial authorities annexed, formal prayer for relief, and partner review with 18-character ICAI UDIN attestation | Full-stack AI tax copilot and legal defense drafter with notice text OCR parser, statutory precedent library, 1-click legal reply generator, and standalone printable HTML submission; 722 passing unit tests; zero frontend build errors; two-lock multi-tenancy |
| 56 | Global Cross-Border Moat: UAE Corporate Tax & Federal Tax Authority (FTA) EmaraTax Desk (Federal Decree-Law No. 47 of 2022): Statutory 9% UAE Corporate Tax calculation engine with AED 375,000 threshold, Qualifying Free Zone Person (QFZP) 0% qualifying vs 9% non-qualifying income categorization with Cabinet Decision No. 55/2023 & MD 139/2023 De Minimis test (lower of 5% or AED 5,000,000), Ministerial Decision No. 73 of 2023 Small Business Relief (AED 3M gross revenue ceiling in current and prior tax periods treating taxable income as AED 0), General Interest Deduction Limitation Rule under Article 30 (capping net interest at the higher of AED 12M or 30% EBITDA), Article 32 50% entertainment disallowance, Article 33 100% fine/penalty & recoverable VAT disallowances, Article 37 75% tax loss relief cap, 15-digit FTA Corporate Tax TRN validator (^100\d{12}$), IAS 21 multi-currency dual-ledger AED / INR conversions, and 1-Click FTA EmaraTax Form CT01 XML & JSON submission package compilation with Partner sign-off attestation | Full-stack UAE Corporate Tax & EmaraTax desk with interactive Form CT01 return studio, real-time statutory computation engine, partner sign-off, and 1-click FTA XML/JSON exports; 736 passing unit tests; zero frontend build errors; two-lock multi-tenancy |
| 57 | Practice Health & Peer Benchmarking Moat: CA Practice Health, Succession & Firm Valuation Benchmarking Desk: Institutional DCF & EBITDA/Revenue multiple valuation tailored for Indian CA practices (recurring retainer multiples, audit franchise stickiness, partner equity value per 1%), partnership deed equity cap table (100% allocation guard), retirement buyout amortization schedule (capital refund + goodwill buyout EMI with interest), client succession transition matrix with churn risk scoring, and real-time operational telemetry benchmarked against ICAI Tier 1/2/3 norms (Revenue per Partner, Realization Rate, Debtor DSO, Staff Leverage Ratio, WIP Lockup Days) | Full-stack CA practice health, succession, and valuation desk with interactive DCF modeler, equity cap table, retirement buyout amortization engine, and peer benchmarking telemetry; 742 passing unit tests; zero frontend build errors; two-lock multi-tenancy |
| 58 | Real-Time Ledger Intelligence Moat: Bank & Ledger Auto-Reconciliation Engine with Tally Prime / Busy Live Sync Agent: Lightweight background sync agent communicating with local Tally Prime (default port 9000 XML over HTTP) and Busy Accounting Software (port 9999 / ODBC / XML), syncing Daybook, Ledger Masters, and Vouchers in real time; 2-Way Bi-directional Voucher Posting directly pushing audited adjustments, Sec 32 depreciation entries, AY 25-26 tax provisions, and SA 700 audit journals into client Tally/Busy ledgers with Partner authorization; AI Bank Feed Discrepancy Matcher featuring 5 probabilistic heuristic matching rules (Exact Amount & Date, TDS Net-of-Tax deductions under Chapter XVII-B [1%, 2%, 5%, 10%], Cheque/UTR Narration matches, Minor Tolerance / FX Rounding variances [+/- ₹10], and Multi-Invoice Bundles) | Full-stack ledger auto-reconciliation engine with Tally/Busy XML protocol builder, probabilistic AI bank matching, 2-way outgoing adjustment posting, interactive workbench in React, and two-lock multi-tenancy; 753 passing unit tests; zero frontend build errors |
| 59 | Tax Litigation & High Court Appeal Brief Moat: ITAT & High Court Appeal Brief Builder & Rule 18 Paper Book Generator: Statutory Forms & Deadlines for Form 35 (CIT(A) Sec 246A/249) and Form 36 (ITAT Sec 253) with 30-day (CIT(A)), 60-day (ITAT), and 120-day (High Court Sec 260A) limitation countdown engines and automated condonation of delay warnings; ITAT Rule 18 Paginated Paper Book Compiler with sequential consecutive pagination, lower authority record vs. Rule 29 additional evidence tagging, and mandatory ITAT Rule 18(6) Certificate of True Copy; Statutory Appeal Filing Fee Calculator (CIT(A) Sec 249(1) ₹250/₹500/₹1,000; ITAT Sec 253(6) ₹500/₹1,500/1% up to ₹10,000 max cap); Judicial Authority Ratio Extractor binding landmark Supreme Court and High Court precedents with ratio propositions; 1-Click Print-Ready HTML Appeal Dossier generator | Full-stack tax litigation, ITAT & High Court appeal brief builder with Rule 18 paper book compiler, limitation countdowns, fee calculator, partner attestation, and standalone HTML dossier; 762 passing unit tests; zero frontend build errors; two-lock multi-tenancy |
| 60 | Enterprise ESG & CSR Statutory Audit Moat: SEBI BRSR Core & Companies Act Section 135 CSR Audit Engine: Companies Act 2013 Section 135(1) 3-pillar applicability gating (Net Worth ≥ ₹500 Cr, Turnover ≥ ₹1,000 Cr, Net Profit ≥ ₹5 Cr) and Section 198 3-year average net profit computation with mandatory 2% statutory CSR obligation; Section 135(5) 3rd proviso excess spent set-off carrying forward for 3 succeeding FYs; Multi-year ongoing project escrow tracking u/s 135(6) with mandatory 30-day transfer to special Unspent CSR Account in scheduled bank vs 6-month transfer to Schedule VII fund for other projects u/s 135(5); Automated CARO 2020 Clause XX(a) & Clause XX(b) statutory audit reporting remark generator with adverse qualification indicators; Schedule VII 12-category project portfolio tracker with Form CSR-1 Implementing Agency validation and aspirational district tagging; SEBI BRSR Core 9-Principle ESG engine with Scope 1 direct fuel emission calculation (Diesel 2.68 kg CO2/L, Natural Gas 1.90 kg CO2/Sm3) and Scope 2 grid electricity calculation (CEA India Baseline 0.82 tCO2/MWh), GHG/water/energy turnover intensities, Zero Liquid Discharge (ZLD) tracking, safety LTIFR and zero fatality benchmarking, gender diversity ratios across Board, KMP, and workforce, Composite BRSR Core ESG score (0-100%), ICAI UDIN partner sign-off attestation, and 1-Click Print-Ready HTML Reasonable Assurance Dossier export | Full-stack Enterprise ESG & Section 135 CSR audit engine with Schedule VII project master, CARO 2020 Clause XX generator, SEBI BRSR Core GHG assurance studio, and print-ready assurance dossier; 774 passing unit tests; zero frontend build errors; two-lock multi-tenancy |

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




## CARO 2020 Automated Audit Checklist & Disclosures Generator (Sprint 30)

- **Statutory Applicability Engine (Companies (Auditor's Report) Order, 2020 / Sec 143(11))**:
  - **Unconditional Statutory Exemptions**: One Person Companies (OPC), Small Companies (Sec 2(85)), Banking companies, Insurance companies, Section 8 not-for-profit companies.
  - **Deemed Public Company Gating (Sec 2(71))**: Any private company that is a subsidiary or holding company of a public company is disqualified from private company exemption.
  - **3-Pillar Cumulative Exemption Rule for Private Limited Companies**: Must satisfy ALL 3 conditions throughout the entire financial year:
    1. Paid-up share capital + Reserves & Surplus &le; ₹1 Crore as on balance sheet date.
    2. Total borrowings from banks & financial institutions &le; ₹1 Crore at any time during the financial year.
    3. Total revenue (including revenue from discontinuing operations) per Schedule III &le; ₹10 Crores during the financial year.
    - If any single threshold is breached, CARO 2020 is flagged as mandatory with precise breach citations.
- **21 Statutory Clauses Verification Workbench (Clauses i to xxi)**:
  - Clause (i): PPE & Intangible assets records, physical verification, title deeds held in company's name, revaluation (&ge; 10%), and Benami proceedings under PBPT Act.
  - Clause (ii): Physical verification of inventory (&ge; 10% discrepancy) & quarterly returns/statements filed with banks for sanctioned working capital limits &gt; ₹5 Crores.
  - Clause (iii): Investments, guarantees, security, and loans/advances granted to parties (terms not prejudicial, repayment schedule stipulation, overdue &gt; 90 days, renewal/fresh loans, repayable on demand/without terms).
  - Clause (iv): Compliance with Sections 185 and 186 (Loans & investments by company).
  - Clause (v): Public deposits & deemed deposits compliance with Sections 73 to 76 and RBI directives.
  - Clause (vi): Maintenance of cost records prescribed under Section 148(1).
  - Clause (vii): Undisputed statutory dues (GST, PF, ESI, Income-tax, Customs, Cess) outstanding &gt; 6 months, and disputed statutory dues with forums.
  - Clause (viii): Unrecorded transactions surrendered/disclosed in income tax assessments under Section 132/133A.
  - Clause (ix): Default in repayment of borrowings/interest, declared wilful defaulter, diversion of term loans, funds raised on short-term basis used for long-term purposes, funds taken to meet obligations of subsidiaries/associates/JVs.
  - Clause (x): Moneys raised by IPO/FPO and preferential allotment/private placement (Sec 42/62 compliance).
  - Clause (xi): Fraud by or on the company, Form ADT-4 filed with Central Govt under Sec 143(12), and whistle-blower complaints considered.
  - Clause (xii): Nidhi company net owned funds to deposit ratio (1:20) and 10% unencumbered term deposits.
  - Clause (xiii): Related party transactions compliance with Sections 177 and 188 with financial statement disclosures.
  - Clause (xiv): Internal audit system commensurate with size and nature, and consideration of internal audit reports.
  - Clause (xv): Non-cash transactions with directors/connected persons (Section 192 compliance).
  - Clause (xvi): Registration under Section 45-IA of RBI Act 1934, Core Investment Company (CIC) status, and group CIC limits.
  - Clause (xvii): Cash losses incurred in financial year and immediately preceding financial year.
  - Clause (xviii): Resignation of statutory auditors, issues/concerns raised considered.
  - Clause (xix): Material uncertainty on capability of company to meet liabilities falling due within 1 year from balance sheet date (financial ratios, ageing, expected dates of realization).
  - Clause (xx): Transfer of unspent CSR funds under Section 135(5) and Section 135(6) to Fund specified in Schedule VII or special account.
  - Clause (xxi): Qualifications or adverse remarks by respective auditors in CARO reports of group companies included in consolidated financial statements.
- **Formal Annexure 'A' to Independent Auditor's Report Generator**:
  - Automatically compiles standard legal Annexure 'A' text adhering to ICAI Guidance Note on CARO 2020.
  - Formats clean standard paragraphs for favorable clauses and structured qualification / adverse remark wording for modified clauses.
  - Exports ready-to-sign annexure draft into audit engagement file.
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `caro_assessments` and `caro_clause_responses` (`0029_caro2020.sql`).

API:
- `GET /api/caro/summary`, `GET /api/caro/clauses-template` (`caro.read`)
- `GET /api/caro/assessments`, `GET /api/caro/assessments/{id}`, `POST /api/caro/assessments` (`caro.read`, `caro.manage`)
- `PUT /api/caro/assessments/{assessmentId}/clauses/{clauseId}` (`caro.manage`)
- `POST /api/caro/assessments/{id}/generate-annexure`, `POST /api/caro/assessments/{id}/finalize` (`caro.manage`, `caro.finalize`)

## Form 3CD Tax Audit Statement & Clause-by-Clause Disclosure Desk (Sprint 31)

- **Section 44AB Statutory Applicability & Gating Engine**:
  - **Business Turnover Limits**: Standard limit ₹1 Crore; Enhanced limit ₹10 Crores (Finance Act 2020/2021 proviso) if both aggregate cash receipts $\le 5\%$ of turnover AND aggregate cash payments $\le 5\%$ of payments.
  - **Professional Gross Receipts Limits**: Standard limit ₹50 Lakhs; Enhanced limit ₹75 Lakhs if aggregate cash receipts $\le 5\%$.
  - **Presumptive Opt-Out Triggers**: Section 44AD(4) 5-year lockout penalty (Section 44AB(e)) and lower presumptive profits declared u/s 44AE/44BB/44BBB (Section 44AB(c)) or 44ADA (Section 44AB(d)).
  - **Audit Form Selection**: Form 3CA-3CD (for entities audited under any other law, e.g. Companies Act) vs Form 3CB-3CD (entities audited solely under Section 44AB).
- **Core Form 3CD Clauses Verification Workbench (Clauses 1 to 44)**:
  - **Part A (Clauses 1-8a)**: Assessee name, PAN, address, GSTIN, status, previous year, 44AB applicable clause, and special tax regimes (115BAA/115BAB/115BAC).
  - **Clause 13 (ICDS Adjustments I to X)**: Method of accounting (Mercantile/Cash), deviations, and schedule of net increase/decrease to taxable business profits across 10 Income Computation and Disclosure Standards.
  - **Clause 14 (Closing Stock)**: Inventory valuation deviations under Section 145A (inclusive method for GST/taxes).
  - **Clause 17 & 18**: Stamp duty valuation differentials (Sec 43CA / 50C) and Income-tax depreciation schedule by block of assets with 180-day 50% cutoff rules.
  - **Clause 20(b) (Sec 36(1)(va))**: Late deposit of employees' PF and ESI contributions beyond the statutory due date under the PF/ESI Act (Checkmate Services SC ruling disallowances).
  - **Clause 21(a) & 21(b) (Disallowances u/s 40(a))**: 100% disallowance on non-resident payments without TDS (Sec 40(a)(i)), 30% disallowance on resident payments without TDS (Sec 40(a)(ia)), and personal expenses/penalties (Sec 37).
  - **Clause 21(d) (Sec 40A(3))**: Cash payments exceeding ₹10,000 per person per day (or ₹35,000 for transport carriages) outside Rule 6DD.
  - **Clause 23 (Sec 40A(2)(b))**: Payments to specified persons and related parties evaluated for excessive consideration.
  - **Clause 26 & Section 43B Dues**: Unpaid statutory taxes, duties, employee dues, and bank interest not paid on or before Section 139(1) return filing due date.
  - **Clause 26/43B(h) (MSME Overdue Payments)**: Statutory 15-day (without agreement) or 45-day (with agreement) limits under Section 15 of MSMED Act, 2006. Payments delayed beyond limit and unpaid at year-end disallowed in the year of accrual.
  - **Clause 31 (Sec 269SS, 269ST, 269T)**: Cash loans/deposits accepted $\ge ₹20,000$, cash receipts $\ge ₹2,00,000$, and cash loan repayments $\ge ₹20,000$.
  - **Clause 34 (TDS/TCS Chapter XVII-B)**: Withholding tax compliance, timely filing of Form 24Q/26Q/27Q, and interest u/s 201(1A)/206C(7).
  - **Clause 40 & Clause 44**: Analytical financial ratios (Gross profit, Net profit, Stock turnover, Material consumed) and GST expenditure breakdown (exempt, composition, registered, unregistered).
- **Official Form 3CA / 3CB Report & Observations Generator**:
  - Auto-drafts legal tax audit report text conforming to ICAI Guidance Note on Tax Audit.
  - Compiles Paragraph 3 observation / qualification remarks for all modified clauses.
  - Summarizes statement of quantified additions to taxable business profit.
  - Formal partner sign-off freeze (`taxaudit.finalize`).
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `form3cd_assessments` and `form3cd_clause_responses` (`0030_form3cd.sql`).

API:
- `GET /api/taxaudit/summary`, `GET /api/taxaudit/clauses-template` (`taxaudit.read`)
- `POST /api/taxaudit/evaluate-applicability` (`taxaudit.read`)
- `GET /api/taxaudit/assessments`, `GET /api/taxaudit/assessments/{id}`, `POST /api/taxaudit/assessments`, `PUT /api/taxaudit/assessments/{id}` (`taxaudit.read`, `taxaudit.manage`)
- `PUT /api/taxaudit/assessments/{assessmentId}/clauses/{clauseId}` (`taxaudit.manage`)
- `POST /api/taxaudit/assessments/{id}/generate-report`, `POST /api/taxaudit/assessments/{id}/finalize` (`taxaudit.manage`, `taxaudit.finalize`)

## Schedule III Financial Ratios & Mandatory Corporate Disclosures Engine (Sprint 32)

- **Mandatory 11 Analytical Financial Ratios (Companies Act 2013, Schedule III)**:
  - Supports Division I (AS Non-Ind AS), Division II (Ind AS), and Division III (NBFC).
  - Computes all 11 statutory ratios for Current Financial Year vs Preceding Financial Year:
    1. *Current Ratio*: Current Assets / Current Liabilities
    2. *Debt-Equity Ratio*: Total Debt / Shareholder's Equity
    3. *Debt Service Coverage Ratio (DSCR)*: EBIT / Debt Service (Interest + Principal Repayments)
    4. *Return on Equity (ROE)*: (Net Profit after Taxes / Shareholder's Equity) $\times 100$
    5. *Inventory Turnover Ratio*: Cost of Goods Sold / Average Inventory
    6. *Trade Receivables Turnover Ratio*: Revenue from Operations / Average Trade Receivables
    7. *Trade Payables Turnover Ratio*: Net Credit Purchases / Average Trade Payables
    8. *Net Capital Turnover Ratio*: Revenue from Operations / Working Capital
    9. *Net Profit Ratio*: (Net Profit after Taxes / Revenue from Operations) $\times 100$
    10. *Return on Capital Employed (ROCE)*: (EBIT / Capital Employed) $\times 100$
    11. *Return on Investment (ROI)*: (Income from Investments / Average Investments) $\times 100$
- **Mandatory Variance Reporting (&ge; 25% Change Rule)**:
  - Automatically computes percentage change: $((CY - PY) / |PY|) \times 100$.
  - Flags any ratio with absolute variance $\ge 25\%$ with statutory alert.
  - Enforces mandatory recording of underlying business explanations required by MCA guidelines.
  - Tracks missing explanations count as a gating item before financial sign-off.
- **Statutory Ageing Schedules Workbench**:
  - **Trade Receivables Ageing**: Standard age buckets ($< 6\text{ months}$, $6\text{ months}-1\text{ year}$, $1-2\text{ years}$, $2-3\text{ years}$, $> 3\text{ years}$, and unbilled dues) cross-tabulated across 6 categories (Undisputed / Disputed, Considered Good / Credit Impaired).
  - **Trade Payables Ageing**: Age buckets ($< 1\text{ year}$, $1-2\text{ years}$, $2-3\text{ years}$, $> 3\text{ years}$, and unbilled dues) categorized by MSME dues, Others, and Disputed dues.
  - **Capital Work-in-Progress (CWIP) & Intangible Assets Ageing**: Projects in progress and projects temporarily suspended.
- **Statutory Rounding Off Compliance Rules (Schedule III General Instructions Paragraph 4)**:
  - If Total Income $< ₹100\text{ Crores}$: Verifies rounding in Hundreds, Thousands, Lakhs, or Millions.
  - If Total Income $\ge ₹100\text{ Crores}$: Flags non-compliance if rounded in Hundreds or Thousands (Schedule III mandates Lakhs, Millions, or Crores).
- **Official Financial Statement Note Generator**:
  - Auto-drafts ready-to-publish Note on "Additional Regulatory Information & Financial Ratios" formatted with statutory tabular presentations, formula disclosures, and variance explanation paragraphs.
  - Formal partner sign-off freeze (`schedule3.finalize`).
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `schedule3_assessments`, `schedule3_ratio_items`, and `schedule3_ageing_items` (`0031_schedule3_ratios.sql`).

API:
- `GET /api/schedule3/summary` (`schedule3.read`)
- `GET /api/schedule3/assessments`, `GET /api/schedule3/assessments/{id}`, `POST /api/schedule3/assessments`, `PUT /api/schedule3/assessments/{id}` (`schedule3.read`, `schedule3.manage`)
- `PUT /api/schedule3/assessments/{assessmentId}/ratios/{ratioId}` (`schedule3.manage`)
- `PUT /api/schedule3/assessments/{assessmentId}/ageing/{ageingId}` (`schedule3.manage`)
- `POST /api/schedule3/assessments/{id}/generate-note`, `POST /api/schedule3/assessments/{id}/finalize` (`schedule3.manage`, `schedule3.finalize`)

## Multi-Firm Collaborative Audit & Branch Audit Consolidation Desk (Sprint 33)

- **Companies Act, 2013, Section 143(8) & Rule 12 Mandate**:
  - Independent audit of branch offices by company's auditor or other qualified branch auditors.
  - Branch auditors prepare and forward certified financial returns and branch audit reports to the principal (statutory) auditor of the company.
  - Supports domestic branches and foreign branches (audited by accountants qualified under foreign law).
- **ICAI Standard on Auditing 600 (SA 600) "Using the Work of Another Auditor"**:
  - **Division of Responsibility**: Principal auditor relies on branch auditor reports without modifying audit opinion solely due to delegation, provided SA 600 procedures are carried out.
  - **Magnitude of Operations Quantification**: Automatically calculates the exact statutory percentage share of operations audited by other auditors:
    - *Branch Assets Magnitude (%)*: $(\text{Total Branch Assets} / \text{Total Consolidated Assets}) \times 100$
    - *Branch Revenue Magnitude (%)*: $(\text{Total Branch Revenue} / \text{Total Consolidated Revenue}) \times 100$
    - *Branch Net Profit Magnitude (%)*: $(\text{Total Branch Net Profit} / |\text{Total Consolidated Net Profit}|) \times 100$
  - **SA 600 Questionnaire & Governance Checkpoints**: Verifies accounting policy consistency, related party transactions, contingent liabilities, subsequent events, fraud/irregularities, internal controls, and CARO branch applicability.
- **Delegated Branch Auditor Portal Access**:
  - Generates secure, 32-byte cryptographic hex tokens (`SHA-256` hashed in database) with 30-day expiry.
  - Allows independent external branch auditors to access dedicated branch portal links without holding full CA firm credentials or crossing firm boundaries.
  - External auditors directly submit certified financial numbers, report type (`Clean`, `Qualified`, `Adverse`, `Disclaimer`), qualification summaries, CARO memos, and SA 600 affirmations.
  - Interactive simulator drawer allows principal auditors to test and verify portal submissions.
- **Principal Auditor Review & Consolidation Matrix**:
  - Two-tier workflow: External submission $\rightarrow$ Principal Auditor Review (`Accept` or `Reject` with remarks).
  - Standalone Head Office books consolidated with certified branch returns:
    - $\text{Total Consolidated Assets} = \text{Head Office Assets} + \sum \text{Branch Assets}$
    - $\text{Total Consolidated Liabilities} = \text{Head Office Liabilities} + \sum \text{Branch Liabilities}$
    - $\text{Total Consolidated Revenues} = \text{Head Office Revenue} + \sum \text{Branch Revenue}$
    - $\text{Total Consolidated Expenses} = \text{Head Office Expenses} + \sum \text{Branch Expenses}$
    - $\text{Total Consolidated Net Profit} = \text{Head Office Net Profit} + \sum \text{Branch Net Profit}$
- **Statutory "Other Matter" Paragraph Generator**:
  - Auto-drafts ready-to-publish "Other Matter - Using the Work of Other Auditors" paragraph for the Independent Auditor's Report.
  - Discloses aggregate branch asset and revenue counts and percentages.
  - Embeds Schedule of Branch Offices Audited under Section 143(8).
  - Details observations / qualifications noted in branch audit reports with firm FRN and partner details.
  - Formal partner sign-off freeze (`branchaudit.consolidate`).
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `branch_audit_engagements` and `branch_audit_offices` (`0032_branch_audit_consolidation.sql`).

API:
- `GET /api/branchaudit/summary` (`branchaudit.read`)
- `GET /api/branchaudit/engagements`, `GET /api/branchaudit/engagements/{id}` (`branchaudit.read`)
- `POST /api/branchaudit/engagements`, `PUT /api/branchaudit/engagements/{id}`, `DELETE /api/branchaudit/engagements/{id}` (`branchaudit.manage`)
- `POST /api/branchaudit/engagements/{id}/branches`, `PUT /api/branchaudit/engagements/{id}/branches/{branchId}`, `DELETE /api/branchaudit/engagements/{id}/branches/{branchId}` (`branchaudit.manage`)
- `POST /api/branchaudit/engagements/{id}/branches/{branchId}/generate-token` (`branchaudit.manage`)
- `GET /api/branchaudit/portal/branch`, `POST /api/branchaudit/portal/submit` (anonymous token-gated)
- `POST /api/branchaudit/engagements/{id}/branches/{branchId}/review` (`branchaudit.manage`)
- `POST /api/branchaudit/engagements/{id}/consolidate`, `POST /api/branchaudit/engagements/{id}/finalize` (`branchaudit.consolidate`)

## Audit Committee & Board Governance Desk (Sprint 34)

- **Companies Act, 2013, Section 177 Mandate**:
  - Examination of financial statements and the auditor's report thereon.
  - Review and monitoring of auditor's independence and performance, and effectiveness of the audit process.
  - Monitoring internal financial controls (IFC) and risk management systems.
  - Verification of Section 144 prohibited non-audit services compliance (accounting/bookkeeping, internal audit, design and implementation of any financial information system, actuarial services, investment advisory/banking, management services).
  - Tracking Audit Committee composition: minimum 3 directors, majority independent directors, financial literacy prerequisites.
- **ICAI SA 265 Internal Control Deficiency Letter Generator**:
  - "Communicating Deficiencies in Internal Control to Those Charged with Governance and Management".
  - Standardized statutory communication emphasizing that the audit's purpose is not to express an opinion on the effectiveness of internal control, but to design audit procedures.
  - Defect categorization:
    - *Design Defect*: Control missing or poorly designed to prevent or detect misstatements.
    - *Operating Effectiveness Defect*: Properly designed control failed in operation or personnel lacked authority/competence.
  - Severity classification: *Significant Deficiency* (merits attention of TCWG) vs. *Other Deficiency* (communicated to management).
  - Direct quantification of potential financial exposure and risk impact (e.g. ₹ Lakhs).
  - Structured auditor recommendation and management formal remediation commitment with target resolution dates.
- **ICAI SA 450 Evaluation of Uncorrected Misstatements**:
  - "Evaluation of Misstatements Identified during the Audit".
  - Schedule of uncorrected misstatements broken down into *Factual*, *Judgmental*, and *Projected* misstatements.
  - Quantification of aggregate misstatement impact across Balance Sheet and Statement of Profit & Loss.
  - Automated materiality threshold benchmarking (comparing against Planning Materiality and Performance Materiality).
  - Management justification tracking for non-adjustment and auditor concurrence.
- **ICAI SA 260 (Revised) Governance Communication Pack**:
  - "Communication with Those Charged with Governance".
  - Comprehensive governance presentation pack compilation:
    - Section 1: Auditor Independence Affirmation (ICAI Code of Ethics, Section 177(4)(i), Section 144 non-audit services limits, safeguards against threats, audit vs non-audit fee disclosures).
    - Section 2: Audit Scope, Planned Timing & Key Milestones.
    - Section 3: Significant Qualitative Findings & Accounting Policies (revenue recognition, provisioning, estimates, unusual transactions).
    - Section 4: Key Audit Matters (KAM - SA 701) preliminary discussion points.
    - Section 5: Annexure A - Formal SA 265 Internal Control Deficiencies Report.
    - Section 6: Annexure B - Schedule of Uncorrected Misstatements under SA 450.
  - Formal partner sign-off and lock mechanism (`auditcommittee.signoff`).
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `audit_committee_engagements`, `internal_control_deficiencies`, `significant_audit_matters`, and `uncorrected_misstatements` (`0033_audit_committee_governance.sql`).

API:
- `GET /api/auditcommittee/summary` (`auditcommittee.read`)
- `GET /api/auditcommittee/engagements`, `GET /api/auditcommittee/engagements/{id}` (`auditcommittee.read`)
- `POST /api/auditcommittee/engagements`, `PUT /api/auditcommittee/engagements/{id}`, `DELETE /api/auditcommittee/engagements/{id}` (`auditcommittee.manage`)
- `POST /api/auditcommittee/engagements/{id}/deficiencies`, `PUT /api/auditcommittee/engagements/{id}/deficiencies/{deficiencyId}`, `DELETE /api/auditcommittee/engagements/{id}/deficiencies/{deficiencyId}` (`auditcommittee.manage`)
- `POST /api/auditcommittee/engagements/{id}/matters`, `PUT /api/auditcommittee/engagements/{id}/matters/{matterId}`, `DELETE /api/auditcommittee/engagements/{id}/matters/{matterId}` (`auditcommittee.manage`)
- `POST /api/auditcommittee/engagements/{id}/misstatements`, `PUT /api/auditcommittee/engagements/{id}/misstatements/{misstatementId}`, `DELETE /api/auditcommittee/engagements/{id}/misstatements/{misstatementId}` (`auditcommittee.manage`)
- `POST /api/auditcommittee/engagements/{id}/generate-letters`, `POST /api/auditcommittee/engagements/{id}/signoff` (`auditcommittee.manage`, `auditcommittee.signoff`)

## Foreign Remittance & Form 15CA / 15CB Certification Engine (Sprint 35)

- **Income Tax Act, 1961 - Section 195 Withholding Mandate**:
  - Evaluation of taxability on payments made to non-residents / foreign companies.
  - Covers Royalties (Sec 9(1)(vi)), Fees for Technical Services (FTS / Sec 9(1)(vii)), Dividends, Interest, Capital Gains, and Business Profits.
  - Automatic calculation of domestic base tax rates, applicable surcharges, and 4% Health & Education Cess ($T_{\text{effective}} = \text{Tax} \times (1 + S) \times (1 + C)$).
- **Double Taxation Avoidance Agreement (DTAA) & Section 90(2) Relief Engine**:
  - "Whichever is more beneficial" principle: compares effective domestic rate against DTAA treaty rates.
  - Verifies mandatory treaty preconditions under Section 90(4) and 90(5):
    - Valid Tax Residency Certificate (TRC) issued by foreign revenue authority.
    - Electronic Form 10F self-declaration verified on the e-filing portal.
    - Non-existence of Permanent Establishment (PE) / Business Connection in India declaration.
    - Beneficial ownership of income confirmation.
    - Multilateral Instrument (MLI) Principal Purpose Test (PPT) compliance.
  - Fallback to domestic rate if any mandatory treaty prerequisite is missing or invalid.
- **Section 195A Net-of-Tax Grossing-Up Calculator**:
  - Automatically gross-up payments when the agreement stipulates that tax is borne by the Indian remitter:
    - $\text{Grossed-Up INR} = \frac{\text{INR Equivalent}}{1 - (\text{Withholding Rate} / 100)}$
    - $\text{TDS INR} = \text{Grossed-Up INR} - \text{INR Equivalent}$
- **Income Tax Rule 37BB Automated Form 15CA Routing**:
  - **Rule 37BB(3) Specified List**: Identifies 33 exempt payment purpose codes (e.g., P0103 import of goods, S0001 travel, S0005 education) where no Form 15CA/15CB is required.
  - **Part A**: Remittance is chargeable to tax and aggregate payments do not exceed ₹5,00,000 in the financial year.
  - **Part B**: Remittance is chargeable to tax and an order/certificate under Section 195(2), 195(3), or 197 has been obtained from the Assessing Officer.
  - **Part C**: Remittance is chargeable to tax and exceeds ₹5,00,000 in the financial year (MANDATORY Form 15CB CA Certificate).
  - **Part D**: Remittance is not chargeable to tax under the provisions of the Act.
- **Form 15CB Chartered Accountant Certification Engine**:
  - Compiles standard Form 15CB statutory certificate with remitter details, non-resident beneficiary particulars, invoice/contract details, domestic chargeability analysis, DTAA article justification, and TDS calculations.
  - Enforces mandatory 18-digit ICAI Unique Document Identification Number (UDIN).
  - Blocks certification until all mandatory documentation checklist items (TRC, Form 10F, No-PE, Invoice, Agreement) are verified or waived.
- **Form 15CA XML / JSON Schema Generator & Filing Tracker**:
  - Generates schema-compliant XML payload ready for direct upload to the Income Tax Department e-Filing portal.
  - Records portal filing with statutory Acknowledgement Number, sealing the record.
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `foreign_remittance_assessments` and `foreign_remittance_checklists` (`0034_foreign_remittance_15ca_15cb.sql`).

API:
- `GET /api/foreign-remittance/summary` (`foreignremittance.read`)
- `GET /api/foreign-remittance/assessments`, `GET /api/foreign-remittance/assessments/{id}` (`foreignremittance.read`)
- `POST /api/foreign-remittance/assessments`, `PUT /api/foreign-remittance/assessments/{id}`, `DELETE /api/foreign-remittance/assessments/{id}` (`foreignremittance.manage`)
- `PUT /api/foreign-remittance/assessments/{id}/checklist/{itemId}` (`foreignremittance.manage`)
- `POST /api/foreign-remittance/assessments/{id}/generate-artifacts` (`foreignremittance.manage`)
- `POST /api/foreign-remittance/assessments/{id}/certify` (`foreignremittance.certify`)
- `POST /api/foreign-remittance/assessments/{id}/file` (`foreignremittance.manage`)

## Related Party Transactions & Section 188 / AS 18 / Ind AS 24 Arm's Length Pricing Desk (Sprint 36)

- **Companies Act, 2013, Section 188 & Rule 15 Compliance**:
  - Related party contracts evaluation across goods, materials, property leasing, services, loans, guarantees, and office of profit remuneration.
  - Automated Rule 15 statutory threshold evaluation against the company's financials:
    - *Sale/Purchase/Supply of goods/materials*: $\ge$ 10% of annual turnover.
    - *Selling/Buying property*: $\ge$ 10% of net worth.
    - *Leasing of property*: $\ge$ 10% of annual turnover.
    - *Rendering/Availing of services*: $\ge$ 10% of annual turnover.
    - *Office of profit remuneration*: exceeding ₹2,50,000 per month (₹30,00,000 per year).
  - Triggers mandatory Special Resolution requirement when statutory thresholds are breached.
- **Section 188(1) Fourth Proviso Exemption & Transfer Pricing Engine**:
  - Automatically assesses the statutory safe harbor: transactions entered into in the **ordinary course of business** on an **arm's length basis** are exempt from Board and Shareholder approval requirements under Section 188(1).
  - Transfer pricing benchmarking methods:
    - Comparable Uncontrolled Price (CUP) Method
    - Resale Price Method (RPM)
    - Cost Plus Method (CPM)
    - Transactional Net Margin Method (TNMM)
    - Independent Valuation Method
  - Price variance percentage calculation with $\pm 3\%$ arm's length tolerance range.
  - Flags Section 40A(2)(b) excessive / unreasonable payment risks for disallowance under the Income Tax Act.
- **Audit Committee Section 177 Omnibus Approvals Management**:
  - Registers Audit Committee omnibus approvals valid for up to 1 financial year.
  - Real-time limit utilization tracking ($\text{Utilized Amount} / \text{Maximum Approved Limit}$) with criteria and validity period monitoring.
- **Form MBP-4 Register of Contracts & Arrangements Generator**:
  - "Register of contracts or arrangements in which directors are interested under Section 184 or contracts/arrangements with related parties under Section 188" pursuant to Section 189(1) and Rule 16(1).
  - Compiles ready-to-print legal register with transaction dates, party names, relationship categories, transaction values, arm's length confirmations, and committee approval dates.
- **AS 18 & Ind AS 24 Notes to Accounts Disclosure Compiler**:
  - Automatically compiles standard financial statement disclosure note:
    - Section A: Categorized list of related parties (Holding, Subsidiary, Fellow Subsidiary, Associate/JV, KMP & Relatives, Enterprises controlled by KMP).
    - Section B: Transaction summary matrix showing nature of transaction, relationship category, current year volume, and year-end closing balances (Debit/Credit).
    - Section C: Mandatory disclosures on terms, pricing basis, bad debt provisions, and guarantees.
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `related_party_entities`, `related_party_transactions`, and `related_party_omnibus_approvals` (`0035_related_party_transactions.sql`).

API:
- `GET /api/related-party/summary` (`relatedparty.read`)
- `GET /api/related-party/entities`, `GET /api/related-party/entities/{id}` (`relatedparty.read`)
- `POST /api/related-party/entities`, `PUT /api/related-party/entities/{id}`, `DELETE /api/related-party/entities/{id}` (`relatedparty.manage`)
- `GET /api/related-party/transactions`, `GET /api/related-party/transactions/{id}` (`relatedparty.read`)
- `POST /api/related-party/transactions`, `PUT /api/related-party/transactions/{id}`, `DELETE /api/related-party/transactions/{id}` (`relatedparty.manage`)
- `POST /api/related-party/transactions/{id}/approve` (`relatedparty.approve`)
- `GET /api/related-party/omnibus`, `POST /api/related-party/omnibus`, `PUT /api/related-party/omnibus/{id}`, `DELETE /api/related-party/omnibus/{id}` (`relatedparty.read`, `relatedparty.manage`)
- `GET /api/related-party/reports` (`relatedparty.read`)

## Internal Audit Risk & Control Matrix (RCM) & IFC Testing Desk (Sprint 37)

- **Statutory & Regulatory Framework**:
  - **Companies Act, 2013, Section 138 & Rule 13 of Companies (Accounts) Rules, 2014**:
    - Mandatory internal audit for listed companies, unlisted public companies ($\ge$ ₹50 Cr capital / ₹200 Cr turnover / ₹100 Cr loans / ₹25 Cr deposits), and private companies ($\ge$ ₹200 Cr turnover / ₹100 Cr loans).
  - **Companies Act, 2013, Section 143(3)(i) & Rule 10A**:
    - Statutory auditor reporting on adequacy and operating effectiveness of Internal Financial Controls with reference to financial statements (IFC-FR).
  - **ICAI Standards on Internal Audit (SIA Framework)**:
    - SIA 110 (Scope), SIA 120 (Internal Controls), SIA 210, SIA 310 (Planning & Universe), SIA 320 (Testing), SIA 330 (Documentation), SIA 340 (Communication).
- **Comprehensive Risk & Control Matrix (RCM) Workbench**:
  - Pre-populated standard baseline controls covering 6 core enterprise business cycles:
    - **Procure to Pay (P2P)**: Automated 3-way matching, PO controls, vendor master authorizations.
    - **Order to Cash (O2C)**: Customer credit limits, order dispatch locks, automated billing.
    - **Hire to Retire (H2R)**: Biometric attendance reconciliation, payroll variance review, ghost employee prevention.
    - **Record to Report (R2R)**: Manual journal entry authorization threshold, financial controller sign-off.
    - **Treasury & Cash Management**: Monthly Bank Reconciliation Statement (BRS) reviews, dual signature mandates.
    - **IT General Controls (ITGC)**: User access management, segregation of duties (SoD), quarterly privileged access audits.
  - Supports extended cycles: Inventory & Warehousing, Fixed Assets & Capitalization.
- **Dual Evaluation Testing Methodology**:
  - **Design Effectiveness Review**: Walkthrough testing, sample documents, design conclusion (Effective vs Deficient).
  - **Test of Operating Effectiveness (ToC)**: Testing methods (Inspection, Reperformance, Observation, Inquiry), sample size, exceptions count, operating conclusion (Operating Effectively vs Ineffective).
  - **Automated Residual Risk Evaluator**:
    - Controls with effective design + operating effectiveness mitigate inherent risk to **Low**.
    - Controls with deficient design, ineffective operating result, or exceptions found are flagged as **High Residual Risk** with mandatory audit findings.
  - **Overall IFC Health Score**: Dynamically calculates the percentage of tested controls operating effectively without exceptions.
- **Internal Audit Observations & Remediation Tracker**:
  - Deficiency title, control reference, severity (Critical, High, Medium, Low).
  - IFC classification (Material Weakness, Significant Deficiency, Operational Deficiency, Process Improvement).
  - Estimated financial exposure (₹), root cause analysis, and auditor recommendations.
  - Management Action Plan tracking: Action owner, target completion date, implementation status (Open, InProgress, Implemented, Overdue).
- **Executive Internal Audit & IFC Report Generator**:
  - Automatically compiles an executive report formatted per ICAI Standards on Internal Audit (SIA) and Section 138/143(3)(i).
  - Features executive summary, overall IFC health score, cycle-wise control effectiveness matrix, and formal schedule of internal audit findings and management action plans.
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `internal_audit_engagements`, `rcm_control_items`, and `internal_audit_findings` (`0036_internal_audit_rcm_ifc.sql`).

API:
- `GET /api/internal-audit/summary` (`internalaudit.read`)
- `GET /api/internal-audit/engagements`, `GET /api/internal-audit/engagements/{id}` (`internalaudit.read`)
- `POST /api/internal-audit/engagements`, `PUT /api/internal-audit/engagements/{id}`, `DELETE /api/internal-audit/engagements/{id}` (`internalaudit.manage`)
- `GET /api/internal-audit/engagements/{id}/controls`, `POST /api/internal-audit/engagements/{id}/controls`, `PUT /api/internal-audit/engagements/{id}/controls/{controlId}`, `DELETE /api/internal-audit/engagements/{id}/controls/{controlId}` (`internalaudit.read`, `internalaudit.manage`)
- `GET /api/internal-audit/engagements/{id}/findings`, `POST /api/internal-audit/engagements/{id}/findings`, `PUT /api/internal-audit/engagements/{id}/findings/{findingId}`, `DELETE /api/internal-audit/engagements/{id}/findings/{findingId}` (`internalaudit.read`, `internalaudit.manage`)
- `PATCH /api/internal-audit/engagements/{id}/findings/{findingId}/status` (`internalaudit.manage`)
- `POST /api/internal-audit/engagements/{id}/generate-report` (`internalaudit.report`)

## GST Annual Audit & Anti-Profiteering / Rate Notification Engine (Sprint 38)

- **Statutory & Regulatory Framework**:
  - **CGST Act, 2017, Section 171 & CGST Rules, 2017, Rules 122–137**:
    - Mandatory pass-through of any tax rate reduction or input tax credit (ITC) benefit to recipients by commensurate reduction in prices.
    - Statutory mandate to prevent suppliers from pocketing tax cuts by hiking pre-tax base prices while keeping consumer selling prices static.
  - **Section 171(3A) & Rule 133(3)(c) Penalty, Refund & Consumer Welfare Fund (CWF)**:
    - Undue profiteered amount must be refunded to identified recipients via commercial credit notes or bank transfers.
    - If recipients are unidentifiable (e.g. over-the-counter FMCG, retail sales, untraceable cash buyers), the undue amount must be credited in equal 50:50 proportion into the **Central Consumer Welfare Fund** and the relevant **State Consumer Welfare Fund**.
    - If delayed beyond statutory timelines, statutory interest @ **18% p.a.** applies from the collection date to the deposit date.
- **Landmark GST Rate Notifications Registry**:
  - Pre-populated baseline rate cuts across major GST Council decisions:
    - **Notification 41/2017-CT(R)**: Restaurants rate cut from 18% to 5% (without ITC).
    - **Notification 18/2018-CT(R)**: White goods, refrigerators, washing machines, TV up to 32" cut from 28% to 18%.
    - **Notification 24/2018-CT(R)**: Footwear & apparel rate rationalization from 18% to 12%.
    - **Notification 14/2019-CT(R)**: Electric Vehicles (EVs) rate reduction from 12% to 5%.
    - **Notification 06/2022-CT(R)**: Pre-packaged & labelled food products rate alignment.
- **Automated Commensurate Price Reduction Engine**:
  - Calculates statutory commensurate price:
    $$\text{Commensurate Selling Price} = \text{Pre-Cut Base Price} \times (1 + \text{New GST Rate})$$
    $$\text{Profiteering Per Unit} = \text{Max}(0, \text{Post-Cut Selling Price} - \text{Commensurate Price})$$
    $$\text{Total Profiteered Amount} = \text{Profiteering Per Unit} \times \text{Post-Cut Quantity Sold}$$
  - Automated classification:
    - **Compliant**: Full price reduction passed through to recipients.
    - **Profiteered**: Base price hiked to neutralize rate cut, resulting in zero or negligible price drop.
    - **Deficient**: Price was reduced, but by less than the full commensurate reduction.
- **Customer Refund & Consumer Welfare Fund (CWF) Desk**:
  - Tracks commercial credit notes / refunds issued directly to identifiable buyers.
  - Generates 50:50 Central CWF and State CWF payment challans for unidentifiable consumers.
  - Computes statutory 18% p.a. interest for overdue refunds beyond 30 days.
- **Statutory CA Audit Certificate under Section 171 & Rules 122–137**:
  - Auto-drafts formal CA certificate verifying SKU price reductions, base price trends, and refund disbursements.
  - Gated with mandatory 18-digit ICAI Unique Document Identification Number (UDIN).
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `gst_rate_notifications`, `anti_profiteering_assessments`, `sku_profiteering_items`, and `profiteering_refund_records` (`0037_anti_profiteering_engine.sql`).

API:
- `GET /api/anti-profiteering/summary` (`antiprofiteering.read`)
- `GET /api/anti-profiteering/notifications`, `GET /api/anti-profiteering/notifications/{id}` (`antiprofiteering.read`)
- `POST /api/anti-profiteering/notifications`, `PUT /api/anti-profiteering/notifications/{id}`, `DELETE /api/anti-profiteering/notifications/{id}` (`antiprofiteering.manage`)
- `GET /api/anti-profiteering/assessments`, `GET /api/anti-profiteering/assessments/{id}` (`antiprofiteering.read`)
- `POST /api/anti-profiteering/assessments`, `PUT /api/anti-profiteering/assessments/{id}`, `DELETE /api/anti-profiteering/assessments/{id}` (`antiprofiteering.manage`)
- `POST /api/anti-profiteering/assessments/{id}/certify` (`antiprofiteering.certify`)
- `GET /api/anti-profiteering/assessments/{id}/skus`, `POST /api/anti-profiteering/assessments/{id}/skus`, `PUT /api/anti-profiteering/assessments/{id}/skus/{skuId}`, `DELETE /api/anti-profiteering/assessments/{id}/skus/{skuId}` (`antiprofiteering.read`, `antiprofiteering.manage`)
- `GET /api/anti-profiteering/assessments/{id}/refunds`, `POST /api/anti-profiteering/assessments/{id}/refunds`, `PUT /api/anti-profiteering/assessments/{id}/refunds/{refundId}`, `DELETE /api/anti-profiteering/assessments/{id}/refunds/{refundId}` (`antiprofiteering.read`, `antiprofiteering.manage`)

## Secretarial Audit & Form MR-3 Compliance Engine (Sprint 39)

- **Statutory & Regulatory Framework**:
  - **Companies Act, 2013, Section 204 & Rule 9 of Companies (Appointment and Remuneration of Managerial Personnel) Rules, 2014**:
    - Mandatory secretarial audit for:
      - All listed companies;
      - Every unlisted public company having paid-up share capital $\ge$ ₹50 crore; OR
      - Every unlisted public company having turnover $\ge$ ₹250 crore; OR
      - Every company (including private companies) having outstanding loans or borrowings from banks or public financial institutions $\ge$ ₹100 crore.
    - Statutory secretarial audit report in **Form MR-3** annexed to Board's Report pursuant to Section 134(3)(f).
- **Multi-Statute Verification Checklist Workbench**:
  - Pre-populated baseline secretarial audit checklist items across all statutory codes:
    - **Companies Act, 2013**: Board composition & independent/woman directors (Sec 149), board meetings & 120-day interval gap (Sec 173), quorum (Sec 174), Audit Committee (Sec 177), NRC & SRC (Sec 178), borrowings limits (Sec 180), loans to directors & investments (Sec 185/186), related party transactions (Sec 188), CSR 2% obligation (Sec 135), and Board report disclosures (Sec 134).
    - **Securities & Exchange Board of India (SEBI)**: LODR Regulation 17 (Board composition & independence), Regulation 30 (Material event disclosures within 24h / 30m), PIT Regulation 3(5) (Structured Digital Database - SDD maintenance), SAST Regulations 29–31 (shareholding disclosures).
    - **Foreign Exchange Management Act, 1999 (FEMA)**: FDI reporting in Form FC-GPR (allotment within 30 days), Form FC-TRS (shares transfer), annual return on Foreign Liabilities and Assets (FLA) by July 15, and monthly ECB-2 returns.
    - **Depositories Act, 1996**: Mandatory dematerialization of promoter/director securities (PAS Rules 9A/9B) and share capital audit under SEBI D&P Reg 76.
    - **Industry-Specific & Social Security Laws**: EPF, ESI, Payment of Gratuity, and State Pollution Control Board consents.
- **MCA V2/V3 Portal E-Filing Audit**:
  - Verifies statutory forms (`AOC-4`, `MGT-7`, `DIR-12`, `CHG-1`, `CHG-4`, `DPT-3`, `BEN-2`, `MGT-14`, `MSME-1`, `PAS-3`).
  - Automated statutory due date calculation, delay counter, and additional fee penalty recording.
- **Board & Committee Meetings Governance Log**:
  - Evaluates Section 173(1) 120-day maximum consecutive interval rule between Board meetings.
  - Validates Section 173(3) 7-day advance notice requirement.
  - Checks Section 174 quorum compliance (greater of 1/3rd or 2 members) and presence of independent directors.
  - Adherence to ICSI Secretarial Standards SS-1 (Meetings of the Board) and SS-2 (General Meetings).
- **Official Form MR-3 Secretarial Audit Report Generator**:
  - Formats statutory Form MR-3 report including corporate details, examined statutes, qualification/reservation paragraphs, board processes, and internal compliance mechanisms.
  - Gated with mandatory 17/18-character ICSI/ICAI Unique Document Identification Number (UDIN).
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `secretarial_audit_engagements`, `secretarial_checklist_items`, `mca_form_filing_audits`, and `board_meeting_audit_records` (`0038_secretarial_audit_mr3.sql`).

API:
- `GET /api/secretarial-audit/summary` (`secretarialaudit.read`)
- `POST /api/secretarial-audit/check-applicability` (`secretarialaudit.read`)
- `GET /api/secretarial-audit/engagements`, `GET /api/secretarial-audit/engagements/{id}` (`secretarialaudit.read`)
- `POST /api/secretarial-audit/engagements`, `PUT /api/secretarial-audit/engagements/{id}`, `DELETE /api/secretarial-audit/engagements/{id}` (`secretarialaudit.manage`)
- `POST /api/secretarial-audit/engagements/{id}/certify` (`secretarialaudit.certify`)
- `POST /api/secretarial-audit/engagements/{id}/generate-mr3` (`secretarialaudit.read`)
- `GET /api/secretarial-audit/engagements/{id}/checklist`, `POST /api/secretarial-audit/engagements/{id}/checklist`, `PUT /api/secretarial-audit/engagements/{id}/checklist/{itemId}`, `DELETE /api/secretarial-audit/engagements/{id}/checklist/{itemId}` (`secretarialaudit.read`, `secretarialaudit.manage`)
- `POST /api/secretarial-audit/engagements/{id}/checklist/batch` (`secretarialaudit.manage`)
- `GET /api/secretarial-audit/engagements/{id}/filings`, `POST /api/secretarial-audit/engagements/{id}/filings`, `PUT /api/secretarial-audit/engagements/{id}/filings/{filingId}`, `DELETE /api/secretarial-audit/engagements/{id}/filings/{filingId}` (`secretarialaudit.read`, `secretarialaudit.manage`)
- `GET /api/secretarial-audit/engagements/{id}/meetings`, `POST /api/secretarial-audit/engagements/{id}/meetings`, `PUT /api/secretarial-audit/engagements/{id}/meetings/{meetingId}`, `DELETE /api/secretarial-audit/engagements/{id}/meetings/{meetingId}` (`secretarialaudit.read`, `secretarialaudit.manage`)

## Transfer Pricing Study & Form 3CEB Certification Desk (Sprint 40)

- **Statutory & Regulatory Framework**:
  - **Income Tax Act, 1961, Section 92E & Rule 10E of Income-tax Rules, 1962**:
    - Mandatory report from an Accountant in **Form No. 3CEB** for every person who has entered into an international transaction or Specified Domestic Transaction (SDT) during a previous year.
  - **Section 92A**: Associated Enterprises (AEs) definitions covering direct/indirect voting power ($\ge$ 26%), loans exceeding 51% of book value of total assets, guarantees exceeding 10% of total borrowings, board appointment control, and 100% dependency on patents/IP or supply.
  - **Section 92B & Section 92BA**: International transactions (Clauses 11 to 20) and Specified Domestic Transactions exceeding statutory threshold of ₹20 Crore (Clauses 21 to 25).
- **Rule 10CA Arm's Length Range & Tolerance Band Engine**:
  - **Percentile Range Concept ($\ge$ 6 comparables)**:
    - Constructs an ordered dataset in ascending order.
    - Calculates 35th percentile, 65th percentile, and 50th percentile (Median).
    - If tested party margin falls between 35th and 65th percentile, transaction is deemed at arm's length.
    - If deficient, primary transfer pricing adjustment under Section 92CA is calculated with reference to the **Median (50th percentile)**.
  - **Arithmetic Mean Concept ($<$ 6 comparables)**:
    - Calculates arithmetic mean with a statutory tolerance band: **1% for wholesale trading** and **3% for other transactions**.
    - If outside tolerance band, adjustment is computed to the arithmetic mean.
- **Associated Enterprises (AE) Registry**:
  - Maintains records of foreign and domestic AEs, country of residence, ISO codes, Tax Identification Numbers (TIN/EIN), Section 92A(2)(a)–(m) relationship basis, and ownership percentages.
- **Comparable Benchmark Database**:
  - Peer company dataset recording Operating Revenue, Operating Cost, Operating Profit, and calculated Profit Level Indicators (PLI: OP/TC, OP/OR, GP/Sales, GP/Cost).
  - Quantitative and qualitative filter rejection reason tracking with automatic recalculation of study benchmark ranges.
- **Official Form No. 3CEB Statutory Report Generator**:
  - Auto-drafts complete Form 3CEB:
    - Accountant's Certificate verifying Rule 10D Local File documentation.
    - Part A: Assessee profile, PAN, and aggregate international & SDT volumes.
    - Part B: Clause-by-clause particulars of international transactions (tangibles, intangibles, services, loans, guarantees, capital issues).
    - Part C: Specified Domestic Transactions under Section 80A/80-IA/115BAB.
    - Schedule of Primary Transfer Pricing Adjustments under Section 92CA.
  - Gated with mandatory 18-digit ICAI Unique Document Identification Number (UDIN).
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `transfer_pricing_studies`, `associated_enterprises`, `transfer_pricing_transactions`, and `comparable_benchmarks` (`0039_transfer_pricing_3ceb.sql`).

API:
- `GET /api/transfer-pricing/summary` (`transferpricing.read`)
- `POST /api/transfer-pricing/calculate-benchmark` (`transferpricing.read`)
- `GET /api/transfer-pricing/studies`, `GET /api/transfer-pricing/studies/{id}` (`transferpricing.read`)
- `POST /api/transfer-pricing/studies`, `PUT /api/transfer-pricing/studies/{id}`, `DELETE /api/transfer-pricing/studies/{id}` (`transferpricing.manage`)
- `POST /api/transfer-pricing/studies/{id}/certify` (`transferpricing.certify`)
- `POST /api/transfer-pricing/studies/{id}/generate-3ceb` (`transferpricing.read`)
- `GET /api/transfer-pricing/studies/{id}/enterprises`, `POST /api/transfer-pricing/studies/{id}/enterprises`, `PUT /api/transfer-pricing/studies/{id}/enterprises/{aeId}`, `DELETE /api/transfer-pricing/studies/{id}/enterprises/{aeId}` (`transferpricing.read`, `transferpricing.manage`)
- `GET /api/transfer-pricing/studies/{id}/transactions`, `POST /api/transfer-pricing/studies/{id}/transactions`, `PUT /api/transfer-pricing/studies/{id}/transactions/{txId}`, `DELETE /api/transfer-pricing/studies/{id}/transactions/{txId}` (`transferpricing.read`, `transferpricing.manage`)
- `GET /api/transfer-pricing/studies/{id}/comparables`, `POST /api/transfer-pricing/studies/{id}/comparables`, `PUT /api/transfer-pricing/studies/{id}/comparables/{compId}`, `DELETE /api/transfer-pricing/studies/{id}/comparables/{compId}` (`transferpricing.read`, `transferpricing.manage`)

### Sprint 41: Cost Audit & Form CRA-1 to CRA-4 Desk (Companies Act, 2013, Section 148 / Cost Records and Audit Rules, 2014)

End-to-end statutory Cost Audit and Form CRA-1 to CRA-4 compliance workbench for Cost and Management Accountants (CMAs) and Chartered Accountants:
- **Statutory Applicability Engine (Rule 3 & Rule 4)**:
  - **Rule 3 (Cost Records in Form CRA-1)**: Mandated for specified domestic and foreign companies with overall turnover from all products/services $\ge$ **₹35 Crore** in the immediately preceding financial year.
  - **Rule 4 (Cost Audit in Form CRA-3)**:
    - **Table A (Regulated Sectors)**: Telecommunication, Electricity, Petroleum, Drugs & Pharmaceuticals, Fertilizers, Sugar & Industrial Alcohol. Thresholds: Overall turnover $\ge$ **₹50 Crore** AND product turnover $\ge$ **₹25 Crore**.
    - **Table B (Non-Regulated Sectors)**: Steel, Cement, Chemicals, Textiles, Automotive Components, Capital Goods, Paper, Mining, etc. Thresholds: Overall turnover $\ge$ **₹100 Crore** AND product turnover $\ge$ **₹35 Crore**.
    - **Rule 4(3) Statutory Exemptions**: Automated exemption checks for companies whose export revenue in foreign exchange exceeds 75% of total revenue, companies operating from Special Economic Zones (SEZs), and electricity generation for captive consumption.
- **Form CRA-1: 30 Standard Heads of Cost Records Matrix**:
  - Pre-seeded baseline of 30 standard statutory heads under Rule 3 (Direct Materials, Wages, Utilities, Service Departments, Depreciation, Factory/Admin/Selling Overheads, R&D, Royalties, Quality Control, Pollution Control, Abnormal Costs, Packing, By-Products, WIP/FG Valuation, Reconciliation, Related Party Transactions, etc.).
  - Tracks compliance status (`Compliant`, `PartiallyCompliant`, `NonCompliant`, `NotApplicable`), ERP system of record, auditor verification test notes, deficiencies, and corrective action recommendations.
  - Live compliance score calculation across applicable heads.
- **Form CRA-3 Annexure Part B / Part C Product Cost Statements & Variance Engine**:
  - Captures quantitative capacity data: Installed capacity, available capacity, actual production, and capacity utilization percentage.
  - Full cost sheet breakdown: Direct Materials, Direct Labor, Utilities, Other Direct Expenses, Prime Cost, Works Overheads, Works Cost, Quality Control, R&D, Admin Overheads, WIP adjustments, Cost of Production, FG adjustments, Selling & Distribution Overheads, Cost of Sales, Net Sales Realization, and Product Margins (amount and %).
  - Automated per-unit metrics: Unit Cost of Sales, Unit Sales Realization, and Unit Margin.
  - Material Cost Variance calculator: Material Price Variance, Material Usage Variance, and Total Cost Variance with favorable/adverse classification.
- **Form CRA-3 Annexure Part D Profit Reconciliation with Audited Financial Accounts**:
  - Bridges Profit as per Cost Records with Audited Profit as per Financial Accounts (P&L).
  - Categorizes additions (Incomes not costed, cost expenses overcharged) and deductions (Expenses not costed like penalties/CSR/goodwill, financial expenses overcharged, stock valuation variances).
  - Displays instant balanced vs discrepancy variance status.
- **Statutory Filing Deadlines & Monitors (Form CRA-2 & Form CRA-4)**:
  - **Form CRA-2 (Auditor Appointment Intimation)**: Automatically calculates statutory due date as earlier of 30 days from Board meeting or 180 days of FY commencement, tracks MCA SRN, and flags overdue status.
  - **Form CRA-4 (Filing Cost Audit Report with MCA)**: Tracks 30-day statutory window from the date of receipt of the Cost Audit Report by the Board, logs MCA SRN, and tracks XBRL schema pre-validation.
- **Official Form CRA-3 Cost Audit Report Generator & UDIN Sign-Off**:
  - Auto-drafts complete statutory Cost Audit Report in MCA CRA-3 format (Cost Auditor's Report, qualifications/adverse remarks, Annexure Part A General Information, Part B Manufacturing Cost Statements, Part D Profit Reconciliation Statement).
  - Sign-off gated by mandatory 18-digit ICMAI / ICAI Unique Document Identification Number (UDIN).
- **Two Locks**: EF Core global query filters and PostgreSQL Row Level Security on `cost_audit_engagements`, `cost_record_heads`, `product_cost_statements`, and `cost_audit_reconciliation_items` (`0040_cost_audit_cra.sql`).

API:
- `GET /api/cost-audit/summary` (`costaudit.read`)
- `POST /api/cost-audit/applicability-check` (`costaudit.read`)
- `POST /api/cost-audit/variance-calculate` (`costaudit.read`)
- `GET /api/cost-audit/engagements`, `GET /api/cost-audit/engagements/{id}` (`costaudit.read`)
- `POST /api/cost-audit/engagements`, `PUT /api/cost-audit/engagements/{id}`, `DELETE /api/cost-audit/engagements/{id}` (`costaudit.manage`)
- `POST /api/cost-audit/engagements/{id}/seed-heads` (`costaudit.manage`)
- `PUT /api/cost-audit/engagements/{id}/heads/{headId}` (`costaudit.manage`)
- `POST /api/cost-audit/engagements/{id}/products`, `PUT /api/cost-audit/engagements/{id}/products/{productId}`, `DELETE /api/cost-audit/engagements/{id}/products/{productId}` (`costaudit.manage`)
- `POST /api/cost-audit/engagements/{id}/reconciliation`, `PUT /api/cost-audit/engagements/{id}/reconciliation/{itemId}`, `DELETE /api/cost-audit/engagements/{id}/reconciliation/{itemId}` (`costaudit.manage`)
- `POST /api/cost-audit/engagements/{id}/cra2-filing` (`costaudit.manage`)
- `POST /api/cost-audit/engagements/{id}/cra4-filing` (`costaudit.manage`)
- `POST /api/cost-audit/engagements/{id}/generate-cra3` (`costaudit.manage`)
- `POST /api/cost-audit/engagements/{id}/certify` (`costaudit.certify`)

### Sprint 42: Universal Trial Balance & Financial Data Importer (The "Friction Killer")

Universal multi-format financial data ingestion engine supporting instant onboarding for prospective CA firms in under 60 seconds:
- **Multi-Format Parsing Engine**: Ingests Tally Prime / ERP 9 XML, Tally CSV, Busy Accounting CSV/TSV with Dr/Cr amount suffixes, SAP ERP General Ledger statements (100000-700000 series in INR), and direct clipboard TSV copy-paste from Excel or Google Sheets.
- **Auto-Detection & Normalization**: Auto-detects delimiters (tab, comma, semicolon, pipe) and case-insensitive headers. Normalizes Indian number formats (lakhs, crores, `Rs.`, `₹`, trailing `Dr`/`Cr`).
- **Heuristic Auto-Classifier**: Auto-classifies ledgers across Schedule III Balance Sheet/P&L, Form 3CD (Turnover, Sec 43B, 43B(h) MSME, 36(1)(va)), CARO 2020 (inventory, working capital, borrowings, statutory dues), and Fixed Asset blocks.
- **Preloaded Test-Drive Datasets**: 3 real-world mathematically balanced datasets (Tally Manufacturing ₹46.45 Cr, Busy Trading ₹18.20 Cr, SAP Cloud Services ₹85.00 Cr).
- **Cascading Multi-Desk Sync**: Synchronizes trial balance figures across 6 statutory desks (Statutory Audit SA 320 materiality, Schedule III 11 ratios, Form 3CD Tax Audit, CARO 2020, Fixed Assets register, and Cost Audit Form CRA-1/3).

### Sprint 43: 1-Click ICAI SA 230 Digital Audit Dossier & Peer Review Vault (Option 1 - The Core Audit Moat)

End-to-end digital audit documentation and regulatory inspection defense platform compliant with ICAI Standards on Auditing (SA 230, SA 220, SA 500, SA 520, SA 530) and NFRA / ICAI Quality Review Board standards:
- **17 Standard Lead Schedules (A to Q)**:
  - Automates substantive analytical lead schedules mapped directly from ingested Trial Balance figures:
    - `A` Cash & Bank Balances (SA 505 / BRS)
    - `B` Trade Receivables / Debtors (SA 505)
    - `C` Inventories & WIP (SA 501 NRV)
    - `D` Loans, Advances & Other Current Assets
    - `E` Property, Plant & Equipment (Fixed Assets Block)
    - `F` Non-Current Investments & Financial Assets
    - `G` Trade Payables & MSME Dues (Sec 43B(h))
    - `H` Borrowings (Working Capital CC / Term Loans)
    - `I` Other Liabilities, Provisions & Statutory Dues
    - `J` Share Capital & Reserves and Surplus
    - `K` Revenue from Operations (Turnover Cut-off)
    - `L` Other Non-Operating Income
    - `M` Cost of Materials Consumed & Direct Purchases
    - `N` Employee Benefit Expenses (PF, ESIC, Gratuity)
    - `O` Finance Costs & Bank Borrowing Interest
    - `P` Depreciation & Amortization (Companies Act Sch II)
    - `Q` Other Operating, Administrative & Selling Expenses
  - Supports audit adjustment journal entries (Dr/Cr adjustments), automated audited final balance computation, prior year comparative variance analysis, and material variance alerts ($> 20\%$ or exceeding SA 320 performance materiality).
- **9-Section ICAI Peer Review & NFRA Inspection Master Index**:
  - `IDX-100`: Appointment, Engagement Terms & Independence Evaluation (SA 210 / SA 220 / SQC 1)
  - `IDX-200`: Audit Planning, Risk Assessment & Materiality Thresholds (SA 300 / SA 315 / SA 320)
  - `IDX-300`: Internal Financial Controls (IFC) & Fraud Risk Assessment (SA 240 / Sec 143(3)(i))
  - `IDX-400`: Lead Schedules (A–Q) & Substantive Testing Workpapers (SA 500 / SA 530 / SA 501)
  - `IDX-500`: Substantive Analytical Procedures & Ratio Variances (SA 520 / Schedule III)
  - `IDX-600`: External Confirmations (Banks, Debtors & Creditors) (SA 505)
  - `IDX-700`: Companies (Auditor's Report) Order (CARO 2020) 21 Clauses
  - `IDX-800`: Subsequent Events, Going Concern & Management Representation Letter (SA 560 / SA 570 / SA 580)
  - `IDX-900`: Engagement Quality Review (EQR) Clearance & Independent Auditor's Report (SA 220 / SA 700 / SA 705)
- **1-Click Cryptographic Audit File Packaging**:
  - Automatically compiles all engagement artifacts, lead schedules, substantive working papers, query resolutions, and CARO findings into a unified digital audit dossier.
  - Generates an immutable **SHA-256 cryptographic digest** over the dossier manifest for tamper-evident regulatory proof.
  - Generates standalone, printable, beautifully formatted HTML dossiers for ICAI Peer Reviewers and NFRA inspection officers.
- **SQC 1 Paragraph 74 Sealing & Archival Lock**:
  - Enforces strict 60-day post-audit report archival deadline.
  - Once signed off by the Engagement Partner, locks working papers and lead schedules against subsequent edits.
- **Complete Desk Navigation**: Wired all statutory compliance desks directly in `App.tsx` navigation.

API:
- `GET /api/audit/engagements/{id}/lead-schedules` (`audit.read`)
- `POST /api/audit/engagements/{id}/lead-schedules/generate` (`audit.manage`)
- `PUT /api/audit/lead-schedules/{id}` (`audit.manage`)
- `GET /api/audit/engagements/{id}/dossier` (`audit.read`)
- `POST /api/audit/engagements/{id}/dossier/compile` (`audit.manage`)
- `POST /api/audit/dossier/{id}/seal` (`audit.review`)
- `GET /api/audit/engagements/{id}/dossier/export-html` (`audit.read`)

## Bank Statement Analyzer & Forensic Fraud Detection Engine (Sprint 44)

- **Option 2 (Verification & Fraud Moat)**: Forensic audit engine built directly into the Bank Feeds module to analyze bank transactions and cross-verify statement accounts against the client's Trial Balance general ledger.
- **7-Rule Statutory & Anti-Avoidance Forensic Scanners**:
  1. **Section 40A(3) Cash Payment Disallowance**: Scans debit cash withdrawals/bearer payments exceeding ₹10,000 general limit or ₹35,000 goods carriage transport limit under Rule 6DD proviso; calculates 100% tax disallowance exposure for Form 3CD Clause 21(d).
  2. **Section 269ST Cash Receipt Ban**: Flags single or daily aggregate cash deposits $\ge$ ₹2,00,000; computes 100% statutory penalty exposure under Section 271DA for Form 3CD Clause 31(ba)/(bb).
  3. **Section 269SS / 269T Cash Loans & Repayments**: Detects loans, hand loans, or deposits accepted or repaid in cash exceeding ₹20,000; flags Section 271D / 271E penalty exposures.
  4. **Circular Flow & Round-Tripping**: Identifies circular money trails (debits paired with credits of similar amount $\pm 1\%$ within a 5-day window) lacking economic substance (PMLA / CARO 2020 Clause (x)).
  5. **Smurfing & Structuring Detection**: Catches deliberate structuring patterns (split cash transactions between ₹8,000 and ₹9,999 within 48 hours to evade the ₹10,000 threshold).
  6. **Dormancy Surge Risk**: Detects sudden high-value transactions ($\ge$ ₹2 Lakhs) following $\ge 60$ days of account inactivity.
  7. **High-Value Round-Sum Anomalies**: Flags transactions $\ge$ ₹5 Lakhs that are exact multiples of ₹1,00,000 with generic descriptions under SA 240 fraud risk indicators.
- **Trial Balance Cross-Reconciliation (Unrecorded Account Detection)**:
  - Automates comparison between statement bank accounts and Trial Balance GL accounts.
  - Generates critical audit red flags for active bank statement accounts not recorded in books of account (potential undisclosed cash flows under Section 68/69).
- **Composite Forensic Risk Meter (0–100 Score)**:
  - Dynamically scores account risk based on weighted criticality of detected anomalies.
- **Auditor Workpaper Register & 1-Click HTML Dossier Export**:
  - Allows marking items as `Flagged`, `Reviewed`, `DisallowedIn3CD`, or `Dismissed` (under Rule 6DD exceptions).
  - Exports a professional, print-ready Forensic Dossier in HTML format.

API:
- `POST /api/bank/accounts/{id}/forensic-scan` (`bankfeed.read`)
- `GET /api/bank/accounts/{id}/forensic-report` (`bankfeed.read`)
- `PUT /api/bank/anomalies/{id}/status` (`bankfeed.manage`)
- `POST /api/bank/clients/{clientId}/reconcile-tb` (`bankfeed.read`)
- `GET /api/bank/accounts/{id}/forensic-report/export-html` (`bankfeed.read`)

## SA 505 Automated External Confirmations Desk (Sprint 45)

- **Option 3 (The Administrative Moat)**: Full end-to-end third-party balance circularization campaign manager for statutory audits under ICAI Standard on Auditing (SA) 505 "External Confirmations".
- **Multi-Category Confirmation Engine**:
  - Supports confirmation targets across Trade Debtors (Sundry Debtors / Schedule III Line B), Trade Creditors (Sundry Creditors / Line G), Bank Accounts, Loans & Advances Given, Borrowings, Security Deposits, and Legal Counsel (pending litigation).
  - Configurable confirmation styles: Positive Stated Balance, Positive Blank Balance, and Negative Confirmation.
- **1-Click Auto-Population from Trial Balance**:
  - Automatically identifies high-value Debtors and Creditors from imported Trial Balance ledgers exceeding custom materiality thresholds (e.g., $\ge$ ₹50,000) and populates circularization batches in seconds.
- **Tokenized Digital Verification Portal (`/confirm/{token}`)**:
  - Dispatches secure, tokenized circularization links with 32-byte cryptographic access tokens.
  - Public mobile-responsive verification portal allows third-party CFOs/accountants to directly verify balances, enter their ledger balance, indicate discrepancy reasons (Goods in Transit, Cheque in Transit, Rate Dispute, TDS Mismatch, Unadjusted Credit Note), attach remarks, and provide signatory credentials.
- **Automated Exception Matching & SA 505 Para 12 Alternative Procedures**:
  - Automatic variance computation with ₹1 tolerance; flags disputed items into exception workpapers.
  - Full support for recording alternative audit procedures for non-responses (subsequent cash collection/payment verification in bank statements, dispatch documents, delivery proofs).
  - Management refusal inquiries (SA 505 para 8) tracking and proposed audit adjustment entries.
- **1-Click SA 505 Summary Workpaper & Circularization Letter Export**:
  - Generates formal, printable audit confirmation request letters on auditor letterhead.
  - Exports a peer-review-ready SA 505 Summary Workpaper in standalone HTML for inclusion into Section `IDX-600` of the SA 230 Audit Dossier Vault.

API:
- `GET /api/audit/engagements/{id}/confirmation-campaigns` (`audit.read`)
- `POST /api/audit/engagements/{id}/confirmation-campaigns` (`audit.manage`)
- `GET /api/audit/confirmation-campaigns/{campaignId}/requests` (`audit.read`)
- `POST /api/audit/confirmation-campaigns/{campaignId}/requests` (`audit.manage`)
- `POST /api/audit/confirmation-campaigns/{campaignId}/auto-populate` (`audit.manage`)
- `POST /api/audit/confirmation-campaigns/{campaignId}/send` (`audit.manage`)
- `PUT /api/audit/confirmations/{requestId}/evaluate` (`audit.manage`)
- `GET /api/audit/confirmation-campaigns/{campaignId}/export-summary` (`audit.read`)
- `GET /api/audit/confirmations/{requestId}/letter-html` (`audit.read`)
- `GET /api/audit/confirmations/public/{token}` (Public Anonymous)
- `POST /api/audit/confirmations/public/{token}/respond` (Public Anonymous)

## 1-Click ICAI Independent Auditor's Report & Schedule III Notes to Accounts (Sprint 46)

- **Option 4 (The Reporting Moat)**: Automated publication engine for the final statutory audit deliverable compliant with ICAI Standards on Auditing (SA 700, 701, 705, 706), Companies Act, 2013 Section 143(3), MCA Rule 11(g) Audit Trail mandate, and Schedule III Division I/II reporting.
- **Statutory Opinion Architecture (SA 700 / SA 705 / SA 706)**:
  - Formulates standard statutory audit opinions:
    - **Unmodified (Clean Opinion)**: Standard SA 700 true and fair view verbiage.
    - **Qualified Opinion (SA 705)**: Automated "except for the effects of the matter described in the Basis for Qualified Opinion" formulation with parameterized qualification details.
    - **Adverse Opinion (SA 705)**: Standard adverse verbiage when fundamental accounting assumptions (e.g. going concern) are vitiated.
    - **Disclaimer of Opinion (SA 705)**: Standard disclaimer verbiage when audit evidence is unavailable or scope is restricted.
  - Optional Emphasis of Matter (SA 706) and Material Uncertainty Related to Going Concern (SA 570) paragraphs.
- **Key Audit Matters (KAM - SA 701)**:
  - Communicates matters of most significance in the audit of financial statements (Revenue Recognition under Ind AS 115, Inventory Valuation, Recoverability of Receivables).
  - Outlines: Category, Why Considered Significant, How Addressed in the Audit, and Lead Schedule / Working Paper references.
- **Section 143(3) Statutory Affirmations & Rule 11 Checklist**:
  - Automatically assesses and renders statutory reporting requirements under Section 143(3)(a) through 143(3)(i).
  - **Rule 11(g) MCA Audit Trail (Edit Log) Compliance**:
    - Evaluates whether the client's accounting software features an audit trail facility operating throughout the year without being tampered with, with preserved logs and auditor verification remarks.
- **ICAI UDIN Validation & Cryptographic Binding**:
  - Enforces mandatory 18-character alphanumeric format: `[2-digit FY][6-digit M.No.][4-digit DocCode][6-digit Hash]`.
  - Validates and binds the partner's UDIN to the final report deliverable.
- **Schedule III Notes to Accounts & 11 Mandatory Financial Ratios**:
  - Automatically generates Note 1 (Corporate Information & Domicile) and Note 2 (Significant Accounting Policies - Indian GAAP / Ind AS accrual basis).
  - Generates Note 23: 11 Mandatory Analytical Ratios (Current Ratio, Debt-Equity Ratio, DSCR, ROE, Inventory Turnover, Trade Receivables Turnover, Trade Payables Turnover, Net Capital Turnover, Net Profit Ratio, ROCE, ROI).
  - Auto-flags any ratio variance exceeding **25%** with mandatory statutory disclosure explanations.
  - Compiles Note 24: Additional Regulatory Disclosures (Benami properties, Wilful Defaulter status, Struck-off companies, Title deeds of immovable property, Crypto currency / Virtual Digital Assets).
- **Print-Ready HTML Publication Engine**:
  - 1-click standalone HTML exports for both the Independent Auditor's Report and the Notes to Accounts, complete with firm letterheads, partner signature blocks, and UDIN badges.

API:
- `GET /api/audit/engagements/{id}/auditor-report` (`audit.read`)
- `POST /api/audit/engagements/{id}/auditor-report/generate` (`audit.manage`)
- `PUT /api/audit/auditor-reports/{id}` (`audit.manage`)
- `POST /api/audit/auditor-reports/{id}/issue-udin` (`audit.manage`)
- `GET /api/audit/engagements/{id}/auditor-report/export-html` (`audit.read`)
- `GET /api/audit/engagements/{id}/notes-to-accounts` (`audit.read`)
- `POST /api/audit/engagements/{id}/notes-to-accounts/generate` (`audit.manage`)
- `PUT /api/audit/notes-to-accounts/{id}` (`audit.manage`)
- `GET /api/audit/engagements/{id}/notes-to-accounts/export-html` (`audit.read`)

## IBC 2016 Insolvency & CIRP Statutory Desk (Sprint 61)

- **Option A (The Insolvency & Resolution Moat)**: Full-featured statutory workbench for Resolution Professionals (IRP/RP) and CA insolvency practices administering Corporate Insolvency Resolution Processes (CIRP) under the Insolvency and Bankruptcy Code, 2016 and IBBI (CIRP) Regulations, 2016.
- **Section 12 Statutory CIRP Timelines & 330-Day Outer Cap Engine**:
  - Automatically computes the mandatory timeline milestones from Insolvency Commencement Date (ICD):
    - 14-day public announcement & claims submission deadline (Reg 6).
    - 180-day initial CIRP completion limit (Sec 12(1)).
    - 270-day extension limit with 66% CoC approval (Sec 12(2)).
    - 330-day absolute statutory cap including all litigations (Sec 12(3) 2nd proviso).
  - Telemetry monitors days elapsed, remaining runway, and triggers visual breach warnings.
- **Committee of Creditors (CoC) & Statutory Voting Threshold Engine**:
  - Class-wise creditor claim verification: Financial Creditors (Secured/Unsecured) and Operational Creditors (Govt/Statutory, Workmen/Employees, Trade Vendors).
  - **Section 21(2) First Proviso Disqualification**: Automatically identifies related party financial creditors, assigns 0% voting share, and excludes them from CoC voting quotas.
  - Proportional voting share allocation across eligible non-related financial debt.
  - Automated e-voting outcome validator across statutory thresholds:
    - **51% Simple Majority**: Routine administrative decisions (Sec 21(8)).
    - **66% Qualified Majority**: Approval of Resolution Plan (Sec 30(4)), Liquidation (Sec 33(2)), CIRP extension (Sec 12(2)), Appointment/Replacement of RP (Sec 22(2), Sec 27(2)), Critical Interim Finance (Sec 28).
    - **90% Supermajority**: Section 12A withdrawal of CIRP application (Regulation 30A).
- **PUFE Forensic Avoidance Transaction Studio (Sections 43, 45, 50, 66)**:
  - Forensic tracking and recoverable clawback estimation for vulnerable transactions under IBBI Regulation 35A:
    - **Section 43 (Preferential Transactions)**: 1-year lookback for unrelated creditors, extended to 2 years for related parties preceding ICD.
    - **Section 45 (Undervalued Transactions)**: Consideration significantly less than market value with 1-year / 2-year lookback.
    - **Section 50 (Extortionate Credit)**: Unconscionable terms/interest within 2 years preceding commencement.
    - **Section 66 (Fraudulent / Wrongful Trading)**: Business carried on with intent to defraud creditors; directors personally liable without financial or time limitation.
  - Automated statutory lookback determination and NCLT application tracking (75d opinion, 115d determination, 135d filing deadline).
- **Regulation 35 Registered Valuations (Fair Value vs. Liquidation Value)**:
  - Multi-class valuation capture: Land & Building, Plant & Machinery, Securities & Financial Assets.
  - Real-time computation of average Fair Value and Liquidation Value between two registered valuers.
  - **Regulation 35(1)(b) Divergence Detector**: Automatically detects when valuations differ by >25% of the higher valuation, triggering the statutory requirement to appoint a third registered valuer.
- **1-Click Regulation 36 Information Memorandum (IM) Dossier**:
  - Compiles a complete, print-ready statutory Information Memorandum conforming to Regulation 36(2) of IBBI (CIRP) Regulations, 2016:
    - Assets & liabilities as on ICD.
    - Preceding two financial years audited financials.
    - List of admitted creditors with voting shares.
    - Details of guarantees, litigations, and PUFE forensic findings.
    - Regulation 36(4) confidentiality undertaking & RP digital attestation block.

API:
- `GET /api/ibc-cirp/cases` (`ibccirp.read`)
- `GET /api/ibc-cirp/cases/{id}` (`ibccirp.read`)
- `POST /api/ibc-cirp/cases` (`ibccirp.manage`)
- `DELETE /api/ibc-cirp/cases/{id}` (`ibccirp.manage`)
- `POST /api/ibc-cirp/coc-members` (`ibccirp.manage`)
- `DELETE /api/ibc-cirp/coc-members/{id}` (`ibccirp.manage`)
- `POST /api/ibc-cirp/resolutions` (`ibccirp.manage`)
- `DELETE /api/ibc-cirp/resolutions/{id}` (`ibccirp.manage`)
- `POST /api/ibc-cirp/pufe` (`ibccirp.manage`)
- `DELETE /api/ibc-cirp/pufe/{id}` (`ibccirp.manage`)
- `POST /api/ibc-cirp/valuations` (`ibccirp.manage`)
- `DELETE /api/ibc-cirp/valuations/{id}` (`ibccirp.manage`)
- `GET /api/ibc-cirp/cases/{id}/im-dossier` (`ibccirp.read`)
- `POST /api/ibc-cirp/calculate-timeline` (`ibccirp.read`)

## RBI FEMA Foreign Remittance, Form 15CA/15CB & Annual FLA Desk (Sprint 62)

- **Option A (The High-Yield Cross-Border Practice Moat)**: Full-featured statutory workbench for cross-border foreign remittances, Section 195/195(6) withholding tax certificates, RBI FIRMS Single Master Form reporting (FC-GPR / FC-TRS), and mandatory annual Foreign Liabilities and Assets (FLA) returns on the RBI FLAIR portal.
- **Form 15CB Statutory Certificate Engine (Section 195(6))**:
  - Automated Withholding Tax (TDS) determination comparing domestic statutory rates (Sec 195 / 115A) against Double Taxation Avoidance Agreements (DTAA / Sec 90).
  - Cross-border nature of remittance classification: Fees for Technical Services (FTS), Royalties, Professional Services, Interest, Dividends, Commercial Goods.
  - Tax gross-up computation for remitter-borne tax agreements under Section 195A.
  - Multi-tier supporting prerequisite verification checklist:
    - Tax Residency Certificate (TRC) validity period.
    - Electronic Form 10F verification on ITD portal.
    - No-PE declaration by non-resident entity.
    - Beneficial ownership confirmation and MLI Principal Purpose Test (PPT).
  - 1-Click generation of official ICAI-compliant Form 15CB Statutory Certificate with UDIN binding.
- **Rule 37BB Form 15CA Undertaking Generator**:
  - Automated routing and XML schema generation across all four parts:
    - **Part A**: Remittance not exceeding ₹5,00,000 in aggregate during the financial year.
    - **Part B**: Remittance where an order or certificate u/s 195(2), 195(3), or 197 has been obtained from the AO.
    - **Part C**: Remittance exceeding ₹5,00,000 chargeable to tax, accompanied by Form 15CB certificate.
    - **Part D**: Remittance not chargeable to tax under the specified list of 33 purpose codes under Rule 37BB.
- **DTAA Treaty, MFN & Article 5 PE Risk Analyzer**:
  - **Most Favoured Nation (MFN) Clause Evaluator**: Evaluates treaty third-state lower tax rates against landmark Supreme Court ruling in *AO v. M/s Nestle SA (Civil Appeal No. 1420 of 2023)*, enforcing mandatory prerequisite of formal CBDT notification under Section 90(1) in the Official Gazette before granting concessional treaty rates.
  - **Article 5 Permanent Establishment (PE) Risk Scorecard**: Real-time evaluation of non-resident exposure across Fixed Place PE (Art 5(1)), Service PE (>90/180 man-days in India), and Dependent Agent PE (authority to conclude contracts or habitually maintain stock of goods under Art 5(5)).
- **RBI FIRMS Single Master Form Portal Engine (FC-GPR & FC-TRS)**:
  - Automated statutory reporting for Foreign Direct Investment (FDI):
    - **Form FC-GPR**: Allotment of capital instruments (Equity, CCDs, CCPS, Warrants) to persons resident outside India within **30 days** of allotment.
    - **Form FC-TRS**: Transfer of capital instruments between resident and non-resident within **60 days** of transfer or receipt/payment of consideration.
  - **FEMA NDI Rule 21 Pricing Guidelines Validator**:
    - FC-GPR & Resident to Non-Resident transfers: Verifies issue/transfer price meets or exceeds the certified SEBI Merchant Banker / CA Discounted Free Cash Flow (DCF) floor.
    - Non-Resident to Resident transfers: Verifies transfer price does not exceed the DCF ceiling.
  - **Late Submission Fee (LSF) Matrix Calculator**: Computes estimated statutory penalty (`₹7,500 + 0.025% × Consideration × Delay Years`, capped at ₹1 Crore) for delayed filings under RBI Master Direction on Reporting.
  - 1-Click CS/CA Compliance Certificate HTML generation for attachment to the FIRMS Single Master Form.
- **RBI Annual Foreign Liabilities and Assets (FLA) Engine (FLAIR Portal)**:
  - Mandatory annual return compiler conforming to RBI FLAIR reporting guidelines for companies with FDI or ODI by **July 15th** following the fiscal year-end.
  - **Automated Balance Sheet & Net Worth Computation**: Computes statutory Net Worth from Paid-up Capital and Free Reserves.
  - **FDI at Net Asset Value (NAV)**: Computes foreign liabilities position based on foreign equity holding percentage applied to audited Net Worth.
  - **Overseas Direct Investment (ODI) Commitment**: Computes financial commitment across equity investment, loans, and corporate guarantees.
  - Net Foreign Liabilities position tracker (Foreign Liabilities minus Overseas Assets).
  - 1-Click official RBI FLAIR Return Dossier compilation with CS/CA digital attestation and Director verification block.

API:
- `GET /api/foreign-remittance/firms/summary` (`fema.firms.manage`)
- `GET /api/foreign-remittance/firms/filings` (`fema.firms.manage`)
- `GET /api/foreign-remittance/firms/filings/{id}` (`fema.firms.manage`)
- `POST /api/foreign-remittance/firms/filings` (`fema.firms.manage`)
- `PUT /api/foreign-remittance/firms/filings/{id}` (`fema.firms.manage`)
- `DELETE /api/foreign-remittance/firms/filings/{id}` (`fema.firms.manage`)
- `POST /api/foreign-remittance/firms/filings/{id}/certify` (`fema.firms.manage`)
- `GET /api/foreign-remittance/firms/filings/{id}/certificate-html` (`fema.firms.manage`)
- `GET /api/foreign-remittance/fla/summary` (`fema.fla.certify`)
- `GET /api/foreign-remittance/fla/returns` (`fema.fla.certify`)
- `GET /api/foreign-remittance/fla/returns/{id}` (`fema.fla.certify`)
- `POST /api/foreign-remittance/fla/returns` (`fema.fla.certify`)
- `PUT /api/foreign-remittance/fla/returns/{id}` (`fema.fla.certify`)
- `DELETE /api/foreign-remittance/fla/returns/{id}` (`fema.fla.certify`)
- `POST /api/foreign-remittance/fla/returns/{id}/certify` (`fema.fla.certify`)
- `POST /api/foreign-remittance/fla/returns/{id}/file` (`fema.fla.certify`)
- `GET /api/foreign-remittance/fla/returns/{id}/report-html` (`fema.fla.certify`)
- `POST /api/foreign-remittance/evaluate-mfn` (`fema.firms.manage`)
- `POST /api/foreign-remittance/evaluate-pe` (`fema.firms.manage`)

## Notes

- Package versions are in `Directory.Packages.props`. If restore can't find a version,
  update it there.
- If you already ran Sprint 1 locally, its database just gets migration `0002_identity.sql` on next start.
  Old test firms made with the Sprint 1 dev endpoint have no users; create new firms through sign-up.
