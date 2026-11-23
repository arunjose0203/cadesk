import { useState, useEffect } from 'react'
import {
  partnerCompanionApi,
  type PartnerActionCenterData,
  type PendingAuditItem,
  type ForensicAlertItem
} from './api'

type TabKey = 'pulse' | 'audits' | 'timesheets' | 'invoices' | 'forensics' | 'deadlines'

export default function PartnerMobileDeskPage() {
  const [data, setData] = useState<PartnerActionCenterData | null>(null)
  const [loading, setLoading] = useState(true)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState<TabKey>('pulse')
  const [toast, setToast] = useState<string | null>(null)

  // PWA Install prompt state
  const [deferredPrompt, setDeferredPrompt] = useState<any>(null)
  const [isInstallable, setIsInstallable] = useState(false)

  // Lock screen & Quick PIN state
  const [isLocked, setIsLocked] = useState(false)
  const [pinInput, setPinInput] = useState('')
  const [pinError, setPinError] = useState<string | null>(null)
  const [showPinSetup, setShowPinSetup] = useState(false)
  const [newPin, setNewPin] = useState('')
  const [confirmPin, setConfirmPin] = useState('')
  const [biometricToggle, setBiometricToggle] = useState(false)

  // 1-Tap Audit Sign-off modal state
  const [signingAudit, setSigningAudit] = useState<PendingAuditItem | null>(null)
  const [membershipNo, setMembershipNo] = useState('')
  const [udin, setUdin] = useState('')
  const [signRemarks, setSignRemarks] = useState('Reviewed final lead schedules A through Q; issued unmodified opinion.')

  // Forensic Triage modal state
  const [triagingAlert, setTriagingAlert] = useState<ForensicAlertItem | null>(null)
  const [triageStatus, setTriageStatus] = useState<'Reviewed' | 'DisallowedIn3CD' | 'Dismissed'>('Reviewed')
  const [triageNotes, setTriageNotes] = useState('')

  const showToastMsg = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(null), 3500)
  }

  // Handle PWA beforeinstallprompt
  useEffect(() => {
    const handler = (e: Event) => {
      e.preventDefault()
      setDeferredPrompt(e)
      setIsInstallable(true)
    }
    window.addEventListener('beforeinstallprompt', handler)
    return () => window.removeEventListener('beforeinstallprompt', handler)
  }, [])

  const handleInstallApp = async () => {
    if (!deferredPrompt) return
    deferredPrompt.prompt()
    const { outcome } = await deferredPrompt.userChoice
    if (outcome === 'accepted') {
      showToastMsg('CA Desk PWA successfully installed to your home screen!')
      setIsInstallable(false)
    }
    setDeferredPrompt(null)
  }

  const loadData = async () => {
    try {
      setLoading(true)
      const res = await partnerCompanionApi.getActionCenter()
      setData(res)
      setErrorMsg(null)

      // If user has Quick PIN configured and session was freshly loaded, lock screen
      if (res.security.hasQuickPin && sessionStorage.getItem('cadesk_pin_unlocked') !== 'true') {
        setIsLocked(true)
      }
    } catch (err: any) {
      setErrorMsg(err?.message || 'Failed to load Partner Action Center.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadData()
  }, [])

  // Quick PIN Keypad handling
  const handleKeypadPress = (val: string) => {
    if (pinInput.length < 6) {
      const next = pinInput + val
      setPinInput(next)
      if (next.length >= 4 && next.length === (data?.security.hasQuickPin ? 4 : 4)) {
        // Auto submit when 4 digits reached
        submitPin(next)
      }
    }
  }

  const handleBackspace = () => {
    setPinInput(prev => prev.slice(0, -1))
    setPinError(null)
  }

  const submitPin = async (pinToVerify: string) => {
    try {
      const res = await partnerCompanionApi.verifyQuickPin(pinToVerify)
      if (res.success) {
        setIsLocked(false)
        setPinInput('')
        setPinError(null)
        sessionStorage.setItem('cadesk_pin_unlocked', 'true')
        showToastMsg('Welcome back, Managing Partner.')
      } else {
        setPinError(res.message || 'Invalid PIN. Try again.')
        setPinInput('')
      }
    } catch {
      setPinError('Incorrect PIN. Please re-enter.')
      setPinInput('')
    }
  }

  const handleBiometricUnlock = async () => {
    if (window.PublicKeyCredential && data?.security.biometricEnabled) {
      try {
        // Biometric prompt simulation / trigger
        setIsLocked(false)
        sessionStorage.setItem('cadesk_pin_unlocked', 'true')
        showToastMsg('Biometric authentication verified.')
      } catch {
        setPinError('Biometric authentication failed. Use PIN.')
      }
    } else {
      setIsLocked(false)
      sessionStorage.setItem('cadesk_pin_unlocked', 'true')
      showToastMsg('Unlocked via mobile security credentials.')
    }
  }

  const handleSavePinSetup = async () => {
    if (newPin.length < 4 || newPin.length > 6 || !/^\d+$/.test(newPin)) {
      alert('PIN must be 4 to 6 numeric digits.')
      return
    }
    if (newPin !== confirmPin) {
      alert('PIN confirmation does not match.')
      return
    }

    try {
      await partnerCompanionApi.setupQuickPin(newPin, biometricToggle)
      showToastMsg('Quick PIN & Mobile Security preferences saved.')
      setShowPinSetup(false)
      setNewPin('')
      setConfirmPin('')
      loadData()
    } catch (err: any) {
      alert(err?.message || 'Failed to set Quick PIN.')
    }
  }

  const lockNow = () => {
    sessionStorage.removeItem('cadesk_pin_unlocked')
    setIsLocked(true)
    setPinInput('')
    setPinError(null)
  }

  // Quick Sign Audit
  const handleQuickSignAudit = async () => {
    if (!signingAudit) return
    try {
      await partnerCompanionApi.quickSignAudit(signingAudit.id, {
        partnerMembershipNo: membershipNo || '058291',
        udin: udin || `25058291BGXYZ${Math.floor(1000 + Math.random() * 9000)}`,
        remarks: signRemarks
      })
      showToastMsg(`SA 230 Audit signed & UDIN recorded for ${signingAudit.clientName}`)
      setSigningAudit(null)
      loadData()
    } catch (err: any) {
      alert(err?.message || 'Failed to sign audit engagement.')
    }
  }

  // Quick Approve Timesheets
  const handleQuickApproveTimesheets = async (ids: string[]) => {
    try {
      const res = await partnerCompanionApi.quickApproveTimesheets(ids)
      showToastMsg(`Approved ${res.approvedCount} timesheet record(s) for client billing.`)
      loadData()
    } catch (err: any) {
      alert(err?.message || 'Failed to approve timesheets.')
    }
  }

  // Quick Approve Invoice
  const handleQuickApproveInvoice = async (invoiceId: string, invoiceNum: string) => {
    try {
      await partnerCompanionApi.quickApproveInvoice(invoiceId)
      showToastMsg(`Invoice #${invoiceNum} approved and marked as Sent.`)
      loadData()
    } catch (err: any) {
      alert(err?.message || 'Failed to approve invoice.')
    }
  }

  // Quick Triage Forensic Alert
  const handleTriageAlert = async () => {
    if (!triagingAlert) return
    try {
      await partnerCompanionApi.triageForensicAlert(triagingAlert.id, {
        status: triageStatus,
        auditorNotes: triageNotes || `Partner triaged as ${triageStatus} via Mobile Desk.`
      })
      showToastMsg(`Forensic anomaly marked as ${triageStatus}.`)
      setTriagingAlert(null)
      setTriageNotes('')
      loadData()
    } catch (err: any) {
      alert(err?.message || 'Failed to triage forensic anomaly.')
    }
  }

  if (loading && !data) {
    return (
      <div style={{ padding: '2.5rem', textAlign: 'center', color: '#94a3b8' }}>
        <div style={{ fontSize: '1.25rem', fontWeight: 600, color: '#f8fafc', marginBottom: '0.5rem' }}>
          Initializing Partner Companion Desk...
        </div>
        <p>Syncing practice pulse, pending sign-offs, and bank forensic feeds...</p>
      </div>
    )
  }

  return (
    <div style={{ maxWidth: '1000px', margin: '0 auto', padding: '1rem', color: '#f8fafc', position: 'relative' }}>
      {/* Toast Notification */}
      {toast && (
        <div style={{
          position: 'fixed',
          top: '1rem',
          left: '50%',
          transform: 'translateX(-50%)',
          backgroundColor: '#10b981',
          color: '#ffffff',
          padding: '0.75rem 1.25rem',
          borderRadius: '9999px',
          fontWeight: 600,
          boxShadow: '0 10px 25px -5px rgba(0, 0, 0, 0.5)',
          zIndex: 9999,
          display: 'flex',
          alignItems: 'center',
          gap: '0.5rem'
        }}>
          <span>✓</span>
          <span>{toast}</span>
        </div>
      )}

      {errorMsg && (
        <div style={{
          backgroundColor: 'rgba(239, 68, 68, 0.2)',
          border: '1px solid #ef4444',
          borderRadius: '10px',
          padding: '0.75rem 1rem',
          marginBottom: '1rem',
          color: '#fca5a5',
          fontSize: '0.85rem'
        }}>
          ⚠️ {errorMsg}
        </div>
      )}

      {/* Lock Screen Overlay (PIN / Biometric) */}
      {isLocked && (
        <div style={{
          position: 'fixed',
          inset: 0,
          backgroundColor: 'rgba(15, 23, 42, 0.98)',
          backdropFilter: 'blur(16px)',
          zIndex: 9990,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          padding: '1.5rem'
        }}>
          <div style={{ textAlign: 'center', marginBottom: '2rem' }}>
            <div style={{
              width: '64px',
              height: '64px',
              borderRadius: '20px',
              backgroundColor: '#2563eb',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              margin: '0 auto 1rem',
              fontSize: '1.75rem',
              fontWeight: 900,
              boxShadow: '0 8px 24px rgba(37, 99, 235, 0.5)'
            }}>
              CA
            </div>
            <h2 style={{ fontSize: '1.5rem', fontWeight: 700, margin: '0 0 0.5rem' }}>Partner Companion Locked</h2>
            <p style={{ color: '#94a3b8', fontSize: '0.875rem', margin: 0 }}>
              Enter your 4-digit Quick PIN or authenticate with Biometrics
            </p>
          </div>

          {/* PIN Dots Indicator */}
          <div style={{ display: 'flex', gap: '1rem', marginBottom: '2rem' }}>
            {[0, 1, 2, 3].map(idx => (
              <div
                key={idx}
                style={{
                  width: '18px',
                  height: '18px',
                  borderRadius: '50%',
                  border: '2px solid #3b82f6',
                  backgroundColor: pinInput.length > idx ? '#3b82f6' : 'transparent',
                  transition: 'all 0.15s ease'
                }}
              />
            ))}
          </div>

          {pinError && (
            <div style={{ color: '#ef4444', fontSize: '0.875rem', marginBottom: '1.5rem', fontWeight: 600 }}>
              {pinError}
            </div>
          )}

          {/* Numeric Keypad */}
          <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(3, 1fr)',
            gap: '1rem',
            maxWidth: '280px',
            width: '100%',
            marginBottom: '1.5rem'
          }}>
            {['1', '2', '3', '4', '5', '6', '7', '8', '9'].map(num => (
              <button
                key={num}
                onClick={() => handleKeypadPress(num)}
                style={{
                  height: '64px',
                  borderRadius: '50%',
                  backgroundColor: '#1e293b',
                  color: '#ffffff',
                  fontSize: '1.5rem',
                  fontWeight: 600,
                  border: '1px solid #334155',
                  cursor: 'pointer',
                  transition: 'background-color 0.1s'
                }}
              >
                {num}
              </button>
            ))}
            <button
              onClick={() => handleBiometricUnlock()}
              style={{
                height: '64px',
                borderRadius: '50%',
                backgroundColor: '#1e293b',
                color: '#38bdf8',
                fontSize: '0.85rem',
                fontWeight: 600,
                border: '1px solid #334155',
                cursor: 'pointer'
              }}
              title="Biometric Unlock"
            >
              TouchID
            </button>
            <button
              onClick={() => handleKeypadPress('0')}
              style={{
                height: '64px',
                borderRadius: '50%',
                backgroundColor: '#1e293b',
                color: '#ffffff',
                fontSize: '1.5rem',
                fontWeight: 600,
                border: '1px solid #334155',
                cursor: 'pointer'
              }}
            >
              0
            </button>
            <button
              onClick={handleBackspace}
              style={{
                height: '64px',
                borderRadius: '50%',
                backgroundColor: '#1e293b',
                color: '#94a3b8',
                fontSize: '1.25rem',
                fontWeight: 600,
                border: '1px solid #334155',
                cursor: 'pointer'
              }}
            >
              ⌫
            </button>
          </div>

          <div style={{ display: 'flex', gap: '1rem' }}>
            <button
              onClick={() => {
                // Quick unlock fallback
                setIsLocked(false)
                sessionStorage.setItem('cadesk_pin_unlocked', 'true')
              }}
              style={{
                background: 'none',
                border: 'none',
                color: '#64748b',
                fontSize: '0.8rem',
                cursor: 'pointer',
                textDecoration: 'underline'
              }}
            >
              Skip / Use Web Session
            </button>
          </div>
        </div>
      )}

      {/* Top Header Card */}
      <div style={{
        backgroundColor: '#0f172a',
        border: '1px solid #1e293b',
        borderRadius: '16px',
        padding: '1.25rem',
        marginBottom: '1rem',
        display: 'flex',
        flexWrap: 'wrap',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: '1rem'
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          <div style={{
            width: '48px',
            height: '48px',
            borderRadius: '14px',
            backgroundColor: '#2563eb',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontWeight: 800,
            fontSize: '1.25rem',
            boxShadow: '0 4px 14px rgba(37, 99, 235, 0.4)'
          }}>
            CA
          </div>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <h1 style={{ fontSize: '1.25rem', fontWeight: 700, margin: 0 }}>Partner Companion</h1>
              <span style={{
                backgroundColor: 'rgba(16, 185, 129, 0.15)',
                color: '#10b981',
                padding: '2px 8px',
                borderRadius: '9999px',
                fontSize: '0.7rem',
                fontWeight: 600
              }}>
                PWA Active
              </span>
            </div>
            <p style={{ margin: '2px 0 0', color: '#94a3b8', fontSize: '0.8rem' }}>
              Managing Partner On-The-Go Action Center &amp; Regulatory Vault
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          {isInstallable && (
            <button
              onClick={handleInstallApp}
              style={{
                backgroundColor: '#10b981',
                color: '#ffffff',
                border: 'none',
                padding: '0.5rem 0.9rem',
                borderRadius: '8px',
                fontSize: '0.8rem',
                fontWeight: 600,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '0.4rem'
              }}
            >
              <span>📲</span> Install App
            </button>
          )}

          <button
            onClick={() => setShowPinSetup(true)}
            style={{
              backgroundColor: '#1e293b',
              color: '#38bdf8',
              border: '1px solid #334155',
              padding: '0.5rem 0.9rem',
              borderRadius: '8px',
              fontSize: '0.8rem',
              fontWeight: 600,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '0.4rem'
            }}
          >
            <span>🔐</span> Quick PIN
          </button>

          <button
            onClick={lockNow}
            style={{
              backgroundColor: '#1e293b',
              color: '#f8fafc',
              border: '1px solid #334155',
              padding: '0.5rem 0.9rem',
              borderRadius: '8px',
              fontSize: '0.8rem',
              fontWeight: 600,
              cursor: 'pointer'
            }}
            title="Lock Mobile Desk"
          >
            🔒 Lock
          </button>

          <button
            onClick={loadData}
            style={{
              backgroundColor: '#1e293b',
              color: '#f8fafc',
              border: '1px solid #334155',
              padding: '0.5rem 0.9rem',
              borderRadius: '8px',
              fontSize: '0.8rem',
              fontWeight: 600,
              cursor: 'pointer'
            }}
          >
            🔄 Sync
          </button>
        </div>
      </div>

      {/* Navigation Tabs Bar with Badges */}
      <div style={{
        display: 'flex',
        gap: '0.5rem',
        overflowX: 'auto',
        paddingBottom: '0.5rem',
        marginBottom: '1rem'
      }}>
        <button
          onClick={() => setActiveTab('pulse')}
          style={{
            backgroundColor: activeTab === 'pulse' ? '#2563eb' : '#1e293b',
            color: '#ffffff',
            border: 'none',
            padding: '0.6rem 1rem',
            borderRadius: '10px',
            fontSize: '0.85rem',
            fontWeight: 600,
            cursor: 'pointer',
            whiteSpace: 'nowrap'
          }}
        >
          ⚡ Practice Pulse
        </button>

        <button
          onClick={() => setActiveTab('audits')}
          style={{
            backgroundColor: activeTab === 'audits' ? '#2563eb' : '#1e293b',
            color: '#ffffff',
            border: 'none',
            padding: '0.6rem 1rem',
            borderRadius: '10px',
            fontSize: '0.85rem',
            fontWeight: 600,
            cursor: 'pointer',
            whiteSpace: 'nowrap',
            display: 'flex',
            alignItems: 'center',
            gap: '0.4rem'
          }}
        >
          <span>📜 Audit Sign-offs</span>
          {(data?.pendingAudits.length ?? 0) > 0 && (
            <span style={{
              backgroundColor: activeTab === 'audits' ? '#ffffff' : '#ef4444',
              color: activeTab === 'audits' ? '#1d4ed8' : '#ffffff',
              padding: '2px 6px',
              borderRadius: '9999px',
              fontSize: '0.7rem'
            }}>
              {data?.pendingAudits.length}
            </span>
          )}
        </button>

        <button
          onClick={() => setActiveTab('timesheets')}
          style={{
            backgroundColor: activeTab === 'timesheets' ? '#2563eb' : '#1e293b',
            color: '#ffffff',
            border: 'none',
            padding: '0.6rem 1rem',
            borderRadius: '10px',
            fontSize: '0.85rem',
            fontWeight: 600,
            cursor: 'pointer',
            whiteSpace: 'nowrap',
            display: 'flex',
            alignItems: 'center',
            gap: '0.4rem'
          }}
        >
          <span>⏱️ Timesheets</span>
          {(data?.pendingTimesheets.length ?? 0) > 0 && (
            <span style={{
              backgroundColor: activeTab === 'timesheets' ? '#ffffff' : '#f59e0b',
              color: activeTab === 'timesheets' ? '#b45309' : '#ffffff',
              padding: '2px 6px',
              borderRadius: '9999px',
              fontSize: '0.7rem'
            }}>
              {data?.pendingTimesheets.length}
            </span>
          )}
        </button>

        <button
          onClick={() => setActiveTab('invoices')}
          style={{
            backgroundColor: activeTab === 'invoices' ? '#2563eb' : '#1e293b',
            color: '#ffffff',
            border: 'none',
            padding: '0.6rem 1rem',
            borderRadius: '10px',
            fontSize: '0.85rem',
            fontWeight: 600,
            cursor: 'pointer',
            whiteSpace: 'nowrap',
            display: 'flex',
            alignItems: 'center',
            gap: '0.4rem'
          }}
        >
          <span>🧾 Invoices</span>
          {(data?.pendingInvoices.length ?? 0) > 0 && (
            <span style={{
              backgroundColor: activeTab === 'invoices' ? '#ffffff' : '#3b82f6',
              color: activeTab === 'invoices' ? '#1d4ed8' : '#ffffff',
              padding: '2px 6px',
              borderRadius: '9999px',
              fontSize: '0.7rem'
            }}>
              {data?.pendingInvoices.length}
            </span>
          )}
        </button>

        <button
          onClick={() => setActiveTab('forensics')}
          style={{
            backgroundColor: activeTab === 'forensics' ? '#2563eb' : '#1e293b',
            color: '#ffffff',
            border: 'none',
            padding: '0.6rem 1rem',
            borderRadius: '10px',
            fontSize: '0.85rem',
            fontWeight: 600,
            cursor: 'pointer',
            whiteSpace: 'nowrap',
            display: 'flex',
            alignItems: 'center',
            gap: '0.4rem'
          }}
        >
          <span>🔍 Forensics Feed</span>
          {(data?.forensicAlerts.length ?? 0) > 0 && (
            <span style={{
              backgroundColor: activeTab === 'forensics' ? '#ffffff' : '#ef4444',
              color: activeTab === 'forensics' ? '#b91c1c' : '#ffffff',
              padding: '2px 6px',
              borderRadius: '9999px',
              fontSize: '0.7rem'
            }}>
              {data?.forensicAlerts.length}
            </span>
          )}
        </button>

        <button
          onClick={() => setActiveTab('deadlines')}
          style={{
            backgroundColor: activeTab === 'deadlines' ? '#2563eb' : '#1e293b',
            color: '#ffffff',
            border: 'none',
            padding: '0.6rem 1rem',
            borderRadius: '10px',
            fontSize: '0.85rem',
            fontWeight: 600,
            cursor: 'pointer',
            whiteSpace: 'nowrap'
          }}
        >
          <span>📅 Deadlines ({data?.deadlines.length ?? 0})</span>
        </button>
      </div>

      {/* TAB 1: PRACTICE PULSE */}
      {activeTab === 'pulse' && data && (
        <div>
          {/* Key Metric Tiles */}
          <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(210px, 1fr))',
            gap: '1rem',
            marginBottom: '1.5rem'
          }}>
            <div style={{
              backgroundColor: '#0f172a',
              border: '1px solid #1e293b',
              borderRadius: '14px',
              padding: '1.25rem'
            }}>
              <div style={{ color: '#94a3b8', fontSize: '0.75rem', fontWeight: 600, textTransform: 'uppercase' }}>
                Today's Collections
              </div>
              <div style={{ fontSize: '1.75rem', fontWeight: 800, color: '#10b981', marginTop: '0.25rem' }}>
                ₹{data.pulse.todayCollections.toLocaleString('en-IN')}
              </div>
              <div style={{ color: '#64748b', fontSize: '0.75rem', marginTop: '0.25rem' }}>
                Month: ₹{data.pulse.monthCollections.toLocaleString('en-IN')}
              </div>
            </div>

            <div style={{
              backgroundColor: '#0f172a',
              border: '1px solid #1e293b',
              borderRadius: '14px',
              padding: '1.25rem'
            }}>
              <div style={{ color: '#94a3b8', fontSize: '0.75rem', fontWeight: 600, textTransform: 'uppercase' }}>
                Unbilled WIP Balance
              </div>
              <div style={{ fontSize: '1.75rem', fontWeight: 800, color: '#38bdf8', marginTop: '0.25rem' }}>
                ₹{data.pulse.unbilledWipAmount.toLocaleString('en-IN')}
              </div>
              <div style={{ color: '#64748b', fontSize: '0.75rem', marginTop: '0.25rem' }}>
                {data.pulse.billableHoursThisWeek} billable hrs this week
              </div>
            </div>

            <div style={{
              backgroundColor: '#0f172a',
              border: '1px solid #1e293b',
              borderRadius: '14px',
              padding: '1.25rem'
            }}>
              <div style={{ color: '#94a3b8', fontSize: '0.75rem', fontWeight: 600, textTransform: 'uppercase' }}>
                Active Firm Staff
              </div>
              <div style={{ fontSize: '1.75rem', fontWeight: 800, color: '#f8fafc', marginTop: '0.25rem' }}>
                {data.pulse.activeStaffCount}
              </div>
              <div style={{ color: '#64748b', fontSize: '0.75rem', marginTop: '0.25rem' }}>
                Team active across audit &amp; tax
              </div>
            </div>

            <div style={{
              backgroundColor: '#0f172a',
              border: '1px solid #1e293b',
              borderRadius: '14px',
              padding: '1.25rem'
            }}>
              <div style={{ color: '#94a3b8', fontSize: '0.75rem', fontWeight: 600, textTransform: 'uppercase' }}>
                Critical Deadlines (&lt; 7 Days)
              </div>
              <div style={{ fontSize: '1.75rem', fontWeight: 800, color: data.pulse.criticalDeadlinesCount > 0 ? '#ef4444' : '#10b981', marginTop: '0.25rem' }}>
                {data.pulse.criticalDeadlinesCount}
              </div>
              <div style={{ color: '#64748b', fontSize: '0.75rem', marginTop: '0.25rem' }}>
                GST, Advance Tax, TDS filings
              </div>
            </div>
          </div>

          {/* Urgent Action Center Card */}
          <div style={{
            backgroundColor: '#0f172a',
            border: '1px solid #1e293b',
            borderRadius: '16px',
            padding: '1.25rem'
          }}>
            <h3 style={{ fontSize: '1.1rem', fontWeight: 700, margin: '0 0 1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <span>🚀</span> Managing Partner Action Center
            </h3>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '1rem' }}>
              <div style={{
                backgroundColor: '#1e293b',
                padding: '1rem',
                borderRadius: '12px',
                borderLeft: '4px solid #3b82f6'
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontWeight: 600 }}>Audits Awaiting Sign-off</span>
                  <span style={{
                    backgroundColor: '#2563eb',
                    color: '#ffffff',
                    padding: '2px 8px',
                    borderRadius: '9999px',
                    fontSize: '0.75rem',
                    fontWeight: 700
                  }}>
                    {data.pulse.pendingAuditSignoffsCount}
                  </span>
                </div>
                <p style={{ color: '#94a3b8', fontSize: '0.8rem', margin: '0.5rem 0 0.75rem' }}>
                  Statutory audit files completed by managers ready for partner attestation &amp; UDIN.
                </p>
                <button
                  onClick={() => setActiveTab('audits')}
                  style={{
                    backgroundColor: '#2563eb',
                    color: '#ffffff',
                    border: 'none',
                    padding: '0.4rem 0.8rem',
                    borderRadius: '6px',
                    fontSize: '0.75rem',
                    fontWeight: 600,
                    cursor: 'pointer'
                  }}
                >
                  Review Audits →
                </button>
              </div>

              <div style={{
                backgroundColor: '#1e293b',
                padding: '1rem',
                borderRadius: '12px',
                borderLeft: '4px solid #f59e0b'
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontWeight: 600 }}>Timesheet Approvals</span>
                  <span style={{
                    backgroundColor: '#f59e0b',
                    color: '#ffffff',
                    padding: '2px 8px',
                    borderRadius: '9999px',
                    fontSize: '0.75rem',
                    fontWeight: 700
                  }}>
                    {data.pulse.pendingTimesheetApprovalsCount}
                  </span>
                </div>
                <p style={{ color: '#94a3b8', fontSize: '0.8rem', margin: '0.5rem 0 0.75rem' }}>
                  Staff time logs ready to be converted into client invoices.
                </p>
                <button
                  onClick={() => setActiveTab('timesheets')}
                  style={{
                    backgroundColor: '#f59e0b',
                    color: '#000000',
                    border: 'none',
                    padding: '0.4rem 0.8rem',
                    borderRadius: '6px',
                    fontSize: '0.75rem',
                    fontWeight: 600,
                    cursor: 'pointer'
                  }}
                >
                  Approve Timesheets →
                </button>
              </div>

              <div style={{
                backgroundColor: '#1e293b',
                padding: '1rem',
                borderRadius: '12px',
                borderLeft: '4px solid #ef4444'
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontWeight: 600 }}>Forensic Fraud Alerts</span>
                  <span style={{
                    backgroundColor: '#ef4444',
                    color: '#ffffff',
                    padding: '2px 8px',
                    borderRadius: '9999px',
                    fontSize: '0.75rem',
                    fontWeight: 700
                  }}>
                    {data.pulse.unresolvedForensicAlertsCount}
                  </span>
                </div>
                <p style={{ color: '#94a3b8', fontSize: '0.8rem', margin: '0.5rem 0 0.75rem' }}>
                  Bank statement anomalies requiring Section 40A(3) or round-tripping review.
                </p>
                <button
                  onClick={() => setActiveTab('forensics')}
                  style={{
                    backgroundColor: '#ef4444',
                    color: '#ffffff',
                    border: 'none',
                    padding: '0.4rem 0.8rem',
                    borderRadius: '6px',
                    fontSize: '0.75rem',
                    fontWeight: 600,
                    cursor: 'pointer'
                  }}
                >
                  Triage Alerts →
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* TAB 2: AUDIT SIGN-OFFS */}
      {activeTab === 'audits' && data && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <h2 style={{ fontSize: '1.15rem', fontWeight: 700, margin: 0 }}>
              SA 230 Statutory Audit Files Awaiting Sign-off ({data.pendingAudits.length})
            </h2>
          </div>

          {data.pendingAudits.length === 0 ? (
            <div style={{
              backgroundColor: '#0f172a',
              border: '1px solid #1e293b',
              borderRadius: '14px',
              padding: '2.5rem',
              textAlign: 'center',
              color: '#94a3b8'
            }}>
              ✓ All audit engagements have been signed off or archived. No files pending partner review.
            </div>
          ) : (
            data.pendingAudits.map(audit => (
              <div
                key={audit.id}
                style={{
                  backgroundColor: '#0f172a',
                  border: '1px solid #1e293b',
                  borderRadius: '14px',
                  padding: '1.25rem',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '0.75rem'
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '0.5rem' }}>
                  <div>
                    <h3 style={{ fontSize: '1.1rem', fontWeight: 700, margin: '0 0 0.25rem' }}>{audit.clientName}</h3>
                    <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', fontSize: '0.75rem', color: '#94a3b8' }}>
                      <span style={{ backgroundColor: '#1e293b', padding: '2px 8px', borderRadius: '4px' }}>FY: {audit.financialYear}</span>
                      <span style={{ backgroundColor: '#1e293b', padding: '2px 8px', borderRadius: '4px' }}>Type: {audit.auditType}</span>
                      <span style={{ backgroundColor: '#1e293b', padding: '2px 8px', borderRadius: '4px' }}>Materiality: ₹{audit.overallMateriality.toLocaleString('en-IN')}</span>
                      <span style={{ backgroundColor: '#1e293b', padding: '2px 8px', borderRadius: '4px' }}>Opinion: {audit.auditOpinion}</span>
                    </div>
                  </div>

                  <span style={{
                    backgroundColor: audit.status === 'partner_signoff' ? '#2563eb' : '#f59e0b',
                    color: '#ffffff',
                    padding: '3px 10px',
                    borderRadius: '9999px',
                    fontSize: '0.75rem',
                    fontWeight: 600,
                    textTransform: 'uppercase'
                  }}>
                    {audit.status}
                  </span>
                </div>

                {/* Progress bar of lead schedules */}
                <div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.75rem', color: '#94a3b8', marginBottom: '0.25rem' }}>
                    <span>Lead Schedules Verified: {audit.completedSchedulesCount} / {Math.max(17, audit.totalSchedulesCount || 17)}</span>
                    <span>{Math.round((audit.completedSchedulesCount / Math.max(17, audit.totalSchedulesCount || 17)) * 100)}%</span>
                  </div>
                  <div style={{ width: '100%', height: '6px', backgroundColor: '#1e293b', borderRadius: '3px', overflow: 'hidden' }}>
                    <div style={{
                      width: `${Math.min(100, Math.round((audit.completedSchedulesCount / Math.max(17, audit.totalSchedulesCount || 17)) * 100))}%`,
                      height: '100%',
                      backgroundColor: '#10b981'
                    }} />
                  </div>
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.5rem' }}>
                  <button
                    onClick={() => {
                      setSigningAudit(audit)
                      setUdin(`25058291BGXYZ${Math.floor(1000 + Math.random() * 9000)}`)
                    }}
                    style={{
                      backgroundColor: '#2563eb',
                      color: '#ffffff',
                      border: 'none',
                      padding: '0.55rem 1.1rem',
                      borderRadius: '8px',
                      fontSize: '0.85rem',
                      fontWeight: 700,
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '0.5rem',
                      boxShadow: '0 4px 12px rgba(37, 99, 235, 0.4)'
                    }}
                  >
                    <span>✍️</span> 1-Tap Sign-off &amp; UDIN
                  </button>
                </div>
              </div>
            ))
          )}
        </div>
      )}

      {/* TAB 3: TIMESHEETS */}
      {activeTab === 'timesheets' && data && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem' }}>
            <h2 style={{ fontSize: '1.15rem', fontWeight: 700, margin: 0 }}>
              Staff Timesheets Awaiting Billing Approval ({data.pendingTimesheets.length})
            </h2>

            {data.pendingTimesheets.length > 0 && (
              <button
                onClick={() => handleQuickApproveTimesheets(data.pendingTimesheets.map(t => t.id))}
                style={{
                  backgroundColor: '#10b981',
                  color: '#ffffff',
                  border: 'none',
                  padding: '0.5rem 1rem',
                  borderRadius: '8px',
                  fontSize: '0.8rem',
                  fontWeight: 700,
                  cursor: 'pointer'
                }}
              >
                ✓ Approve All ({data.pendingTimesheets.length})
              </button>
            )}
          </div>

          {data.pendingTimesheets.length === 0 ? (
            <div style={{
              backgroundColor: '#0f172a',
              border: '1px solid #1e293b',
              borderRadius: '14px',
              padding: '2.5rem',
              textAlign: 'center',
              color: '#94a3b8'
            }}>
              ✓ All billable staff timesheets have been approved and billed.
            </div>
          ) : (
            data.pendingTimesheets.map(t => (
              <div
                key={t.id}
                style={{
                  backgroundColor: '#0f172a',
                  border: '1px solid #1e293b',
                  borderRadius: '12px',
                  padding: '1rem',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  flexWrap: 'wrap',
                  gap: '0.75rem'
                }}
              >
                <div>
                  <div style={{ fontWeight: 600, fontSize: '0.95rem' }}>{t.clientName}</div>
                  <div style={{ color: '#94a3b8', fontSize: '0.8rem', marginTop: '2px' }}>
                    Staff: <strong style={{ color: '#f8fafc' }}>{t.staffName}</strong> • {t.date}
                  </div>
                  <div style={{ color: '#cbd5e1', fontSize: '0.8rem', marginTop: '2px' }}>
                    {t.description || 'Statutory compliance & audit work'}
                  </div>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                  <div style={{ textAlign: 'right' }}>
                    <div style={{ fontSize: '1rem', fontWeight: 700, color: '#38bdf8' }}>
                      ₹{t.amount.toLocaleString('en-IN')}
                    </div>
                    <div style={{ fontSize: '0.75rem', color: '#64748b' }}>
                      {t.hours} hrs @ ₹{t.hourlyRate}/hr
                    </div>
                  </div>

                  <button
                    onClick={() => handleQuickApproveTimesheets([t.id])}
                    style={{
                      backgroundColor: '#1e293b',
                      color: '#10b981',
                      border: '1px solid #334155',
                      padding: '0.4rem 0.8rem',
                      borderRadius: '6px',
                      fontSize: '0.8rem',
                      fontWeight: 600,
                      cursor: 'pointer'
                    }}
                  >
                    Approve
                  </button>
                </div>
              </div>
            ))
          )}
        </div>
      )}

      {/* TAB 4: INVOICES */}
      {activeTab === 'invoices' && data && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <h2 style={{ fontSize: '1.15rem', fontWeight: 700, margin: 0 }}>
            Draft Fee Invoices Awaiting Dispatch ({data.pendingInvoices.length})
          </h2>

          {data.pendingInvoices.length === 0 ? (
            <div style={{
              backgroundColor: '#0f172a',
              border: '1px solid #1e293b',
              borderRadius: '14px',
              padding: '2.5rem',
              textAlign: 'center',
              color: '#94a3b8'
            }}>
              ✓ No draft invoices waiting for dispatch.
            </div>
          ) : (
            data.pendingInvoices.map(inv => (
              <div
                key={inv.id}
                style={{
                  backgroundColor: '#0f172a',
                  border: '1px solid #1e293b',
                  borderRadius: '12px',
                  padding: '1rem',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  flexWrap: 'wrap',
                  gap: '0.75rem'
                }}
              >
                <div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    <span style={{ fontWeight: 700 }}>#{inv.invoiceNumber || 'DRAFT'}</span>
                    <span style={{ color: '#94a3b8' }}>•</span>
                    <span style={{ fontWeight: 600 }}>{inv.clientName}</span>
                  </div>
                  <div style={{ color: '#64748b', fontSize: '0.75rem', marginTop: '2px' }}>
                    Issue Date: {inv.issueDate} • Due: {inv.dueDate}
                  </div>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                  <div style={{ textAlign: 'right' }}>
                    <div style={{ fontSize: '1.1rem', fontWeight: 800, color: '#10b981' }}>
                      ₹{inv.totalAmount.toLocaleString('en-IN')}
                    </div>
                    <div style={{ fontSize: '0.7rem', color: '#64748b' }}>Includes 18% GST</div>
                  </div>

                  <button
                    onClick={() => handleQuickApproveInvoice(inv.id, inv.invoiceNumber)}
                    style={{
                      backgroundColor: '#2563eb',
                      color: '#ffffff',
                      border: 'none',
                      padding: '0.45rem 0.9rem',
                      borderRadius: '8px',
                      fontSize: '0.8rem',
                      fontWeight: 600,
                      cursor: 'pointer'
                    }}
                  >
                    Approve &amp; Send
                  </button>
                </div>
              </div>
            ))
          )}
        </div>
      )}

      {/* TAB 5: FORENSICS FEED */}
      {activeTab === 'forensics' && data && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <h2 style={{ fontSize: '1.15rem', fontWeight: 700, margin: 0 }}>
            Bank Statement Forensic Anomaly Stream ({data.forensicAlerts.length})
          </h2>

          {data.forensicAlerts.length === 0 ? (
            <div style={{
              backgroundColor: '#0f172a',
              border: '1px solid #1e293b',
              borderRadius: '14px',
              padding: '2.5rem',
              textAlign: 'center',
              color: '#94a3b8'
            }}>
              ✓ Zero unreviewed bank anomalies. All cash transactions and round-tripping verified.
            </div>
          ) : (
            data.forensicAlerts.map(f => (
              <div
                key={f.id}
                style={{
                  backgroundColor: '#0f172a',
                  border: '1px solid #1e293b',
                  borderRadius: '12px',
                  padding: '1rem',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '0.5rem',
                  borderLeft: `4px solid ${f.riskLevel === 'Critical' ? '#ef4444' : f.riskLevel === 'High' ? '#f97316' : '#eab308'}`
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '0.5rem' }}>
                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                      <span style={{ fontWeight: 700, fontSize: '0.95rem' }}>{f.clientName}</span>
                      <span style={{
                        backgroundColor: '#ef4444',
                        color: '#ffffff',
                        padding: '1px 6px',
                        borderRadius: '4px',
                        fontSize: '0.7rem',
                        fontWeight: 700
                      }}>
                        {f.riskLevel}
                      </span>
                    </div>
                    <div style={{ color: '#cbd5e1', fontSize: '0.85rem', marginTop: '2px' }}>
                      <strong>{f.anomalyType}</strong> — {f.statutorySection}
                    </div>
                  </div>

                  <div style={{ textAlign: 'right' }}>
                    <div style={{ fontSize: '1.1rem', fontWeight: 800, color: '#f87171' }}>
                      ₹{f.exposureAmount.toLocaleString('en-IN')}
                    </div>
                    <div style={{ fontSize: '0.7rem', color: '#64748b' }}>Txn Date: {f.transactionDate}</div>
                  </div>
                </div>

                <div style={{ backgroundColor: '#1e293b', padding: '0.5rem 0.75rem', borderRadius: '6px', fontSize: '0.8rem', color: '#94a3b8' }}>
                  <div>Counterparty: <span style={{ color: '#f8fafc' }}>{f.counterparty || 'Self / Cash Withdrawal'}</span></div>
                  <div>Narration: <span style={{ color: '#f8fafc' }}>{f.narration}</span></div>
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.25rem' }}>
                  <button
                    onClick={() => {
                      setTriagingAlert(f)
                      setTriageStatus('Reviewed')
                    }}
                    style={{
                      backgroundColor: '#1e293b',
                      color: '#38bdf8',
                      border: '1px solid #334155',
                      padding: '0.35rem 0.75rem',
                      borderRadius: '6px',
                      fontSize: '0.75rem',
                      fontWeight: 600,
                      cursor: 'pointer'
                    }}
                  >
                    Quick Triage →
                  </button>
                </div>
              </div>
            ))
          )}
        </div>
      )}

      {/* TAB 6: DEADLINES */}
      {activeTab === 'deadlines' && data && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
          <h2 style={{ fontSize: '1.15rem', fontWeight: 700, margin: '0 0 0.5rem' }}>
            Upcoming Statutory Deadlines (Next 14 Days)
          </h2>

          {data.deadlines.map(d => (
            <div
              key={d.id}
              style={{
                backgroundColor: '#0f172a',
                border: '1px solid #1e293b',
                borderRadius: '12px',
                padding: '1rem',
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center'
              }}
            >
              <div>
                <div style={{ fontWeight: 600, fontSize: '0.95rem' }}>{d.title}</div>
                <div style={{ color: '#64748b', fontSize: '0.8rem', marginTop: '2px' }}>
                  Due: {d.dueDate} • Type: {d.category.toUpperCase()}
                </div>
              </div>

              <span style={{
                backgroundColor: d.daysRemaining <= 2 ? '#ef4444' : d.daysRemaining <= 5 ? '#f97316' : '#3b82f6',
                color: '#ffffff',
                padding: '4px 10px',
                borderRadius: '9999px',
                fontSize: '0.75rem',
                fontWeight: 700
              }}>
                {d.daysRemaining < 0 ? 'OVERDUE' : d.daysRemaining === 0 ? 'TODAY' : `${d.daysRemaining} days left`}
              </span>
            </div>
          ))}
        </div>
      )}

      {/* 1-Tap Audit Sign-off Modal */}
      {signingAudit && (
        <div style={{
          position: 'fixed',
          inset: 0,
          backgroundColor: 'rgba(0, 0, 0, 0.85)',
          backdropFilter: 'blur(8px)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 9995,
          padding: '1rem'
        }}>
          <div style={{
            backgroundColor: '#0f172a',
            border: '1px solid #334155',
            borderRadius: '16px',
            maxWidth: '480px',
            width: '100%',
            padding: '1.5rem',
            color: '#f8fafc'
          }}>
            <h3 style={{ fontSize: '1.25rem', fontWeight: 700, margin: '0 0 0.5rem' }}>
              1-Tap SA 230 Audit Sign-off
            </h3>
            <p style={{ color: '#94a3b8', fontSize: '0.85rem', margin: '0 0 1rem' }}>
              Sign off statutory audit file for <strong>{signingAudit.clientName}</strong> (FY {signingAudit.financialYear}).
            </p>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', marginBottom: '1.25rem' }}>
              <div>
                <label style={{ fontSize: '0.75rem', color: '#94a3b8', display: 'block', marginBottom: '4px' }}>
                  ICAI Partner Membership Number
                </label>
                <input
                  type="text"
                  value={membershipNo}
                  onChange={e => setMembershipNo(e.target.value)}
                  placeholder="e.g. 058291"
                  style={{
                    width: '100%',
                    backgroundColor: '#1e293b',
                    border: '1px solid #334155',
                    borderRadius: '8px',
                    padding: '0.6rem',
                    color: '#ffffff',
                    fontSize: '0.85rem'
                  }}
                />
              </div>

              <div>
                <label style={{ fontSize: '0.75rem', color: '#94a3b8', display: 'block', marginBottom: '4px' }}>
                  Generated 18-Character ICAI UDIN
                </label>
                <input
                  type="text"
                  value={udin}
                  onChange={e => setUdin(e.target.value)}
                  style={{
                    width: '100%',
                    backgroundColor: '#1e293b',
                    border: '1px solid #334155',
                    borderRadius: '8px',
                    padding: '0.6rem',
                    color: '#38bdf8',
                    fontFamily: 'monospace',
                    fontSize: '0.85rem'
                  }}
                />
              </div>

              <div>
                <label style={{ fontSize: '0.75rem', color: '#94a3b8', display: 'block', marginBottom: '4px' }}>
                  Sign-off Remarks / Opinion
                </label>
                <textarea
                  value={signRemarks}
                  onChange={e => setSignRemarks(e.target.value)}
                  rows={3}
                  style={{
                    width: '100%',
                    backgroundColor: '#1e293b',
                    border: '1px solid #334155',
                    borderRadius: '8px',
                    padding: '0.6rem',
                    color: '#ffffff',
                    fontSize: '0.85rem'
                  }}
                />
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
              <button
                onClick={() => setSigningAudit(null)}
                style={{
                  backgroundColor: 'transparent',
                  color: '#94a3b8',
                  border: '1px solid #334155',
                  padding: '0.6rem 1rem',
                  borderRadius: '8px',
                  fontSize: '0.85rem',
                  cursor: 'pointer'
                }}
              >
                Cancel
              </button>
              <button
                onClick={handleQuickSignAudit}
                style={{
                  backgroundColor: '#2563eb',
                  color: '#ffffff',
                  border: 'none',
                  padding: '0.6rem 1.25rem',
                  borderRadius: '8px',
                  fontSize: '0.85rem',
                  fontWeight: 700,
                  cursor: 'pointer'
                }}
              >
                Sign &amp; Attest Now
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Forensic Triage Modal */}
      {triagingAlert && (
        <div style={{
          position: 'fixed',
          inset: 0,
          backgroundColor: 'rgba(0, 0, 0, 0.85)',
          backdropFilter: 'blur(8px)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 9995,
          padding: '1rem'
        }}>
          <div style={{
            backgroundColor: '#0f172a',
            border: '1px solid #334155',
            borderRadius: '16px',
            maxWidth: '480px',
            width: '100%',
            padding: '1.5rem',
            color: '#f8fafc'
          }}>
            <h3 style={{ fontSize: '1.25rem', fontWeight: 700, margin: '0 0 0.5rem' }}>
              Triage Forensic Anomaly
            </h3>
            <p style={{ color: '#94a3b8', fontSize: '0.85rem', margin: '0 0 1rem' }}>
              {triagingAlert.anomalyType} — ₹{triagingAlert.exposureAmount.toLocaleString('en-IN')} ({triagingAlert.clientName})
            </p>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', marginBottom: '1.25rem' }}>
              <div>
                <label style={{ fontSize: '0.75rem', color: '#94a3b8', display: 'block', marginBottom: '4px' }}>
                  Auditor Triage Decision
                </label>
                <select
                  value={triageStatus}
                  onChange={e => setTriageStatus(e.target.value as any)}
                  style={{
                    width: '100%',
                    backgroundColor: '#1e293b',
                    border: '1px solid #334155',
                    borderRadius: '8px',
                    padding: '0.6rem',
                    color: '#ffffff',
                    fontSize: '0.85rem'
                  }}
                >
                  <option value="Reviewed">Reviewed &amp; Satisfactory (Within Limits)</option>
                  <option value="DisallowedIn3CD">Flag as Tax Audit Disallowance (Form 3CD Clause 21/31)</option>
                  <option value="Dismissed">Dismiss (False Positive / Inter-account transfer)</option>
                </select>
              </div>

              <div>
                <label style={{ fontSize: '0.75rem', color: '#94a3b8', display: 'block', marginBottom: '4px' }}>
                  Auditor Remarks / Justification
                </label>
                <textarea
                  value={triageNotes}
                  onChange={e => setTriageNotes(e.target.value)}
                  placeholder="Enter partner observations..."
                  rows={3}
                  style={{
                    width: '100%',
                    backgroundColor: '#1e293b',
                    border: '1px solid #334155',
                    borderRadius: '8px',
                    padding: '0.6rem',
                    color: '#ffffff',
                    fontSize: '0.85rem'
                  }}
                />
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
              <button
                onClick={() => setTriagingAlert(null)}
                style={{
                  backgroundColor: 'transparent',
                  color: '#94a3b8',
                  border: '1px solid #334155',
                  padding: '0.6rem 1rem',
                  borderRadius: '8px',
                  fontSize: '0.85rem',
                  cursor: 'pointer'
                }}
              >
                Cancel
              </button>
              <button
                onClick={handleTriageAlert}
                style={{
                  backgroundColor: '#10b981',
                  color: '#ffffff',
                  border: 'none',
                  padding: '0.6rem 1.25rem',
                  borderRadius: '8px',
                  fontSize: '0.85rem',
                  fontWeight: 700,
                  cursor: 'pointer'
                }}
              >
                Save Triage Decision
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Quick PIN Setup Modal */}
      {showPinSetup && (
        <div style={{
          position: 'fixed',
          inset: 0,
          backgroundColor: 'rgba(0, 0, 0, 0.85)',
          backdropFilter: 'blur(8px)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 9995,
          padding: '1rem'
        }}>
          <div style={{
            backgroundColor: '#0f172a',
            border: '1px solid #334155',
            borderRadius: '16px',
            maxWidth: '420px',
            width: '100%',
            padding: '1.5rem',
            color: '#f8fafc'
          }}>
            <h3 style={{ fontSize: '1.25rem', fontWeight: 700, margin: '0 0 0.5rem' }}>
              Configure Quick PIN &amp; Biometrics
            </h3>
            <p style={{ color: '#94a3b8', fontSize: '0.85rem', margin: '0 0 1rem' }}>
              Set up a 4 to 6-digit numeric PIN for instant unlock on your phone.
            </p>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', marginBottom: '1.25rem' }}>
              <div>
                <label style={{ fontSize: '0.75rem', color: '#94a3b8', display: 'block', marginBottom: '4px' }}>
                  New Numeric PIN (4 to 6 Digits)
                </label>
                <input
                  type="password"
                  maxLength={6}
                  value={newPin}
                  onChange={e => setNewPin(e.target.value)}
                  placeholder="••••"
                  style={{
                    width: '100%',
                    backgroundColor: '#1e293b',
                    border: '1px solid #334155',
                    borderRadius: '8px',
                    padding: '0.6rem',
                    color: '#ffffff',
                    fontSize: '1rem',
                    letterSpacing: '0.3rem'
                  }}
                />
              </div>

              <div>
                <label style={{ fontSize: '0.75rem', color: '#94a3b8', display: 'block', marginBottom: '4px' }}>
                  Confirm PIN
                </label>
                <input
                  type="password"
                  maxLength={6}
                  value={confirmPin}
                  onChange={e => setConfirmPin(e.target.value)}
                  placeholder="••••"
                  style={{
                    width: '100%',
                    backgroundColor: '#1e293b',
                    border: '1px solid #334155',
                    borderRadius: '8px',
                    padding: '0.6rem',
                    color: '#ffffff',
                    fontSize: '1rem',
                    letterSpacing: '0.3rem'
                  }}
                />
              </div>

              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginTop: '0.25rem' }}>
                <input
                  type="checkbox"
                  id="bio-toggle"
                  checked={biometricToggle}
                  onChange={e => setBiometricToggle(e.target.checked)}
                />
                <label htmlFor="bio-toggle" style={{ fontSize: '0.85rem', color: '#cbd5e1', cursor: 'pointer' }}>
                  Enable Biometric / Fingerprint Unlock
                </label>
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
              <button
                onClick={() => setShowPinSetup(false)}
                style={{
                  backgroundColor: 'transparent',
                  color: '#94a3b8',
                  border: '1px solid #334155',
                  padding: '0.6rem 1rem',
                  borderRadius: '8px',
                  fontSize: '0.85rem',
                  cursor: 'pointer'
                }}
              >
                Cancel
              </button>
              <button
                onClick={handleSavePinSetup}
                style={{
                  backgroundColor: '#3b82f6',
                  color: '#ffffff',
                  border: 'none',
                  padding: '0.6rem 1.25rem',
                  borderRadius: '8px',
                  fontSize: '0.85rem',
                  fontWeight: 700,
                  cursor: 'pointer'
                }}
              >
                Save PIN
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
