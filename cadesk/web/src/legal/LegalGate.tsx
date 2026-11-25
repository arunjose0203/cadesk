import { useEffect, useState } from 'react'
import { legalApi, SIGNUP_ACCEPTED_KEY, type LegalStatus } from './legal'

/**
 * Asks the user to accept the current terms, privacy notice and (for partners) data processing terms
 * before using the app, and again whenever a version changes (Sprint 71, DPDP Act 2023).
 */
export function LegalGate({ onSignOut }: { onSignOut: () => void }) {
  const [status, setStatus] = useState<LegalStatus | null>(null)
  const [agreed, setAgreed] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let live = true
    legalApi.status().then(async s => {
      let fromSignup = false
      try { fromSignup = sessionStorage.getItem(SIGNUP_ACCEPTED_KEY) === '1'; sessionStorage.removeItem(SIGNUP_ACCEPTED_KEY) } catch { /* storage off */ }
      if (fromSignup && s.pending.length > 0) s = await legalApi.accept(s.pending)
      if (live) setStatus(s)
    }).catch(() => { /* the server may predate Sprint 71: don't block the app */ })
    return () => { live = false }
  }, [])

  if (!status || status.pending.length === 0) return null
  const pending = status.documents.filter(d => status.pending.includes(d.document))

  async function accept() {
    setBusy(true); setError(null)
    try { setStatus(await legalApi.accept(status!.pending)) }
    catch { setError('Your acceptance could not be saved. Try again.') }
    finally { setBusy(false) }
  }

  return (
    <div className="legal-gate" role="dialog" aria-modal="true" aria-labelledby="legal-gate-title">
      <div className="legal-gate-card">
        <h2 id="legal-gate-title">{status.documents.some(d => d.accepted) ? 'Our terms have changed' : 'Before you start'}</h2>
        <p>Please read and accept these to keep using CaDesk:</p>
        <ul>
          {pending.map(d => (
            <li key={d.document}>
              <a href={d.url} target="_blank" rel="noopener noreferrer">{d.title}</a>
              <span className="muted small"> version {d.version}</span>
            </li>
          ))}
        </ul>
        {status.pending.includes('dpa') && (
          <p className="small muted">You accept the data processing terms for your firm. Your firm decides why client data is processed; CaDesk processes it only to run the service for you.</p>
        )}
        <label className="legal-gate-check">
          <input type="checkbox" checked={agreed} onChange={e => setAgreed(e.target.checked)} />
          <span>I have read and accept {pending.length === 1 ? 'this document' : 'these documents'}</span>
        </label>
        {error && <p className="error small" role="alert">{error}</p>}
        <div className="legal-gate-actions">
          <button type="button" className="ghost small" onClick={onSignOut}>Sign out</button>
          <button type="button" disabled={!agreed || busy} onClick={accept}>{busy ? 'Saving…' : 'Accept and continue'}</button>
        </div>
      </div>
    </div>
  )
}
