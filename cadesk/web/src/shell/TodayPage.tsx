import { useEffect, useState } from 'react'
import { addDaysIso, api, fmtDate, todayIso, type BillingSummary, type DeadlinePage, type TaskPage } from '../api'
import type { NavItem, Page } from './nav'

type Props = {
  name: string
  can: (perm: string) => boolean
  quickDesks: NavItem[]
  onOpen: (page: Page) => void
}

const inr = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 })

function greeting(now = new Date()) {
  const h = now.getHours()
  return h < 12 ? 'Good morning' : h < 17 ? 'Good afternoon' : 'Good evening'
}

function daysFrom(today: string, iso: string) {
  return Math.round((Date.parse(iso) - Date.parse(today)) / 86_400_000)
}

function whenLabel(today: string, iso: string) {
  const d = daysFrom(today, iso)
  if (d < 0) return { text: `${-d}d late`, late: true }
  if (d === 0) return { text: 'Today', late: false }
  if (d === 1) return { text: 'Tomorrow', late: false }
  return { text: fmtDate(iso).replace(/ \d{4}$/, ''), late: false }
}

function clientInitials(label: string) {
  const parts = label.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return 'CL'
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase()
  return (parts[0][0] + parts[1][0]).toUpperCase()
}

/* The first screen after sign-in: compliance progress, what is late, what is due, what is mine. */
export function TodayPage({ name, can, quickDesks, onOpen }: Props) {
  const today = todayIso()
  const [deadlines, setDeadlines] = useState<DeadlinePage | null>(null)
  const [tasks, setTasks] = useState<TaskPage | null>(null)
  const [billing, setBilling] = useState<BillingSummary | null>(null)
  const [failed, setFailed] = useState(false)

  const canDeadlines = can('compliance.read')
  const canTasks = can('tasks.read')
  const canBilling = can('billing.read')

  useEffect(() => {
    let live = true
    const q = new URLSearchParams({ status: 'open', to: addDaysIso(today, 14), take: '8' })
    const jobs: Promise<unknown>[] = []
    if (canDeadlines) jobs.push(api<DeadlinePage>(`/api/deadlines?${q}`).then(r => { if (live) setDeadlines(r) }))
    if (canTasks) jobs.push(api<TaskPage>('/api/tasks?assignee=me&status=active&take=8').then(r => { if (live) setTasks(r) }))
    if (canBilling) jobs.push(api<BillingSummary>('/api/invoices/summary').then(r => { if (live) setBilling(r) }))
    Promise.allSettled(jobs).then(results => {
      if (live && results.some(r => r.status === 'rejected')) setFailed(true)
    })
    return () => { live = false }
  }, [today, canDeadlines, canTasks, canBilling])

  const s = deadlines?.summary
  const first = name.split(' ')[0] || name
  const dateLine = new Date().toLocaleDateString('en-IN', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })

  const filedCount = s?.filedThisMonth ?? 0
  const openCount = s?.open ?? 0
  const cycleTotal = filedCount + openCount
  const completionPct = cycleTotal > 0 ? Math.round((filedCount / cycleTotal) * 100) : 0
  const dueSoonPct = cycleTotal > 0 && s ? Math.round((s.dueThisWeek / cycleTotal) * 100) : 0

  return (
    <>
      <div className="today-head">
        <div>
          <h1>{greeting()}, {first}</h1>
          <p>{dateLine} &middot; Practice Command Center</p>
        </div>
        <div className="row" style={{ gap: 10 }}>
          {canDeadlines && (
            <button className="ghost small" onClick={() => onOpen('deadlines')}>All deadlines</button>
          )}
          {can('gst.read') && (
            <button className="primary small" onClick={() => onOpen('gstr1-export')}>Open GSTR-1 &amp; 3B</button>
          )}
          {canTasks && (
            <button className="ghost small" onClick={() => onOpen('tasks')}>Open task board</button>
          )}
        </div>
      </div>

      {failed && <p className="notice small">Some figures could not be loaded. Refresh the page to try again.</p>}

      <div className="today-kpi-grid" role="group" aria-label="Today's figures">
        {canDeadlines && (
          <button className="today-kpi kpi-red" onClick={() => onOpen('deadlines')}>
            <div className="today-kpi-top">
              <span>Filings overdue</span>
              <span className="today-kpi-icon" aria-hidden="true">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <circle cx="12" cy="12" r="10" /><line x1="12" y1="8" x2="12" y2="12" /><line x1="12" y1="16" x2="12.01" y2="16" />
                </svg>
              </span>
            </div>
            <div className={`today-kpi-val ${s && s.overdue > 0 ? 'is-bad' : ''}`}>{s ? s.overdue : '–'}</div>
            <div className="today-kpi-sub">{s && s.overdue > 0 ? 'Requires immediate filing attention' : 'All filings up to date'}</div>
          </button>
        )}

        {canDeadlines && (
          <button className="today-kpi kpi-amber" onClick={() => onOpen('deadlines')}>
            <div className="today-kpi-top">
              <span>Due in next 7 days</span>
              <span className="today-kpi-icon" aria-hidden="true">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <circle cx="12" cy="12" r="10" /><polyline points="12 6 12 12 16 14" />
                </svg>
              </span>
            </div>
            <div className="today-kpi-val">{s ? s.dueThisWeek : '–'}</div>
            <div className="today-kpi-sub">
              {cycleTotal > 0 ? `${completionPct}% filed this month (${filedCount}/${cycleTotal})` : 'Statutory queue this week'}
            </div>
            {cycleTotal > 0 && (
              <div className="today-progress-track" aria-hidden="true">
                <div className="today-progress-fill-green" style={{ width: `${completionPct}%` }} />
                <div className="today-progress-fill-amber" style={{ width: `${Math.min(100 - completionPct, dueSoonPct)}%` }} />
              </div>
            )}
          </button>
        )}

        {canTasks && (
          <button className="today-kpi kpi-blue" onClick={() => onOpen('tasks')}>
            <div className="today-kpi-top">
              <span>Your open tasks</span>
              <span className="today-kpi-icon" aria-hidden="true">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="m9 11 3 3L22 4" /><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" />
                </svg>
              </span>
            </div>
            <div className="today-kpi-val">{tasks ? tasks.total : '–'}</div>
            <div className="today-kpi-sub">Assigned to you across active clients</div>
          </button>
        )}

        {canBilling ? (
          <button className="today-kpi kpi-green" onClick={() => onOpen('billing')}>
            <div className="today-kpi-top">
              <span>Fees outstanding</span>
              <span className="today-kpi-icon" aria-hidden="true">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M12 2v20M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
                </svg>
              </span>
            </div>
            <div className="today-kpi-val">{billing ? inr.format(billing.totalOutstanding) : '–'}</div>
            <div className="today-kpi-sub">
              {billing
                ? billing.totalOverdue > 0
                  ? `${inr.format(billing.totalOverdue)} overdue · ${inr.format(billing.collectedThisMonth)} collected this mo.`
                  : `${inr.format(billing.collectedThisMonth)} collected this month`
                : 'Invoices & Razorpay collections'}
            </div>
          </button>
        ) : canDeadlines && (
          <button className="today-kpi kpi-green" onClick={() => onOpen('deadlines')}>
            <div className="today-kpi-top">
              <span>Filed this month</span>
              <span className="today-kpi-icon" aria-hidden="true">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <polyline points="20 6 9 17 4 12" />
                </svg>
              </span>
            </div>
            <div className="today-kpi-val">{s ? s.filedThisMonth : '–'}</div>
            <div className="today-kpi-sub">Completed statutory returns</div>
          </button>
        )}
      </div>

      <div className="today-cols">
        {canDeadlines && (
          <section className="today-panel" aria-labelledby="today-due">
            <div className="today-panel-head">
              <h2 id="today-due">Coming due in 14 days</h2>
              <button className="link" onClick={() => onOpen('deadlines')}>All deadlines &rarr;</button>
            </div>
            {deadlines && deadlines.items.length === 0 && (
              <div className="today-empty">Nothing due in the next 14 days. Filed work shows under Deadlines.</div>
            )}
            {deadlines && deadlines.items.length > 0 && (
              <ul className="today-rows">
                {deadlines.items.map(d => {
                  const w = whenLabel(today, d.dueDate)
                  return (
                    <li key={d.id} className="today-row">
                      <span className={`tag ${w.late ? 'overdue' : 'info'}`}>{w.text}</span>
                      <span className="what">
                        <b>{d.typeName} &middot; {d.periodLabel}</b>
                        <small>{d.clientName}</small>
                      </span>
                      <span className="who row" style={{ gap: 6, flexWrap: 'nowrap' }}>
                        <span
                          className="inline-flex items-center justify-center w-6 h-6 rounded-md text-xs font-bold"
                          style={{ background: 'var(--accent-subtle)', color: 'var(--accent)' }}
                        >
                          {clientInitials(d.assignedUserName ?? 'UN')}
                        </span>
                        <span>{d.assignedUserName ?? 'Unassigned'}</span>
                      </span>
                    </li>
                  )
                })}
              </ul>
            )}
          </section>
        )}

        <div className="stack" style={{ gap: 20 }}>
          {canTasks && (
            <section className="today-panel" aria-labelledby="today-mine">
              <div className="today-panel-head">
                <h2 id="today-mine">Your tasks</h2>
                <button className="link" onClick={() => onOpen('tasks')}>All tasks &rarr;</button>
              </div>
              {tasks && tasks.items.length === 0 && (
                <div className="today-empty">No open tasks are assigned to you.</div>
              )}
              {tasks && tasks.items.length > 0 && (
                <ul className="today-rows">
                  {tasks.items.map(t => {
                    const w = t.dueDate ? whenLabel(today, t.dueDate) : { text: 'No date', late: false }
                    return (
                      <li key={t.id} className="today-row">
                        <span className={`tag ${t.isOverdue ? 'overdue' : 'pending'}`}>{w.text}</span>
                        <span className="what">
                          <b>{t.title}</b>
                          {t.clientName && <small>{t.clientName}</small>}
                        </span>
                        <span className="who">
                          {t.checklistTotal > 0 ? `${t.checklistDone}/${t.checklistTotal} steps` : ''}
                        </span>
                      </li>
                    )
                  })}
                </ul>
              )}
            </section>
          )}

          {quickDesks.length > 0 && (
            <section className="today-panel" aria-labelledby="today-desks">
              <div className="today-panel-head">
                <h2 id="today-desks">Your pinned &amp; recent desks</h2>
              </div>
              <div className="today-desks">
                {quickDesks.map(d => (
                  <button key={d.id} className="today-desk" onClick={() => onOpen(d.id)}>
                    {d.icon}
                    <span>{d.label}</span>
                  </button>
                ))}
              </div>
            </section>
          )}
        </div>
      </div>
    </>
  )
}
