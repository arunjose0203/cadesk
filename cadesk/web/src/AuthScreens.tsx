import { useState, type FormEvent } from 'react'
import { api, ApiError, type AuthResult } from './api'

type Props = { onSignedIn: (r: AuthResult) => void }

function messageOf(e: unknown) {
  if (e instanceof ApiError) return e.errors ? Object.values(e.errors).flat().join(' ') : e.message
  return 'Something went wrong. Try again.'
}

export default function AuthScreens({ onSignedIn }: Props) {
  const inviteToken = new URLSearchParams(location.search).get('invite')
  const [mode, setMode] = useState<'login' | 'signup'>('login')
  if (inviteToken) return <AcceptInvite token={inviteToken} onSignedIn={onSignedIn} />

  return (
    <section className="card narrow">
      <nav className="tabs">
        <button className={mode === 'login' ? 'active' : ''} onClick={() => setMode('login')}>Sign in</button>
        <button className={mode === 'signup' ? 'active' : ''} onClick={() => setMode('signup')}>Create a firm</button>
      </nav>
      {mode === 'login' ? <Login onSignedIn={onSignedIn} /> : <SignUp onSignedIn={onSignedIn} />}
    </section>
  )
}

function Login({ onSignedIn }: Props) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [mfaToken, setMfaToken] = useState<string | null>(null)
  const [code, setCode] = useState('')
  const [firms, setFirms] = useState<AuthResult['firms']>()
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const handle = (r: AuthResult) => {
    if (r.status === 'ok') onSignedIn(r)
    else if (r.status === 'mfa_required') setMfaToken(r.mfaToken ?? null)
    else if (r.status === 'choose_firm') setFirms(r.firms)
  }

  const run = async (fn: () => Promise<void>) => {
    setBusy(true); setError(null)
    try { await fn() } catch (e) { setError(messageOf(e)) } finally { setBusy(false) }
  }

  const login = (firmId?: string) => run(async () =>
    handle(await api<AuthResult>('/api/auth/login', {
      method: 'POST', body: JSON.stringify({ email, password, firmId }),
    })))

  const verify = (e: FormEvent) => { e.preventDefault(); run(async () =>
    handle(await api<AuthResult>('/api/auth/mfa/verify', {
      method: 'POST', body: JSON.stringify({ mfaToken, code }),
    }))) }

  if (mfaToken) return (
    <form onSubmit={verify} className="stack">
      <p>Enter the 6-digit code from your authenticator app, or a recovery code.</p>
      <input autoFocus inputMode="numeric" autoComplete="one-time-code" placeholder="123 456"
        value={code} onChange={e => setCode(e.target.value)} />
      <button disabled={busy}>Verify</button>
      {error && <p className="error">{error}</p>}
    </form>
  )

  if (firms?.length) return (
    <div className="stack">
      <p>Your email is used in more than one firm. Which one?</p>
      {firms.map(f => <button key={f.firmId} onClick={() => login(f.firmId)}>{f.firmName}</button>)}
    </div>
  )

  return (
    <form onSubmit={e => { e.preventDefault(); login() }} className="stack">
      <input type="email" autoComplete="username" placeholder="Email" value={email} onChange={e => setEmail(e.target.value)} />
      <input type="password" autoComplete="current-password" placeholder="Password" value={password}
        onChange={e => setPassword(e.target.value)} />
      <button disabled={busy}>Sign in</button>
      {error && <p className="error">{error}</p>}
    </form>
  )
}

function SignUp({ onSignedIn }: Props) {
  const [f, setF] = useState({ firmName: '', name: '', email: '', password: '' })
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault(); setBusy(true); setError(null)
    try {
      onSignedIn(await api<AuthResult>('/api/auth/signup', {
        method: 'POST', body: JSON.stringify({ ...f, countryCode: 'IN' }),
      }))
    } catch (err) { setError(messageOf(err)) } finally { setBusy(false) }
  }

  return (
    <form onSubmit={submit} className="stack">
      <input placeholder="Firm name" value={f.firmName} onChange={e => setF({ ...f, firmName: e.target.value })} />
      <input placeholder="Your name" value={f.name} onChange={e => setF({ ...f, name: e.target.value })} />
      <input type="email" autoComplete="username" placeholder="Email" value={f.email}
        onChange={e => setF({ ...f, email: e.target.value })} />
      <input type="password" autoComplete="new-password" placeholder="Password (10+ characters)" value={f.password}
        onChange={e => setF({ ...f, password: e.target.value })} />
      <button disabled={busy}>Create firm</button>
      <p className="muted small">India for now. You become the firm's Partner; invite your team next.</p>
      {error && <p className="error">{error}</p>}
    </form>
  )
}

function AcceptInvite({ token, onSignedIn }: Props & { token: string }) {
  const [name, setName] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)

  const submit = async (e: FormEvent) => {
    e.preventDefault(); setError(null)
    try {
      onSignedIn(await api<AuthResult>('/api/auth/accept-invite', {
        method: 'POST', body: JSON.stringify({ token, name, password }),
      }))
    } catch (err) { setError(messageOf(err)) }
  }

  return (
    <section className="card narrow">
      <h2>Join your firm</h2>
      <form onSubmit={submit} className="stack">
        <input placeholder="Your name" value={name} onChange={e => setName(e.target.value)} />
        <input type="password" autoComplete="new-password" placeholder="Choose a password (10+ characters)"
          value={password} onChange={e => setPassword(e.target.value)} />
        <button>Join</button>
        {error && <p className="error">{error}</p>}
      </form>
    </section>
  )
}
