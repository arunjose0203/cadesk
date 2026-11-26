import { lazy, Suspense, useCallback, useEffect, useRef, useState } from 'react'
import { api, onSessionEnded, refreshSession, setAccessToken, featuresApi, type AuthResult, type UserProfile } from './api'
import AuthScreens from './AuthScreens'
import SecurityPage from './SecurityPage'
import PublicConfirmationPortal from './PublicConfirmationPortal'
import { LandingPage } from './LandingPage'
import { OnboardingWizard } from './OnboardingWizard'
import { ProductTourModal } from './ProductTourModal'
import { buildNav, findDesk, TODAY_ICON, type NavItem, type Page } from './shell/nav'
import { getPlanDesks, lockedPlan, PLAN_NAMES, type PlanDesks } from './shell/plan'
import { PlanLocked } from './shell/PlanLocked'
import { LegalGate } from './legal/LegalGate'
import { startMonitoring } from './shell/monitor'
import { ReportProblem } from './shell/ReportProblem'
import { CommandPalette } from './shell/CommandPalette'
import { TodayPage } from './shell/TodayPage'
import { DeskBoundary } from './shell/DeskBoundary'
import { currentFinancialYear, pushRecent, useStored, useTheme } from './shell/prefs'

// Desks load on first visit, so signing in downloads only the shell and the open desk.
const ClientsPage = lazy(() => import('./ClientsPage'))
const UsersPage = lazy(() => import('./UsersPage'))
const SettingsPage = lazy(() => import('./SettingsPage'))
const AuditPage = lazy(() => import('./AuditPage'))
const DeadlinesPage = lazy(() => import('./DeadlinesPage'))
const TasksPage = lazy(() => import('./TasksPage'))
const BillingPage = lazy(() => import('./BillingPage'))
const DscPage = lazy(() => import('./DscPage'))
const PortalPage = lazy(() => import('./PortalPage'))
const TrialBalancePage = lazy(() => import('./TrialBalancePage'))
const AuditWorkspacePage = lazy(() => import('./AuditWorkspacePage').then(m => ({ default: m.AuditWorkspacePage })))
const TaxPage = lazy(() => import('./TaxPage').then(m => ({ default: m.TaxPage })))
const GstReconciliationPage = lazy(() => import('./GstReconciliationPage').then(m => ({ default: m.GstReconciliationPage })))
const TdsPage = lazy(() => import('./TdsPage'))
const McaCompliancePage = lazy(() => import('./McaCompliancePage'))
const EngagementKycPage = lazy(() => import('./EngagementKycPage'))
const BankFeedsPage = lazy(() => import('./BankFeedsPage').then(m => ({ default: m.BankFeedsPage })))
const ReportsPage = lazy(() => import('./ReportsPage').then(m => ({ default: m.ReportsPage })))
const TaxNoticesPage = lazy(() => import('./TaxNoticesPage').then(m => ({ default: m.TaxNoticesPage })))
const PayrollPage = lazy(() => import('./PayrollPage').then(m => ({ default: m.PayrollPage })))
const EInvoicingPage = lazy(() => import('./EInvoicingPage').then(m => ({ default: m.EInvoicingPage })))
const FixedAssetsPage = lazy(() => import('./FixedAssetsPage').then(m => ({ default: m.FixedAssetsPage })))
const AdvanceTaxPage = lazy(() => import('./AdvanceTaxPage').then(m => ({ default: m.AdvanceTaxPage })))
const Gstr1ExportPage = lazy(() => import('./Gstr1ExportPage'))
const Gstr9Page = lazy(() => import('./Gstr9Page').then(m => ({ default: m.Gstr9Page })))
const PresumptiveTaxPage = lazy(() => import('./PresumptiveTaxPage').then(m => ({ default: m.PresumptiveTaxPage })))
const CaroAuditDeskPage = lazy(() => import('./CaroAuditDeskPage').then(m => ({ default: m.CaroAuditDeskPage })))
const TaxAuditDeskPage = lazy(() => import('./TaxAuditDeskPage').then(m => ({ default: m.TaxAuditDeskPage })))
const Schedule3DeskPage = lazy(() => import('./Schedule3DeskPage').then(m => ({ default: m.Schedule3DeskPage })))
const BranchAuditDeskPage = lazy(() => import('./BranchAuditDeskPage').then(m => ({ default: m.BranchAuditDeskPage })))
const AuditCommitteeDeskPage = lazy(() => import('./AuditCommitteeDeskPage').then(m => ({ default: m.AuditCommitteeDeskPage })))
const ForeignRemittancePage = lazy(() => import('./ForeignRemittancePage').then(m => ({ default: m.ForeignRemittancePage })))
const RelatedPartyPage = lazy(() => import('./RelatedPartyPage').then(m => ({ default: m.RelatedPartyPage })))
const InternalAuditPage = lazy(() => import('./InternalAuditPage').then(m => ({ default: m.InternalAuditPage })))
const AntiProfiteeringPage = lazy(() => import('./AntiProfiteeringPage').then(m => ({ default: m.AntiProfiteeringPage })))
const SecretarialAuditPage = lazy(() => import('./SecretarialAuditPage').then(m => ({ default: m.SecretarialAuditPage })))
const TransferPricingPage = lazy(() => import('./TransferPricingPage').then(m => ({ default: m.TransferPricingPage })))
const CostAuditPage = lazy(() => import('./CostAuditPage'))
const SubscriptionsPage = lazy(() => import('./SubscriptionsPage'))
const AqmmDeskPage = lazy(() => import('./AqmmDeskPage'))
const CommunicationsGatewayPage = lazy(() => import('./CommunicationsGatewayPage'))
const SecurityComplianceVaultPage = lazy(() => import('./SecurityComplianceVaultPage'))
const PartnerMobileDeskPage = lazy(() => import('./PartnerMobileDeskPage'))
const UaeCorporateTaxDesk = lazy(() => import('./UaeCorporateTaxDesk'))
const PracticeHealthDeskPage = lazy(() => import('./PracticeHealthDeskPage'))
const LedgerSyncDeskPage = lazy(() => import('./LedgerSyncDeskPage').then(m => ({ default: m.LedgerSyncDeskPage })))
const TaxLitigationDeskPage = lazy(() => import('./TaxLitigationDeskPage').then(m => ({ default: m.TaxLitigationDeskPage })))
const EsgCsrDeskPage = lazy(() => import('./EsgCsrDeskPage').then(m => ({ default: m.EsgCsrDeskPage })))
const IbcCirpDeskPage = lazy(() => import('./IbcCirpDeskPage').then(m => ({ default: m.IbcCirpDeskPage })))

function FeatureDisabledNotice({ featureName }: { featureName: string }) {
  return (
    <div className="feature-off">
      <h2>{featureName} is switched off for your firm</h2>
      <p>A partner can switch it on under Settings, in the modules section.</p>
    </div>
  )
}

function pageFromUrl(): Page {
  const q = new URLSearchParams(window.location.search)
  if (q.get('pwa') === 'partner') return 'partner-desk'
  return (q.get('page') as Page | null) ?? 'today'
}

const ThemeIcon = ({ theme }: { theme: 'system' | 'light' | 'dark' }) => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    {theme === 'light' && <><circle cx="12" cy="12" r="4"></circle><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"></path></>}
    {theme === 'dark' && <path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z"></path>}
    {theme === 'system' && <><rect x="2" y="4" width="20" height="13" rx="2"></rect><path d="M8 21h8M12 17v4"></path></>}
  </svg>
)

const Chevron = () => (
  <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <polyline points="6 9 12 15 18 9"></polyline>
  </svg>
)

const PinIcon = ({ on }: { on: boolean }) => (
  <svg width="13" height="13" viewBox="0 0 24 24" fill={on ? 'currentColor' : 'none'} stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="M12 17v5M9 3h6l-1 7 4 3v2H6v-2l4-3z"></path>
  </svg>
)

export default function App() {
  // Public SA 505 confirmation links need no workspace, sign-in or hooks.
  const isConfirmLink = window.location.pathname.startsWith('/confirm') || new URLSearchParams(window.location.search).has('confirm')
  return isConfirmLink ? <PublicConfirmationPortal /> : <Workspace />
}

function initialPublicView(): 'landing' | 'auth' | 'wizard' {
  const q = new URLSearchParams(window.location.search)
  if (q.has('invite') || q.get('auth') === 'signin' || q.get('auth') === 'signup') return 'auth'
  if (q.get('trial') === '1') return 'wizard'
  return 'landing'
}

function Workspace() {
  const [user, setUser] = useState<UserProfile | null>(null)
  const [features, setFeatures] = useState<Record<string, boolean>>({})
  const [plan, setPlan] = useState<PlanDesks | null>(null)
  const [reporting, setReporting] = useState(false)
  const [checking, setChecking] = useState(true)
  const [page, setPageState] = useState<Page>(pageFromUrl)
  const [notice, setNotice] = useState<string | null>(null)
  const [publicView, setPublicView] = useState<'landing' | 'auth' | 'wizard'>(initialPublicView)
  const [showTour, setShowTour] = useState<boolean>(false)
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const [paletteOpen, setPaletteOpen] = useState(false)
  const [collapsed, setCollapsed] = useStored<Record<string, boolean>>('cadesk.nav.collapsed', { audit: true, corporate: true, admin: true })
  const [pinned, setPinned] = useStored<Page[]>('cadesk.nav.pinned', [])
  const [recents, setRecents] = useStored<Page[]>('cadesk.nav.recent', [])
  const [theme, cycleTheme] = useTheme()
  const idleTimer = useRef<number | undefined>(undefined)

  // Each desk has its own address (?page=gstr9), so links can be shared and Back works.
  const setPage = useCallback((next: Page) => {
    setPageState(next)
    setRecents(r => pushRecent(r, next))
    const url = new URL(window.location.href)
    url.searchParams.delete('pwa')
    if (next === 'today') url.searchParams.delete('page')
    else url.searchParams.set('page', next)
    if (url.href !== window.location.href) history.pushState({ page: next }, '', url)
    window.scrollTo(0, 0)
  }, [setRecents])

  useEffect(() => {
    const onPop = () => setPageState(pageFromUrl())
    window.addEventListener('popstate', onPop)
    return () => window.removeEventListener('popstate', onPop)
  }, [])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        setPaletteOpen(o => !o)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  useEffect(() => {
    if (user) {
      featuresApi.getFeatures()
        .then(res => setFeatures(res))
        .catch(() => setFeatures({}))
      getPlanDesks().then(setPlan).catch(() => setPlan(null))
      startMonitoring()
    }
  }, [user])

  const isFeatureEnabled = (key: string): boolean => {
    if (key in features) return Boolean(features[key])
    const disabledByDefault = [
      'banking', 'connected-banking', 'einvoicing', 'tax-copilot',
      'uae-tax', 'practice-health', 'ledger-sync', 'tax-litigation',
      'esg-csr', 'ibc-cirp'
    ]
    if (disabledByDefault.includes(key.toLowerCase())) return false
    return true
  }

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
    history.replaceState(null, '', page === 'today' ? '/' : `/?page=${page}`) // drop ?invite=..., keep the desk
  }

  const signOut = async (reason?: string) => {
    await api('/api/auth/logout', { method: 'POST' }).catch(() => { })
    setAccessToken(null)
    setUser(null)
    setNotice(reason ?? null)
    setPublicView('auth')
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
      <>
        {publicView === 'landing' && (
          <LandingPage
            onStartTrial={() => setPublicView('wizard')}
            onSignIn={() => setPublicView('auth')}
            onOpenTour={() => setShowTour(true)}
          />
        )}
        {publicView === 'wizard' && (
          <OnboardingWizard
            onSignedIn={signedIn}
            onCancel={() => setPublicView('landing')}
          />
        )}
        {publicView === 'auth' && (
          <main className="auth-shell">
            <div className="auth-split">
              <div className="auth-brand-panel">
                <div className="stack" style={{ gap: '18px' }}>
                  <div className="row" style={{ gap: '12px' }}>
                    <div className="brand-icon">CA</div>
                    <div>
                      <div style={{ fontSize: '1.15rem', fontWeight: 700, letterSpacing: '-0.02em' }}>CaDesk</div>
                      <div style={{ fontSize: '0.76rem', color: '#93c5fd', fontWeight: 500 }}>Practice Software for Indian CAs</div>
                    </div>
                  </div>
                  <div>
                    <h1 style={{ fontSize: '1.65rem', lineHeight: 1.25, margin: '8px 0 12px' }}>
                      One disciplined ledger for your entire practice
                    </h1>
                    <p style={{ fontSize: '0.88rem', color: '#cbd5e1', lineHeight: 1.6, margin: 0 }}>
                      Manage client registrations, GSTR-1 &amp; 3B exports, CBDT ITR &amp; Form 3CD filings, TDS RPU returns, statutory audit dossiers, and online fee collection in one workspace.
                    </p>
                  </div>
                </div>
                <div className="auth-trust-box">
                  <div style={{ fontWeight: 700, color: '#7dd3fc', textTransform: 'uppercase', fontSize: '0.72rem', letterSpacing: '0.06em', marginBottom: '8px' }}>
                    Security &amp; Data Residency
                  </div>
                  <div style={{ color: '#e2e8f0' }}>
                    Hosted in India &middot; Two-Lock PostgreSQL Row-Level Isolation &middot; Authenticator 2FA &middot; DPDP Act 2023 Aligned
                  </div>
                </div>
              </div>
              <div className="auth-form-panel">
                <div className="row spread" style={{ marginBottom: '18px' }}>
                  <button
                    className="ghost small"
                    onClick={() => setPublicView('landing')}
                    style={{ cursor: 'pointer' }}
                  >
                    ← Back to the CaDesk home page
                  </button>
                  <span style={{ fontSize: '0.78rem', color: 'var(--muted)', fontWeight: 500 }}>India GST / ITR / TDS</span>
                </div>
                {notice && <p className="notice small" style={{ marginBottom: '16px' }}>{notice}</p>}
                <AuthScreens onSignedIn={signedIn} />
              </div>
            </div>
          </main>
        )}
        <ProductTourModal
          isOpen={showTour}
          onClose={() => setShowTour(false)}
          onStartTrial={() => {
            setShowTour(false)
            setPublicView('wizard')
          }}
        />
      </>
    )
  }

  if (user.mfaSetupRequired) {
    return (
      <main className="page">
        <header className="top">
          <div className="brand-badge">
            <div className="brand-icon">CA</div>
            <div className="brand-meta">
              <h1>CaDesk</h1>
              <span className="firm-pill">{user.firmName}</span>
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

  // Plain calls, not hooks: this runs after the early returns above.
  const nav = buildNav(can, isFeatureEnabled)
  const todayItem: NavItem = { id: 'today', label: 'Today', icon: TODAY_ICON, visible: true }
  const visibleDesks = nav.flatMap(c => c.items.filter(i => i.visible))
  const deskById = (id: Page) => visibleDesks.find(i => i.id === id)
  const found = findDesk(nav, page)
  const current = page === 'today'
    ? { title: 'Today', category: user.firmName }
    : found && found.item.visible
      ? { title: found.item.label, category: found.category.title }
      : null
  const pinnedItems = pinned.map(deskById).filter((i): i is NavItem => Boolean(i))
  const quickDesks = [...pinnedItems, ...recents.map(deskById).filter((i): i is NavItem => Boolean(i) && !pinned.includes(i!.id))].slice(0, 8)
  const { fy, ay } = currentFinancialYear()
  const themeLabel = theme === 'system' ? 'Theme: follows your computer' : theme === 'light' ? 'Theme: light' : 'Theme: dark'

  const go = (p: Page) => {
    setPage(p)
    setSidebarOpen(false)
    setPaletteOpen(false)
  }
  const togglePin = (id: Page) => setPinned(list => list.includes(id) ? list.filter(p => p !== id) : [...list, id])

  const navButton = (item: NavItem) => (
    <div key={item.id} style={{ position: 'relative' }}>
      <button
        className={`sidebar-nav-btn ${page === item.id ? 'active' : ''}`}
        aria-current={page === item.id ? 'page' : undefined}
        onClick={() => go(item.id)}
      >
        <span className="sidebar-btn-icon">{item.icon}</span>
        <span className="sidebar-btn-label">{item.label}</span>
        {lockedPlan(plan, item.id) && <span className="sidebar-plan-badge" title={`In the ${PLAN_NAMES[lockedPlan(plan, item.id)!]} plan`}>{PLAN_NAMES[lockedPlan(plan, item.id)!]}</span>}
        {item.id !== 'today' && (
          <span
            role="button"
            tabIndex={0}
            className="sidebar-pin"
            aria-pressed={pinned.includes(item.id)}
            aria-label={pinned.includes(item.id) ? `Unpin ${item.label}` : `Pin ${item.label}`}
            title={pinned.includes(item.id) ? 'Unpin' : 'Pin to the top'}
            onClick={e => { e.stopPropagation(); togglePin(item.id) }}
            onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); e.stopPropagation(); togglePin(item.id) } }}
          >
            <PinIcon on={pinned.includes(item.id)} />
          </span>
        )}
      </button>
    </div>
  )

  return (
    <div className="app-shell">
      {sidebarOpen && <div className="app-sidebar-backdrop" onClick={() => setSidebarOpen(false)} aria-hidden="true" />}

      <aside className={`app-sidebar ${sidebarOpen ? 'open' : ''}`} aria-label="Desks">
        <div className="app-sidebar-header">
          <div className="sidebar-brand">
            <div className="sidebar-brand-left">
              <div className="sidebar-brand-icon" aria-hidden="true">CA</div>
              <div className="sidebar-brand-text">
                <span className="sidebar-brand-title">CaDesk</span>
                <span className="sidebar-brand-subtitle">Practice OS</span>
              </div>
            </div>
            <span className="sidebar-pro-pill">{plan?.planTier ?? 'PRO'}</span>
          </div>
          <div className="sidebar-firm-card" title={user.firmName}>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="M3 21h18M5 21V7l7-4 7 4v14M9 21v-6h6v6"></path>
            </svg>
            <span className="sidebar-firm-name">{user.firmName}</span>
          </div>
        </div>

        <div className="sidebar-search-box">
          <button type="button" className="sidebar-find-btn" onClick={() => setPaletteOpen(true)}>
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" aria-hidden="true">
              <circle cx="11" cy="11" r="8"></circle>
              <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
            </svg>
            <span>Find a desk</span>
            <span className="kbd">Ctrl K</span>
          </button>
        </div>

        <nav className="app-sidebar-nav" aria-label="Main navigation">
          <div className="sidebar-section">
            {navButton(todayItem)}
            {pinnedItems.map(navButton)}
          </div>
          {nav.map(category => {
            const items = category.items.filter(i => i.visible)
            if (items.length === 0) return null
            const isCollapsed = Boolean(collapsed[category.id]) && !items.some(i => i.id === page)
            return (
              <div key={category.id} className="sidebar-section">
                <button
                  type="button"
                  className="sidebar-section-title"
                  aria-expanded={!isCollapsed}
                  onClick={() => setCollapsed(c => ({ ...c, [category.id]: !isCollapsed }))}
                >
                  <span>{category.title}</span>
                  <span className="row" style={{ gap: 6 }}>
                    {isCollapsed && <span className="sidebar-section-count">{items.length}</span>}
                    <Chevron />
                  </span>
                </button>
                {!isCollapsed && items.map(navButton)}
              </div>
            )
          })}
        </nav>

        <div className="sidebar-footer">
          <button type="button" className="sidebar-user-info" onClick={() => go('security')} title="My account" style={{ background: 'none', border: 0, padding: 0, cursor: 'pointer', textAlign: 'left', font: 'inherit' }}>
            <span className="sidebar-user-avatar" aria-hidden="true">{initials}</span>
            <span className="sidebar-user-meta">
              <span className="sidebar-user-name">{user.name}</span>
              <span className="sidebar-user-role">{user.roles[0] || 'Staff'}</span>
            </span>
          </button>
          <a className="sidebar-icon-btn" href="/help/index.html" target="_blank" rel="noopener noreferrer" title="Help centre" aria-label="Help centre">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <circle cx="12" cy="12" r="10"></circle><path d="M9.1 9a3 3 0 0 1 5.8 1c0 2-3 3-3 3"></path><path d="M12 17h.01"></path>
            </svg>
          </a>
          <button type="button" className="sidebar-icon-btn" onClick={() => setReporting(true)} title="Report a problem" aria-label="Report a problem">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z"></path><path d="M4 22v-7"></path>
            </svg>
          </button>
          <button type="button" className="sidebar-icon-btn" onClick={cycleTheme} title={themeLabel} aria-label={themeLabel}>
            <ThemeIcon theme={theme} />
          </button>
          <button type="button" className="sidebar-icon-btn" onClick={() => signOut()} title="Sign out" aria-label="Sign out">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"></path>
              <polyline points="16 17 21 12 16 7"></polyline>
              <line x1="21" y1="12" x2="9" y2="12"></line>
            </svg>
          </button>
        </div>
      </aside>

      <div className="app-main">
        <header className="app-header">
          <div className="app-header-left">
            <button className="mobile-hamburger-btn" onClick={() => setSidebarOpen(o => !o)} aria-label="Show desks">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <line x1="3" y1="12" x2="21" y2="12"></line>
                <line x1="3" y1="6" x2="21" y2="6"></line>
                <line x1="3" y1="18" x2="21" y2="18"></line>
              </svg>
            </button>
            <div className="app-breadcrumb">
              {current && page !== 'today' && (
                <>
                  <span className="app-breadcrumb-category">{current.category}</span>
                  <span className="app-breadcrumb-sep" aria-hidden="true">/</span>
                </>
              )}
              <span className="app-breadcrumb-active">{current ? current.title : 'Desk not available'}</span>
            </div>
          </div>
          <div className="app-header-right">
            <span className="fy-badge" title={`Assessment year ${ay}`}>FY <b>{fy}</b></span>
            <button type="button" className="header-find-btn" onClick={() => setPaletteOpen(true)}>
              <span className="row" style={{ gap: 8 }}>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
                  <circle cx="11" cy="11" r="8"></circle>
                  <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
                </svg>
                <span>Find a desk</span>
              </span>
              <span className="kbd">Ctrl K</span>
            </button>
          </div>
        </header>

        <main className="app-content">
          {!current && (
            <div className="feature-off">
              <h2>This desk isn't available</h2>
              <p>The link may be old, or your role doesn't include this desk. Ask a partner if you need access.</p>
              <p><button className="small" onClick={() => go('today')}>Go to Today</button></p>
            </div>
          )}
          {current && page !== 'today' && lockedPlan(plan, page) && plan && (
            <PlanLocked desk={current.title} needs={lockedPlan(plan, page)!} current={plan.planTier}
              canUpgrade={can('settings.manage')} onUpgrade={() => go('subscriptions')} />
          )}
          {!(page !== 'today' && lockedPlan(plan, page)) && (
          <DeskBoundary deskName={current?.title ?? 'This desk'} resetKey={page} onHome={() => go('today')}>
          <Suspense fallback={<div className="app-loading">Opening {current?.title ?? 'desk'}…</div>}>
            {page === 'today' && <TodayPage name={user.name} can={can} quickDesks={quickDesks} onOpen={go} />}
            {page === 'tasks' && can('tasks.read') && <TasksPage can={can} meId={user.id} />}
            {page === 'deadlines' && can('compliance.read') && <DeadlinesPage can={can} />}
            {page === 'clients' && <ClientsPage can={can} meId={user.id} />}
            {page === 'billing' && <BillingPage can={can} meId={user.id} />}
            {page === 'trial-balance' && can('trialbalance.read') && <TrialBalancePage can={can} />}
            {page === 'tax' && can('computations.run') && <TaxPage />}
            {page === 'audit-workspace' && can('audit.read') && <AuditWorkspacePage me={user} />}
            {page === 'aqmm' && can('audit.read') && <AqmmDeskPage />}
            {page === 'gst-reconcile' && can('gst.read') && <GstReconciliationPage />}
            {page === 'tds' && can('tds.read') && <TdsPage />}
            {page === 'mca' && can('mca.read') && <McaCompliancePage />}
            {page === 'engagement' && can('engagement.read') && <EngagementKycPage />}
            {page === 'banking' && can('bank.read') && <BankFeedsPage isFeatureEnabled={isFeatureEnabled} />}
            {page === 'ledger-sync' && (can('ledgersync.read') || can('bank.read')) && (isFeatureEnabled('ledger-sync') ? <LedgerSyncDeskPage /> : <FeatureDisabledNotice featureName="Ledger Sync & Auto-Reconciliation" />)}
            {page === 'reports' && can('reports.read') && <ReportsPage can={can} />}
            {page === 'practice-health' && can('practicehealth.read') && (isFeatureEnabled('practice-health') ? <PracticeHealthDeskPage /> : <FeatureDisabledNotice featureName="Practice Health & Valuation" />)}
            {page === 'tax-notices' && can('notices.read') && <TaxNoticesPage can={can} isFeatureEnabled={isFeatureEnabled} />}
            {page === 'tax-litigation' && (can('taxlitigation.read') || can('notices.read')) && (isFeatureEnabled('tax-litigation') ? <TaxLitigationDeskPage /> : <FeatureDisabledNotice featureName="ITAT & High Court Appeals" />)}
            {page === 'esg-csr' && (can('esgcsr.read') || can('caro.read') || can('audit.read')) && (isFeatureEnabled('esg-csr') ? <EsgCsrDeskPage /> : <FeatureDisabledNotice featureName="SEBI BRSR & CSR Statutory Audit" />)}
            {page === 'ibc-cirp' && (can('ibccirp.read') || can('audit.read')) && (isFeatureEnabled('ibc-cirp') ? <IbcCirpDeskPage can={can} /> : <FeatureDisabledNotice featureName="IBC 2016 Insolvency & CIRP Desk" />)}
            {page === 'payroll' && can('payroll.read') && <PayrollPage can={can} />}
            {page === 'einvoicing' && can('einvoice.read') && (isFeatureEnabled('einvoicing') ? <EInvoicingPage can={can} /> : <FeatureDisabledNotice featureName="E-Invoicing & E-Way Bill" />)}
            {page === 'fixed-assets' && can('assets.read') && <FixedAssetsPage can={can} />}
            {page === 'advance-tax' && can('advancetax.read') && <AdvanceTaxPage can={can} />}
            {page === 'gstr9' && can('gstr9.read') && <Gstr9Page />}
            {page === 'gstr1-export' && can('gst.read') && <Gstr1ExportPage />}
            {page === 'presumptive' && can('presumptive.read') && <PresumptiveTaxPage can={can} />}
            {page === 'caro' && can('caro.read') && <CaroAuditDeskPage can={can} />}
            {page === 'taxaudit' && can('taxaudit.read') && <TaxAuditDeskPage can={can} />}
            {page === 'schedule3' && can('schedule3.read') && <Schedule3DeskPage can={can} />}
            {page === 'branchaudit' && can('branchaudit.read') && <BranchAuditDeskPage can={can} />}
            {page === 'auditcommittee' && can('auditcommittee.read') && <AuditCommitteeDeskPage can={can} />}
            {page === 'foreignremittance' && can('foreignremittance.read') && <ForeignRemittancePage />}
            {page === 'relatedparty' && can('relatedparty.read') && <RelatedPartyPage />}
            {page === 'internalaudit' && can('internalaudit.read') && <InternalAuditPage />}
            {page === 'antiprofiteering' && can('antiprofiteering.read') && <AntiProfiteeringPage />}
            {page === 'secretarialaudit' && can('secretarialaudit.read') && <SecretarialAuditPage />}
            {page === 'transferpricing' && can('transferpricing.read') && <TransferPricingPage />}
            {page === 'uae-tax' && can('uaecorporatetax.read') && (isFeatureEnabled('uae-tax') ? <UaeCorporateTaxDesk /> : <FeatureDisabledNotice featureName="UAE Corporate Tax" />)}
            {page === 'costaudit' && can('costaudit.read') && <CostAuditPage />}
            {page === 'dsc' && can('dsc.read') && <DscPage can={can} meId={user.id} />}
            {page === 'portal' && can('portal.read') && <PortalPage can={can} meId={user.id} />}
            {page === 'users' && can('users.read') && <UsersPage can={can} me={user} />}
            {page === 'subscriptions' && can('settings.manage') && <SubscriptionsPage />}
            {page === 'communications' && can('settings.manage') && <CommunicationsGatewayPage />}
            {page === 'security-compliance' && can('settings.manage') && <SecurityComplianceVaultPage />}
            {page === 'partner-desk' && <PartnerMobileDeskPage />}
            {page === 'settings' && can('settings.manage') && <SettingsPage />}
            {page === 'audit' && can('audit.read') && <AuditPage />}
            {page === 'security' && <SecurityPage me={user} onChanged={afterSecurityChange} />}
          </Suspense>
          </DeskBoundary>
          )}
        </main>
      </div>

      <LegalGate onSignOut={() => signOut()} />
      {reporting && <ReportProblem deskName={current?.title ?? 'Unknown desk'} onClose={() => setReporting(false)} />}
      {paletteOpen && (
        <CommandPalette
          nav={[{ id: 'home', title: 'Home', items: [todayItem] }, ...nav]}
          recents={recents}
          onPick={go}
          onClose={() => setPaletteOpen(false)}
        />
      )}
    </div>
  )
}
