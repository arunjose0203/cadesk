import { useState } from 'react'
import { api } from '../api'

type PaymentLink = { invoiceId: string; invoiceNumber: string; url: string; status: string; amount: number }

const inr = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' })

/* Sprint 69: a Razorpay payment link for the invoice's balance, to share with the client.
   When the client pays, Razorpay's webhook records the payment and marks the invoice paid. */
export function PaymentLinkButton({ invoiceId, clientName }: { invoiceId: string; clientName?: string | null }) {
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const [link, setLink] = useState<PaymentLink | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  const start = async () => {
    setOpen(true); setBusy(true); setError(null); setCopied(false)
    try {
      setLink(await api<PaymentLink>(`/api/invoices/${invoiceId}/payment-link`, { method: 'POST' }))
    } catch (e) {
      setError(e instanceof Error ? e.message : 'The payment link could not be made.')
    } finally {
      setBusy(false)
    }
  }

  const message = link
    ? `Dear ${clientName || 'Sir/Madam'}, please pay ${inr.format(link.amount)} for invoice ${link.invoiceNumber} here: ${link.url}`
    : ''

  const copy = async () => {
    if (!link) return
    try { await navigator.clipboard.writeText(link.url); setCopied(true) } catch { setCopied(false) }
  }

  return (
    <>
      <button className="small ghost" onClick={() => void start()} title="Payment link the client can pay online (UPI, card, net banking)">
        Payment link
      </button>
      {open && (
        <div className="palette-backdrop" onMouseDown={e => { if (e.target === e.currentTarget) setOpen(false) }}>
          <div className="palette" role="dialog" aria-modal="true" aria-label="Payment link" style={{ padding: 20, maxWidth: 520 }}
            onKeyDown={e => { if (e.key === 'Escape') setOpen(false) }}>
            <h2 style={{ fontSize: '1.1rem' }}>Online payment link</h2>
            {busy && <p className="muted">Making the link…</p>}
            {error && <p className="notice small">{error}</p>}
            {link && (
              <>
                <p className="small muted" style={{ marginTop: 4 }}>
                  {inr.format(link.amount)} for invoice {link.invoiceNumber}. When the client pays, the payment is recorded and the invoice is marked paid.
                </p>
                <input readOnly value={link.url} onFocus={e => e.currentTarget.select()} style={{ width: '100%', marginTop: 8 }} aria-label="Payment link" />
                <div className="row" style={{ marginTop: 12 }}>
                  <button className="small" onClick={() => void copy()}>{copied ? 'Copied' : 'Copy link'}</button>
                  <a className="button-like ghost-like small" href={`https://wa.me/?text=${encodeURIComponent(message)}`} target="_blank" rel="noreferrer">Send on WhatsApp</a>
                  <a className="button-like ghost-like small" href={link.url} target="_blank" rel="noreferrer">Open</a>
                </div>
              </>
            )}
            <div className="row" style={{ justifyContent: 'flex-end', marginTop: 16 }}>
              <button className="small ghost" onClick={() => setOpen(false)}>Close</button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
