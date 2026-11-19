import { useCallback, useEffect, useState } from 'react'
import { api, errorList, fmtDate, TASK_STATUS, type Client, type ClientPage, type TaskItem, type TaskPage, type UserListItem, type WorkloadRow } from './api'
import TaskPanel from './TaskPanel'

type Tab = 'mine' | 'review' | 'all' | 'workload'

export default function TasksPage({ can, meId }: { can: (p: string) => boolean; meId: string }) {
  const [tab, setTab] = useState<Tab>('mine')
  const [status, setStatus] = useState('active')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState<TaskPage | null>(null)
  const [workload, setWorkload] = useState<WorkloadRow[]>([])
  const [users, setUsers] = useState<UserListItem[]>([])
  const [open, setOpen] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)
  const [assignee, setAssignee] = useState('all')
  const canSeeUsers = can('users.read')

  useEffect(() => { if (canSeeUsers) api<UserListItem[]>('/api/users').then(setUsers).catch(() => {}) }, [canSeeUsers])

  const load = useCallback(async () => {
    if (tab === 'workload') { setWorkload(await api<WorkloadRow[]>('/api/tasks/workload')); return }
    const q = new URLSearchParams({ take: '300', status })
    if (tab === 'mine') q.set('assignee', 'me')
    if (tab === 'review') { q.set('toReview', 'true'); q.set('assignee', 'all'); q.set('status', 'in_review') }
    if (tab === 'all') q.set('assignee', assignee)
    if (search.trim()) q.set('search', search.trim())
    setPage(await api<TaskPage>(`/api/tasks?${q}`))
  }, [tab, status, search, assignee])
  useEffect(() => { load().catch(() => {}) }, [load])

  return (
    <>
      <nav className="tabs">
        <button className={tab === 'mine' ? 'active' : ''} onClick={() => setTab('mine')}>My tasks</button>
        {can('tasks.review') && <button className={tab === 'review' ? 'active' : ''} onClick={() => setTab('review')}>To review</button>}
        <button className={tab === 'all' ? 'active' : ''} onClick={() => setTab('all')}>All tasks</button>
        <button className={tab === 'workload' ? 'active' : ''} onClick={() => setTab('workload')}>Workload</button>
      </nav>

      {open && <TaskPanel id={open} can={can} meId={meId} users={users} onClose={() => setOpen(null)} onChanged={() => load().catch(() => {})} />}
      {adding && <NewTask users={users} onDone={id => { setAdding(false); load(); if (id) setOpen(id) }} />}

      {tab === 'workload' ? (
        <section className="card">
          <h2>Workload</h2>
          <table>
            <thead><tr><th>Person</th><th>Open</th><th>Overdue</th><th>Due in 7 days</th><th>Waiting for review</th><th>Can review</th></tr></thead>
            <tbody>
              {workload.map(w => (
                <tr key={w.userId ?? 'none'} className="clickable" onClick={() => { if (w.userId) { setAssignee(w.userId); setTab('all') } }}>
                  <td>{w.name}</td>
                  <td><Bar value={w.open} max={Math.max(...workload.map(x => x.open), 1)} /></td>
                  <td className={w.overdue ? 'error' : 'muted'}>{w.overdue}</td>
                  <td>{w.dueThisWeek}</td><td>{w.waitingReview}</td><td>{w.toReview}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="muted small">Click a person to see their tasks.</p>
        </section>
      ) : (
        <section className="card">
          <div className="row spread">
            <h2>{tab === 'mine' ? 'My tasks' : tab === 'review' ? 'Waiting for your review' : 'All tasks'} <span className="muted small">{page?.total ?? ''}</span></h2>
            {can('tasks.manage') && !adding && <button onClick={() => setAdding(true)}>New task</button>}
          </div>
          {tab !== 'review' && (
            <div className="grid">
              <input placeholder="Search task or client" value={search} onChange={e => setSearch(e.target.value)} />
              <select value={status} onChange={e => setStatus(e.target.value)}>
                <option value="active">Not finished</option><option value="overdue">Overdue</option>
                <option value="in_review">In review</option><option value="changes_requested">Sent back</option>
                <option value="done">Done</option><option value="all">Everything</option>
              </select>
              {tab === 'all' && users.length > 0 && (
                <select value={assignee} onChange={e => setAssignee(e.target.value)}>
                  <option value="all">Everyone</option><option value="unassigned">Unassigned</option>
                  {users.map(u => <option key={u.id} value={u.id}>{u.name}</option>)}
                </select>
              )}
            </div>
          )}
          <table>
            <thead><tr><th>Aim</th><th>Task</th><th>Client</th>{tab !== 'mine' && <th>Assigned</th>}<th>Steps</th><th>Status</th></tr></thead>
            <tbody>
              {page?.items.map((t: TaskItem) => (
                <tr key={t.id} className={`clickable ${t.isOverdue ? 'overdue' : ''}`} onClick={() => setOpen(t.id)}>
                  <td className="nowrap">{t.dueDate ? fmtDate(t.dueDate) : '—'}</td>
                  <td>{t.title} {t.priority === 'high' && <span className="tag delete small">high</span>}</td>
                  <td>{t.clientName ?? ''}</td>
                  {tab !== 'mine' && <td>{t.assignedUserName ?? <span className="muted">—</span>}</td>}
                  <td className="nowrap">{t.checklistTotal ? <Bar value={t.checklistDone} max={t.checklistTotal} label={`${t.checklistDone}/${t.checklistTotal}`} /> : ''}</td>
                  <td className="nowrap"><span className={`tag ${t.status === 'done' ? 'create' : t.status === 'changes_requested' ? 'delete' : ''}`}>
                    {TASK_STATUS[t.status]}</span>{t.isOverdue && <span className="error small"> overdue</span>}</td>
                </tr>
              ))}
              {page && page.items.length === 0 && <tr><td colSpan={6} className="muted">Nothing here.</td></tr>}
            </tbody>
          </table>
        </section>
      )}
    </>
  )
}

function Bar({ value, max, label }: { value: number; max: number; label?: string }) {
  return (
    <span className="bar" title={label ?? String(value)}>
      <span className="bar-fill" style={{ width: `${Math.round((value / Math.max(max, 1)) * 100)}%` }} />
      <span className="bar-label">{label ?? value}</span>
    </span>
  )
}

function NewTask({ users, onDone }: { users: UserListItem[]; onDone: (id?: string) => void }) {
  const [f, setF] = useState({ title: '', description: '', clientId: '', dueDate: '', priority: 'normal', assignedUserId: '', reviewRequired: false, steps: '' })
  const [clients, setClients] = useState<Client[]>([])
  const [errors, setErrors] = useState<string[] | null>(null)
  useEffect(() => { api<ClientPage>('/api/clients?take=200').then(p => setClients(p.items)).catch(() => {}) }, [])

  const save = async () => {
    setErrors(null)
    try {
      const t = await api<{ task: { id: string } }>('/api/tasks', {
        method: 'POST',
        body: JSON.stringify({
          title: f.title, description: f.description || null, clientId: f.clientId || null, dueDate: f.dueDate || null,
          priority: f.priority, assignedUserId: f.assignedUserId || null, reviewerUserId: null, reviewRequired: f.reviewRequired,
          checklist: f.steps.split('\n').map(s => s.trim()).filter(Boolean),
        }),
      })
      onDone(t.task.id)
    } catch (e) { setErrors(errorList(e)) }
  }

  return (
    <section className="card">
      <h2>New task</h2>
      <div className="grid">
        <label className="field">Title<input value={f.title} onChange={e => setF({ ...f, title: e.target.value })} placeholder="e.g. Reply to GST notice" /></label>
        <label className="field">Client
          <select value={f.clientId} onChange={e => setF({ ...f, clientId: e.target.value })}>
            <option value="">— none —</option>{clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select></label>
        <label className="field">Aim to finish by<input type="date" value={f.dueDate} onChange={e => setF({ ...f, dueDate: e.target.value })} /></label>
        <label className="field">Priority
          <select value={f.priority} onChange={e => setF({ ...f, priority: e.target.value })}><option>low</option><option>normal</option><option>high</option></select></label>
        {users.length > 0 && <label className="field">Assigned to
          <select value={f.assignedUserId} onChange={e => setF({ ...f, assignedUserId: e.target.value })}>
            <option value="">— nobody —</option>{users.filter(u => u.isActive).map(u => <option key={u.id} value={u.id}>{u.name}</option>)}
          </select></label>}
      </div>
      <label className="field" style={{ marginTop: 8 }}>Details<input value={f.description} onChange={e => setF({ ...f, description: e.target.value })} /></label>
      <label className="field" style={{ marginTop: 8 }}>Checklist (one step per line)
        <textarea rows={4} value={f.steps} onChange={e => setF({ ...f, steps: e.target.value })} /></label>
      <label className="row small" style={{ marginTop: 8 }}><input type="checkbox" checked={f.reviewRequired} onChange={e => setF({ ...f, reviewRequired: e.target.checked })} /> Needs review before it's done</label>
      <div className="row" style={{ marginTop: 8 }}>
        <button onClick={save} disabled={!f.title.trim()}>Create task</button>
        <button className="ghost" onClick={() => onDone()}>Cancel</button>
      </div>
      {errors && <ul className="error">{errors.map(m => <li key={m}>{m}</li>)}</ul>}
    </section>
  )
}
