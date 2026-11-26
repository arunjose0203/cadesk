import { useEffect, useState } from 'react'

interface ProductTourModalProps {
  isOpen: boolean
  onClose: () => void
  onStartTrial: () => void
}

export function ProductTourModal({ isOpen, onClose, onStartTrial }: ProductTourModalProps) {
  const [step, setStep] = useState<number>(0)

  useEffect(() => {
    if (!isOpen) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [isOpen, onClose])

  if (!isOpen) return null

  const tourSteps = [
    {
      title: 'Workflow 1: Universal Trial Balance Importer',
      tag: '60-SECOND INGESTION',
      description: 'Ingest financial data directly from Tally Prime (XML/CSV), Busy, SAP, or copy-paste Excel sheets. Our fuzzy AI automatically maps accounts to Schedule III Division I/II, Form 3CD, and CARO 2020.',
      visual: (
        <div style={{ background: '#0f172a', padding: '16px', borderRadius: '6px', color: '#f8fafc', fontSize: '12px', fontFamily: 'monospace' }}>
          <div style={{ color: '#38bdf8', marginBottom: '8px' }}>// Detected: Tally Prime Manufacturing Trial Balance (₹46.5 Cr)</div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '8px', borderBottom: '1px solid #334155', paddingBottom: '6px' }}>
            <span>Ledger Name</span>
            <span>Closing Balance</span>
            <span>Auto-Mapped Category</span>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '8px', padding: '4px 0' }}>
            <span>HDFC Current A/c</span>
            <span style={{ color: '#4ade80' }}>₹1,45,20,000 Dr</span>
            <span style={{ color: '#facc15' }}>Cash & Bank (Sch III)</span>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '8px', padding: '4px 0' }}>
            <span>Sundry Debtors</span>
            <span style={{ color: '#4ade80' }}>₹8,42,10,000 Dr</span>
            <span style={{ color: '#facc15' }}>Trade Receivables (SA 505)</span>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '8px', padding: '4px 0' }}>
            <span>Plant & Machinery</span>
            <span style={{ color: '#4ade80' }}>₹14,20,000 Dr</span>
            <span style={{ color: '#facc15' }}>Fixed Assets (Sec 32 / Sch II)</span>
          </div>
        </div>
      )
    },
    {
      title: 'Workflow 2: Forensic Bank Analyzer & Fraud Detection Engine',
      tag: 'INDIAN BANK STATEMENTS',
      description: 'Ingest PDF, Excel, or CSV bank statements from SBI, HDFC, ICICI, Axis, PNB. The 7-rule statutory forensics engine cross-checks transactions against Trial Balance ledgers to catch fraud instantly.',
      visual: (
        <div style={{ background: 'rgba(220, 38, 38, 0.08)', border: '1px solid rgba(220, 38, 38, 0.3)', padding: '16px', borderRadius: '6px', fontSize: '12px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px', color: 'var(--error)', fontWeight: 700 }}>
            <span>STATUTORY VIOLATION DETECTED</span>
            <span style={{ background: 'rgba(220, 38, 38, 0.14)', padding: '2px 6px', borderRadius: '4px' }}>Section 40A(3)</span>
          </div>
          <p style={{ margin: '0 0 6px 0', color: 'var(--text)' }}>
            <strong>Txn:</strong> Cash withdrawal of <strong>₹45,000</strong> on 14-Aug-2024 to "Vendor Site Expense" exceeds statutory threshold of ₹10,000/day.
          </p>
          <div style={{ fontSize: '11px', color: 'var(--error)' }}>
            ➔ Automatically flagged for 100% disallowance in Form 3CD Clause 21(d) &amp; CARO 2020 Clause (ix).
          </div>
        </div>
      )
    },
    {
      title: 'Workflow 3: SA 505 Automated External Confirmations Desk',
      tag: 'THIRD-PARTY CIRCULARIZATION',
      description: 'Send tokenized, secure digital verification links to Debtors, Creditors, and Banks. Third parties click the link on mobile or desktop to confirm balances directly, with alternative procedures logged under SA 505.12.',
      visual: (
        <div style={{ background: 'rgba(22, 163, 74, 0.08)', border: '1px solid rgba(22, 163, 74, 0.3)', padding: '16px', borderRadius: '6px', fontSize: '12px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
            <span style={{ fontWeight: 700, color: 'var(--success)' }}>SA 505 Balance Confirmation Link</span>
            <span style={{ background: 'rgba(22, 163, 74, 0.14)', color: 'var(--success)', padding: '2px 8px', borderRadius: '12px', fontWeight: 700 }}>VERIFIED</span>
          </div>
          <p style={{ margin: '0 0 8px 0', color: 'var(--text)' }}>
            "Apex Global Technologies confirms balance of <strong>₹24,50,000 Dr</strong> with Bharat Steel &amp; Infra Pvt Ltd as on 31-Mar-2025."
          </p>
          <div style={{ fontSize: '11px', color: 'var(--success)' }}>
            Digital Sign-off: CA Desk Public Portal | IP: 103.21.244.2 | Token: SEC-CONF-88219
          </div>
        </div>
      )
    },
    {
      title: 'Workflow 4: 1-Click ICAI Independent Auditor\'s Report (SA 700)',
      tag: 'RULE 11(g) + 18-CHAR UDIN',
      description: 'Generate complete ICAI compliant Independent Auditor\'s Reports (Unmodified, Qualified, Adverse, Disclaimer). Verifies Rule 11(g) software audit trail edit logs and binds the report with an 18-character UDIN.',
      visual: (
        <div style={{ background: 'var(--card-subtle)', border: '1px solid var(--border)', padding: '16px', borderRadius: '6px', fontSize: '12px', color: 'var(--text-secondary)' }}>
          <div style={{ fontWeight: 700, color: 'var(--text)', marginBottom: '4px', textTransform: 'uppercase' }}>
            Independent Auditor's Report (SA 700)
          </div>
          <p style={{ margin: '0 0 6px 0', fontSize: '11px', color: 'var(--muted)' }}>
            "In our opinion and to the best of our information... the aforesaid financial statements give the information required by the Act in the manner so required and give a true and fair view in conformity with the accounting principles generally accepted in India..."
          </p>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderTop: '1px solid var(--border)', paddingTop: '6px', fontSize: '11px' }}>
            <span style={{ color: 'var(--success)', fontWeight: 700 }}>Rule 11(g) Audit Trail: COMPLIANT</span>
            <span style={{ fontFamily: 'monospace', fontWeight: 700, background: 'var(--card)', color: 'var(--text)', border: '1px solid var(--border)', padding: '2px 6px', borderRadius: '4px' }}>UDIN: 25054321AAAA123456</span>
          </div>
        </div>
      )
    },
    {
      title: 'Workflow 5: ICAI Audit Quality Maturity Model (AQMM v1.0) Desk',
      tag: '600-POINT CAQ MATURITY',
      description: 'Automated 600-point peer-review scoring across Practice Management (190 pts), Human Resources (110 pts), and Audit Execution / SQC 1 (300 pts). Auto-evaluates points from CA Desk live telemetry!',
      visual: (
        <div style={{ background: 'var(--card-subtle)', border: '1px solid var(--border)', padding: '16px', borderRadius: '6px', fontSize: '12px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
            <span style={{ fontWeight: 700, color: 'var(--text)' }}>ICAI AQMM Classification: Level 3</span>
            <span style={{ background: 'rgba(22, 163, 74, 0.14)', color: 'var(--success)', padding: '4px 10px', borderRadius: '20px', fontWeight: 800 }}>
              535 / 600 Points (89.2%)
            </span>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '8px', textAlign: 'center', fontSize: '11px', color: 'var(--text-secondary)' }}>
            <div style={{ background: 'var(--card)', padding: '8px', border: '1px solid var(--border)', borderRadius: '4px' }}>
              <div>Operations (Sec A)</div>
              <strong style={{ color: 'var(--accent)' }}>175 / 190</strong>
            </div>
            <div style={{ background: 'var(--card)', padding: '8px', border: '1px solid var(--border)', borderRadius: '4px' }}>
              <div>HR (Sec B)</div>
              <strong style={{ color: '#6366f1' }}>95 / 110</strong>
            </div>
            <div style={{ background: 'var(--card)', padding: '8px', border: '1px solid var(--border)', borderRadius: '4px' }}>
              <div>Audit (Sec C)</div>
              <strong style={{ color: 'var(--success)' }}>265 / 300</strong>
            </div>
          </div>
        </div>
      )
    }
  ]

  const current = tourSteps[step]

  return (
    <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(9, 21, 46, 0.78)', backdropFilter: 'blur(4px)', zIndex: 9999, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '20px' }}>
      <div style={{ background: 'var(--card)', color: 'var(--text)', border: '1px solid var(--border)', width: '100%', maxWidth: '680px', borderRadius: '14px', boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.35)', overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
        {/* Header */}
        <div style={{ padding: '20px 24px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>
            <span style={{ fontSize: '11px', fontWeight: 700, color: 'var(--accent)', background: 'var(--accent-subtle)', padding: '3px 8px', borderRadius: '4px', textTransform: 'uppercase' }}>
              {current.tag} (Step {step + 1} of {tourSteps.length})
            </span>
            <h2 style={{ fontSize: '18px', fontWeight: 700, margin: '6px 0 0 0', color: 'var(--text)' }}>
              {current.title}
            </h2>
          </div>
          <button onClick={onClose} aria-label="Close" style={{ background: 'none', border: 'none', fontSize: '20px', cursor: 'pointer', color: 'var(--muted)' }}>✕</button>
        </div>

        {/* Body */}
        <div style={{ padding: '24px' }}>
          <p style={{ fontSize: '14px', color: 'var(--text-secondary)', lineHeight: 1.6, margin: '0 0 16px 0' }}>
            {current.description}
          </p>

          <div style={{ marginBottom: '24px' }}>
            {current.visual}
          </div>

          {/* Progress dots */}
          <div style={{ display: 'flex', justifyContent: 'center', gap: '8px' }}>
            {tourSteps.map((_, i) => (
              <div
                key={i}
                onClick={() => setStep(i)}
                style={{ width: i === step ? '24px' : '8px', height: '8px', borderRadius: '4px', background: i === step ? 'var(--accent)' : 'var(--border)', cursor: 'pointer', transition: 'all 0.2s' }}
              />
            ))}
          </div>
        </div>

        {/* Footer */}
        <div style={{ padding: '16px 24px', background: 'var(--card-subtle)', borderTop: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <button
            onClick={() => setStep(prev => Math.max(0, prev - 1))}
            disabled={step === 0}
            style={{ padding: '8px 16px', background: 'none', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', cursor: step === 0 ? 'not-allowed' : 'pointer', opacity: step === 0 ? 0.5 : 1 }}
          >
            ← Previous
          </button>

          <div style={{ display: 'flex', gap: '10px' }}>
            {step < tourSteps.length - 1 ? (
              <button
                onClick={() => setStep(prev => Math.min(tourSteps.length - 1, prev + 1))}
                style={{ padding: '8px 20px', background: 'linear-gradient(135deg, #09152e 0%, #1d4ed8 100%)', color: '#fff', border: 'none', borderRadius: '6px', fontSize: '13px', fontWeight: 600, cursor: 'pointer' }}
              >
                Next Workflow →
              </button>
            ) : (
              <button
                onClick={() => { onClose(); onStartTrial(); }}
                style={{ padding: '8px 20px', background: 'var(--accent)', color: '#fff', border: 'none', borderRadius: '6px', fontSize: '13px', fontWeight: 700, cursor: 'pointer' }}
              >
                Start 14-Day Free Trial
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
