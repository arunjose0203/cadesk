import { useEffect, useRef, useState } from 'react'
import { api, onSessionEnded, refreshSession, setAccessToken, type AuthResult, type UserProfile } from './api'
import AuthScreens from './AuthScreens'
import ClientsPage from './ClientsPage'
import UsersPage from './UsersPage'
import SecurityPage from './SecurityPage'
import SettingsPage from './SettingsPage'
import AuditPage from './AuditPage'
import DeadlinesPage from './DeadlinesPage'
import TasksPage from './TasksPage'

type Page = 'tasks' | 'deadlines' | 'clients' | 'users' | 'settings' | 'audit' | 'security'

export default function App() {
  const [user, setUser] = useState<UserProfile | null>(null)
  const [checking, setChecking] = useState(true)
  const [page, setPage] = useState<Page>('clients')
  const [notice, setNotice] = useState<string | null>(null)
  const idleTimer = useRef<number | undefined>(undefined)

  // Restore the session from the refresh cookie on page load.
  useEffect(() => {
    onSessionEnded(() => setUser(null))
    refreshSession()
      .then(async ok => { if (ok) setUser(await api<UserProfile>('/api/auth/me')) })
      .catch(() => setUser(null))
      .finally(() => setChecking(false))
  }, [])

  const signedIn = (r: AuthResult) => {
    setAccessToken(r.accessToken ?? null)
    setUser(r.user ?? null)
    history.replaceState(null, '', '/') // drop ?invite=...
  }

  const signOut = async (reason?: string) => {
    await api('/api/auth/logout', { method: 'POST' }).catch(() => {})
    setAccessToken(null)
    setUser(null)
    setNotice(reason ?? null)
  }

  // Sign out after the firm's inactivity limit (security.session_timeout_min). The server enforces it too.
  const timeoutMinutes = user?.sessionTimeoutMinutes
  useEffect(() => {
    if (!timeoutMinutes) return
    const reset = () => {
      window.clearTimeout(idleTimer.current)
      idleTimer.current = window.setTimeout(
        () => signOut('You were signed out after a period of inactivity.'), timeoutMinutes * 60_000)
    }
    const events = ['mousemove', 'keydown', 'click', 'scroll', 'touchstart'] as const
    events.forEach(e => window.addEventListener(e, reset, { passive: true }))
    reset()
    return () => { window.clearTimeout(idleTimer.current); events.forEach(e => window.removeEventListener(e, reset)) }
  }, [timeoutMinutes])

  // After two-step set-up, refresh so the session gets full access.
  const afterSecurityChange = async () => {
    await refreshSession()
    setUser(await api<UserProfile>('/api/auth/me'))
  }

  if (checking) return <main className="page"><p className="muted">Loading…</p></main>
  if (!user) return (
    <main className="page">
      <h1>CA Desk</h1>
      {notice && <p className="notice small">{notice}</p>}
      <AuthScreens onSignedIn={signedIn} />
    </main>
  )

  if (user.mfaSetupRequired) return (
    <main className="page">
      <header className="top"><h1>CA Desk</h1><button className="ghost" onClick={() => signOut()}>Sign out</button></header>
      <p className="notice">Your firm requires two-step sign-in for your role. Set it up below to continue.</p>
      <SecurityPage me={user} onChanged={afterSecurityChange} />
    </main>
  )

  const can = (p: string) => user.permissions.includes(p)

  return (
    <main className="page">
      <header className="top">
        <div>
          <h1>CA Desk</h1>
          <span className="muted">{user.firmName} · {user.name} ({user.roles.join(', ')})</span>
        </div>
        <button className="ghost" onClick={() => signOut()}>Sign out</button>
      </header>
      <nav className="tabs">
        {can('tasks.read') && (
          <button className={page === 'tasks' ? 'active' : ''} onClick={() => setPage('tasks')}>Tasks</button>
        )}
        {can('compliance.read') && (
          <button className={page === 'deadlines' ? 'active' : ''} onClick={() => setPage('deadlines')}>Deadlines</button>
        )}
        <button className={page === 'clients' ? 'active' : ''} onClick={() => setPage('clients')}>Clients</button>
        {can('users.read') && (
          <button className={page === 'users' ? 'active' : ''} onClick={() => setPage('users')}>Users</button>
        )}
        {can('settings.manage') && (
          <button className={page === 'settings' ? 'active' : ''} onClick={() => setPage('settings')}>Settings</button>
        )}
        {can('audit.read') && (
          <button className={page === 'audit' ? 'active' : ''} onClick={() => setPage('audit')}>Audit log</button>
        )}
        <button className={page === 'security' ? 'active' : ''} onClick={() => setPage('security')}>My account</button>
      </nav>
      {page === 'tasks' && can('tasks.read') && <TasksPage can={can} meId={user.id} />}
      {page === 'deadlines' && can('compliance.read') && <DeadlinesPage can={can} />}
      {page === 'clients' && <ClientsPage can={can} meId={user.id} />}
      {page === 'users' && can('users.read') && <UsersPage can={can} me={user} />}
      {page === 'settings' && can('settings.manage') && <SettingsPage />}
      {page === 'audit' && can('audit.read') && <AuditPage />}
      {page === 'security' && <SecurityPage me={user} onChanged={afterSecurityChange} />}
    </main>
  )
}
