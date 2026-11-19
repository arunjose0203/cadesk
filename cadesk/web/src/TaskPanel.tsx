import { useCallback, useEffect, useState } from 'react'
import { api, errorList, fmtDate, todayIso, TASK_STATUS, type TaskDetail, type UserListItem } from './api'
import TaskDocuments from './TaskDocuments'

type Props = { id: string; can: (p: string) => boolean; meId: string; users: UserListItem[]; onClose: () => void; onChanged: () => void }

/** One task: checklist, filing details, review, comments and history. */
export default function TaskPanel({ id, can, meId, users, onClose, onChanged }: Props) {
  const [t, setT] = useState<TaskDetail | null>(null)
  const [comment, setComment] = useState('')
  const [step, setStep] = useState('')
  const [filing, setFiling] = useState({ filedOn: todayIso(), ackNo: '' })
  const [reviewNote, setReviewNote] = useState('')
  const [errors, setErrors] = useState<string[] | null>(null)
  const manage = can('tasks.manage')
  const review = can('tasks.review')

  const load = useCallback(async () => setT(await api<TaskDetail>(`/api/tasks/${id}`)), [id])
  useEffect(() => { load().catch(() => {}) }, [load])

  const act = async (path: string, body: unknown, method = 'POST') => {
    setErrors(null)
    try {
      setT(await api<TaskDetail>(`/api/tasks/${id}${path}`, { method, body: body === undefined ? undefined : JSON.stringify(body) }))
      onChanged()
    } catch (e) { setErrors(errorList(e)) }
  }

  if (!t) return <section className="card"><p className="muted">Loading…</p></section>
  const s = t.task.status
  const working = s === 'open' || s === 'in_progress' || s === 'changes_requested'
  const allTicked = t.checklist.every(i => i.isDone)
  const canReview = review && s === 'in_review' && (!t.task.reviewerUserId || t.task.reviewerUserId === meId)

  return (
    <section className="card task-panel">
      <div className="row spread">
        <h2>{t.task.title}</h2>
        <button className="ghost" onClick={onClose}>Close</button>
      </div>
      <div className="row small">
        <span className={`tag ${s === 'done' ? 'create' : s === 'changes_requested' || t.task.isOverdue ? 'delete' : ''}`}>{TASK_STATUS[s]}</span>
        {t.task.clientName && <span>{t.task.clientName} <span className="muted">{t.task.clientCode}</span></span>}
        {t.task.dueDate && <span className={t.task.isOverdue ? 'error' : 'muted'}>Aim: {fmtDate(t.task.dueDate)}</span>}
        {t.deadline && <span className="muted">· Due by law: {fmtDate(t.deadline.dueDate)}</span>}
        {t.task.priority === 'high' && <span className="tag delete">high priority</span>}
        {t.task.reviewRequired && <span className="muted">· needs review{t.task.reviewerName ? ` by ${t.task.reviewerName}` : ''}</span>}
      </div>
      {t.description && <p>{t.description}</p>}

      {manage && working && (
        <div className="row small" style={{ marginTop: 8 }}>
          Assigned to
          <select value={t.task.assignedUserId ?? ''} onChange={e => act('', {
            title: t.task.title, description: t.description, clientId: t.task.clientId, dueDate: t.task.dueDate,
            priority: t.task.priority, assignedUserId: e.target.value || null, reviewerUserId: t.task.reviewerUserId, version: t.task.version,
          }, 'PUT')}>
            <option value="">— nobody —</option>
            {users.filter(u => u.isActive).map(u => <option key={u.id} value={u.id}>{u.name}</option>)}
          </select>
        </div>
      )}

      <h3 className="small muted" style={{ marginTop: 16 }}>Checklist {t.checklist.length > 0 && `(${t.checklist.filter(i => i.isDone).length}/${t.checklist.length})`}</h3>
      <ul className="checklist">
        {t.checklist.map(i => (
          <li key={i.id}>
            <label className="row">
              <input type="checkbox" checked={i.isDone} disabled={!manage || !working}
                onChange={e => act(`/checklist/${i.id}`, { done: e.target.checked }, 'PUT')} />
              <span className={i.isDone ? 'muted strike' : ''}>{i.text}</span>
              {i.isDone && <span className="muted small">{i.doneByName}</span>}
            </label>
            {manage && working && !i.isDone && <button className="ghost small" onClick={() => act(`/checklist/${i.id}`, undefined, 'DELETE')}>Not needed</button>}
          </li>
        ))}
      </ul>
      {manage && working && (
        <div className="row">
          <input placeholder="Add a step" value={step} onChange={e => setStep(e.target.value)} />
          <button className="ghost" disabled={!step.trim()} onClick={async () => { await act('/checklist', { text: step }); setStep('') }}>Add</button>
        </div>
      )}

      <TaskDocuments taskId={id} clientId={t.task.clientId} can={can}
        canAdd={working || s === 'in_review'} canRemove={working} />

      {manage && working && (
        <div className="notice" style={{ marginTop: 16 }}>
          {t.deadline && (
            <div className="grid">
              <label className="field">Filed on
                <input type="date" max={todayIso()} value={filing.filedOn} onChange={e => setFiling({ ...filing, filedOn: e.target.value })} /></label>
              <label className="field">Acknowledgement / ARN
                <input value={filing.ackNo} onChange={e => setFiling({ ...filing, ackNo: e.target.value })} /></label>
            </div>
          )}
          <div className="row" style={{ marginTop: 8 }}>
            {s === 'open' && <button className="ghost" onClick={() => act('/start', {})}>Start</button>}
            <button disabled={!allTicked} title={allTicked ? '' : 'Tick every step first'}
              onClick={() => act('/complete', { filedOn: t.deadline ? filing.filedOn : null, ackNo: filing.ackNo || null, comment: null, version: t.task.version })}>
              {t.task.reviewRequired ? 'Submit for review' : t.deadline ? 'Done — mark filed' : 'Done'}
            </button>
            <button className="ghost" onClick={() => act('/cancel', { reason: null })}>{t.deadline ? 'Not needed this period' : 'Cancel task'}</button>
          </div>
        </div>
      )}

      {s === 'in_review' && (
        <div className="notice" style={{ marginTop: 16 }}>
          <p className="small">Submitted by {t.submittedByName}{t.filedOn ? ` · filed ${fmtDate(t.filedOn)}` : ''}{t.ackNo ? ` · ${t.ackNo}` : ''}</p>
          {canReview ? (
            <div className="row">
              <input style={{ flex: 1 }} placeholder="Note (required to send back)" value={reviewNote} onChange={e => setReviewNote(e.target.value)} />
              <button onClick={() => act('/review', { approve: true, comment: reviewNote || null })}>Approve</button>
              <button className="ghost" onClick={() => act('/review', { approve: false, comment: reviewNote })}>Send back</button>
            </div>
          ) : <p className="muted small">Waiting for a reviewer.</p>}
        </div>
      )}

      {(s === 'done' || s === 'cancelled') && manage && (
        <div className="row" style={{ marginTop: 12 }}>
          {s === 'done' && <span className="ok small">Done {t.completedAt ? new Date(t.completedAt).toLocaleDateString() : ''} by {t.completedByName}</span>}
          <button className="ghost" onClick={() => act('/reopen', {})}>Reopen</button>
        </div>
      )}

      {errors && <ul className="error">{errors.map(m => <li key={m}>{m}</li>)}</ul>}

      <h3 className="small muted" style={{ marginTop: 16 }}>Activity</h3>
      <ul className="notes">
        {t.comments.map(c => (
          <li key={c.id} className={c.kind === 'comment' ? '' : 'muted small'}>
            {c.kind === 'review' ? <b>{c.body}</b> : c.body}
            <div className="muted small">{c.createdByName ?? 'System'} · {new Date(c.createdAt).toLocaleString()}</div>
          </li>
        ))}
      </ul>
      <div className="row">
        <input style={{ flex: 1 }} placeholder="Add a comment" value={comment} onChange={e => setComment(e.target.value)} />
        <button disabled={!comment.trim()} onClick={async () => { await act('/comments', { body: comment }); setComment('') }}>Comment</button>
      </div>
    </section>
  )
}
