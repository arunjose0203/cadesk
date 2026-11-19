import { useCallback, useEffect, useState } from 'react'
import { api, errorList, type Client, type ClientFormInfo, type ClientPage, type Group, type UserListItem } from './api'
import ClientForm from './ClientForm'
import ClientDetail from './ClientDetail'
import ImportPanel from './ImportPanel'

const PAGE = 25

export default function ClientsPage({ can, meId }: { can: (p: string) => boolean; meId: string }) {
  const [info, setInfo] = useState<ClientFormInfo | null>(null)
  const [groups, setGroups] = useState<Group[]>([])
  const [users, setUsers] = useState<UserListItem[]>([])
  const [page, setPage] = useState<ClientPage>({ items: [], total: 0 })
  const [search, setSearch] = useState('')
  const [groupId, setGroupId] = useState('')
  const [entityType, setEntityType] = useState('')
  const [active, setActive] = useState('true')
  const [skip, setSkip] = useState(0)
  const [view, setView] = useState<'list' | 'add' | 'import' | 'groups' | { id: string }>('list')
  const [notice, setNotice] = useState<string[] | null>(null)

  const canSeeUsers = can('users.read')   // a boolean, so the effect below doesn't re-run on every render
  const loadRefs = useCallback(async () => {
    const [i, g] = await Promise.all([api<ClientFormInfo>('/api/clients/form'), api<Group[]>('/api/client-groups')])
    setInfo(i); setGroups(g)
    if (canSeeUsers) setUsers(await api<UserListItem[]>('/api/users'))
  }, [canSeeUsers])

  const load = useCallback(async () => {
    const q = new URLSearchParams({ skip: String(skip), take: String(PAGE) })
    const s = search.trim()
    // something that looks like a PAN / GSTIN / TRN searches tax IDs; anything else searches names and codes
    if (/^[A-Za-z0-9]{10,15}$/.test(s) && /\d/.test(s)) q.set('taxId', s)
    else if (s) q.set('search', s)
    if (groupId) q.set('groupId', groupId)
    if (entityType) q.set('entityType', entityType)
    if (active) q.set('active', active)
    setPage(await api<ClientPage>(`/api/clients?${q}`))
  }, [search, groupId, entityType, active, skip])

  useEffect(() => { loadRefs().catch(() => {}) }, [loadRefs])
  useEffect(() => { load().catch(() => {}) }, [load])
  useEffect(() => { setSkip(0) }, [search, groupId, entityType, active])

  const refresh = () => { load().catch(() => {}); loadRefs().catch(() => {}) }

  if (!info) return <section className="card"><p className="muted">Loading…</p></section>

  if (typeof view === 'object')
    return <ClientDetail id={view.id} info={info} groups={groups} users={users} can={can} meId={meId}
      onBack={() => { setView('list'); refresh() }} onChanged={refresh} />

  const write = can('clients.write')
  const taxCols = info.taxIdTypes.slice(0, 2)

  return (
    <>
      {view === 'add' && (
        <section className="card">
          <h2>Add client</h2>
          <ClientForm info={info} groups={groups} users={users}
            onSaved={c => { setNotice(c.warnings?.length ? c.warnings : [`Added ${c.name} as ${c.code}.`]); refresh() }}
            onCancel={() => setView('list')} />
        </section>
      )}
      {view === 'import' && <ImportPanel onDone={refresh} onClose={() => setView('list')} />}
      {view === 'groups' && <GroupsPanel groups={groups} onChanged={refresh} onClose={() => setView('list')} />}

      <section className="card">
        <div className="row spread">
          <h2>Clients <span className="muted small">{page.total}</span></h2>
          {write && view === 'list' && (
            <div className="row">
              <button onClick={() => { setNotice(null); setView('add') }}>Add client</button>
              <button className="ghost" onClick={() => setView('import')}>Import from Excel</button>
              <button className="ghost" onClick={() => setView('groups')}>Groups</button>
            </div>
          )}
        </div>
        {notice && <ul className="warn small">{notice.map(m => <li key={m}>{m}</li>)}</ul>}
        <div className="grid">
          <input placeholder="Search name, code, PAN, GSTIN…" value={search} onChange={e => setSearch(e.target.value)} />
          <select value={groupId} onChange={e => setGroupId(e.target.value)}>
            <option value="">All groups</option>
            {groups.map(g => <option key={g.id} value={g.id}>{g.name}</option>)}
          </select>
          <select value={entityType} onChange={e => setEntityType(e.target.value)}>
            <option value="">All types</option>
            {info.entityTypes.map(t => <option key={t}>{t}</option>)}
          </select>
          <select value={active} onChange={e => setActive(e.target.value)}>
            <option value="true">Active</option><option value="false">Inactive</option><option value="">All</option>
          </select>
        </div>
        <table>
          <thead><tr><th>Code</th><th>Name</th><th>Type</th>{taxCols.map(t => <th key={t.code}>{t.code}</th>)}<th>Group</th><th>Assigned</th></tr></thead>
          <tbody>
            {page.items.map((c: Client) => (
              <tr key={c.id} className="clickable" onClick={() => setView({ id: c.id })}>
                <td>{c.code}</td><td>{c.name}</td><td>{c.entityType}</td>
                {taxCols.map(t => <td key={t.code} className="mono">{c.taxIds[t.code] ?? ''}</td>)}
                <td>{c.groupName ?? ''}</td><td>{c.assignedUserName ?? ''}</td>
              </tr>
            ))}
            {page.items.length === 0 && <tr><td colSpan={5 + taxCols.length} className="muted">No clients match.</td></tr>}
          </tbody>
        </table>
        {page.total > PAGE && (
          <div className="row" style={{ marginTop: 8 }}>
            <button className="ghost" disabled={skip === 0} onClick={() => setSkip(Math.max(0, skip - PAGE))}>← Previous</button>
            <span className="muted small">{skip + 1}–{Math.min(skip + PAGE, page.total)} of {page.total}</span>
            <button className="ghost" disabled={skip + PAGE >= page.total} onClick={() => setSkip(skip + PAGE)}>Next →</button>
          </div>
        )}
      </section>
    </>
  )
}

function GroupsPanel({ groups, onChanged, onClose }: { groups: Group[]; onChanged: () => void; onClose: () => void }) {
  const [name, setName] = useState('')
  const [errors, setErrors] = useState<string[] | null>(null)
  const run = async (fn: () => Promise<unknown>) => {
    setErrors(null)
    try { await fn(); onChanged() } catch (e) { setErrors(errorList(e)) }
  }
  return (
    <section className="card">
      <div className="row spread"><h2>Family and business groups</h2><button className="ghost" onClick={onClose}>Close</button></div>
      <table>
        <thead><tr><th>Group</th><th>Clients</th><th /></tr></thead>
        <tbody>
          {groups.map(g => (
            <tr key={g.id}>
              <td>{g.name}</td><td>{g.clientCount}</td>
              <td className="nowrap">
                <button className="ghost" onClick={() => {
                  const n = prompt('New name', g.name)
                  if (n) run(() => api(`/api/client-groups/${g.id}`, { method: 'PUT', body: JSON.stringify({ name: n }) }))
                }}>Rename</button>
                <button className="ghost" disabled={g.clientCount > 0} title={g.clientCount > 0 ? 'Move its clients out first' : ''}
                  onClick={() => run(() => api(`/api/client-groups/${g.id}`, { method: 'DELETE' }))}>Delete</button>
              </td>
            </tr>
          ))}
          {groups.length === 0 && <tr><td colSpan={3} className="muted">No groups yet.</td></tr>}
        </tbody>
      </table>
      <div className="row" style={{ marginTop: 12 }}>
        <input placeholder="e.g. Menon family" value={name} onChange={e => setName(e.target.value)} />
        <button disabled={!name.trim()} onClick={() => run(async () => {
          await api('/api/client-groups', { method: 'POST', body: JSON.stringify({ name }) }); setName('')
        })}>Add group</button>
      </div>
      {errors && <ul className="error">{errors.map(m => <li key={m}>{m}</li>)}</ul>}
    </section>
  )
}
