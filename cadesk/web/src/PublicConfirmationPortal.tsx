import React, { useState, useEffect } from 'react'
import { auditApi, type PublicConfirmationDetailsDto, type SubmitConfirmationResponseRequest } from './api'

export const PublicConfirmationPortal: React.FC = () => {
  const [token, setToken] = useState<string>('')
  const [loading, setLoading] = useState<boolean>(true)
  const [details, setDetails] = useState<PublicConfirmationDetailsDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<boolean>(false)
  const [submitting, setSubmitting] = useState<boolean>(false)

  // Form
  const [agreesWithBalance, setAgreesWithBalance] = useState<boolean>(true)
  const [confirmedBalance, setConfirmedBalance] = useState<number>(0)
  const [disputeReason, setDisputeReason] = useState<string>('None')
  const [remarks, setRemarks] = useState<string>('')
  const [signatoryName, setSignatoryName] = useState<string>('')
  const [designation, setDesignation] = useState<string>('')

  useEffect(() => {
    // Extract token from pathname /confirm/{token} or ?confirm={token}
    const pathParts = window.location.pathname.split('/')
    let foundToken = ''
    const confirmIdx = pathParts.indexOf('confirm')
    if (confirmIdx !== -1 && pathParts.length > confirmIdx + 1) {
      foundToken = pathParts[confirmIdx + 1]
    }
    if (!foundToken) {
      const q = new URLSearchParams(window.location.search)
      foundToken = q.get('confirm') || ''
    }

    if (!foundToken) {
      setError('Invalid confirmation access URL. Please check your circularization link.')
      setLoading(false)
      return
    }

    setToken(foundToken)
    loadDetails(foundToken)
  }, [])

  const loadDetails = async (t: string) => {
    try {
      setLoading(true)
      setError(null)
      const data = await auditApi.getPublicConfirmation(t)
      setDetails(data)
      if (data.bookBalanceAmount != null) {
        setConfirmedBalance(data.bookBalanceAmount)
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to retrieve confirmation details.')
    } finally {
      setLoading(false)
    }
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!token) return

    if (!agreesWithBalance && disputeReason === 'None') {
      alert('Please specify the dispute or difference reason (e.g., Goods in Transit, Cheque in Transit, TDS Mismatch).')
      return
    }

    try {
      setSubmitting(true)
      setError(null)

      const payload: SubmitConfirmationResponseRequest = {
        confirmedBalanceAmount: agreesWithBalance && details?.bookBalanceAmount != null ? details.bookBalanceAmount : confirmedBalance,
        disputeReason: agreesWithBalance ? 'None' : disputeReason,
        thirdPartyRemarks: remarks,
        thirdPartySignatoryName: signatoryName,
        thirdPartyDesignation: designation
      }

      await auditApi.submitPublicConfirmation(token, payload)
      setSuccess(true)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Submission failed.')
    } finally {
      setSubmitting(false)
    }
  }

  const fmtInr = (val?: number | null) => {
    if (val == null) return '—'
    return '₹' + Number(val).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
  }

  if (loading) {
    return (
      <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'var(--card-subtle)', fontFamily: 'sans-serif' }}>
        <div style={{ textAlign: 'center', padding: '40px' }}>
          <div style={{ fontSize: '32px', marginBottom: '12px' }}>🔒</div>
          <h3 style={{ margin: 0, color: '#1e3a8a' }}>Connecting to Secure Audit Confirmation Vault…</h3>
          <p style={{ color: 'var(--muted)', fontSize: '14px' }}>Verifying encrypted circularization token under ICAI SA 505</p>
        </div>
      </div>
    )
  }

  return (
    <div style={{ minHeight: '100vh', background: 'var(--card-subtle)', padding: '40px 20px', fontFamily: "'Segoe UI', Tahoma, Geneva, Verdana, sans-serif" }}>
      <div style={{ maxWidth: '750px', margin: '0 auto', background: 'var(--card)', borderRadius: '12px', boxShadow: '0 10px 25px -5px rgba(0,0,0,0.1)', overflow: 'hidden', border: '1px solid var(--border)' }}>
        {/* Header */}
        <div style={{ background: 'linear-gradient(135deg, #1e3a8a 0%, #1e40af 100%)', color: '#fff', padding: '28px 32px' }}>
          <div style={{ fontSize: '11px', textTransform: 'uppercase', letterSpacing: '0.08em', color: '#93c5fd', fontWeight: 700 }}>
            Standard on Auditing (SA) 505 • Direct External Confirmation
          </div>
          <h1 style={{ margin: '6px 0 0 0', fontSize: '24px', fontWeight: 800 }}>
            Official Statutory Balance Confirmation
          </h1>
          <p style={{ margin: '6px 0 0 0', fontSize: '13px', color: '#bfdbfe' }}>
            Issued by independent auditors <strong>{details?.auditorFirmName}</strong> on behalf of <strong>{details?.clientName}</strong>
          </p>
        </div>

        {/* Content Body */}
        <div style={{ padding: '32px' }}>
          {error && (
            <div style={{ padding: '14px 18px', background: 'var(--error-bg)', color: 'var(--error)', borderRadius: '8px', marginBottom: '20px', fontSize: '14px' }}>
              ⚠️ {error}
            </div>
          )}

          {success ? (
            <div style={{ textAlign: 'center', padding: '40px 20px' }}>
              <div style={{ width: '64px', height: '64px', borderRadius: '50%', background: 'var(--success-bg)', color: '#16a34a', fontSize: '32px', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 16px' }}>
                ✓
              </div>
              <h2 style={{ color: 'var(--success-text)', margin: '0 0 8px 0', fontSize: '22px', fontWeight: 700 }}>
                Balance Confirmation Successfully Registered!
              </h2>
              <p style={{ color: 'var(--text-secondary)', fontSize: '14px', maxWidth: '520px', margin: '0 auto 24px' }}>
                Thank you for verifying your balances with <strong>{details?.clientName}</strong>. Your direct response has been cryptographically recorded in the statutory audit file of <strong>{details?.auditorFirmName}</strong> in compliance with ICAI SA 505.
              </p>
              <div style={{ padding: '12px', background: 'var(--card-subtle)', borderRadius: '6px', fontSize: '12px', color: 'var(--muted)', display: 'inline-block' }}>
                Verification Ref: <code>{token.substring(0, 16)}...</code> | Timestamp: {new Date().toLocaleString('en-IN')}
              </div>
            </div>
          ) : details?.alreadyResponded ? (
            <div style={{ textAlign: 'center', padding: '30px' }}>
              <div style={{ fontSize: '36px', marginBottom: '10px' }}>✅</div>
              <h3 style={{ margin: '0 0 8px 0', color: 'var(--success)' }}>Response Already Submitted</h3>
              <p style={{ color: 'var(--muted)', fontSize: '14px' }}>
                A confirmation response has already been submitted for this account ({details.partyName}).
                Current Audit Status: <strong>{details.currentStatus}</strong>.
              </p>
            </div>
          ) : details?.isExpired ? (
            <div style={{ textAlign: 'center', padding: '30px', color: 'var(--error)' }}>
              <div style={{ fontSize: '36px', marginBottom: '10px' }}>⏱️</div>
              <h3 style={{ margin: '0 0 8px 0' }}>Confirmation Link Expired</h3>
              <p style={{ color: 'var(--muted)', fontSize: '14px' }}>
                This confirmation request link has passed its validity window. Please reach out to the auditors ({details?.auditorFirmName}).
              </p>
            </div>
          ) : (
            <form onSubmit={handleSubmit}>
              <div style={{ background: 'var(--card-subtle)', border: '1px solid var(--border)', borderRadius: '8px', padding: '20px', marginBottom: '24px' }}>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px' }}>
                  <div>
                    <span style={{ fontSize: '11px', color: 'var(--muted)', textTransform: 'uppercase', fontWeight: 600 }}>Confirming Entity</span>
                    <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--text)', marginTop: '2px' }}>{details?.partyName}</div>
                    <span style={{ fontSize: '12px', color: 'var(--muted)' }}>Category: {details?.partyType}</span>
                  </div>
                  <div>
                    <span style={{ fontSize: '11px', color: 'var(--muted)', textTransform: 'uppercase', fontWeight: 600 }}>Audit As Of Date</span>
                    <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--text)', marginTop: '2px' }}>{details?.asOfDate}</div>
                    <span style={{ fontSize: '12px', color: 'var(--muted)' }}>Financial Year-End Balance</span>
                  </div>
                  <div>
                    <span style={{ fontSize: '11px', color: 'var(--muted)', textTransform: 'uppercase', fontWeight: 600 }}>Client Book Balance</span>
                    <div style={{ fontSize: '18px', fontWeight: 800, color: '#1e3a8a', marginTop: '2px' }}>
                      {details?.bookBalanceAmount != null ? fmtInr(details.bookBalanceAmount) : '[Positive Blank Balance Requested]'}
                    </div>
                    <span style={{ fontSize: '11px', color: 'var(--muted)' }}>As per {details?.clientName} records</span>
                  </div>
                </div>
              </div>

              {/* Agreement Choice */}
              <div style={{ marginBottom: '20px' }}>
                <label style={{ display: 'block', fontSize: '14px', fontWeight: 700, marginBottom: '10px' }}>
                  Does your organization agree with the balance stated above as of {details?.asOfDate}?
                </label>
                <div style={{ display: 'flex', gap: '16px' }}>
                  <label style={{
                    flex: 1,
                    display: 'flex',
                    alignItems: 'center',
                    gap: '10px',
                    padding: '14px 18px',
                    borderRadius: '8px',
                    border: agreesWithBalance ? '2px solid #16a34a' : '1px solid var(--border-subtle)',
                    background: agreesWithBalance ? 'var(--success-bg)' : 'var(--card)',
                    cursor: 'pointer'
                  }}>
                    <input
                      type="radio"
                      name="agreeStatus"
                      checked={agreesWithBalance}
                      onChange={() => setAgreesWithBalance(true)}
                    />
                    <div>
                      <strong style={{ color: 'var(--success)' }}>Yes, We Agree</strong>
                      <div style={{ fontSize: '12px', color: 'var(--muted)' }}>Balance matches our books without difference</div>
                    </div>
                  </label>

                  <label style={{
                    flex: 1,
                    display: 'flex',
                    alignItems: 'center',
                    gap: '10px',
                    padding: '14px 18px',
                    borderRadius: '8px',
                    border: !agreesWithBalance ? '2px solid #dc2626' : '1px solid var(--border-subtle)',
                    background: !agreesWithBalance ? 'var(--error-bg)' : 'var(--card)',
                    cursor: 'pointer'
                  }}>
                    <input
                      type="radio"
                      name="agreeStatus"
                      checked={!agreesWithBalance}
                      onChange={() => setAgreesWithBalance(false)}
                    />
                    <div>
                      <strong style={{ color: 'var(--error)' }}>No, We Disagree / Difference Exists</strong>
                      <div style={{ fontSize: '12px', color: 'var(--muted)' }}>State your ledger balance and discrepancy reasons</div>
                    </div>
                  </label>
                </div>
              </div>

              {/* Dispute & Alternate Balance Details if Disagreed */}
              {!agreesWithBalance && (
                <div style={{ padding: '18px', background: 'var(--error-bg)', border: '1px solid var(--error-border)', borderRadius: '8px', marginBottom: '20px' }}>
                  <h4 style={{ margin: '0 0 12px 0', color: 'var(--error-text)', fontSize: '15px' }}>
                    Discrepancy &amp; Reconciliation Details
                  </h4>
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '12px' }}>
                    <div>
                      <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>
                        Balance as per YOUR Records (₹) *
                      </label>
                      <input
                        type="number"
                        step="0.01"
                        value={confirmedBalance}
                        onChange={e => setConfirmedBalance(parseFloat(e.target.value) || 0)}
                        required
                        style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '14px' }}
                      />
                    </div>
                    <div>
                      <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>
                        Primary Difference Reason *
                      </label>
                      <select
                        value={disputeReason}
                        onChange={e => setDisputeReason(e.target.value)}
                        required
                        style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '14px' }}
                      >
                        <option value="GoodsInTransit">Goods in Transit (Billed but not received)</option>
                        <option value="ChequeInTransit">Cheque / Payment in Transit (Pending clearance)</option>
                        <option value="RateDiscountDispute">Rate, Quality, or Scheme Discount Dispute</option>
                        <option value="TdsMismatch">TDS / Form 26AS Deductions Mismatch</option>
                        <option value="UnadjustedDebitCreditNote">Unadjusted Debit Note / Credit Note</option>
                        <option value="OtherVariance">Other Accounting Timing Difference</option>
                      </select>
                    </div>
                  </div>

                  {details?.bookBalanceAmount != null && (
                    <div style={{ fontSize: '13px', color: '#7f1d1d', fontWeight: 600 }}>
                      Calculated Difference / Variance: {fmtInr(details.bookBalanceAmount - confirmedBalance)}
                    </div>
                  )}
                </div>
              )}

              {/* Remarks */}
              <div style={{ marginBottom: '18px' }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>
                  Remarks / Reconciliation Notes (Optional)
                </label>
                <textarea
                  rows={3}
                  value={remarks}
                  onChange={e => setRemarks(e.target.value)}
                  placeholder="e.g. Cheque of ₹50,000 issued on 30-Mar cleared on 03-Apr; or Invoice #402 had 5% trade discount unapplied."
                  style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '13px' }}
                />
              </div>

              {/* Signatory Info */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '24px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>
                    Signatory Full Name *
                  </label>
                  <input
                    type="text"
                    value={signatoryName}
                    onChange={e => setSignatoryName(e.target.value)}
                    required
                    placeholder="e.g. Rajesh Kumar"
                    style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '14px' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>
                    Designation / Title *
                  </label>
                  <input
                    type="text"
                    value={designation}
                    onChange={e => setDesignation(e.target.value)}
                    required
                    placeholder="e.g. Chief Financial Officer / Accounts Head"
                    style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '14px' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                <button
                  type="submit"
                  disabled={submitting}
                  style={{
                    padding: '12px 28px',
                    borderRadius: '8px',
                    border: 'none',
                    background: '#1e3a8a',
                    color: '#fff',
                    fontWeight: 700,
                    fontSize: '15px',
                    cursor: submitting ? 'not-allowed' : 'pointer'
                  }}
                >
                  {submitting ? 'Submitting to Auditor...' : '✓ Submit Official Balance Confirmation'}
                </button>
              </div>
            </form>
          )}
        </div>
      </div>
    </div>
  )
}

export default PublicConfirmationPortal
