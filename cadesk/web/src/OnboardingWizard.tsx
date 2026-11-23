import { useState } from 'react'
import { api, type AuthResult, setAccessToken, subscriptionApi } from './api'

interface OnboardingWizardProps {
  onSignedIn: (r: AuthResult) => void
  onCancel: () => void
}

export function OnboardingWizard({ onSignedIn, onCancel }: OnboardingWizardProps) {
  const [step, setStep] = useState<1 | 2 | 3>(1)
  const [busy, setBusy] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  // Step 1: Firm Info
  const [firmName, setFirmName] = useState<string>('')
  const [frn, setFrn] = useState<string>('')
  const [firmPan, setFirmPan] = useState<string>('')
  const [firmGstin, setFirmGstin] = useState<string>('')
  const [city, setCity] = useState<string>('')
  const [state, setState] = useState<string>('Maharashtra')

  // Step 2: Managing Partner Info
  const [partnerName, setPartnerName] = useState<string>('')
  const [membershipNo, setMembershipNo] = useState<string>('')
  const [email, setEmail] = useState<string>('')
  const [password, setPassword] = useState<string>('')
  const [mobile, setMobile] = useState<string>('')
  const [designation, setDesignation] = useState<string>('Senior Partner')

  // Step 3: Practice Setup & Demo Pre-loader
  const [practiceSize, setPracticeSize] = useState<string>('MidFirm') // Solo, MidFirm, Large
  const [primaryFocus, setPrimaryFocus] = useState<string>('StatutoryAudit')
  const [seedDemoData, setSeedDemoData] = useState<boolean>(true)

  async function handleCompleteRegistration(e: React.FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)

    try {
      // 1. Call standard signup API
      const signupPayload = {
        firmName: firmName.trim(),
        countryCode: 'IN',
        name: partnerName.trim(),
        email: email.trim().toLowerCase(),
        password: password
      }

      const res = await api<AuthResult>('/api/auth/signup', {
        method: 'POST',
        body: JSON.stringify(signupPayload)
      })

      if (res.status === 'ok' && res.accessToken) {
        setAccessToken(res.accessToken)

        // 2. If user opted for demo firm pre-loader, seed realistic clients
        if (seedDemoData) {
          try {
            await subscriptionApi.seedDemoData()
          } catch {
            // Non-fatal if demo seeder encounters an edge case
          }
        }

        onSignedIn(res)
      } else {
        setError('Sign up succeeded but token was not returned. Please sign in.')
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Registration failed. Please check inputs.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div style={{ maxWidth: '640px', margin: '40px auto', background: '#fff', border: '1px solid #cbd5e1', borderRadius: '12px', boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)', overflow: 'hidden' }}>
      {/* Wizard Header */}
      <div style={{ padding: '24px', background: '#0f172a', color: '#fff' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
          <div style={{ fontSize: '11px', fontWeight: 700, letterSpacing: '1px', textTransform: 'uppercase', color: '#38bdf8' }}>
            CA Desk • Firm Onboarding Wizard
          </div>
          <button onClick={onCancel} style={{ background: 'none', border: 'none', color: '#94a3b8', fontSize: '18px', cursor: 'pointer' }}>✕</button>
        </div>
        <h2 style={{ fontSize: '20px', fontWeight: 700, margin: '0 0 6px 0' }}>
          {step === 1 && 'Step 1 of 3: Chartered Accountant Firm Profile'}
          {step === 2 && 'Step 2 of 3: Managing Partner & Signing Authority'}
          {step === 3 && 'Step 3 of 3: Practice Customization & Instant Demo Workspace'}
        </h2>
        <p style={{ fontSize: '13px', color: '#94a3b8', margin: 0 }}>
          {step === 1 && 'Register your CA firm with ICAI Firm Registration details to establish two-lock isolated tenancy.'}
          {step === 2 && 'Set up your primary partner account for signing audit workpapers, UDIN reports, and tax filings.'}
          {step === 3 && 'Tailor your workspace modules and pre-load sample audit clients for an instant test drive.'}
        </p>

        {/* Step Indicator Bar */}
        <div style={{ display: 'flex', gap: '8px', marginTop: '16px' }}>
          <div style={{ flex: 1, height: '4px', borderRadius: '2px', background: step >= 1 ? '#38bdf8' : '#334155' }} />
          <div style={{ flex: 1, height: '4px', borderRadius: '2px', background: step >= 2 ? '#38bdf8' : '#334155' }} />
          <div style={{ flex: 1, height: '4px', borderRadius: '2px', background: step >= 3 ? '#38bdf8' : '#334155' }} />
        </div>
      </div>

      {/* Wizard Body */}
      <div style={{ padding: '28px' }}>
        {error && (
          <div style={{ padding: '12px 16px', background: '#fef2f2', border: '1px solid #fecaca', borderRadius: '6px', color: '#991b1b', fontSize: '13px', marginBottom: '20px' }}>
            ⚠️ {error}
          </div>
        )}

        {/* STEP 1 */}
        {step === 1 && (
          <div>
            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                Full Legal Name of CA Firm: *
              </label>
              <input
                type="text"
                required
                value={firmName}
                onChange={e => setFirmName(e.target.value)}
                placeholder="e.g. M/s K. S. Sharma &amp; Associates, Chartered Accountants"
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '16px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                  ICAI Firm Registration No (FRN): *
                </label>
                <input
                  type="text"
                  required
                  value={frn}
                  onChange={e => setFrn(e.target.value.toUpperCase())}
                  placeholder="e.g. 104523W or 012345N"
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px', fontFamily: 'monospace' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                  Firm PAN (Permanent Account No): *
                </label>
                <input
                  type="text"
                  required
                  maxLength={10}
                  value={firmPan}
                  onChange={e => setFirmPan(e.target.value.toUpperCase())}
                  placeholder="e.g. AAAFR1234A"
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px', fontFamily: 'monospace' }}
                />
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '14px', marginBottom: '24px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                  City / Location:
                </label>
                <input
                  type="text"
                  value={city}
                  onChange={e => setCity(e.target.value)}
                  placeholder="e.g. Mumbai, New Delhi"
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                  State / Jurisdiction:
                </label>
                <input
                  type="text"
                  value={state}
                  onChange={e => setState(e.target.value)}
                  placeholder="e.g. Maharashtra"
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                  Firm GSTIN (Optional):
                </label>
                <input
                  type="text"
                  maxLength={15}
                  value={firmGstin}
                  onChange={e => setFirmGstin(e.target.value.toUpperCase())}
                  placeholder="e.g. 27AAAFR1234A1Z5"
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px', fontFamily: 'monospace' }}
                />
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
              <button
                type="button"
                onClick={() => {
                  if (!firmName.trim()) { alert('Please enter your firm name.'); return; }
                  setStep(2)
                }}
                style={{ padding: '10px 24px', background: '#0f172a', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 700, fontSize: '13px', cursor: 'pointer' }}
              >
                Next: Partner Identity →
              </button>
            </div>
          </div>
        )}

        {/* STEP 2 */}
        {step === 2 && (
          <div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '14px', marginBottom: '16px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                  Partner Full Name: *
                </label>
                <input
                  type="text"
                  required
                  value={partnerName}
                  onChange={e => setPartnerName(e.target.value)}
                  placeholder="e.g. CA Rajesh Kumar"
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                  ICAI Membership Number: *
                </label>
                <input
                  type="text"
                  required
                  value={membershipNo}
                  onChange={e => setMembershipNo(e.target.value)}
                  placeholder="e.g. 054321 (FCA / ACA)"
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                  Partner Designation:
                </label>
                <select
                  value={designation}
                  onChange={e => setDesignation(e.target.value)}
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
                >
                  <option value="Senior Partner">Senior Partner</option>
                  <option value="Managing Partner">Managing Partner</option>
                  <option value="Sole Proprietor">Sole Proprietor</option>
                  <option value="Audit Partner">Audit Partner</option>
                </select>
              </div>
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                Official Work Email Address: *
              </label>
              <input
                type="email"
                required
                value={email}
                onChange={e => setEmail(e.target.value)}
                placeholder="rajesh@yourfirm.in"
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '24px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                  Account Password: *
                </label>
                <input
                  type="password"
                  required
                  value={password}
                  onChange={e => setPassword(e.target.value)}
                  placeholder="••••••••••••"
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                  Mobile (For OTP &amp; Alerts):
                </label>
                <input
                  type="tel"
                  value={mobile}
                  onChange={e => setMobile(e.target.value)}
                  placeholder="+91 98765 43210"
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
                />
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <button
                type="button"
                onClick={() => setStep(1)}
                style={{ padding: '8px 18px', background: 'none', border: '1px solid #cbd5e1', borderRadius: '6px', fontSize: '13px', fontWeight: 600, color: '#475569', cursor: 'pointer' }}
              >
                ← Back
              </button>
              <button
                type="button"
                onClick={() => {
                  if (!partnerName.trim() || !email.trim() || !password) {
                    alert('Please enter your name, email, and password.')
                    return
                  }
                  setStep(3)
                }}
                style={{ padding: '10px 24px', background: '#0f172a', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 700, fontSize: '13px', cursor: 'pointer' }}
              >
                Next: Practice Customization →
              </button>
            </div>
          </div>
        )}

        {/* STEP 3 */}
        {step === 3 && (
          <form onSubmit={handleCompleteRegistration}>
            <div style={{ marginBottom: '18px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '8px' }}>
                Firm Scale &amp; Practice Structure:
              </label>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px' }}>
                {[
                  { id: 'Solo', label: 'Sole Practitioner', desc: '1 CA + 2-3 Articles' },
                  { id: 'MidFirm', label: 'Mid-Sized Firm', desc: '2-6 Partners + Audit Team' },
                  { id: 'Large', label: 'Large / Multi-Branch', desc: '7+ Partners & Corporate Desk' }
                ].map(opt => (
                  <div
                    key={opt.id}
                    onClick={() => setPracticeSize(opt.id)}
                    style={{ padding: '12px', borderRadius: '6px', border: practiceSize === opt.id ? '2px solid #0284c7' : '1px solid #cbd5e1', background: practiceSize === opt.id ? '#f0f9ff' : '#fff', cursor: 'pointer' }}
                  >
                    <div style={{ fontWeight: 700, fontSize: '13px', color: '#0f172a' }}>{opt.label}</div>
                    <div style={{ fontSize: '11px', color: '#64748b', marginTop: '2px' }}>{opt.desc}</div>
                  </div>
                ))}
              </div>
            </div>

            <div style={{ marginBottom: '20px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '8px' }}>
                Primary Practice Domain:
              </label>
              <select
                value={primaryFocus}
                onChange={e => setPrimaryFocus(e.target.value)}
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
              >
                <option value="StatutoryAudit">Statutory Audit &amp; CARO 2020 Focus (Companies Act / ICAI)</option>
                <option value="TaxPractice">Direct Tax, Form 3CD &amp; TDS Practice (Income Tax Act)</option>
                <option value="FullService">Full-Service Practice (Audit, Tax, GST, MCA, Payroll, AQMM)</option>
                <option value="CrossBorder">Cross-Border, Transfer Pricing (3CEB) &amp; Foreign Remittance (15CA/CB)</option>
              </select>
            </div>

            {/* Instant Demo Firm Pre-loader Toggle */}
            <div style={{ background: '#f0fdf4', border: '1px solid #86efac', borderRadius: '8px', padding: '16px', marginBottom: '24px' }}>
              <label style={{ display: 'flex', alignItems: 'flex-start', gap: '10px', cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={seedDemoData}
                  onChange={e => setSeedDemoData(e.target.checked)}
                  style={{ marginTop: '3px' }}
                />
                <div>
                  <span style={{ fontWeight: 700, color: '#166534', fontSize: '13px' }}>
                    ⚡ Pre-load Sample CA Firm Workspace (Recommended for New Signups)
                  </span>
                  <p style={{ margin: '4px 0 0 0', fontSize: '12px', color: '#15803d', lineHeight: 1.5 }}>
                    Instantly populates your workspace with 3 realistic client entities (Apex Global Technologies ₹46.5 Cr turnover, Bharat Steel ₹18.2 Cr with bank forensics, Vanguard Healthcare) with active statutory audits, lead schedules, and bank statement anomaly logs so you can explore immediately. You can clear them in 1-click at any time.
                  </p>
                </div>
              </label>
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <button
                type="button"
                onClick={() => setStep(2)}
                style={{ padding: '8px 18px', background: 'none', border: '1px solid #cbd5e1', borderRadius: '6px', fontSize: '13px', fontWeight: 600, color: '#475569', cursor: 'pointer' }}
              >
                ← Back
              </button>
              <button
                type="submit"
                disabled={busy}
                style={{ padding: '10px 28px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 700, fontSize: '14px', cursor: 'pointer' }}
              >
                {busy ? 'Creating Your CA Workspace...' : '🚀 Launch My 14-Day Free Trial'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  )
}
