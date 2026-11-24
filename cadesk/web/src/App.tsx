import { useEffect, useRef, useState } from 'react'
import { api, onSessionEnded, refreshSession, setAccessToken, featuresApi, type AuthResult, type UserProfile } from './api'
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
import TrialBalancePage from './TrialBalancePage'
import { AuditWorkspacePage } from './AuditWorkspacePage'
import { TaxPage } from './TaxPage'
import { GstReconciliationPage } from './GstReconciliationPage'
import TdsPage from './TdsPage'
import McaCompliancePage from './McaCompliancePage'
import EngagementKycPage from './EngagementKycPage'
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
import { InternalAuditPage } from './InternalAuditPage'
import { AntiProfiteeringPage } from './AntiProfiteeringPage'
import { SecretarialAuditPage } from './SecretarialAuditPage'
import { TransferPricingPage } from './TransferPricingPage'
import CostAuditPage from './CostAuditPage'
import PublicConfirmationPortal from './PublicConfirmationPortal'
import SubscriptionsPage from './SubscriptionsPage'
import AqmmDeskPage from './AqmmDeskPage'
import CommunicationsGatewayPage from './CommunicationsGatewayPage'
import SecurityComplianceVaultPage from './SecurityComplianceVaultPage'
import PartnerMobileDeskPage from './PartnerMobileDeskPage'
import UaeCorporateTaxDesk from './UaeCorporateTaxDesk'
import PracticeHealthDeskPage from './PracticeHealthDeskPage'
import { LedgerSyncDeskPage } from './LedgerSyncDeskPage'
import { TaxLitigationDeskPage } from './TaxLitigationDeskPage'
import { EsgCsrDeskPage } from './EsgCsrDeskPage'
import { IbcCirpDeskPage } from './IbcCirpDeskPage'
import { LandingPage } from './LandingPage'
import { OnboardingWizard } from './OnboardingWizard'
import { ProductTourModal } from './ProductTourModal'

function FeatureDisabledNotice({ featureName }: { featureName: string }) {
  return (
    <div style={{ padding: '60px 20px', textAlign: 'center', maxWidth: '600px', margin: '40px auto', background: '#f8fafc', borderRadius: '12px', border: '1px solid #e2e8f0' }}>
      <div style={{ fontSize: '48px', marginBottom: '16px' }}>🔒</div>
      <h2 style={{ fontSize: '20px', fontWeight: 700, color: '#0f172a', marginBottom: '8px' }}>
        {featureName} is Not Enabled
      </h2>
      <p style={{ color: '#64748b', fontSize: '14px', lineHeight: 1.5, marginBottom: '20px' }}>
        This module is currently disabled for your firm under firm security settings. Please contact your Managing Partner or firm administrator to enable access.
      </p>
      <div style={{ display: 'inline-block', fontSize: '12px', background: '#e2e8f0', color: '#475569', padding: '4px 10px', borderRadius: '4px', fontWeight: 600 }}>
        HTTP 404 / Feature Gated
      </div>
    </div>
  )
}

type Page = 'tasks' | 'deadlines' | 'clients' | 'billing' | 'trial-balance' | 'tax' | 'audit-workspace' | 'aqmm' | 'gst-reconcile' | 'tds' | 'mca' | 'engagement' | 'banking' | 'ledger-sync' | 'reports' | 'practice-health' | 'tax-notices' | 'tax-litigation' | 'payroll' | 'einvoicing' | 'fixed-assets' | 'advance-tax' | 'gstr9' | 'presumptive' | 'caro' | 'taxaudit' | 'schedule3' | 'branchaudit' | 'auditcommittee' | 'foreignremittance' | 'relatedparty' | 'internalaudit' | 'antiprofiteering' | 'secretarialaudit' | 'transferpricing' | 'uae-tax' | 'costaudit' | 'dsc' | 'portal' | 'users' | 'settings' | 'audit' | 'security' | 'subscriptions' | 'communications' | 'security-compliance' | 'partner-desk' | 'esg-csr' | 'ibc-cirp'

export default function App() {
  const isConfirmLink = window.location.pathname.startsWith('/confirm') || new URLSearchParams(window.location.search).has('confirm')
  if (isConfirmLink) {
    return <PublicConfirmationPortal />
  }

  const [user, setUser] = useState<UserProfile | null>(null)
  const [features, setFeatures] = useState<Record<string, boolean>>({})
  const [checking, setChecking] = useState(true)
  const searchParams = new URLSearchParams(window.location.search)
  const initialPage = (searchParams.get('page') as Page) || (searchParams.get('pwa') === 'partner' ? 'partner-desk' : 'clients')
  const [page, setPage] = useState<Page>(initialPage)
  const [notice, setNotice] = useState<string | null>(null)
  const [publicView, setPublicView] = useState<'landing' | 'auth' | 'wizard'>('landing')
  const [showTour, setShowTour] = useState<boolean>(false)
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const [deskSearch, setDeskSearch] = useState('')
  const [collapsedCategories, setCollapsedCategories] = useState<Record<string, boolean>>({})
  const searchInputRef = useRef<HTMLInputElement>(null)
  const idleTimer = useRef<number | undefined>(undefined)

  const toggleCategory = (catId: string) => {
    setCollapsedCategories(prev => ({ ...prev, [catId]: !prev[catId] }))
  }

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        searchInputRef.current?.focus()
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [])

  useEffect(() => {
    if (user) {
      featuresApi.getFeatures()
        .then(res => setFeatures(res))
        .catch(() => setFeatures({}))
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
    history.replaceState(null, '', '/') // drop ?invite=...
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
          <main className="page" style={{ maxWidth: '480px', margin: '40px auto' }}>
            <button
              className="ghost small"
              onClick={() => setPublicView('landing')}
              style={{ marginBottom: '16px', cursor: 'pointer' }}
            >
              ← Back to CA Desk Home
            </button>
            <div style={{ textAlign: 'center', margin: '20px 0 24px' }}>
              <div className="brand-icon" style={{ margin: '0 auto 16px', width: '56px', height: '56px', fontSize: '24px' }}>CA</div>
              <h1 style={{ fontSize: '2rem', letterSpacing: '-0.03em', marginBottom: '8px' }}>CA Desk</h1>
              <p className="muted" style={{ fontSize: '1rem', maxWidth: '460px', margin: '0 auto' }}>
                Multi-Tenant Practice Management &amp; Regulatory Compliance for Accounting Firms
              </p>
            </div>
            {notice && <p className="notice small" style={{ maxWidth: '440px', margin: '0 auto 16px' }}>{notice}</p>}
            <AuthScreens onSignedIn={signedIn} />
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

  const PAGE_INFO: Record<Page, { title: string; category: string }> = {
    tasks: { title: 'Tasks', category: 'Core Practice' },
    deadlines: { title: 'Deadlines', category: 'Core Practice' },
    clients: { title: 'Clients', category: 'Core Practice' },
    billing: { title: 'Billing & Time', category: 'Core Practice' },
    'partner-desk': { title: 'Partner Desk (PWA)', category: 'Core Practice' },
    portal: { title: 'Client Desk', category: 'Core Practice' },

    'trial-balance': { title: 'Trial Balance Hub', category: 'Accounting & Ingestion' },
    banking: { title: 'Bank Feeds & BRS', category: 'Accounting & Ingestion' },
    'ledger-sync': { title: 'Tally/Busy Auto-Recon', category: 'Accounting & Ingestion' },
    'fixed-assets': { title: 'Fixed Assets & Depr', category: 'Accounting & Ingestion' },

    tax: { title: 'Tax Desk', category: 'Direct Tax & Compliance' },
    'advance-tax': { title: 'Advance Tax & 234', category: 'Direct Tax & Compliance' },
    presumptive: { title: 'Presumptive Tax (44AD)', category: 'Direct Tax & Compliance' },
    tds: { title: 'TDS Desk', category: 'Direct Tax & Compliance' },
    'tax-notices': { title: 'Tax Notices & Scrutiny', category: 'Direct Tax & Compliance' },
    'tax-litigation': { title: 'ITAT & HC Appeals', category: 'Direct Tax & Compliance' },
    transferpricing: { title: 'Transfer Pricing (Form 3CEB)', category: 'Direct Tax & Compliance' },

    'gst-reconcile': { title: 'GST 2B Match', category: 'GST & Indirect Taxes' },
    gstr9: { title: 'GSTR-9 / 9C Desk', category: 'GST & Indirect Taxes' },
    einvoicing: { title: 'E-Invoice & EWB', category: 'GST & Indirect Taxes' },
    antiprofiteering: { title: 'Anti-Profiteering (Sec 171)', category: 'GST & Indirect Taxes' },

    'audit-workspace': { title: 'Audit Workspace', category: 'Audit & Assurance' },
    aqmm: { title: 'AQMM Desk (ICAI)', category: 'Audit & Assurance' },
    caro: { title: 'CARO 2020 Desk', category: 'Audit & Assurance' },
    taxaudit: { title: 'Tax Audit (3CD)', category: 'Audit & Assurance' },
    schedule3: { title: 'Schedule III Desk', category: 'Audit & Assurance' },
    branchaudit: { title: 'Branch Audit (SA 600)', category: 'Audit & Assurance' },
    auditcommittee: { title: 'Audit Committee (SA 260)', category: 'Audit & Assurance' },
    internalaudit: { title: 'Internal Audit (RCM & IFC)', category: 'Audit & Assurance' },
    secretarialaudit: { title: 'Secretarial Audit (Sec 204)', category: 'Audit & Assurance' },
    costaudit: { title: 'Cost Audit (CRA 1-4)', category: 'Audit & Assurance' },
    'esg-csr': { title: 'ESG & CSR Audit', category: 'Audit & Assurance' },

    mca: { title: 'MCA / ROC', category: 'Corporate & Global' },
    engagement: { title: 'Engagements & KYC', category: 'Corporate & Global' },
    dsc: { title: 'DSC Custody', category: 'Corporate & Global' },
    foreignremittance: { title: 'Foreign Remittance (15CA/CB)', category: 'Corporate & Global' },
    relatedparty: { title: 'Related Party (Sec 188)', category: 'Corporate & Global' },
    'uae-tax': { title: 'UAE Corporate Tax', category: 'Corporate & Global' },
    'ibc-cirp': { title: 'IBC & CIRP Desk', category: 'Corporate & Global' },

    users: { title: 'Team', category: 'Firm Management' },
    'practice-health': { title: 'Valuation & Health', category: 'Firm Management' },
    reports: { title: 'MIS & Reports', category: 'Firm Management' },
    payroll: { title: 'Payroll & PF/ESI', category: 'Firm Management' },
    subscriptions: { title: 'Plan & Billing', category: 'Firm Management' },
    communications: { title: 'Communications', category: 'Firm Management' },
    'security-compliance': { title: 'Security & Trust Vault', category: 'Firm Management' },
    settings: { title: 'Settings', category: 'Firm Management' },
    audit: { title: 'Audit Log', category: 'Firm Management' },
    security: { title: 'My Account', category: 'Firm Management' }
  }

  type NavItem = {
    id: Page
    label: string
    icon: React.ReactNode
    visible: boolean
  }

  type NavCategory = {
    id: string
    title: string
    icon: string
    items: NavItem[]
  }

  const categories: NavCategory[] = [
    {
      id: 'practice',
      title: 'Core Practice',
      icon: '💼',
      items: [
        {
          id: 'tasks',
          label: 'Tasks',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"></path>
              <rect x="8" y="2" width="8" height="4" rx="1" ry="1"></rect>
              <path d="M9 14l2 2 4-4"></path>
            </svg>
          ),
          visible: can('tasks.read')
        },
        {
          id: 'deadlines',
          label: 'Deadlines',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
              <line x1="16" y1="2" x2="16" y2="6"></line>
              <line x1="8" y1="2" x2="8" y2="6"></line>
              <line x1="3" y1="10" x2="21" y2="10"></line>
            </svg>
          ),
          visible: can('compliance.read')
        },
        {
          id: 'clients',
          label: 'Clients',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="2" y="7" width="20" height="14" rx="2" ry="2"></rect>
              <path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16"></path>
            </svg>
          ),
          visible: true
        },
        {
          id: 'billing',
          label: 'Billing & Time',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="12" y1="1" x2="12" y2="23"></line>
              <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
            </svg>
          ),
          visible: true
        },
        {
          id: 'partner-desk',
          label: 'Partner Desk (PWA)',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="5" y="2" width="14" height="20" rx="2" ry="2"></rect>
              <line x1="12" y1="18" x2="12.01" y2="18"></line>
            </svg>
          ),
          visible: true
        },
        {
          id: 'portal',
          label: 'Client Desk',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z"></path>
            </svg>
          ),
          visible: can('portal.read')
        }
      ]
    },
    {
      id: 'accounting',
      title: 'Accounting & Data In',
      icon: '📊',
      items: [
        {
          id: 'trial-balance',
          label: 'Trial Balance Hub',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="20" x2="18" y2="10"></line>
              <line x1="12" y1="20" x2="12" y2="4"></line>
              <line x1="6" y1="20" x2="6" y2="14"></line>
            </svg>
          ),
          visible: can('trialbalance.read')
        },
        {
          id: 'banking',
          label: 'Bank Feeds & BRS',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="2" y="5" width="20" height="14" rx="2"></rect>
              <line x1="2" y1="10" x2="22" y2="10"></line>
            </svg>
          ),
          visible: can('bank.read')
        },
        {
          id: 'ledger-sync',
          label: 'Tally/Busy Auto-Recon',
          icon: <span style={{ fontSize: '13px', lineHeight: 1 }}>🔄</span>,
          visible: (can('ledgersync.read') || can('bank.read')) && isFeatureEnabled('ledger-sync')
        },
        {
          id: 'fixed-assets',
          label: 'Fixed Assets & Depr',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"></path>
              <polyline points="3.27 6.96 12 12.01 20.73 6.96"></polyline>
              <line x1="12" y1="22.08" x2="12" y2="12"></line>
            </svg>
          ),
          visible: can('assets.read')
        }
      ]
    },
    {
      id: 'tax',
      title: 'Direct Tax & Compliance',
      icon: '📑',
      items: [
        {
          id: 'tax',
          label: 'Tax Desk',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
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
          ),
          visible: can('computations.run')
        },
        {
          id: 'advance-tax',
          label: 'Advance Tax & 234',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10"></circle>
              <polyline points="12 6 12 12 16 14"></polyline>
            </svg>
          ),
          visible: can('advancetax.read')
        },
        {
          id: 'presumptive',
          label: 'Presumptive Tax (44AD)',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 2v20"></path>
              <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
            </svg>
          ),
          visible: can('presumptive.read')
        },
        {
          id: 'tds',
          label: 'TDS Desk',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="16" y1="13" x2="8" y2="13"></line>
              <line x1="16" y1="17" x2="8" y2="17"></line>
            </svg>
          ),
          visible: can('tds.read')
        },
        {
          id: 'tax-notices',
          label: 'Tax Notices & Scrutiny',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="12" y1="18" x2="12" y2="12"></line>
              <line x1="12" y1="9" x2="12.01" y2="9"></line>
            </svg>
          ),
          visible: can('notices.read')
        },
        {
          id: 'tax-litigation',
          label: 'ITAT & HC Appeals',
          icon: <span style={{ fontSize: '13px', lineHeight: 1 }}>🏛️</span>,
          visible: (can('taxlitigation.read') || can('notices.read')) && isFeatureEnabled('tax-litigation')
        },
        {
          id: 'transferpricing',
          label: 'Transfer Pricing (Form 3CEB)',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="2" y1="12" x2="22" y2="12"></line>
              <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path>
            </svg>
          ),
          visible: can('transferpricing.read')
        }
      ]
    },
    {
      id: 'gst',
      title: 'GST & Indirect Taxes',
      icon: '🧾',
      items: [
        {
          id: 'gst-reconcile',
          label: 'GST 2B Match',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M16 16v1a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2h11a2 2 0 0 1 2 2v1"></path>
              <path d="M18 8h4a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-4"></path>
              <polyline points="10 12 14 12 14 8"></polyline>
            </svg>
          ),
          visible: can('gst.read')
        },
        {
          id: 'gstr9',
          label: 'GSTR-9 / 9C Desk',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="16" y1="13" x2="8" y2="13"></line>
              <line x1="16" y1="17" x2="8" y2="17"></line>
            </svg>
          ),
          visible: can('gstr9.read')
        },
        {
          id: 'einvoicing',
          label: 'E-Invoice & EWB',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="1" y="3" width="15" height="13"></rect>
              <polygon points="16 8 20 8 23 11 23 16 16 16 16 8"></polygon>
              <circle cx="5.5" cy="18.5" r="2.5"></circle>
              <circle cx="18.5" cy="18.5" r="2.5"></circle>
            </svg>
          ),
          visible: can('einvoice.read') && isFeatureEnabled('einvoicing')
        },
        {
          id: 'antiprofiteering',
          label: 'Anti-Profiteering (Sec 171)',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M3 6l3 1m0 0l-3 9a5.002 5.002 0 006.001 0M6 7l3 9M6 7l6-2m6 2l3-1m-3 1l-3 9a5.002 5.002 0 006.001 0M18 7l3 9m-3-9l-6-2m0-2v2m0 16V5m0 16H9m3 0h3"></path>
            </svg>
          ),
          visible: can('antiprofiteering.read')
        }
      ]
    },
    {
      id: 'audit',
      title: 'Audit & Assurance',
      icon: '⚖️',
      items: [
        {
          id: 'audit-workspace',
          label: 'Audit Workspace',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="16" y1="13" x2="8" y2="13"></line>
              <line x1="16" y1="17" x2="8" y2="17"></line>
            </svg>
          ),
          visible: can('audit.read')
        },
        {
          id: 'aqmm',
          label: 'AQMM Desk (ICAI)',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>
              <path d="M9 12l2 2 4-4"></path>
            </svg>
          ),
          visible: can('audit.read')
        },
        {
          id: 'caro',
          label: 'CARO 2020 Desk',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="9" y1="15" x2="15" y2="15"></line>
            </svg>
          ),
          visible: can('caro.read')
        },
        {
          id: 'taxaudit',
          label: 'Tax Audit (3CD)',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="2" y="3" width="20" height="14" rx="2" ry="2"></rect>
              <line x1="8" y1="21" x2="16" y2="21"></line>
              <line x1="12" y1="17" x2="12" y2="21"></line>
            </svg>
          ),
          visible: can('taxaudit.read')
        },
        {
          id: 'schedule3',
          label: 'Schedule III Desk',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="20" x2="18" y2="10"></line>
              <line x1="12" y1="20" x2="12" y2="4"></line>
              <line x1="6" y1="20" x2="6" y2="14"></line>
            </svg>
          ),
          visible: can('schedule3.read')
        },
        {
          id: 'branchaudit',
          label: 'Branch Audit (SA 600)',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
              <circle cx="8.5" cy="7" r="4"></circle>
              <line x1="20" y1="8" x2="20" y2="14"></line>
              <line x1="23" y1="11" x2="17" y2="11"></line>
            </svg>
          ),
          visible: can('branchaudit.read')
        },
        {
          id: 'auditcommittee',
          label: 'Audit Committee (SA 260)',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M3 21h18"></path>
              <path d="M5 21V7l8-4v18"></path>
              <path d="M19 21V11l-6-4"></path>
              <circle cx="9" cy="9" r="1"></circle>
              <circle cx="9" cy="13" r="1"></circle>
            </svg>
          ),
          visible: can('auditcommittee.read')
        },
        {
          id: 'internalaudit',
          label: 'Internal Audit (RCM & IFC)',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>
              <path d="M9 12l2 2 4-4"></path>
            </svg>
          ),
          visible: can('internalaudit.read')
        },
        {
          id: 'secretarialaudit',
          label: 'Secretarial Audit (Sec 204)',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="16" y1="13" x2="8" y2="13"></line>
              <line x1="16" y1="17" x2="8" y2="17"></line>
            </svg>
          ),
          visible: can('secretarialaudit.read')
        },
        {
          id: 'costaudit',
          label: 'Cost Audit (CRA 1-4)',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M2 20h20"></path>
              <path d="M5 20V8l5 4V8l5 4V4l5 4v12"></path>
            </svg>
          ),
          visible: can('costaudit.read')
        },
        {
          id: 'esg-csr',
          label: 'ESG & CSR Audit',
          icon: <span style={{ fontSize: '13px', lineHeight: 1 }}>🌱</span>,
          visible: (can('esgcsr.read') || can('caro.read') || can('audit.read')) && isFeatureEnabled('esg-csr')
        }
      ]
    },
    {
      id: 'corporate',
      title: 'Corporate, Legal & Global',
      icon: '🏛️',
      items: [
        {
          id: 'mca',
          label: 'MCA / ROC',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M3 21h18"></path>
              <path d="M5 21V7l8-4v18"></path>
              <path d="M19 21V11l-6-4"></path>
              <line x1="9" y1="9" x2="9" y2="9.01"></line>
              <line x1="9" y1="13" x2="9" y2="13.01"></line>
              <line x1="9" y1="17" x2="9" y2="17.01"></line>
            </svg>
          ),
          visible: can('mca.read')
        },
        {
          id: 'engagement',
          label: 'Engagements & KYC',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="16" y1="13" x2="8" y2="13"></line>
              <line x1="16" y1="17" x2="8" y2="17"></line>
              <path d="M10 9H8"></path>
            </svg>
          ),
          visible: can('engagement.read')
        },
        {
          id: 'dsc',
          label: 'DSC Custody',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
              <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
            </svg>
          ),
          visible: can('dsc.read')
        },
        {
          id: 'foreignremittance',
          label: 'Foreign Remittance (15CA/CB)',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="2" y1="12" x2="22" y2="12"></line>
              <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path>
            </svg>
          ),
          visible: can('foreignremittance.read')
        },
        {
          id: 'relatedparty',
          label: 'Related Party (Sec 188)',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
              <circle cx="9" cy="7" r="4"></circle>
              <path d="M23 21v-2a4 4 0 0 0-3-3.87"></path>
              <path d="M16 3.13a4 4 0 0 1 0 7.75"></path>
            </svg>
          ),
          visible: can('relatedparty.read')
        },
        {
          id: 'uae-tax',
          label: 'UAE Corporate Tax',
          icon: <span style={{ fontSize: '13px', lineHeight: 1 }}>🇦🇪</span>,
          visible: can('uaecorporatetax.read') && isFeatureEnabled('uae-tax')
        },
        {
          id: 'ibc-cirp',
          label: 'IBC & CIRP Desk',
          icon: <span style={{ fontSize: '13px', lineHeight: 1 }}>⚖️</span>,
          visible: (can('ibccirp.read') || can('audit.read')) && isFeatureEnabled('ibc-cirp')
        }
      ]
    },
    {
      id: 'admin',
      title: 'Firm Management & Ops',
      icon: '⚙️',
      items: [
        {
          id: 'users',
          label: 'Team',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
              <circle cx="9" cy="7" r="4"></circle>
              <path d="M23 21v-2a4 4 0 0 0-3-3.87"></path>
              <path d="M16 3.13a4 4 0 0 1 0 7.75"></path>
            </svg>
          ),
          visible: can('users.read')
        },
        {
          id: 'practice-health',
          label: 'Valuation & Health',
          icon: <span style={{ fontSize: '13px', lineHeight: 1 }}>💎</span>,
          visible: can('practicehealth.read') && isFeatureEnabled('practice-health')
        },
        {
          id: 'reports',
          label: 'MIS & Reports',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="20" x2="18" y2="10"></line>
              <line x1="12" y1="20" x2="12" y2="4"></line>
              <line x1="6" y1="20" x2="6" y2="14"></line>
            </svg>
          ),
          visible: can('reports.read')
        },
        {
          id: 'payroll',
          label: 'Payroll & PF/ESI',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="2" y="4" width="20" height="16" rx="2"></rect>
              <line x1="6" y1="8" x2="10" y2="8"></line>
              <line x1="6" y1="12" x2="18" y2="12"></line>
              <line x1="6" y1="16" x2="14" y2="16"></line>
            </svg>
          ),
          visible: can('payroll.read')
        },
        {
          id: 'subscriptions',
          label: 'Plan & Billing',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="1" y="4" width="22" height="16" rx="2"></rect>
              <line x1="1" y1="10" x2="23" y2="10"></line>
            </svg>
          ),
          visible: can('settings.manage')
        },
        {
          id: 'communications',
          label: 'Communications',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path>
            </svg>
          ),
          visible: can('settings.manage')
        },
        {
          id: 'security-compliance',
          label: 'Security & Trust Vault',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>
            </svg>
          ),
          visible: can('settings.manage')
        },
        {
          id: 'settings',
          label: 'Settings',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="3"></circle>
              <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path>
            </svg>
          ),
          visible: can('settings.manage')
        },
        {
          id: 'audit',
          label: 'Audit Log',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>
            </svg>
          ),
          visible: can('audit.read')
        },
        {
          id: 'security',
          label: 'My Account',
          icon: (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
              <circle cx="12" cy="7" r="4"></circle>
            </svg>
          ),
          visible: true
        }
      ]
    }
  ]

  const currentMeta = PAGE_INFO[page] || { title: page, category: 'Desk' }

  return (
    <div className="app-shell">
      {/* Mobile Drawer Backdrop */}
      {sidebarOpen && (
        <div
          className="app-sidebar-backdrop"
          onClick={() => setSidebarOpen(false)}
          aria-hidden="true"
        />
      )}

      {/* Modern Executive Sidebar */}
      <aside className={`app-sidebar ${sidebarOpen ? 'open' : ''}`}>
        <div className="app-sidebar-header">
          <div className="sidebar-brand">
            <div className="sidebar-brand-icon">CA</div>
            <div className="sidebar-brand-text">
              <span className="sidebar-brand-title">CA Desk</span>
              <span className="sidebar-brand-subtitle">Chartered Accountants SaaS</span>
            </div>
          </div>
          <div className="sidebar-firm-card" title={user.firmName}>
            <span className="firm-icon">🏢</span>
            <span className="sidebar-firm-name">{user.firmName}</span>
            <span className="firm-verified" title="Verified Tenant">✓</span>
          </div>
        </div>

        {/* Quick Desk Search Filter */}
        <div className="sidebar-search-box">
          <div className="sidebar-search-wrapper">
            <svg className="sidebar-search-icon" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
              <circle cx="11" cy="11" r="8"></circle>
              <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
            </svg>
            <input
              ref={searchInputRef}
              type="text"
              className="sidebar-search-input"
              placeholder="Jump to desk... (Ctrl+K)"
              value={deskSearch}
              onChange={e => setDeskSearch(e.target.value)}
            />
            {deskSearch && (
              <button
                type="button"
                className="sidebar-search-clear"
                onClick={() => setDeskSearch('')}
                title="Clear search"
              >
                ✕
              </button>
            )}
          </div>
        </div>

        {/* Categorized Navigation */}
        <nav className="app-sidebar-nav tabs" aria-label="Main Navigation">
          {categories.map(category => {
            const visibleItems = category.items.filter(item => {
              if (!item.visible) return false
              if (!deskSearch.trim()) return true
              const q = deskSearch.toLowerCase().trim()
              return item.label.toLowerCase().includes(q) || category.title.toLowerCase().includes(q)
            })

            if (visibleItems.length === 0) return null

            const isCollapsed = Boolean(collapsedCategories[category.id] && !deskSearch.trim())

            return (
              <div key={category.id} className="sidebar-section">
                <div
                  className="sidebar-section-title"
                  onClick={() => !deskSearch.trim() && toggleCategory(category.id)}
                  style={{ cursor: deskSearch.trim() ? 'default' : 'pointer' }}
                >
                  <span>{category.icon} {category.title}</span>
                  {!deskSearch.trim() && (
                    <span style={{ fontSize: '10px', opacity: 0.6 }}>
                      {isCollapsed ? '▼' : '▲'}
                    </span>
                  )}
                </div>
                {!isCollapsed && visibleItems.map(item => (
                  <button
                    key={item.id}
                    className={`sidebar-nav-btn ${page === item.id ? 'active' : ''}`}
                    onClick={() => {
                      setPage(item.id)
                      setSidebarOpen(false)
                    }}
                  >
                    <span className="sidebar-btn-icon">{item.icon}</span>
                    <span className="sidebar-btn-label">{item.label}</span>
                    {page === item.id && <span className="sidebar-active-indicator" />}
                  </button>
                ))}
              </div>
            )
          })}
        </nav>

        {/* Sidebar Footer User Card */}
        <div className="sidebar-footer">
          <div className="sidebar-user-info">
            <div className="sidebar-user-avatar">{initials}</div>
            <div className="sidebar-user-meta">
              <span className="sidebar-user-name" title={user.name}>{user.name}</span>
              <span className="sidebar-user-role">{user.roles[0] || 'Staff'}</span>
            </div>
          </div>
          <button
            className="sidebar-signout-btn"
            onClick={() => signOut()}
            title="Sign out of your account"
          >
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"></path>
              <polyline points="16 17 21 12 16 7"></polyline>
              <line x1="21" y1="12" x2="9" y2="12"></line>
            </svg>
            <span className="sr-only">Sign out</span>
          </button>
        </div>
      </aside>

      {/* Main Layout Area */}
      <div className="app-main">
        {/* Modern Sticky Top Header */}
        <header className="app-header">
          <div className="app-header-left">
            <button
              className="mobile-hamburger-btn"
              onClick={() => setSidebarOpen(!sidebarOpen)}
              aria-label="Toggle navigation"
            >
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="3" y1="12" x2="21" y2="12"></line>
                <line x1="3" y1="6" x2="21" y2="6"></line>
                <line x1="3" y1="18" x2="21" y2="18"></line>
              </svg>
            </button>
            <div className="app-breadcrumb">
              <span className="app-breadcrumb-firm">🏢 {user.firmName}</span>
              <span className="app-breadcrumb-sep">/</span>
              <span className="app-breadcrumb-category">{currentMeta.category}</span>
              <span className="app-breadcrumb-sep">/</span>
              <span className="app-breadcrumb-active">{currentMeta.title}</span>
            </div>
          </div>

          <div className="app-header-right">
            <div className="fy-badge">
              <span className="badge-dot"></span>
              <span>FY 2024-25 (AY 2025-26)</span>
            </div>
            <div className="app-header-user">
              <div className="app-header-avatar">{initials}</div>
              <span className="app-header-username">{user.name}</span>
              <span className="role-tag">{user.roles[0] || 'Staff'}</span>
              <button
                className="ghost small"
                onClick={() => signOut()}
                title="Sign out of your account"
                style={{ marginLeft: '4px' }}
              >
                Sign out
              </button>
            </div>
          </div>
        </header>

        {/* Main Content Area */}
        <main className="app-content">
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
      {page === 'ledger-sync' && (can('ledgersync.read') || can('bank.read')) && (isFeatureEnabled('ledger-sync') ? <LedgerSyncDeskPage can={can} /> : <FeatureDisabledNotice featureName="Ledger Sync & Auto-Reconciliation" />)}
      {page === 'reports' && can('reports.read') && <ReportsPage can={can} />}
      {page === 'practice-health' && can('practicehealth.read') && (isFeatureEnabled('practice-health') ? <PracticeHealthDeskPage can={can} /> : <FeatureDisabledNotice featureName="Practice Health & Valuation" />)}
      {page === 'tax-notices' && can('notices.read') && <TaxNoticesPage can={can} isFeatureEnabled={isFeatureEnabled} />}
      {page === 'tax-litigation' && (can('taxlitigation.read') || can('notices.read')) && (isFeatureEnabled('tax-litigation') ? <TaxLitigationDeskPage can={can} /> : <FeatureDisabledNotice featureName="ITAT & High Court Appeals" />)}
      {page === 'esg-csr' && (can('esgcsr.read') || can('caro.read') || can('audit.read')) && (isFeatureEnabled('esg-csr') ? <EsgCsrDeskPage can={can} /> : <FeatureDisabledNotice featureName="SEBI BRSR & CSR Statutory Audit" />)}
      {page === 'ibc-cirp' && (can('ibccirp.read') || can('audit.read')) && (isFeatureEnabled('ibc-cirp') ? <IbcCirpDeskPage can={can} /> : <FeatureDisabledNotice featureName="IBC 2016 Insolvency & CIRP Desk" />)}
      {page === 'payroll' && can('payroll.read') && <PayrollPage can={can} />}
      {page === 'einvoicing' && can('einvoice.read') && (isFeatureEnabled('einvoicing') ? <EInvoicingPage can={can} /> : <FeatureDisabledNotice featureName="E-Invoicing & E-Way Bill" />)}
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
      {page === 'internalaudit' && can('internalaudit.read') && <InternalAuditPage />}
      {page === 'antiprofiteering' && can('antiprofiteering.read') && <AntiProfiteeringPage />}
      {page === 'secretarialaudit' && can('secretarialaudit.read') && <SecretarialAuditPage />}
      {page === 'transferpricing' && can('transferpricing.read') && <TransferPricingPage />}
      {page === 'uae-tax' && can('uaecorporatetax.read') && (isFeatureEnabled('uae-tax') ? <UaeCorporateTaxDesk can={can} /> : <FeatureDisabledNotice featureName="UAE Corporate Tax" />)}
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
        </main>
      </div>
    </div>
  )
}
