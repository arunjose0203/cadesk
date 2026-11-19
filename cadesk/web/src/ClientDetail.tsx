import { useCallback, useEffect, useState } from 'react'
import { api, errorList, MESSAGE_NAMES, type NotificationPage, type Client, type ClientFormInfo, type Contact, type Group, type Note, type UserListItem } from './api'
import ClientForm from './ClientForm'
import ClientCompliances from './ClientCompliances'
import DocumentsPanel from './DocumentsPanel'

type Props = {
  id: string
  info: ClientFormInfo
  groups: Group[]
  users: UserListItem[]
  can: (p: string) => boolean
  meId: string
  onBack: () => void
  onChanged: () => void
}

const blankContact = { name: '', role: '', email: '', phone: '', isPrimary: false, receivesReminders: true }

export default function ClientDetail({ id, info, groups, users, can, meId, onBack, onChanged }: Props) {
  const [client, setClient] = useState<Client | null>(null)
  const [contacts, setContacts] = useState<Contact[]>([])
  const [notes, setNotes] = useState<Note[]>([])
  const [editing, setEditing] = useState(false)
  const [contact, setContact] = useState(blankContact)
  const [note, setNote] = useState('')
  const [errors, setErrors] = useState<string[] | null>(null)
  const write = can('clients.write')

  const load = useCallback(async () => {
    const [c, cs, ns] = await Promise.all([
      api<Client>(`/api/clients/${id}`),
      api<Contact[]>(`/api/clients/${id}/contacts`),
      api<Note[]>(`/api/clients/${id}/notes`),
    ])
    setClient(c); setContacts(cs); setNotes(ns)
  }, [id])
  useEffect(() => { load().catch(() => {}) }, [load])

  const run = async (fn: () => Promise<unknown>) => {
    setErrors(null)
    try { await fn(); await load() } catch (e) { setErrors(errorList(e)) }
  }

  const addContact = () => run(async () => {
    await api(`/api/clients/${id}/contacts`, { method: 'POST', body: JSON.stringify({ ...contact, isPrimary: contact.isPrimary || null }) })
    setContact(blankContact)
  })
  const makePrimary = (c: Contact) => run(() =>
    api(`/api/clients/${id}/contacts/${c.id}`, { method: 'PUT', body: JSON.stringify({ ...c, isPrimary: true }) }))
  const removeContact = (c: Contact) => run(() => api(`/api/clients/${id}/contacts/${c.id}`, { method: 'DELETE' }))
  const addNote = () => run(async () => {
    await api(`/api/clients/${id}/notes`, { method: 'POST', body: JSON.stringify({ body: note }) })
    setNote('')
  })
  const removeNote = (n: Note) => run(() => api(`/api/clients/${id}/notes/${n.id}`, { method: 'DELETE' }))

  if (!client) return <section className="card"><p className="muted">Loading…</p></section>

  const taxLabel = (code: string) => info.taxIdTypes.find(t => t.code === code)?.label ?? code
  const customLabel = (key: string) => info.customFields.find(f => f.key === key)?.label ?? key

  return (
    <>
      <section className="card">
        <div className="row spread">
          <button className="ghost" onClick={onBack}>← All clients</button>
          {write && !editing && <button onClick={() => setEditing(true)}>Edit</button>}
        </div>
        {editing ? (
          <>
            <h2>Edit {client.name}</h2>
            <ClientForm info={info} groups={groups} users={users} client={client}
              onSaved={c => { setClient(c); setEditing(false); onChanged() }} onCancel={() => setEditing(false)} />
          </>
        ) : (
          <>
            <h2>{client.name} <span className="muted small">{client.code}</span> {!client.isActive && <span className="tag delete">inactive</span>}</h2>
            <dl className="facts">
              <dt>Entity type</dt><dd>{client.entityType}</dd>
              {client.legalName && <><dt>Legal name</dt><dd>{client.legalName}</dd></>}
              {Object.entries(client.taxIds).map(([k, v]) => <FactRow key={k} label={taxLabel(k)} value={v} />)}
              {client.email && <FactRow label="Email" value={client.email} />}
              {client.phone && <FactRow label="Phone" value={client.phone} />}
              {client.groupName && <FactRow label="Group" value={client.groupName} />}
              {client.assignedUserName && <FactRow label="Assigned to" value={client.assignedUserName} />}
              {Object.entries(client.custom ?? {}).map(([k, v]) => (
                <FactRow key={k} label={customLabel(k)} value={v === 'true' ? 'Yes' : v === 'false' ? 'No' : v} />
              ))}
            </dl>
          </>
        )}
      </section>

      {can('compliance.read') && <ClientCompliances clientId={id} can={can} />}

      {can('documents.read') && <DocumentsPanel clientId={id} can={can} />}

      <section className="card">
        <h2>Contacts</h2>
        <table>
          <thead><tr><th>Name</th><th>Role</th><th>Email</th><th>Phone</th><th /></tr></thead>
          <tbody>
            {contacts.map(c => (
              <tr key={c.id}>
                <td>{c.name} {c.isPrimary && <span className="tag create">primary</span>}</td>
                <td>{c.role}</td><td>{c.email}</td><td>{c.phone}</td>
                <td className="nowrap">{write && <>
                  {!c.isPrimary && <button className="ghost" onClick={() => makePrimary(c)}>Make primary</button>}
                  <button className="ghost" onClick={() => removeContact(c)}>Remove</button>
                </>}</td>
              </tr>
            ))}
            {contacts.length === 0 && <tr><td colSpan={5} className="muted">No contacts yet.</td></tr>}
          </tbody>
        </table>
        {write && (
          <div className="grid" style={{ marginTop: 12 }}>
            <input placeholder="Name" value={contact.name} onChange={e => setContact({ ...contact, name: e.target.value })} />
            <input placeholder="Role (Director, Accountant…)" value={contact.role} onChange={e => setContact({ ...contact, role: e.target.value })} />
            <input placeholder="Email" value={contact.email} onChange={e => setContact({ ...contact, email: e.target.value })} />
            <input placeholder="Phone" value={contact.phone} onChange={e => setContact({ ...contact, phone: e.target.value })} />
            <button onClick={addContact}>Add contact</button>
          </div>
        )}
      </section>

      <section className="card">
        <h2>Notes</h2>
        {write && (
          <div className="row">
            <input style={{ flex: 1 }} placeholder="Add a note (calls, decisions, reminders)…" value={note} onChange={e => setNote(e.target.value)} />
            <button onClick={addNote} disabled={!note.trim()}>Add note</button>
          </div>
        )}
        <ul className="notes">
          {notes.map(n => (
            <li key={n.id}>
              <div>{n.body}</div>
              <div className="muted small">
                {n.createdByName ?? 'Someone'} · {new Date(n.createdAt).toLocaleString()}
                {n.createdBy === meId && <button className="ghost" onClick={() => removeNote(n)}>Delete</button>}
              </div>
            </li>
          ))}
          {notes.length === 0 && <li className="muted">No notes yet.</li>}
        </ul>
      </section>
      <ClientReminders clientId={id} />
      {errors && <ul className="error">{errors.map(m => <li key={m}>{m}</li>)}</ul>}
    </>
  )
}

function FactRow({ label, value }: { label: string; value: string }) {
  return <><dt>{label}</dt><dd>{value}</dd></>
}

/** Reminders sent to this client and its contacts. */
function ClientReminders({ clientId }: { clientId: string }) {
  const [items, setItems] = useState<NotificationPage['items']>([])
  useEffect(() => { api<NotificationPage>(`/api/clients/${clientId}/notifications`).then(p => setItems(p.items)).catch(() => {}) }, [clientId])
  if (items.length === 0) return null
  return (
    <section className="card">
      <h2>Reminders</h2>
      <ul className="deadline-list">
        {items.slice(0, 10).map(n => (
          <li key={n.id}>
            <b>{new Date(n.sentAt ?? n.scheduledAt).toLocaleDateString()}</b> · {MESSAGE_NAMES[n.templateKey] ?? n.templateKey}
            <span className="muted small"> · {n.channel} to {n.recipientName} · {n.status}</span>
            <div className="muted small">{n.subject}</div>
          </li>
        ))}
      </ul>
    </section>
  )
}
