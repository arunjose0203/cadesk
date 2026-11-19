import { useCallback, useEffect, useState } from 'react'
import { api, errorList, fmtDate, type HolidayItem } from './api'

/** Public holidays (from the platform) and the firm's own closed days, which move internal targets earlier. */
export default function HolidaysEditor() {
  const [year, setYear] = useState(new Date().getFullYear())
  const [items, setItems] = useState<HolidayItem[]>([])
  const [f, setF] = useState({ date: '', name: '' })
  const [errors, setErrors] = useState<string[] | null>(null)

  const load = useCallback(async () => setItems(await api<HolidayItem[]>(`/api/holidays?year=${year}`)), [year])
  useEffect(() => { load().catch(() => {}) }, [load])

  const run = async (fn: () => Promise<unknown>) => {
    setErrors(null)
    try { await fn(); await load() } catch (e) { setErrors(errorList(e)) }
  }

  return (
    <section className="card">
      <div className="row spread">
        <h2>Holidays</h2>
        <div className="row">
          <button className="ghost" onClick={() => setYear(year - 1)}>←</button><b>{year}</b>
          <button className="ghost" onClick={() => setYear(year + 1)}>→</button>
        </div>
      </div>
      <p className="muted small">Public holidays come from the platform and move government due dates where the law allows.
        Add days your office is closed: your internal targets will be set before them.</p>
      <table>
        <tbody>
          {items.map(h => (
            <tr key={h.id}>
              <td className="nowrap">{fmtDate(h.date)}</td><td>{h.name}</td>
              <td>{h.firmOnly ? <span className="tag">your firm</span> : <span className="tag create">public</span>}</td>
              <td>{h.firmOnly && <button className="ghost" onClick={() => run(() => api(`/api/holidays/${h.id}`, { method: 'DELETE' }))}>Remove</button>}</td>
            </tr>
          ))}
          {items.length === 0 && <tr><td colSpan={4} className="muted">No holidays listed for {year}.</td></tr>}
        </tbody>
      </table>
      <div className="row" style={{ marginTop: 12 }}>
        <input type="date" value={f.date} onChange={e => setF({ ...f, date: e.target.value })} />
        <input placeholder="e.g. Onam" value={f.name} onChange={e => setF({ ...f, name: e.target.value })} />
        <button disabled={!f.date || !f.name.trim()} onClick={() => run(async () => {
          await api('/api/holidays', { method: 'POST', body: JSON.stringify(f) }); setF({ date: '', name: '' })
        })}>Add office holiday</button>
      </div>
      {errors && <ul className="error">{errors.map(m => <li key={m}>{m}</li>)}</ul>}
    </section>
  )
}
