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

/* The first screen after sign-in: what is late, what is due, what is mine. */
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

  return (
    <>
      <div className="today-head">
        <div>
          <h1>{greeting()}, {first}</h1>
          <p>{dateLine}</p>
        </div>
        {canTasks && <button className="ghost small" onClick={() => onOpen('tasks')}>Open task board</button>}
      </div>

      {failed && <p className="notice small">Some figures could not be loaded. Refresh the page to try again.</p>}

      <div className="today-ledger" role="group" aria-label="Today's figures">
        {canDeadlines && (
          <button className={`today-figure ${s && s.overdue > 0 ? 'is-bad' : ''}`} onClick={() => onOpen('deadlines')}>
            <span className="fig">{s ? s.overdue : '–'}</span>
            <span className="lbl">Filings overdue</span>
          </button>
        )}
        {canDeadlines && (
          <button className="today-figure" onClick={() => onOpen('deadlines')}>
            <span className="fig">{s ? s.dueThisWeek : '–'}</span>
            <span className="lbl">Due in the next 7 days</span>
          </button>
        )}
        {canTasks && (
          <button className="today-figure" onClick={() => onOpen('tasks')}>
            <span className="fig">{tasks ? tasks.total : '–'}</span>
            <span className="lbl">Open tasks assigned to you</span>
          </button>
        )}
        {canBilling ? (
          <button className={`today-figure ${billing && billing.totalOverdue > 0 ? 'is-bad' : ''}`} onClick={() => onOpen('billing')}>
            <span className="fig">{billing ? inr.format(billing.totalOutstanding) : '–'}</span>
            <span className="lbl">
              Fees outstanding{billing && billing.totalOverdue > 0 ? `, ${inr.format(billing.totalOverdue)} overdue` : ''}
            </span>
          </button>
        ) : canDeadlines && (
          <button className="today-figure is-good" onClick={() => onOpen('deadlines')}>
            <span className="fig">{s ? s.filedThisMonth : '–'}</span>
            <span className="lbl">Filed this month</span>
          </button>
        )}
      </div>

      <div className="today-cols">
        {canDeadlines && (
          <section className="today-panel" aria-labelledby="today-due">
            <div className="today-panel-head">
              <h2 id="today-due">Coming due in 14 days</h2>
              <button className="link" onClick={() => onOpen('deadlines')}>All deadlines</button>
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
                      <span className={`when ${w.late ? 'late' : ''}`}>{w.text}</span>
                      <span className="what">
                        <b>{d.typeName} · {d.periodLabel}</b>
                        <small>{d.clientName}</small>
                      </span>
                      <span className="who">{d.assignedUserName ?? 'Unassigned'}</span>
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
                <button className="link" onClick={() => onOpen('tasks')}>All tasks</button>
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
                        <span className={`when ${t.isOverdue ? 'late' : ''}`}>{w.text}</span>
                        <span className="what">
                          <b>{t.title}</b>
                          {t.clientName && <small>{t.clientName}</small>}
                        </span>
                        <span className="who">
                          {t.checklistTotal > 0 ? `${t.checklistDone}/${t.checklistTotal}` : ''}
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
                <h2 id="today-desks">Your desks</h2>
              </div>
              <div className="today-desks">
                {quickDesks.map(d => (
                  <button key={d.id} className="today-desk" onClick={() => onOpen(d.id)}>
                    {d.icon}
                    {d.label}
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
