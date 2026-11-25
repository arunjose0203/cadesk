import { useState } from 'react'
import { api } from '../api'
import { appVersion, currentPage, recentErrors } from './monitor'

/** "Report a problem": a message to the CaDesk team, with the page and recent errors attached (Sprint 72). */
export function ReportProblem({ deskName, onClose }: { deskName: string; onClose: () => void }) {
  const [message, setMessage] = useState('')
  const [includeDetails, setIncludeDetails] = useState(true)
  const [busy, setBusy] = useState(false)
  const [done, setDone] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const errors = recentErrors()

  async function send() {
    setBusy(true); setError(null)
    try {
      const detail = includeDetails
        ? [`Desk: ${deskName}`, `Screen: ${window.innerWidth}×${window.innerHeight}`, ...errors.map(e => `${e.at} [${e.page}] ${e.message}`)].join('\n')
        : undefined
      const res = await api<{ reference: string }>('/api/problem-reports', {
        method: 'POST',
        body: JSON.stringify({ kind: 'feedback', message: message.trim(), page: currentPage(), detail, appVersion: appVersion() }),
      })
      setDone(res.reference)
    } catch {
      setError('The report could not be sent. Check your connection and try again.')
    } finally { setBusy(false) }
  }

  return (
    <div className="legal-gate" role="dialog" aria-modal="true" aria-labelledby="report-title" onKeyDown={e => { if (e.key === 'Escape') onClose() }}>
      <div className="legal-gate-card">
        {done ? (
          <>
            <h2 id="report-title">Thanks, we have it</h2>
            <p>Your report is <strong>{done}</strong>. Quote it if you write to us or call about this.</p>
            <div className="legal-gate-actions"><span /><button type="button" onClick={onClose} autoFocus>Close</button></div>
          </>
        ) : (
          <>
            <h2 id="report-title">Report a problem</h2>
            <p className="small muted">Tell us what you were doing and what went wrong. Please don't include clients' PAN, Aadhaar or bank details.</p>
            <label className="field" style={{ display: 'block' }}>
              <span className="sr-only">What went wrong?</span>
              <textarea className="report-textarea" rows={5} value={message} onChange={e => setMessage(e.target.value)} maxLength={2000}
                placeholder="e.g. On GST returns, the GSTR-1 file for Kochi Spices was rejected by the offline tool" autoFocus />
            </label>
            <label className="legal-gate-check" style={{ marginTop: 8 }}>
              <input type="checkbox" checked={includeDetails} onChange={e => setIncludeDetails(e.target.checked)} />
              <span>Attach technical details: this desk ({deskName}){errors.length > 0 ? ` and the last ${errors.length} error${errors.length === 1 ? '' : 's'}` : ''}</span>
            </label>
            {error && <p className="error small" role="alert">{error}</p>}
            <div className="legal-gate-actions">
              <button type="button" className="ghost small" onClick={onClose}>Cancel</button>
              <button type="button" disabled={busy || message.trim().length < 5} onClick={send}>{busy ? 'Sending…' : 'Send report'}</button>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
