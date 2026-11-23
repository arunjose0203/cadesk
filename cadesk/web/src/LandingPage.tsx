import { useState } from 'react'

interface LandingPageProps {
  onStartTrial: () => void
  onSignIn: () => void
  onOpenTour: () => void
}

export function LandingPage({ onStartTrial, onSignIn, onOpenTour }: LandingPageProps) {
  const [billingCycle, setBillingCycle] = useState<'monthly' | 'annual'>('annual')

  return (
    <div style={{ fontFamily: "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif", color: '#0f172a', background: '#fff', minHeight: '100vh' }}>
      {/* Navigation Header */}
      <header style={{ borderBottom: '1px solid #e2e8f0', background: '#ffffff', position: 'sticky', top: 0, zIndex: 100 }}>
        <div style={{ maxWidth: '1280px', margin: '0 auto', padding: '16px 24px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <div style={{ width: '38px', height: '38px', borderRadius: '8px', background: '#0f172a', color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 800, fontSize: '18px' }}>
              CA
            </div>
            <div>
              <span style={{ fontSize: '20px', fontWeight: 800, color: '#0f172a', letterSpacing: '-0.5px' }}>CA Desk</span>
              <span style={{ fontSize: '10px', background: '#e0e7ff', color: '#3730a3', padding: '2px 6px', borderRadius: '4px', fontWeight: 700, marginLeft: '8px' }}>
                ICAI &amp; NFRA READY
              </span>
            </div>
          </div>

          <nav style={{ display: 'flex', alignItems: 'center', gap: '28px' }}>
            <a href="#moats" style={{ textDecoration: 'none', color: '#475569', fontSize: '14px', fontWeight: 600 }}>6 Core Moats</a>
            <a href="#pricing" style={{ textDecoration: 'none', color: '#475569', fontSize: '14px', fontWeight: 600 }}>Pricing</a>
            <a href="#security" style={{ textDecoration: 'none', color: '#475569', fontSize: '14px', fontWeight: 600 }}>Two-Lock Security</a>
            <a href="#faq" style={{ textDecoration: 'none', color: '#475569', fontSize: '14px', fontWeight: 600 }}>FAQ</a>
          </nav>

          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <button
              onClick={onOpenTour}
              style={{ padding: '8px 16px', background: '#f8fafc', border: '1px solid #cbd5e1', borderRadius: '6px', fontSize: '13px', fontWeight: 600, color: '#334155', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '6px' }}
            >
              <span>🎮 Interactive Tour</span>
            </button>
            <button
              onClick={onSignIn}
              style={{ padding: '8px 16px', background: 'none', border: 'none', fontSize: '14px', fontWeight: 600, color: '#0f172a', cursor: 'pointer' }}
            >
              Sign In
            </button>
            <button
              onClick={onStartTrial}
              style={{ padding: '9px 20px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontSize: '13px', fontWeight: 700, cursor: 'pointer', boxShadow: '0 4px 6px -1px rgba(2, 132, 199, 0.2)' }}
            >
              Start 14-Day Free Trial
            </button>
          </div>
        </div>
      </header>

      {/* Hero Section */}
      <section style={{ padding: '72px 24px 60px 24px', background: 'linear-gradient(180deg, #f8fafc 0%, #ffffff 100%)', textAlign: 'center' }}>
        <div style={{ maxWidth: '960px', margin: '0 auto' }}>
          <div style={{ display: 'inline-flex', alignItems: 'center', gap: '8px', background: '#eff6ff', border: '1px solid #bfdbfe', borderRadius: '24px', padding: '6px 16px', marginBottom: '24px' }}>
            <span style={{ fontSize: '13px', color: '#1d4ed8', fontWeight: 600 }}>
              🇮🇳 India's Operating System for Chartered Accountants &amp; Statutory Auditors
            </span>
          </div>

          <h1 style={{ fontSize: '48px', fontWeight: 800, lineHeight: 1.15, letterSpacing: '-1.5px', color: '#0f172a', margin: '0 0 20px 0' }}>
            From Tally &amp; SAP Trial Balance to <br />
            <span style={{ color: '#0284c7' }}>1-Click ICAI Independent Auditor's Reports</span>
          </h1>

          <p style={{ fontSize: '18px', color: '#475569', lineHeight: 1.6, maxWidth: '780px', margin: '0 auto 36px auto' }}>
            Replace fragmented desktop software with an all-in-one cloud platform. Automated forensic bank fraud checks, SA 505 balance confirmations, digital working paper dossiers, and ICAI AQMM v1.0 Level 3 compliance.
          </p>

          <div style={{ display: 'flex', justifyContent: 'center', gap: '16px', flexWrap: 'wrap' }}>
            <button
              onClick={onStartTrial}
              style={{ padding: '14px 32px', background: '#0f172a', color: '#fff', border: 'none', borderRadius: '8px', fontSize: '15px', fontWeight: 700, cursor: 'pointer', boxShadow: '0 10px 15px -3px rgba(15, 23, 42, 0.2)' }}
            >
              🚀 Start 14-Day Free Trial (No Card Needed)
            </button>
            <button
              onClick={onOpenTour}
              style={{ padding: '14px 28px', background: '#fff', color: '#0f172a', border: '1px solid #cbd5e1', borderRadius: '8px', fontSize: '15px', fontWeight: 600, cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '8px' }}
            >
              <span>🎮 3-Minute Interactive Test Drive</span>
            </button>
          </div>

          {/* Trust Pillars */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '20px', marginTop: '54px', paddingTop: '36px', borderTop: '1px solid #e2e8f0' }}>
            {[
              { val: '600-Pt AQMM', lbl: 'ICAI CAQ Level 3 Desk' },
              { val: '17 Lead Schedules', lbl: 'SA 230 Workpaper Dossier' },
              { val: '7 Fraud Rules', lbl: 'Bank Statement Forensics' },
              { val: 'Two-Lock RLS', lbl: 'PostgreSQL Multi-Tenancy' },
              { val: 'AES-256', lbl: 'Encrypted Daily Backups' }
            ].map((kpi, idx) => (
              <div key={idx}>
                <div style={{ fontSize: '22px', fontWeight: 800, color: '#0f172a' }}>{kpi.val}</div>
                <div style={{ fontSize: '12px', color: '#64748b', marginTop: '2px', fontWeight: 500 }}>{kpi.lbl}</div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* The 6 Core Moats Section */}
      <section id="moats" style={{ padding: '80px 24px', maxWidth: '1280px', margin: '0 auto' }}>
        <div style={{ textAlign: 'center', marginBottom: '56px' }}>
          <h2 style={{ fontSize: '14px', fontWeight: 700, color: '#0284c7', textTransform: 'uppercase', letterSpacing: '1px', margin: '0 0 10px 0' }}>
            Unmatched Competitive Advantage
          </h2>
          <h3 style={{ fontSize: '36px', fontWeight: 800, color: '#0f172a', letterSpacing: '-1px', margin: 0 }}>
            The 6 Core Audit &amp; Practice Moats
          </h3>
          <p style={{ fontSize: '16px', color: '#64748b', marginTop: '10px' }}>
            Built specifically to solve the real daily bottlenecks faced by Indian Chartered Accountants.
          </p>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(360px, 1fr))', gap: '28px' }}>
          {[
            {
              icon: '📊',
              title: '1. Universal Trial Balance Importer',
              subtitle: '60-Second Ingestion Hub',
              desc: 'Intelligently ingest data from Tally Prime (XML/CSV), Busy, SAP, or raw Excel clipboard paste. Fuzzy AI auto-maps Chart of Accounts to Schedule III, Form 3CD, and CARO 2020.',
              points: ['Fuzzy column heuristics', 'Auto Schedule III tagging', 'Cascading 6-desk sync']
            },
            {
              icon: '🔍',
              title: '2. Forensic Bank Analyzer & Fraud Engine',
              subtitle: 'Statutory Violation Detection',
              desc: 'Ingests Indian bank statements (PDF/CSV/Excel) and cross-reconciles against books. Flags Sec 40A(3) cash violations, round-tripping, smurfing, and unusual dormancy surges.',
              points: ['Sec 40A(3) & 269ST alerts', 'Round-trip fund tracking', 'TB vs Bank cross-matching']
            },
            {
              icon: '✉️',
              title: '3. SA 505 Automated External Confirmations',
              subtitle: 'Tokenized Circularization Portal',
              desc: 'Direct auditor control over third-party circularization. Debtors and Creditors confirm balances via a mobile-friendly public portal. Alternative procedures logged under SA 505.12.',
              points: ['Digital verification links', 'Direct third-party sign-off', 'Alternative audit procedures']
            },
            {
              icon: '📁',
              title: '4. SA 230 Digital Workpaper Dossier',
              subtitle: 'ICAI & NFRA Inspection Vault',
              desc: 'Complete audit file packager with 17 substantive analytical lead schedules (A to Q), immutable 60-day archival lock under SQC 1, and SHA-256 cryptographic tamper-evident seal.',
              points: ['Lead schedules A to Q', 'Immutable 60-day lock', 'SHA-256 seal & peer-review export']
            },
            {
              icon: '📜',
              title: '5. 1-Click SA 700 Independent Auditor\'s Report',
              subtitle: 'Rule 11(g) & 18-Digit UDIN Validator',
              desc: 'Automated legal verbiage engine generating Unmodified, Qualified, Adverse, or Disclaimer opinions. Verifies accounting software audit trail edit logs under MCA Rule 11(g).',
              points: ['Automated SA 700/701 verbiage', 'Rule 11(g) audit trail check', '18-character UDIN validation']
            },
            {
              icon: '🛡️',
              title: '6. ICAI AQMM v1.0 Maturity Desk',
              subtitle: '600-Point Quality Evaluation',
              desc: 'Mandatory standard for firms auditing listed / public interest entities. Auto-evaluates points across Operations (190), HR (110), and Audit Execution (300) from CA Desk telemetry.',
              points: ['Automated Level 1/2/3 grading', 'Dynamic gap analysis roadmap', '1-click peer review dossier']
            }
          ].map((moat, i) => (
            <div key={i} style={{ padding: '28px', borderRadius: '12px', border: '1px solid #e2e8f0', background: '#ffffff', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.05)', display: 'flex', flexDirection: 'column' }}>
              <div style={{ fontSize: '32px', marginBottom: '14px' }}>{moat.icon}</div>
              <div style={{ fontSize: '11px', fontWeight: 700, color: '#0284c7', textTransform: 'uppercase', letterSpacing: '0.5px' }}>{moat.subtitle}</div>
              <h4 style={{ fontSize: '18px', fontWeight: 700, color: '#0f172a', margin: '4px 0 10px 0' }}>{moat.title}</h4>
              <p style={{ fontSize: '14px', color: '#475569', lineHeight: 1.6, margin: '0 0 16px 0', flex: 1 }}>{moat.desc}</p>
              <div style={{ borderTop: '1px solid #f1f5f9', paddingTop: '14px', display: 'flex', flexDirection: 'column', gap: '6px' }}>
                {moat.points.map((pt, pIdx) => (
                  <div key={pIdx} style={{ fontSize: '12px', color: '#166534', display: 'flex', alignItems: 'center', gap: '6px' }}>
                    <span>✓</span> <strong>{pt}</strong>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* Pricing Section */}
      <section id="pricing" style={{ padding: '80px 24px', background: '#f8fafc', borderTop: '1px solid #e2e8f0' }}>
        <div style={{ maxWidth: '1200px', margin: '0 auto', textAlign: 'center' }}>
          <h2 style={{ fontSize: '14px', fontWeight: 700, color: '#0284c7', textTransform: 'uppercase', letterSpacing: '1px', margin: '0 0 10px 0' }}>
            Transparent Indian SaaS Pricing
          </h2>
          <h3 style={{ fontSize: '36px', fontWeight: 800, color: '#0f172a', letterSpacing: '-1px', margin: '0 0 16px 0' }}>
            Pick the Perfect Tier for Your Firm
          </h3>
          <p style={{ fontSize: '16px', color: '#64748b', margin: '0 0 32px 0' }}>
            All plans include full SAC 998311 statutory GST tax invoicing with input tax credit for your firm.
          </p>

          {/* Monthly / Annual Toggle */}
          <div style={{ display: 'inline-flex', alignItems: 'center', background: '#e2e8f0', padding: '4px', borderRadius: '8px', marginBottom: '48px' }}>
            <button
              onClick={() => setBillingCycle('monthly')}
              style={{ padding: '8px 20px', border: 'none', borderRadius: '6px', fontSize: '13px', fontWeight: 600, background: billingCycle === 'monthly' ? '#fff' : 'transparent', color: billingCycle === 'monthly' ? '#0f172a' : '#64748b', cursor: 'pointer' }}
            >
              Monthly Billing
            </button>
            <button
              onClick={() => setBillingCycle('annual')}
              style={{ padding: '8px 20px', border: 'none', borderRadius: '6px', fontSize: '13px', fontWeight: 600, background: billingCycle === 'annual' ? '#0f172a' : 'transparent', color: billingCycle === 'annual' ? '#fff' : '#64748b', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '6px' }}
            >
              <span>Annual Billing</span>
              <span style={{ fontSize: '10px', background: '#22c55e', color: '#fff', padding: '2px 6px', borderRadius: '10px', fontWeight: 700 }}>Save 20%</span>
            </button>
          </div>

          {/* Pricing Cards */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '28px', textAlign: 'left' }}>
            {/* Starter */}
            <div style={{ padding: '32px', borderRadius: '12px', border: '1px solid #cbd5e1', background: '#fff', display: 'flex', flexDirection: 'column' }}>
              <div style={{ fontSize: '18px', fontWeight: 700, color: '#0f172a' }}>Starter (Solo CA)</div>
              <p style={{ fontSize: '13px', color: '#64748b', margin: '4px 0 20px 0' }}>Ideal for sole practitioners and independent CAs up to 3 staff.</p>
              <div style={{ fontSize: '36px', fontWeight: 800, color: '#0f172a', marginBottom: '4px' }}>
                {billingCycle === 'annual' ? '₹1,249' : '₹1,499'}
                <span style={{ fontSize: '14px', fontWeight: 500, color: '#64748b' }}> / month</span>
              </div>
              <div style={{ fontSize: '11px', color: '#94a3b8', marginBottom: '24px' }}>+ 18% GST (SAC 998311)</div>
              <ul style={{ listStyle: 'none', padding: 0, margin: '0 0 32px 0', fontSize: '13px', color: '#475569', display: 'flex', flexDirection: 'column', gap: '10px', flex: 1 }}>
                <li>✓ 3 User Seats &amp; 100 Clients</li>
                <li>✓ Universal Trial Balance Importer (Tally/Excel)</li>
                <li>✓ Direct Tax Computation &amp; 115BAC Regime Comparison</li>
                <li>✓ Form 3CD Tax Audit Statement (Clauses 1–44)</li>
                <li>✓ Client Self-Service Portal &amp; DSC Token Custody</li>
              </ul>
              <button
                onClick={onStartTrial}
                style={{ width: '100%', padding: '10px', background: '#0f172a', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 700, fontSize: '14px', cursor: 'pointer' }}
              >
                Start 14-Day Free Trial
              </button>
            </div>

            {/* Growth */}
            <div style={{ padding: '32px', borderRadius: '12px', border: '2px solid #0284c7', background: '#fff', position: 'relative', display: 'flex', flexDirection: 'column', boxShadow: '0 10px 25px -5px rgba(2, 132, 199, 0.15)' }}>
              <div style={{ position: 'absolute', top: '-12px', right: '24px', background: '#0284c7', color: '#fff', padding: '4px 12px', borderRadius: '12px', fontSize: '11px', fontWeight: 800 }}>
                MOST POPULAR
              </div>
              <div style={{ fontSize: '18px', fontWeight: 700, color: '#0f172a' }}>Growth (Mid-Sized Firm)</div>
              <p style={{ fontSize: '13px', color: '#64748b', margin: '4px 0 20px 0' }}>The complete audit operating system for 2–6 partner firms.</p>
              <div style={{ fontSize: '36px', fontWeight: 800, color: '#0f172a', marginBottom: '4px' }}>
                {billingCycle === 'annual' ? '₹4,166' : '₹4,999'}
                <span style={{ fontSize: '14px', fontWeight: 500, color: '#64748b' }}> / month</span>
              </div>
              <div style={{ fontSize: '11px', color: '#94a3b8', marginBottom: '24px' }}>+ 18% GST (SAC 998311)</div>
              <ul style={{ listStyle: 'none', padding: 0, margin: '0 0 32px 0', fontSize: '13px', color: '#475569', display: 'flex', flexDirection: 'column', gap: '10px', flex: 1 }}>
                <li>✓ <strong>All Starter features included</strong></li>
                <li>✓ 15 User Seats &amp; 500 Clients</li>
                <li>✓ <strong>Unlimited Concurrent Statutory Audits</strong></li>
                <li>✓ CARO 2020 Automated 21-Clause Checklist</li>
                <li>✓ Forensic Bank Analyzer &amp; Sec 40A(3) Fraud Engine</li>
                <li>✓ SA 505 Automated External Confirmations Desk</li>
                <li>✓ 1-Click SA 700 Independent Auditor's Report &amp; UDIN</li>
                <li>✓ Schedule III Notes to Accounts &amp; 11 Ratios</li>
              </ul>
              <button
                onClick={onStartTrial}
                style={{ width: '100%', padding: '10px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 700, fontSize: '14px', cursor: 'pointer' }}
              >
                Start 14-Day Free Trial
              </button>
            </div>

            {/* Enterprise */}
            <div style={{ padding: '32px', borderRadius: '12px', border: '1px solid #cbd5e1', background: '#fff', display: 'flex', flexDirection: 'column' }}>
              <div style={{ fontSize: '18px', fontWeight: 700, color: '#0f172a' }}>Enterprise (Corporate Practice)</div>
              <p style={{ fontSize: '13px', color: '#64748b', margin: '4px 0 20px 0' }}>Unlimited power for multi-branch firms and cross-border specialists.</p>
              <div style={{ fontSize: '36px', fontWeight: 800, color: '#0f172a', marginBottom: '4px' }}>
                {billingCycle === 'annual' ? '₹12,499' : '₹14,999'}
                <span style={{ fontSize: '14px', fontWeight: 500, color: '#64748b' }}> / month</span>
              </div>
              <div style={{ fontSize: '11px', color: '#94a3b8', marginBottom: '24px' }}>+ 18% GST (SAC 998311)</div>
              <ul style={{ listStyle: 'none', padding: 0, margin: '0 0 32px 0', fontSize: '13px', color: '#475569', display: 'flex', flexDirection: 'column', gap: '10px', flex: 1 }}>
                <li>✓ <strong>Unlimited Users, Clients &amp; Audits</strong></li>
                <li>✓ Transfer Pricing Study &amp; Form 3CEB Certification</li>
                <li>✓ Cost Audit &amp; Form CRA-1 to CRA-4 Desk</li>
                <li>✓ 1-Click SA 230 Digital Workpaper Dossier (SHA-256)</li>
                <li>✓ ICAI AQMM v1.0 600-Point Quality Maturity Desk</li>
                <li>✓ Custom Domain (CNAME Whitelabel) &amp; Firm Branding</li>
                <li>✓ Dedicated 24/7 CA Technical Concierge Support</li>
              </ul>
              <button
                onClick={onStartTrial}
                style={{ width: '100%', padding: '10px', background: '#0f172a', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 700, fontSize: '14px', cursor: 'pointer' }}
              >
                Contact Sales / Start Trial
              </button>
            </div>
          </div>
        </div>
      </section>

      {/* Security Section */}
      <section id="security" style={{ padding: '80px 24px', maxWidth: '1000px', margin: '0 auto', textAlign: 'center' }}>
        <h2 style={{ fontSize: '14px', fontWeight: 700, color: '#0284c7', textTransform: 'uppercase', letterSpacing: '1px', margin: '0 0 10px 0' }}>
          Enterprise Architecture
        </h2>
        <h3 style={{ fontSize: '32px', fontWeight: 800, color: '#0f172a', margin: '0 0 16px 0' }}>
          Two-Lock Isolation &amp; Military-Grade Cryptography
        </h3>
        <p style={{ fontSize: '16px', color: '#475569', lineHeight: 1.6, margin: '0 0 36px 0' }}>
          Your client data never leaks. Firm separation is enforced at two independent architectural layers: EF Core global tenant query filters and PostgreSQL Row-Level Security (RLS) at the database kernel.
        </p>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '20px', textAlign: 'left' }}>
          <div style={{ padding: '20px', borderRadius: '8px', background: '#f8fafc', border: '1px solid #e2e8f0' }}>
            <div style={{ fontWeight: 700, color: '#0f172a', marginBottom: '6px' }}>🔒 Row-Level Security</div>
            <div style={{ fontSize: '13px', color: '#64748b' }}>PostgreSQL RLS ensures Firm A can never read or query Firm B records.</div>
          </div>
          <div style={{ padding: '20px', borderRadius: '8px', background: '#f8fafc', border: '1px solid #e2e8f0' }}>
            <div style={{ fontWeight: 700, color: '#0f172a', marginBottom: '6px' }}>🛡️ AES-256 Backups</div>
            <div style={{ fontSize: '13px', color: '#64748b' }}>Automated daily pg_dump backups encrypted with OpenSSL AES-256-CBC.</div>
          </div>
          <div style={{ padding: '20px', borderRadius: '8px', background: '#f8fafc', border: '1px solid #e2e8f0' }}>
            <div style={{ fontWeight: 700, color: '#0f172a', marginBottom: '6px' }}>🦠 ClamAV Antivirus</div>
            <div style={{ fontSize: '13px', color: '#64748b' }}>Every client document uploaded to the vault is actively scanned for malware.</div>
          </div>
          <div style={{ padding: '20px', borderRadius: '8px', background: '#f8fafc', border: '1px solid #e2e8f0' }}>
            <div style={{ fontWeight: 700, color: '#0f172a', marginBottom: '6px' }}>⚡ TOTP Multi-Factor</div>
            <div style={{ fontSize: '13px', color: '#64748b' }}>Mandatory authenticator app two-step sign-in for partners and managers.</div>
          </div>
        </div>
      </section>

      {/* FAQ Section */}
      <section id="faq" style={{ padding: '80px 24px', background: '#f8fafc', borderTop: '1px solid #e2e8f0' }}>
        <div style={{ maxWidth: '800px', margin: '0 auto' }}>
          <h3 style={{ fontSize: '32px', fontWeight: 800, textAlign: 'center', color: '#0f172a', margin: '0 0 36px 0' }}>
            Frequently Asked Questions
          </h3>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
            {[
              {
                q: 'Can we import real client data from Tally Prime and Busy?',
                a: 'Yes. CA Desk includes a Universal Trial Balance Importer that accepts Tally Prime XML/CSV exports, Busy Accounting reports, SAP GL statements, or simple Excel copy-paste. Account heads are auto-categorized into Schedule III and Form 3CD in seconds.'
              },
              {
                q: 'How does the 14-day free trial work?',
                a: 'You get full, unrestricted access to the platform for 14 days without entering credit card details. You can even check our "Pre-load Sample CA Firm" option during signup to immediately test with 3 realistic audit clients.'
              },
              {
                q: 'Is CA Desk compliant with ICAI and NFRA standards?',
                a: '100%. CA Desk strictly implements ICAI SQC 1 (60-day archival locks), SA 210, SA 230 (Lead schedules A-Q), SA 505 (External Confirmations), SA 700/701 (Independent Auditor Reports with UDIN validation), and the new ICAI AQMM v1.0 600-point maturity framework.'
              },
              {
                q: 'Will my firm get a GST tax invoice with input credit?',
                a: 'Yes. All subscription payments generate an official GST tax invoice under SAC 998311 with 18% GST (9% CGST + 9% SGST or 18% IGST), recording your firm\'s GSTIN so you can claim full Input Tax Credit (ITC).'
              }
            ].map((faq, idx) => (
              <div key={idx} style={{ padding: '20px', borderRadius: '8px', background: '#fff', border: '1px solid #e2e8f0' }}>
                <h4 style={{ fontSize: '15px', fontWeight: 700, margin: '0 0 8px 0', color: '#0f172a' }}>{faq.q}</h4>
                <p style={{ fontSize: '13px', color: '#475569', lineHeight: 1.6, margin: 0 }}>{faq.a}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Call to Action Footer Banner */}
      <section style={{ padding: '60px 24px', background: '#0f172a', color: '#fff', textAlign: 'center' }}>
        <h3 style={{ fontSize: '32px', fontWeight: 800, margin: '0 0 14px 0' }}>
          Ready to Modernize Your CA Practice?
        </h3>
        <p style={{ fontSize: '16px', color: '#94a3b8', margin: '0 0 28px 0' }}>
          Join leading Indian Chartered Accountants transforming their audit, tax, and compliance workflows.
        </p>
        <button
          onClick={onStartTrial}
          style={{ padding: '14px 36px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '8px', fontSize: '15px', fontWeight: 700, cursor: 'pointer', boxShadow: '0 10px 15px -3px rgba(2, 132, 199, 0.4)' }}
        >
          🚀 Start Your 14-Day Free Trial Now
        </button>
      </section>

      {/* Footer */}
      <footer style={{ borderTop: '1px solid #1e293b', background: '#020617', color: '#64748b', padding: '32px 24px', fontSize: '12px' }}>
        <div style={{ maxWidth: '1280px', margin: '0 auto', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '14px' }}>
          <div>
            © {new Date().getFullYear()} CA Desk Technologies Pvt Ltd. All rights reserved. Built for Indian Chartered Accountants.
          </div>
          <div style={{ display: 'flex', gap: '20px' }}>
            <span>Companies Act, 2013</span>
            <span>Income Tax Act, 1961</span>
            <span>ICAI SAs</span>
            <span>SAC 998311</span>
          </div>
        </div>
      </footer>
    </div>
  )
}
export default LandingPage
