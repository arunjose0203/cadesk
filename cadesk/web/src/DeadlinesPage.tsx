import { useCallback, useEffect, useState } from 'react'
import { addDaysIso, api, errorList, fmtDate, todayIso, type ComplianceType, type Deadline, type DeadlinePage, type UserListItem } from './api'

const VIEWS = {
  next30: 'Next 30 days',
  overdue: 'Overdue',
  month: 'This month',
  open: 'All open',
  filed: 'Filed',
} as const
type View = keyof typeof VIEWS

const STATUS: Record<Deadline['status'], string> = { pending: 'Pending', in_progress: 'In progress', filed: 'Filed', not_applicable: 'Not needed' }

export default function DeadlinesPage({ can }: { can: (p: string) => boolean }) {
  const [view, setView] = useState<View>('next30')
  const [type, setType] = useState('')
  const [assigned, setAssigned] = useState('')
  const [types, setTypes] = useState<ComplianceType[]>([])
  const [users, setUsers] = useState<UserListItem[]>([])
  const [page, setPage] = useState<DeadlinePage | null>(null)
  const [filing, setFiling] = useState<Deadline | null>(null)
  const [msg, setMsg] = useState<string[] | null>(null)
  const manage = can('compliance.manage')
  const canSeeUsers = can('users.read')

  useEffect(() => {
    api<ComplianceType[]>('/api/compliance-types').then(setTypes).catch(() => {})
    if (canSeeUsers) api<UserListItem[]>('/api/users').then(setUsers).catch(() => {})
  }, [canSeeUsers])

  const load = useCallback(async () => {
    const today = todayIso()
    const q = new URLSearchParams({ take: '500' })
    if (view === 'next30') { q.set('status', 'open'); q.set('to', addDaysIso(today, 30)) }
    if (view === 'overdue') q.set('status', 'overdue')
    if (view === 'month') {
      const first = today.slice(0, 8) + '01'
      q.set('from', first)
      q.set('to', addDaysIso(addDaysIso(first, 32).slice(0, 8) + '01', -1))
    }
    if (view === 'open') q.set('status', 'open')
    if (view === 'filed') q.set('status', 'filed')
    if (type) q.set('type', type)
    if (assigned) q.set('assignedUserId', assigned)
    setPage(await api<DeadlinePage>(`/api/deadlines?${q}`))
  }, [view, type, assigned])
  useEffect(() => { load().catch(() => {}) }, [load])

  const save = async (d: Deadline, patch: Partial<Deadline>) => {
    setMsg(null)
    try {
      const next = { ...d, ...patch }
      await api(`/api/deadlines/${d.id}`, {
        method: 'PUT',
        body: JSON.stringify({ status: next.status, filedOn: next.filedOn || null, ackNo: next.ackNo || null,
          remarks: next.remarks || null, assignedUserId: next.assignedUserId || null, version: d.version }),
      })
      setFiling(null)
      await load()
    } catch (e) { setMsg(errorList(e)) }
  }

  const regenerate = async () => {
    const r = await api<{ created: number; updated: number; removed: number }>('/api/deadlines/generate', { method: 'POST' })
    setMsg([`Deadlines refreshed: ${r.created} new, ${r.updated} changed, ${r.removed} removed.`])
    load()
  }

  const s = page?.summary
  return (
    <>
      <section className="tiles">
        <button className={`tile ${view === 'overdue' ? 'active' : ''}`} onClick={() => setView('overdue')}>
          <b className={s?.overdue ? 'error' : ''}>{s?.overdue ?? '–'}</b><span>Overdue</span></button>
        <button className={`tile ${view === 'next30' ? 'active' : ''}`} onClick={() => setView('next30')}>
          <b>{s?.dueThisWeek ?? '–'}</b><span>Due in 7 days</span></button>
        <button className={`tile ${view === 'open' ? 'active' : ''}`} onClick={() => setView('open')}>
          <b>{s?.open ?? '–'}</b><span>Open</span></button>
        <button className={`tile ${view === 'filed' ? 'active' : ''}`} onClick={() => setView('filed')}>
          <b className="ok">{s?.filedThisMonth ?? '–'}</b><span>Filed this month</span></button>
      </section>

      <section className="card">
        <div className="row spread">
          <h2>Deadlines <span className="muted small">{page?.total ?? ''}</span></h2>
          {manage && <button className="ghost" onClick={regenerate}>Refresh deadlines</button>}
        </div>
        <div className="grid">
          <select value={view} onChange={e => setView(e.target.value as View)}>
            {Object.entries(VIEWS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
          <select value={type} onChange={e => setType(e.target.value)}>
            <option value="">All compliances</option>
            {types.map(t => <option key={t.code} value={t.code}>{t.name}</option>)}
          </select>
          {users.length > 0 && (
            <select value={assigned} onChange={e => setAssigned(e.target.value)}>
              <option value="">Everyone</option>
              {users.map(u => <option key={u.id} value={u.id}>{u.name}</option>)}
            </select>
          )}
        </div>
        {msg && <ul className="warn small">{msg.map(m => <li key={m}>{m}</li>)}</ul>}
        {filing && (
          <div className="notice">
            <h2>Mark filed: {filing.typeName} · {filing.periodLabel} · {filing.clientName}</h2>
            <div className="grid">
              <label className="field">Filed on
                <input type="date" max={todayIso()} value={filing.filedOn ?? ''} onChange={e => setFiling({ ...filing, filedOn: e.target.value })} /></label>
              <label className="field">Acknowledgement / ARN
                <input value={filing.ackNo ?? ''} onChange={e => setFiling({ ...filing, ackNo: e.target.value })} /></label>
              <label className="field">Remarks
                <input value={filing.remarks ?? ''} onChange={e => setFiling({ ...filing, remarks: e.target.value })} /></label>
            </div>
            <div className="row" style={{ marginTop: 8 }}>
              <button onClick={() => save(page!.items.find(x => x.id === filing.id)!, filing)}>Save</button>
              <button className="ghost" onClick={() => setFiling(null)}>Cancel</button>
              <button className="ghost" onClick={() => save(page!.items.find(x => x.id === filing.id)!, { status: 'not_applicable', filedOn: null })}>Not needed this time</button>
            {can('tasks.manage') && <button className="ghost" onClick={async () => {
              try { await api(`/api/deadlines/${filing.id}/task`, { method: 'POST' }); setFiling(null); setMsg(['Task ready — find it under Tasks to work through the checklist and submit for review.']) }
              catch (e) { setMsg(errorList(e)) }
            }}>Work on it as a task</button>}
            </div>
          </div>
        )}
        <table>
          <thead><tr><th>Due</th><th>Client</th><th>Compliance</th><th>Period</th><th>Assigned</th><th>Status</th><th /></tr></thead>
          <tbody>
            {page?.items.map(d => (
              <tr key={d.id} className={d.isOverdue ? 'overdue' : ''}>
                <td className="nowrap">
                  <b>{fmtDate(d.dueDate)}</b>
                  {d.dueDate !== d.statutoryDue && <div className="muted small" title="Moved off a weekend / public holiday">law: {fmtDate(d.statutoryDue)}</div>}
                  {d.extended && <div className="tag create small">extended</div>}
                  {d.status !== 'filed' && <div className="muted small">aim: {fmtDate(d.internalDue)}</div>}
                </td>
                <td>{d.clientName}<div className="muted small">{d.clientCode}</div></td>
                <td>{d.typeName}</td>
                <td className="nowrap">{d.periodLabel}</td>
                <td>{d.assignedUserName ?? ''}</td>
                <td>
                  <span className={`tag ${d.status === 'filed' ? 'create' : d.isOverdue ? 'delete' : ''}`}>{d.isOverdue ? 'Overdue' : STATUS[d.status]}</span>
                  {d.status === 'filed' && <div className="muted small">{fmtDate(d.filedOn)}{d.ackNo ? ` · ${d.ackNo}` : ''}</div>}
                </td>
                <td className="nowrap">{manage && (d.status === 'filed' || d.status === 'not_applicable'
                  ? <button className="ghost" onClick={() => save(d, { status: 'pending', filedOn: null })}>Reopen</button>
                  : <>
                    {d.status === 'pending' && <button className="ghost" onClick={() => save(d, { status: 'in_progress' })}>Start</button>}
                    <button onClick={() => setFiling({ ...d, filedOn: todayIso(), status: 'filed' })}>Mark filed</button>
                  </>)}
                </td>
              </tr>
            ))}
            {page && page.items.length === 0 && <tr><td colSpan={7} className="muted">Nothing here.</td></tr>}
          </tbody>
        </table>
      </section>

    </>
  )
}
