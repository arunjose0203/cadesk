import { useCallback, useEffect, useState } from 'react'
import { api, errorList, fmtDate, type ClientCompliance, type Deadline, type DeadlinePage } from './api'

const QUARTERS: Record<number, string> = { 3: 'Mar / Jun / Sep / Dec', 1: 'Jan / Apr / Jul / Oct', 2: 'Feb / May / Aug / Nov' }

/** Which filings apply to this client (rules + the firm's choices) and its next deadlines. */
export default function ClientCompliances({ clientId, can }: { clientId: string; can: (p: string) => boolean }) {
  const [items, setItems] = useState<ClientCompliance[]>([])
  const [upcoming, setUpcoming] = useState<Deadline[]>([])
  const [errors, setErrors] = useState<string[] | null>(null)
  const manage = can('compliance.manage')

  const load = useCallback(async () => {
    const [list, page] = await Promise.all([
      api<ClientCompliance[]>(`/api/clients/${clientId}/compliances`),
      api<DeadlinePage>(`/api/deadlines?clientId=${clientId}&status=open&take=8`),
    ])
    setItems(list); setUpcoming(page.items)
  }, [clientId])
  useEffect(() => { load().catch(() => {}) }, [load])

  const save = async (c: ClientCompliance, patch: Partial<ClientCompliance>) => {
    setErrors(null)
    const n = { ...c, ...patch }
    try {
      await api(`/api/clients/${clientId}/compliances/${c.code}`, {
        method: 'PUT',
        body: JSON.stringify({ isEnabled: n.enabled, frequency: n.frequency, quarterEndMonth: n.quarterEndMonth ?? null, fyStartMonth: n.fyStartMonth ?? null }),
      })
      await load()
    } catch (e) { setErrors(errorList(e)) }
  }
  const reset = async (c: ClientCompliance) => {
    await api(`/api/clients/${clientId}/compliances/${c.code}`, { method: 'DELETE' })
    await load()
  }

  return (
    <section className="card">
      <h2>Compliance</h2>
      <div className="two-col">
        <div>
          <h3 className="small muted">Filings for this client</h3>
          <table>
            <tbody>
              {items.map(c => (
                <tr key={c.code} className={c.enabled ? '' : 'muted'}>
                  <td style={{ width: 28 }}>
                    <input type="checkbox" checked={c.enabled} disabled={!manage} onChange={e => save(c, { enabled: e.target.checked })} />
                  </td>
                  <td>
                    {c.name} <span className="muted small">{c.authority}</span>
                    <div className="muted small">{c.appliesTo}{c.chosen && ' · set for this client'}</div>
                    {c.enabled && manage && c.frequencies.length > 1 && (
                      <select className="small" value={c.frequency} onChange={e => save(c, { frequency: e.target.value })}>
                        {c.frequencies.map(f => <option key={f}>{f}</option>)}
                      </select>
                    )}
                    {c.enabled && manage && c.frequency === 'quarterly' && c.code.endsWith('VAT') && (
                      <select className="small" value={c.quarterEndMonth ?? 3} onChange={e => save(c, { quarterEndMonth: Number(e.target.value) })}>
                        {Object.entries(QUARTERS).map(([k, v]) => <option key={k} value={k}>Quarters end {v}</option>)}
                      </select>
                    )}
                  </td>
                  <td>{c.chosen && manage && <button className="ghost" onClick={() => reset(c)}>Use rule</button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div>
          <h3 className="small muted">Next deadlines</h3>
          <ul className="deadline-list">
            {upcoming.map(d => (
              <li key={d.id} className={d.isOverdue ? 'error' : ''}>
                <b>{fmtDate(d.dueDate)}</b> · {d.typeName} <span className="muted small">{d.periodLabel}</span>
              </li>
            ))}
            {upcoming.length === 0 && <li className="muted">Nothing open.</li>}
          </ul>
        </div>
      </div>
      {errors && <ul className="error">{errors.map(m => <li key={m}>{m}</li>)}</ul>}
    </section>
  )
}
