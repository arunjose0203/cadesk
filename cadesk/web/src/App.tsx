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
import BillingPage from './BillingPage'
import DscPage from './DscPage'
import PortalPage from './PortalPage'
import { TaxPage } from './TaxPage'
import { AuditWorkspacePage } from './AuditWorkspacePage'
import { GstReconciliationPage } from './GstReconciliationPage'
import { TdsPage } from './TdsPage'
import { McaCompliancePage } from './McaCompliancePage'
import { EngagementKycPage } from './EngagementKycPage'
import { BankFeedsPage } from './BankFeedsPage'
import { ReportsPage } from './ReportsPage'
import { TaxNoticesPage } from './TaxNoticesPage'
import { PayrollPage } from './PayrollPage'
import { EInvoicingPage } from './EInvoicingPage'
import { FixedAssetsPage } from './FixedAssetsPage'
import { AdvanceTaxPage } from './AdvanceTaxPage'
import { Gstr9Page } from './Gstr9Page'
import { PresumptiveTaxPage } from './PresumptiveTaxPage'
import { CaroAuditDeskPage } from './CaroAuditDeskPage'
import { TaxAuditDeskPage } from './TaxAuditDeskPage'
import { Schedule3DeskPage } from './Schedule3DeskPage'
import { BranchAuditDeskPage } from './BranchAuditDeskPage'
import { AuditCommitteeDeskPage } from './AuditCommitteeDeskPage'
import { ForeignRemittancePage } from './ForeignRemittancePage'
import { RelatedPartyPage } from './RelatedPartyPage'

type Page = 'tasks' | 'deadlines' | 'clients' | 'billing' | 'tax' | 'audit-workspace' | 'gst-reconcile' | 'tds' | 'mca' | 'engagement' | 'banking' | 'reports' | 'tax-notices' | 'payroll' | 'einvoicing' | 'fixed-assets' | 'advance-tax' | 'gstr9' | 'presumptive' | 'caro' | 'taxaudit' | 'schedule3' | 'branchaudit' | 'auditcommittee' | 'foreignremittance' | 'relatedparty' | 'dsc' | 'portal' | 'users' | 'settings' | 'audit' | 'security'

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

  if (checking) {
    return (
      <main className="page" style={{ display: 'flex', minHeight: '60vh', alignItems: 'center', justifyContent: 'center' }}>
        <div style={{ textAlign: 'center' }}>
          <div className="brand-icon" style={{ margin: '0 auto 16px', width: '48px', height: '48px', fontSize: '20px' }}>CA</div>
          <p className="muted" style={{ fontWeight: 500 }}>Loading workspace…</p>
        </div>
      </main>
    )
  }

  if (!user) {
    return (
      <main className="page">
        <div style={{ textAlign: 'center', margin: '48px 0 24px' }}>
          <div className="brand-icon" style={{ margin: '0 auto 16px', width: '56px', height: '56px', fontSize: '24px' }}>CA</div>
          <h1 style={{ fontSize: '2rem', letterSpacing: '-0.03em', marginBottom: '8px' }}>CA Desk</h1>
          <p className="muted" style={{ fontSize: '1rem', maxWidth: '460px', margin: '0 auto' }}>
            Multi-Tenant Practice Management & Regulatory Compliance for Accounting Firms
          </p>
        </div>
        {notice && <p className="notice small" style={{ maxWidth: '440px', margin: '0 auto 16px' }}>{notice}</p>}
        <AuthScreens onSignedIn={signedIn} />
      </main>
    )
  }

  if (user.mfaSetupRequired) {
    return (
      <main className="page">
        <header className="top">
          <div className="brand-badge">
            <div className="brand-icon">CA</div>
            <div className="brand-meta">
              <h1>CA Desk</h1>
              <span className="firm-pill">🏢 {user.firmName}</span>
            </div>
          </div>
          <button className="ghost small" onClick={() => signOut()}>Sign out</button>
        </header>
        <p className="notice">Your firm requires two-step sign-in for your role. Set it up below to continue.</p>
        <SecurityPage me={user} onChanged={afterSecurityChange} />
      </main>
    )
  }

  const can = (p: string) => user.permissions.includes(p)
  const initials = user.name.split(' ').map(n => n[0]).filter(Boolean).slice(0, 2).join('').toUpperCase() || 'U'

  return (
    <main className="page">
      <header className="top">
        <div className="brand-badge">
          <div className="brand-icon">CA</div>
          <div className="brand-meta">
            <h1>CA Desk</h1>
            <div className="firm-pill">
              <span>🏢</span>
              <strong>{user.firmName}</strong>
            </div>
          </div>
        </div>

        <div className="row" style={{ gap: '12px' }}>
          <div className="user-badge">
            <div className="user-avatar">{initials}</div>
            <span style={{ fontWeight: 600 }}>{user.name}</span>
            <span className="role-tag">{user.roles[0] || 'Staff'}</span>
          </div>
          <button className="ghost small" onClick={() => signOut()} title="Sign out of your account">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"></path>
              <polyline points="16 17 21 12 16 7"></polyline>
              <line x1="21" y1="12" x2="9" y2="12"></line>
            </svg>
            Sign out
          </button>
        </div>
      </header>

      <nav className="tabs">
        {can('tasks.read') && (
          <button className={page === 'tasks' ? 'active' : ''} onClick={() => setPage('tasks')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"></path>
              <rect x="8" y="2" width="8" height="4" rx="1" ry="1"></rect>
              <path d="M9 14l2 2 4-4"></path>
            </svg>
            Tasks
          </button>
        )}
        {can('compliance.read') && (
          <button className={page === 'deadlines' ? 'active' : ''} onClick={() => setPage('deadlines')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
              <line x1="16" y1="2" x2="16" y2="6"></line>
              <line x1="8" y1="2" x2="8" y2="6"></line>
              <line x1="3" y1="10" x2="21" y2="10"></line>
            </svg>
            Deadlines
          </button>
        )}
        <button className={page === 'clients' ? 'active' : ''} onClick={() => setPage('clients')}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <rect x="2" y="7" width="20" height="14" rx="2" ry="2"></rect>
            <path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16"></path>
          </svg>
          Clients
        </button>
        <button className={page === 'billing' ? 'active' : ''} onClick={() => setPage('billing')}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <line x1="12" y1="1" x2="12" y2="23"></line>
            <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
          </svg>
          Billing & Time
        </button>
        {can('computations.run') && (
          <button className={page === 'tax' ? 'active' : ''} onClick={() => setPage('tax')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="4" y="2" width="16" height="20" rx="2"></rect>
              <line x1="8" y1="6" x2="16" y2="6"></line>
              <line x1="16" y1="14" x2="16" y2="18"></line>
              <path d="M16 10h.01"></path>
              <path d="M12 10h.01"></path>
              <path d="M8 10h.01"></path>
              <path d="M12 14h.01"></path>
              <path d="M8 14h.01"></path>
              <path d="M12 18h.01"></path>
              <path d="M8 18h.01"></path>
            </svg>
            Tax Desk
          </button>
        )}
        {can('audit.read') && (
          <button className={page === 'audit-workspace' ? 'active' : ''} onClick={() => setPage('audit-workspace')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="16" y1="13" x2="8" y2="13"></line>
              <line x1="16" y1="17" x2="8" y2="17"></line>
              <polyline points="10 9 9 9 8 9"></polyline>
            </svg>
            Audit Workspace
          </button>
        )}
        {can('gst.read') && (
          <button className={page === 'gst-reconcile' ? 'active' : ''} onClick={() => setPage('gst-reconcile')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M16 16v1a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2h11a2 2 0 0 1 2 2v1"></path>
              <path d="M18 8h4a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-4"></path>
              <polyline points="10 12 14 12 14 8"></polyline>
            </svg>
            GST 2B Match
          </button>
        )}
        {can('tds.read') && (
          <button className={page === 'tds' ? 'active' : ''} onClick={() => setPage('tds')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="16" y1="13" x2="8" y2="13"></line>
              <line x1="16" y1="17" x2="8" y2="17"></line>
              <polyline points="10 9 9 9 8 9"></polyline>
            </svg>
            TDS Desk
          </button>
        )}
        {can('mca.read') && (
          <button className={page === 'mca' ? 'active' : ''} onClick={() => setPage('mca')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M3 21h18"></path>
              <path d="M5 21V7l8-4v18"></path>
              <path d="M19 21V11l-6-4"></path>
              <line x1="9" y1="9" x2="9" y2="9.01"></line>
              <line x1="9" y1="13" x2="9" y2="13.01"></line>
              <line x1="9" y1="17" x2="9" y2="17.01"></line>
            </svg>
            MCA / ROC
          </button>
        )}
        {can('engagement.read') && (
          <button className={page === 'engagement' ? 'active' : ''} onClick={() => setPage('engagement')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="16" y1="13" x2="8" y2="13"></line>
              <line x1="16" y1="17" x2="8" y2="17"></line>
              <path d="M10 9H8"></path>
            </svg>
            Engagements & KYC
          </button>
        )}
        {can('bank.read') && (
          <button className={page === 'banking' ? 'active' : ''} onClick={() => setPage('banking')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="2" y="5" width="20" height="14" rx="2"></rect>
              <line x1="2" y1="10" x2="22" y2="10"></line>
            </svg>
            Bank Feeds & BRS
          </button>
        )}
        {can('reports.read') && (
          <button className={page === 'reports' ? 'active' : ''} onClick={() => setPage('reports')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="20" x2="18" y2="10"></line>
              <line x1="12" y1="20" x2="12" y2="4"></line>
              <line x1="6" y1="20" x2="6" y2="14"></line>
            </svg>
            MIS & Reports
          </button>
        )}
        {can('notices.read') && (
          <button className={page === 'tax-notices' ? 'active' : ''} onClick={() => setPage('tax-notices')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="12" y1="18" x2="12" y2="12"></line>
              <line x1="12" y1="9" x2="12.01" y2="9"></line>
            </svg>
            Tax Notices & Scrutiny
          </button>
        )}
        {can('payroll.read') && (
          <button className={page === 'payroll' ? 'active' : ''} onClick={() => setPage('payroll')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="2" y="4" width="20" height="16" rx="2"></rect>
              <line x1="6" y1="8" x2="10" y2="8"></line>
              <line x1="6" y1="12" x2="18" y2="12"></line>
              <line x1="6" y1="16" x2="14" y2="16"></line>
            </svg>
            Payroll & PF/ESI
          </button>
        )}
        {can('einvoice.read') && (
          <button className={page === 'einvoicing' ? 'active' : ''} onClick={() => setPage('einvoicing')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="1" y="3" width="15" height="13"></rect>
              <polygon points="16 8 20 8 23 11 23 16 16 16 16 8"></polygon>
              <circle cx="5.5" cy="18.5" r="2.5"></circle>
              <circle cx="18.5" cy="18.5" r="2.5"></circle>
            </svg>
            E-Invoice & EWB
          </button>
        )}
        {can('assets.read') && (
          <button className={page === 'fixed-assets' ? 'active' : ''} onClick={() => setPage('fixed-assets')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"></path>
              <polyline points="3.27 6.96 12 12.01 20.73 6.96"></polyline>
              <line x1="12" y1="22.08" x2="12" y2="12"></line>
            </svg>
            Fixed Assets & Depr
          </button>
        )}
        {can('advancetax.read') && (
          <button className={page === 'advance-tax' ? 'active' : ''} onClick={() => setPage('advance-tax')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10"></circle>
              <polyline points="12 6 12 12 16 14"></polyline>
            </svg>
            Advance Tax & 234
          </button>
        )}
        {can('gstr9.read') && (
          <button className={page === 'gstr9' ? 'active' : ''} onClick={() => setPage('gstr9')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="16" y1="13" x2="8" y2="13"></line>
              <line x1="16" y1="17" x2="8" y2="17"></line>
              <polyline points="10 9 9 9 8 9"></polyline>
            </svg>
            GSTR-9 / 9C Desk
          </button>
        )}
        {can('presumptive.read') && (
          <button className={page === 'presumptive' ? 'active' : ''} onClick={() => setPage('presumptive')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 2v20"></path>
              <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
            </svg>
            Presumptive Tax (44AD)
          </button>
        )}
        {can('caro.read') && (
          <button className={page === 'caro' ? 'active' : ''} onClick={() => setPage('caro')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="9" y1="15" x2="15" y2="15"></line>
            </svg>
            CARO 2020 Desk
          </button>
        )}
        {can('taxaudit.read') && (
          <button className={page === 'taxaudit' ? 'active' : ''} onClick={() => setPage('taxaudit')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="2" y="3" width="20" height="14" rx="2" ry="2"></rect>
              <line x1="8" y1="21" x2="16" y2="21"></line>
              <line x1="12" y1="17" x2="12" y2="21"></line>
            </svg>
            Tax Audit (3CD)
          </button>
        )}
        {can('schedule3.read') && (
          <button className={page === 'schedule3' ? 'active' : ''} onClick={() => setPage('schedule3')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="20" x2="18" y2="10"></line>
              <line x1="12" y1="20" x2="12" y2="4"></line>
              <line x1="6" y1="20" x2="6" y2="14"></line>
            </svg>
            Schedule III Desk
          </button>
        )}
        {can('branchaudit.read') && (
          <button className={page === 'branchaudit' ? 'active' : ''} onClick={() => setPage('branchaudit')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
              <circle cx="8.5" cy="7" r="4"></circle>
              <line x1="20" y1="8" x2="20" y2="14"></line>
              <line x1="23" y1="11" x2="17" y2="11"></line>
            </svg>
            Branch Audit (SA 600)
          </button>
        )}
        {can('auditcommittee.read') && (
          <button className={page === 'auditcommittee' ? 'active' : ''} onClick={() => setPage('auditcommittee')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M3 21h18"></path>
              <path d="M5 21V7l8-4v18"></path>
              <path d="M19 21V11l-6-4"></path>
              <circle cx="9" cy="9" r="1"></circle>
              <circle cx="9" cy="13" r="1"></circle>
            </svg>
            Audit Committee (SA 260)
          </button>
        )}
        {can('foreignremittance.read') && (
          <button className={page === 'foreignremittance' ? 'active' : ''} onClick={() => setPage('foreignremittance')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="2" y1="12" x2="22" y2="12"></line>
              <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path>
            </svg>
            Foreign Remittance (15CA/CB)
          </button>
        )}
        {can('relatedparty.read') && (
          <button className={page === 'relatedparty' ? 'active' : ''} onClick={() => setPage('relatedparty')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
              <circle cx="9" cy="7" r="4"></circle>
              <path d="M23 21v-2a4 4 0 0 0-3-3.87"></path>
              <path d="M16 3.13a4 4 0 0 1 0 7.75"></path>
            </svg>
            Related Party (Sec 188)
          </button>
        )}
        {can('dsc.read') && (
          <button className={page === 'dsc' ? 'active' : ''} onClick={() => setPage('dsc')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
              <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
            </svg>
            DSC Custody
          </button>
        )}
        {can('portal.read') && (
          <button className={page === 'portal' ? 'active' : ''} onClick={() => setPage('portal')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z"></path>
            </svg>
            Client Desk
          </button>
        )}
        {can('users.read') && (
          <button className={page === 'users' ? 'active' : ''} onClick={() => setPage('users')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
              <circle cx="9" cy="7" r="4"></circle>
              <path d="M23 21v-2a4 4 0 0 0-3-3.87"></path>
              <path d="M16 3.13a4 4 0 0 1 0 7.75"></path>
            </svg>
            Team
          </button>
        )}
        {can('settings.manage') && (
          <button className={page === 'settings' ? 'active' : ''} onClick={() => setPage('settings')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="3"></circle>
              <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path>
            </svg>
            Settings
          </button>
        )}
        {can('audit.read') && (
          <button className={page === 'audit' ? 'active' : ''} onClick={() => setPage('audit')}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>
            </svg>
            Audit Log
          </button>
        )}
        <button className={page === 'security' ? 'active' : ''} onClick={() => setPage('security')}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
            <circle cx="12" cy="7" r="4"></circle>
          </svg>
          My Account
        </button>
      </nav>

      {page === 'tasks' && can('tasks.read') && <TasksPage can={can} meId={user.id} />}
      {page === 'deadlines' && can('compliance.read') && <DeadlinesPage can={can} />}
      {page === 'clients' && <ClientsPage can={can} meId={user.id} />}
      {page === 'billing' && <BillingPage can={can} meId={user.id} />}
      {page === 'tax' && can('computations.run') && <TaxPage />}
      {page === 'audit-workspace' && can('audit.read') && <AuditWorkspacePage me={user} />}
      {page === 'gst-reconcile' && can('gst.read') && <GstReconciliationPage />}
      {page === 'tds' && can('tds.read') && <TdsPage />}
      {page === 'mca' && can('mca.read') && <McaCompliancePage />}
      {page === 'engagement' && can('engagement.read') && <EngagementKycPage />}
      {page === 'banking' && can('bank.read') && <BankFeedsPage />}
      {page === 'reports' && can('reports.read') && <ReportsPage can={can} />}
      {page === 'tax-notices' && can('notices.read') && <TaxNoticesPage can={can} />}
      {page === 'payroll' && can('payroll.read') && <PayrollPage can={can} />}
      {page === 'einvoicing' && can('einvoice.read') && <EInvoicingPage can={can} />}
      {page === 'fixed-assets' && can('assets.read') && <FixedAssetsPage can={can} />}
      {page === 'advance-tax' && can('advancetax.read') && <AdvanceTaxPage can={can} />}
      {page === 'gstr9' && can('gstr9.read') && <Gstr9Page />}
      {page === 'presumptive' && can('presumptive.read') && <PresumptiveTaxPage can={can} />}
      {page === 'caro' && can('caro.read') && <CaroAuditDeskPage can={can} />}
      {page === 'taxaudit' && can('taxaudit.read') && <TaxAuditDeskPage can={can} />}
      {page === 'schedule3' && can('schedule3.read') && <Schedule3DeskPage can={can} />}
      {page === 'branchaudit' && can('branchaudit.read') && <BranchAuditDeskPage can={can} />}
      {page === 'auditcommittee' && can('auditcommittee.read') && <AuditCommitteeDeskPage can={can} />}
      {page === 'foreignremittance' && can('foreignremittance.read') && <ForeignRemittancePage />}
      {page === 'relatedparty' && can('relatedparty.read') && <RelatedPartyPage />}
      {page === 'dsc' && can('dsc.read') && <DscPage can={can} meId={user.id} />}
      {page === 'portal' && can('portal.read') && <PortalPage can={can} meId={user.id} />}
      {page === 'users' && can('users.read') && <UsersPage can={can} me={user} />}
      {page === 'settings' && can('settings.manage') && <SettingsPage />}
      {page === 'audit' && can('audit.read') && <AuditPage />}
      {page === 'security' && <SecurityPage me={user} onChanged={afterSecurityChange} />}
    </main>
  )
}
