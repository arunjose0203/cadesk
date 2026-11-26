import { useEffect, useState } from 'react'
import QRCode from 'qrcode'
import { api, ApiError, setAccessToken, type AuthResult, type UserProfile } from './api'
import { ValueEditor } from './ValueEditor'
import type { Json, Schema } from './format'

type Props = { me: UserProfile; onChanged: () => void }

function messageOf(e: unknown) {
  if (e instanceof ApiError) return e.errors ? Object.values(e.errors).flat().join(' ') : e.message
  return 'Something went wrong.'
}

export default function SecurityPage({ me, onChanged }: Props) {
  return (
    <>
      <Preferences />
      <TwoStep me={me} onChanged={onChanged} />
      <ChangePassword />
    </>
  )
}

function TwoStep({ me, onChanged }: Props) {
  const [setup, setSetup] = useState<{ secret: string; otpAuthUri: string } | null>(null)
  const [qr, setQr] = useState<string | null>(null)
  const [code, setCode] = useState('')
  const [recovery, setRecovery] = useState<string[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (setup) QRCode.toDataURL(setup.otpAuthUri, { margin: 1, width: 200 }).then(setQr).catch(() => setQr(null))
  }, [setup])

  const run = async (fn: () => Promise<void>) => {
    setError(null)
    try { await fn() } catch (e) { setError(messageOf(e)) }
  }

  const begin = () => run(async () => setSetup(await api('/api/auth/mfa/setup', { method: 'POST' })))
  const enable = () => run(async () => {
    const r = await api<{ recoveryCodes: string[] }>('/api/auth/mfa/enable', { method: 'POST', body: JSON.stringify({ code }) })
    setRecovery(r.recoveryCodes); setSetup(null); setCode(''); onChanged()
  })
  const disable = () => run(async () => {
    await api('/api/auth/mfa/disable', { method: 'POST', body: JSON.stringify({ code }) })
    setCode(''); onChanged()
  })

  return (
    <section className="card">
      <h2>Two-step sign-in</h2>
      {recovery && (
        <div className="notice">
          <p><strong>Save these recovery codes</strong> somewhere safe. Each works once if you lose your phone.</p>
          <ul className="codes">{recovery.map(c => <li key={c}><code>{c}</code></li>)}</ul>
        </div>
      )}
      {me.mfaEnabled ? (
        <div className="stack">
          <p>On. You'll enter a code from your authenticator app when you sign in.</p>
          <div className="row">
            <input inputMode="numeric" placeholder="Code to turn off" value={code} onChange={e => setCode(e.target.value)} />
            <button className="ghost" onClick={disable}>Turn off</button>
          </div>
        </div>
      ) : setup ? (
        <div className="stack">
          <p>Scan this with Google Authenticator, Microsoft Authenticator or similar, then enter the 6-digit code.</p>
          {qr && <img src={qr} alt="QR code for your authenticator app" width={200} height={200} />}
          <p className="small muted">Can't scan? Enter this key: <code className="break">{setup.secret}</code></p>
          <div className="row">
            <input inputMode="numeric" autoComplete="one-time-code" placeholder="123456" value={code} onChange={e => setCode(e.target.value)} />
            <button onClick={enable}>Turn on</button>
          </div>
        </div>
      ) : (
        <div className="stack">
          <p>Off. Partners should turn this on: it protects all your clients' data.</p>
          <button onClick={begin}>Set up</button>
        </div>
      )}
      {error && <p className="error">{error}</p>}
    </section>
  )
}

function ChangePassword() {
  const [f, setF] = useState({ currentPassword: '', newPassword: '' })
  const [msg, setMsg] = useState<string | null>(null)

  const submit = async () => {
    setMsg(null)
    try {
      const r = await api<AuthResult>('/api/auth/password', { method: 'POST', body: JSON.stringify(f) })
      setAccessToken(r.accessToken ?? null)
      setF({ currentPassword: '', newPassword: '' })
      setMsg('Password changed. Other devices have been signed out.')
    } catch (e) { setMsg(messageOf(e)) }
  }

  return (
    <section className="card">
      <h2>Change password</h2>
      <div className="grid">
        <input type="password" autoComplete="current-password" placeholder="Current password" value={f.currentPassword}
          onChange={e => setF({ ...f, currentPassword: e.target.value })} />
        <input type="password" autoComplete="new-password" placeholder="New password (10+ characters)" value={f.newPassword}
          onChange={e => setF({ ...f, newPassword: e.target.value })} />
        <button onClick={submit}>Change</button>
      </div>
      {msg && <p className="small">{msg}</p>}
    </section>
  )
}

type Pref = { key: string; label: string; schema: Schema; value: Json; source: string }

function Preferences() {
  const [prefs, setPrefs] = useState<Pref[]>([])
  const [msg, setMsg] = useState<string | null>(null)
  const load = async () => setPrefs(await api<Pref[]>('/api/me/settings'))
  useEffect(() => { load().catch(() => {}) }, [])

  const save = async (p: Pref, value: Json) => {
    setMsg(null)
    try {
      await api(`/api/me/settings/${p.key}`, { method: 'PUT', body: JSON.stringify({ value }) })
      await load(); setMsg('Saved.')
    } catch (e) { setMsg(messageOf(e)) }
  }

  return (
    <section className="card">
      <h2>My preferences</h2>
      <table>
        <tbody>
          {prefs.map(p => (
            <tr key={p.key}>
              <td>{p.label}<div className="muted small">{p.source === 'user' ? 'Your choice' : 'Firm default'}</div></td>
              <td><ValueEditor schema={p.schema} value={p.value} onChange={v => save(p, v)} /></td>
            </tr>
          ))}
        </tbody>
      </table>
      {msg && <p className="small">{msg}</p>}
    </section>
  )
}
