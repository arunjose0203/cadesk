import { useCallback, useEffect, useState } from 'react'
import { api, ApiError, errorList, fmtDate, fmtSize, type RetentionPage } from './api'

/** Documents kept longer than the retention period, for a person to remove. Nothing is removed automatically. */
export default function RetentionPanel() {
  const [page, setPage] = useState<RetentionPage | null>(null)
  const [hidden, setHidden] = useState(false)
  const [picked, setPicked] = useState<Set<string>>(new Set())
  const [confirm, setConfirm] = useState(false)
  const [msg, setMsg] = useState<string | null>(null)
  const [errors, setErrors] = useState<string[] | null>(null)

  const load = useCallback(async () => {
    try { setPage(await api<RetentionPage>('/api/documents/retention?take=500')) }
    catch (e) { if (e instanceof ApiError && e.status === 403) setHidden(true) }
  }, [])
  useEffect(() => { load() }, [load])

  const toggle = (id: string) => setPicked(p => { const n = new Set(p); if (n.has(id)) n.delete(id); else n.add(id); return n })
  const purge = async () => {
    setErrors(null); setMsg(null)
    try {
      const r = await api<{ removed: number }>('/api/documents/retention/purge', { method: 'POST', body: JSON.stringify({ ids: [...picked] }) })
      setMsg(`Removed ${r.removed} file${r.removed === 1 ? '' : 's'}. Their records stay in the vault and the audit log.`)
      setPicked(new Set()); setConfirm(false); await load()
    } catch (e) { setErrors(errorList(e)) }
  }
  const scanNow = async () => {
    setErrors(null); setMsg(null)
    try {
      const r = await api<{ scanned: number; infected: number; trashPurged: number }>('/api/documents/maintenance', { method: 'POST' })
      setMsg(`Scanned ${r.scanned} waiting file(s)${r.infected ? `, ${r.infected} with a virus (removed)` : ''}; emptied ${r.trashPurged} from the trash.`)
    } catch (e) { setErrors(errorList(e)) }
  }

  if (hidden) return null
  return (
    <section className="card">
      <div className="row spread">
        <h2>Past retention</h2>
        <button className="ghost" onClick={scanNow}>Scan waiting files and empty old trash now</button>
      </div>
      {page && <p className="muted small">
        Documents are kept {page.retentionYears} years after the end of their financial year (permanent folders: after the client is deleted).
        Files on legal hold are never listed. Choose what to remove — nothing is removed automatically.
      </p>}
      {msg && <p className="ok small">{msg}</p>}
      {errors && <ul className="error">{errors.map(m => <li key={m}>{m}</li>)}</ul>}
      {page && page.items.length > 0 ? (
        <>
          <div className="row">
            <button className="ghost" onClick={() => setPicked(new Set(page.items.map(i => i.id)))}>Select all {page.items.length}</button>
            {!confirm
              ? <button disabled={picked.size === 0} onClick={() => setConfirm(true)}>Remove {picked.size} file{picked.size === 1 ? '' : 's'}…</button>
              : <><span className="error small">The files can't be recovered.</span>
                  <button onClick={purge}>Yes, remove {picked.size}</button>
                  <button className="ghost" onClick={() => setConfirm(false)}>Cancel</button></>}
          </div>
          <table>
            <thead><tr><th /><th>Client</th><th>Document</th><th>Kept until</th><th>Size</th></tr></thead>
            <tbody>
              {page.items.map(i => (
                <tr key={i.id}>
                  <td><input type="checkbox" checked={picked.has(i.id)} onChange={() => toggle(i.id)} aria-label={`Select ${i.name}`} /></td>
                  <td className="small">{i.clientName} <span className="muted">{i.clientCode}</span>{i.clientDeleted && <div className="muted">client deleted</div>}</td>
                  <td>{i.name}<div className="muted small">{i.folderName}{i.fy && ` · FY ${i.fy}`}</div></td>
                  <td className="small">{fmtDate(i.keepUntil)}</td>
                  <td className="small">{fmtSize(i.sizeBytes)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {page.total > page.items.length && <p className="muted small">Showing {page.items.length} of {page.total}.</p>}
        </>
      ) : page && <p className="muted">No documents are past retention.</p>}
    </section>
  )
}
