import { useEffect, useState } from 'react'
import {
  communicationsApi,
  type CommunicationsOverviewDto,
  type StatutoryDispatchResult,
  type Sa505DispatchResult,
  type NotificationItem
} from './api'

export default function CommunicationsGatewayPage() {
  const [activeTab, setActiveTab] = useState<'status' | 'deadlines' | 'sa505' | 'invoices' | 'logs'>('status')
  const [overview, setOverview] = useState<CommunicationsOverviewDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [actionMessage, setActionMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  // Test Console State
  const [testPhone, setTestPhone] = useState('9876543210')
  const [testWaText, setTestWaText] = useState('')
  const [testEmail, setTestEmail] = useState('partner@cadesk.in')
  const [testEmailSubject, setTestEmailSubject] = useState('Test Notice from CA Desk Gateway')
  const [testEmailBody, setTestEmailBody] = useState('This is a test transactional email sent via CA Desk.')

  // Statutory Deadlines Campaign State
  const [selectedRegime, setSelectedRegime] = useState<'GST' | 'AdvanceTax' | 'ITR' | 'All'>('GST')
  const [horizonDays, setHorizonDays] = useState<number>(30)
  const [statutorySendWa, setStatutorySendWa] = useState(true)
  const [statutorySendEmail, setStatutorySendEmail] = useState(true)
  const [statutoryResult, setStatutoryResult] = useState<StatutoryDispatchResult | null>(null)

  // SA 505 Confirmations State
  const [sa505CampaignId, setSa505CampaignId] = useState('')
  const [sa505Result, setSa505Result] = useState<Sa505DispatchResult | null>(null)

  // OTP Simulator State
  const [otpDestination, setOtpDestination] = useState('+91 98765 43210')
  const [otpChannel, setOtpChannel] = useState<'whatsapp' | 'email'>('whatsapp')

  async function loadOverview() {
    try {
      setLoading(true)
      const data = await communicationsApi.getGatewayStatus()
      setOverview(data)
      setError(null)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load gateway overview.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadOverview()
  }, [])

  async function handleSendTestWhatsApp() {
    try {
      setBusy(true)
      setActionMessage(null)
      const res = await communicationsApi.testWhatsApp({
        toPhone: testPhone,
        customMessage: testWaText || undefined
      })
      setActionMessage(res.message)
      await loadOverview()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to dispatch test WhatsApp message.')
    } finally {
      setBusy(false)
    }
  }

  async function handleSendTestEmail() {
    try {
      setBusy(true)
      setActionMessage(null)
      const res = await communicationsApi.testEmail({
        toEmail: testEmail,
        subject: testEmailSubject,
        body: testEmailBody
      })
      setActionMessage(res.message)
      await loadOverview()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to dispatch test email.')
    } finally {
      setBusy(false)
    }
  }

  async function handleDispatchStatutoryDeadlines() {
    try {
      setBusy(true)
      setError(null)
      setActionMessage(null)
      const res = await communicationsApi.dispatchStatutoryDeadlines({
        regime: selectedRegime,
        daysHorizon: horizonDays,
        sendWhatsApp: statutorySendWa,
        sendEmail: statutorySendEmail
      })
      setStatutoryResult(res)
      setActionMessage(`Statutory campaign completed! Notified ${res.clientsNotified} clients (${res.whatsAppMessagesQueued} WhatsApp, ${res.emailMessagesQueued} Email).`)
      await loadOverview()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to dispatch statutory deadline campaign.')
    } finally {
      setBusy(false)
    }
  }

  async function handleSendPortalOtp() {
    try {
      setBusy(true)
      setActionMessage(null)
      const res = await communicationsApi.sendPortalOtp({
        destination: otpDestination,
        channel: otpChannel
      })
      setActionMessage(`${res.message} Masked: ${res.maskedDestination}`)
      await loadOverview()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to send verification code.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div style={{ maxWidth: '1280px', margin: '0 auto', padding: '24px 16px', fontFamily: "'Inter', sans-serif" }}>
      {/* Top Banner */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '24px' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '6px' }}>
            <span style={{ fontSize: '24px' }}>💬</span>
            <h1 style={{ fontSize: '24px', fontWeight: 800, margin: 0, color: '#0f172a' }}>
              Communications &amp; Real-Time Automation Gateway
            </h1>
            <span style={{ fontSize: '11px', background: '#dcfce7', color: '#15803d', padding: '3px 8px', borderRadius: '4px', fontWeight: 700 }}>
              META GRAPH API v21.0
            </span>
            <span style={{ fontSize: '11px', background: '#e0e7ff', color: '#3730a3', padding: '3px 8px', borderRadius: '4px', fontWeight: 700 }}>
              TRANSACTIONAL GATEWAY
            </span>
          </div>
          <p style={{ margin: 0, color: '#64748b', fontSize: '14px' }}>
            Multi-channel client communication hub for Indian CA firms. Automated WhatsApp filing alerts, SA 505 external confirmation links, client portal OTPs, and SendGrid/SES invoice delivery.
          </p>
        </div>

        <button
          onClick={loadOverview}
          disabled={loading || busy}
          style={{ padding: '8px 16px', background: '#f8fafc', border: '1px solid #cbd5e1', borderRadius: '6px', fontSize: '13px', fontWeight: 600, color: '#334155', cursor: 'pointer' }}
        >
          🔄 Refresh Status
        </button>
      </div>

      {/* Global Alerts */}
      {actionMessage && (
        <div style={{ background: '#f0fdf4', border: '1px solid #bbf7d0', color: '#166534', padding: '12px 16px', borderRadius: '6px', marginBottom: '20px', fontSize: '13px', display: 'flex', justifyContent: 'space-between' }}>
          <span>✓ {actionMessage}</span>
          <button onClick={() => setActionMessage(null)} style={{ background: 'none', border: 'none', color: '#166534', cursor: 'pointer', fontWeight: 700 }}>✕</button>
        </div>
      )}

      {error && (
        <div style={{ background: '#fef2f2', border: '1px solid #fecaca', color: '#991b1b', padding: '12px 16px', borderRadius: '6px', marginBottom: '20px', fontSize: '13px', display: 'flex', justifyContent: 'space-between' }}>
          <span>⚠️ {error}</span>
          <button onClick={() => setError(null)} style={{ background: 'none', border: 'none', color: '#991b1b', cursor: 'pointer', fontWeight: 700 }}>✕</button>
        </div>
      )}

      {/* KPI Overview Cards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px', marginBottom: '24px' }}>
        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
          <div style={{ fontSize: '12px', fontWeight: 600, color: '#64748b', marginBottom: '4px' }}>WhatsApp Cloud Status</div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span style={{ width: '10px', height: '10px', borderRadius: '50%', background: overview?.whatsAppStatus.isConnected ? '#22c55e' : '#f59e0b' }}></span>
            <span style={{ fontSize: '18px', fontWeight: 800, color: '#0f172a' }}>
              {overview?.whatsAppStatus.isConnected ? 'Connected (Live)' : 'Simulator Mode'}
            </span>
          </div>
          <div style={{ fontSize: '11px', color: '#94a3b8', marginTop: '6px' }}>
            WABA: {overview?.whatsAppStatus.wabaId || '—'} · Lang: {overview?.whatsAppStatus.templateLanguage || 'en'}
          </div>
        </div>

        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
          <div style={{ fontSize: '12px', fontWeight: 600, color: '#64748b', marginBottom: '4px' }}>Email Gateway Provider</div>
          <div style={{ fontSize: '18px', fontWeight: 800, color: '#0f172a' }}>
            {overview?.emailStatus.provider || 'SendGrid / AWS SES'}
          </div>
          <div style={{ fontSize: '11px', color: '#94a3b8', marginTop: '6px' }}>
            From: {overview?.emailStatus.fromAddress || 'notifications@cadesk.in'}
          </div>
        </div>

        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
          <div style={{ fontSize: '12px', fontWeight: 600, color: '#64748b', marginBottom: '4px' }}>Total Messages Dispatched</div>
          <div style={{ fontSize: '18px', fontWeight: 800, color: '#0f172a' }}>
            {overview?.totalDispatched ?? 0}
          </div>
          <div style={{ fontSize: '11px', color: '#94a3b8', marginTop: '6px' }}>
            WhatsApp: {overview?.whatsAppStatus.sentCount ?? 0} · Email: {overview?.emailStatus.sentCount ?? 0}
          </div>
        </div>

        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
          <div style={{ fontSize: '12px', fontWeight: 600, color: '#64748b', marginBottom: '4px' }}>Delivery Success Rate</div>
          <div style={{ fontSize: '18px', fontWeight: 800, color: '#16a34a' }}>
            {overview?.deliverySuccessRate ?? 100}%
          </div>
          <div style={{ fontSize: '11px', color: '#94a3b8', marginTop: '6px' }}>
            {overview?.whatsAppStatus.failedCount ?? 0} failed / undelivered
          </div>
        </div>
      </div>

      {/* Tabs Navigation */}
      <div style={{ display: 'flex', gap: '8px', borderBottom: '1px solid #e2e8f0', marginBottom: '24px' }}>
        {[
          { id: 'status', label: '⚙️ Gateway Status & Test Console' },
          { id: 'deadlines', label: '📅 Statutory Deadlines Campaign (GST/Tax/ITR)' },
          { id: 'sa505', label: '✉️ SA 505 Confirmation WhatsApp Links' },
          { id: 'invoices', label: '🧾 Invoices & SAC 998311 Delivery' },
          { id: 'logs', label: '📋 Live Outbox & Delivery Audit Log' }
        ].map(t => (
          <button
            key={t.id}
            onClick={() => setActiveTab(t.id as typeof activeTab)}
            style={{
              padding: '10px 18px',
              border: 'none',
              borderBottom: activeTab === t.id ? '2px solid #2563eb' : '2px solid transparent',
              background: 'none',
              fontWeight: activeTab === t.id ? 700 : 500,
              color: activeTab === t.id ? '#2563eb' : '#64748b',
              fontSize: '13px',
              cursor: 'pointer'
            }}
          >
            {t.label}
          </button>
        ))}
      </div>

      {/* TAB 1: GATEWAY STATUS & TEST CONSOLE */}
      {activeTab === 'status' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '24px' }}>
          {/* Meta WhatsApp Test Card */}
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '14px' }}>
              <span style={{ fontSize: '20px' }}>📱</span>
              <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 700, color: '#0f172a' }}>Meta WhatsApp Cloud API Test</h3>
            </div>
            <p style={{ fontSize: '12px', color: '#64748b', marginBottom: '16px' }}>
              Send a test WhatsApp message using approved Meta templates or live simulation to verify your webhook and recipient formatting.
            </p>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                Recipient Mobile Number (+91 India):
              </label>
              <input
                type="text"
                value={testPhone}
                onChange={e => setTestPhone(e.target.value)}
                placeholder="e.g. 9876543210"
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
              />
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                Custom Message / Remarks:
              </label>
              <textarea
                rows={3}
                value={testWaText}
                onChange={e => setTestWaText(e.target.value)}
                placeholder="Optional custom preview text..."
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
              />
            </div>

            <button
              onClick={handleSendTestWhatsApp}
              disabled={busy}
              style={{ width: '100%', padding: '10px 16px', background: '#15803d', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 700, fontSize: '13px', cursor: 'pointer' }}
            >
              {busy ? 'Dispatching WhatsApp...' : '📤 Send Test WhatsApp Message'}
            </button>
          </div>

          {/* Transactional Email Test Card */}
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '14px' }}>
              <span style={{ fontSize: '20px' }}>📧</span>
              <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 700, color: '#0f172a' }}>Transactional Email Gateway Test</h3>
            </div>
            <p style={{ fontSize: '12px', color: '#64748b', marginBottom: '16px' }}>
              Dispatch a test email via SendGrid v3 API / Amazon SES / SMTP relay to confirm SPF, DKIM, and deliverability.
            </p>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                Recipient Email Address:
              </label>
              <input
                type="email"
                value={testEmail}
                onChange={e => setTestEmail(e.target.value)}
                placeholder="partner@yourfirm.in"
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
              />
            </div>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                Email Subject:
              </label>
              <input
                type="text"
                value={testEmailSubject}
                onChange={e => setTestEmailSubject(e.target.value)}
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
              />
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                Email Body:
              </label>
              <textarea
                rows={2}
                value={testEmailBody}
                onChange={e => setTestEmailBody(e.target.value)}
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
              />
            </div>

            <button
              onClick={handleSendTestEmail}
              disabled={busy}
              style={{ width: '100%', padding: '10px 16px', background: '#0f172a', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 700, fontSize: '13px', cursor: 'pointer' }}
            >
              {busy ? 'Sending Email...' : '✉️ Send Test Transactional Email'}
            </button>
          </div>

          {/* Client Portal OTP Simulator */}
          <div style={{ gridColumn: 'span 2', background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '12px' }}>
              <span style={{ fontSize: '18px' }}>🔐</span>
              <h3 style={{ margin: 0, fontSize: '15px', fontWeight: 700, color: '#0f172a' }}>Client Portal OTP &amp; 2-Factor Authentication Simulator</h3>
            </div>
            <p style={{ fontSize: '12px', color: '#64748b', marginBottom: '14px' }}>
              Test instant 6-digit cryptographic TOTP / OTP delivery over WhatsApp Cloud API and Email for portal client logins and DSC approvals.
            </p>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr auto', gap: '12px', alignItems: 'flex-end' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>Destination (Phone or Email):</label>
                <input
                  type="text"
                  value={otpDestination}
                  onChange={e => setOtpDestination(e.target.value)}
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>Dispatch Channel:</label>
                <select
                  value={otpChannel}
                  onChange={e => setOtpChannel(e.target.value as 'whatsapp' | 'email')}
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
                >
                  <option value="whatsapp">📱 WhatsApp Cloud API (Template: portal_otp)</option>
                  <option value="email">📧 Transactional Email</option>
                </select>
              </div>

              <button
                onClick={handleSendPortalOtp}
                disabled={busy}
                style={{ padding: '8px 20px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 700, fontSize: '13px', cursor: 'pointer' }}
              >
                Send Verification OTP
              </button>
            </div>
          </div>
        </div>
      )}

      {/* TAB 2: STATUTORY DEADLINES CAMPAIGN */}
      {activeTab === 'deadlines' && (
        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '24px' }}>
          <div style={{ marginBottom: '20px' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 6px 0', color: '#0f172a' }}>
              Automated Statutory Filing Deadline Campaign Engine
            </h3>
            <p style={{ margin: 0, fontSize: '13px', color: '#64748b' }}>
              Automatically scans upcoming deadlines across your client portfolio, compiles filing details, and dispatches batch alerts via WhatsApp and Email to prevent Section 47 late fees and interest under Section 234A/B/C.
            </p>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px', background: '#f8fafc', padding: '16px', borderRadius: '6px', marginBottom: '20px' }}>
            <div>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>Statutory Regime:</label>
              <select
                value={selectedRegime}
                onChange={e => setSelectedRegime(e.target.value as typeof selectedRegime)}
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
              >
                <option value="GST">GST (GSTR-1, GSTR-3B, GSTR-9)</option>
                <option value="AdvanceTax">Advance Tax (Sec 211 / 234C)</option>
                <option value="ITR">Income Tax Return (ITR AY 2025-26)</option>
                <option value="All">All Statutory Compliances</option>
              </select>
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>Days Horizon:</label>
              <select
                value={horizonDays}
                onChange={e => setHorizonDays(Number(e.target.value))}
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
              >
                <option value={7}>Next 7 Days (Urgent)</option>
                <option value={15}>Next 15 Days</option>
                <option value={30}>Next 30 Days (Standard)</option>
                <option value={60}>Next 60 Days</option>
              </select>
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>Dispatch Channels:</label>
              <div style={{ display: 'flex', gap: '12px', paddingTop: '6px' }}>
                <label style={{ fontSize: '13px', display: 'flex', alignItems: 'center', gap: '6px', cursor: 'pointer' }}>
                  <input type="checkbox" checked={statutorySendWa} onChange={e => setStatutorySendWa(e.target.checked)} />
                  WhatsApp
                </label>
                <label style={{ fontSize: '13px', display: 'flex', alignItems: 'center', gap: '6px', cursor: 'pointer' }}>
                  <input type="checkbox" checked={statutorySendEmail} onChange={e => setStatutorySendEmail(e.target.checked)} />
                  Email
                </label>
              </div>
            </div>

            <div style={{ display: 'flex', alignItems: 'flex-end' }}>
              <button
                onClick={handleDispatchStatutoryDeadlines}
                disabled={busy}
                style={{ width: '100%', padding: '10px 16px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 700, fontSize: '13px', cursor: 'pointer' }}
              >
                {busy ? 'Running Campaign...' : '🚀 Dispatch Deadline Campaign'}
              </button>
            </div>
          </div>

          {statutoryResult && (
            <div style={{ background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: '6px', padding: '16px', marginTop: '16px' }}>
              <div style={{ fontWeight: 700, color: '#166534', marginBottom: '8px' }}>Campaign Execution Summary:</div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '12px', fontSize: '13px', color: '#14532d', marginBottom: '12px' }}>
                <div>Deadlines Checked: <b>{statutoryResult.deadlinesEvaluated}</b></div>
                <div>Clients Notified: <b>{statutoryResult.clientsNotified}</b></div>
                <div>WhatsApp Sent: <b>{statutoryResult.whatsAppMessagesQueued}</b></div>
                <div>Emails Dispatched: <b>{statutoryResult.emailMessagesQueued}</b></div>
              </div>
              {statutoryResult.dispatchedItems.length > 0 && (
                <div>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: '#166534', marginBottom: '4px' }}>Sample Dispatched Notices:</div>
                  <ul style={{ margin: 0, paddingLeft: '20px', fontSize: '12px', color: '#14532d' }}>
                    {statutoryResult.dispatchedItems.map((item, idx) => (
                      <li key={idx}>{item}</li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* TAB 3: SA 505 CONFIRMATION WHATSAPP LINKS */}
      {activeTab === 'sa505' && (
        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '24px' }}>
          <div style={{ marginBottom: '20px' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 6px 0', color: '#0f172a' }}>
              SA 505 Automated External Confirmation WhatsApp Circularization
            </h3>
            <p style={{ margin: 0, fontSize: '13px', color: '#64748b' }}>
              Directly circulates tokenized digital confirmation links to third-party debtors, creditors, and banks via WhatsApp with 1-click verification buttons and transactional email letters.
            </p>
          </div>

          <div style={{ background: '#eff6ff', border: '1px solid #bfdbfe', borderRadius: '6px', padding: '16px', marginBottom: '20px', fontSize: '13px', color: '#1e40af' }}>
            <div style={{ fontWeight: 700, marginBottom: '6px' }}>Meta WhatsApp Interactive Button Preview:</div>
            <div style={{ background: '#fff', border: '1px solid #cbd5e1', borderRadius: '6px', padding: '12px', maxWidth: '440px', color: '#0f172a' }}>
              <div style={{ fontSize: '12px', marginBottom: '8px' }}>
                Dear <b>Apex Enterprises Ltd</b>,<br /><br />
                We are conducting the statutory audit of <b>Bharat Manufacturing Ltd</b> for FY ended 31 Mar 2025. Their books reflect a balance of <b>₹1,45,20,000 Dr</b> with your organization.<br /><br />
                Please verify and confirm this balance via your secure link below.
              </div>
              <div style={{ textAlign: 'center', background: '#f8fafc', border: '1px solid #cbd5e1', padding: '8px', borderRadius: '4px', color: '#0284c7', fontWeight: 700, fontSize: '12px' }}>
                🔗 Review &amp; Digitally Confirm Balance
              </div>
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', gap: '16px', alignItems: 'flex-end' }}>
            <div>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                Campaign Reference / ID:
              </label>
              <input
                type="text"
                value={sa505CampaignId}
                onChange={e => setSa505CampaignId(e.target.value)}
                placeholder="Enter or select Confirmation Campaign ID..."
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
              />
            </div>

            <button
              onClick={async () => {
                if (!sa505CampaignId.trim()) {
                  alert('Please enter a Campaign ID or navigate to Audit Workspace → Confirmations to trigger campaign dispatch.')
                  return
                }
                try {
                  setBusy(true)
                  const res = await communicationsApi.dispatchSa505Confirmations({
                    campaignId: sa505CampaignId.trim(),
                    sendWhatsApp: true,
                    sendEmail: true
                  })
                  setSa505Result(res)
                  setActionMessage(`Dispatched SA 505 confirmations to ${res.totalProcessed} parties!`)
                } catch (err: unknown) {
                  setError(err instanceof Error ? err.message : 'Failed to dispatch confirmations.')
                } finally {
                  setBusy(false)
                }
              }}
              disabled={busy}
              style={{ padding: '10px 24px', background: '#0f172a', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 700, fontSize: '13px', cursor: 'pointer' }}
            >
              📤 Dispatch SA 505 WhatsApp &amp; Email Links
            </button>
          </div>

          {sa505Result && (
            <div style={{ marginTop: '20px', background: '#f0fdf4', padding: '16px', borderRadius: '6px', border: '1px solid #bbf7d0' }}>
              <div style={{ fontWeight: 700, color: '#166534', marginBottom: '6px' }}>SA 505 Circularization Dispatched:</div>
              <div style={{ fontSize: '13px', color: '#14532d' }}>
                Total Requests: {sa505Result.totalProcessed} · WhatsApp: {sa505Result.whatsAppSent} · Email: {sa505Result.emailSent}
              </div>
            </div>
          )}
        </div>
      )}

      {/* TAB 4: INVOICES & SAC 998311 DELIVERY */}
      {activeTab === 'invoices' && (
        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '24px' }}>
          <div style={{ marginBottom: '20px' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 6px 0', color: '#0f172a' }}>
              Transactional Invoice Delivery (SAC 998311)
            </h3>
            <p style={{ margin: 0, fontSize: '13px', color: '#64748b' }}>
              Dispatches statutory tax invoices with PDF attachments via SendGrid / AWS SES, along with instant WhatsApp summary alerts and client payment portal links.
            </p>
          </div>

          <div style={{ background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: '6px', padding: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div>
              <div style={{ fontWeight: 700, fontSize: '14px', color: '#0f172a' }}>Demo Invoice: INV-2025-001 (Bharat Manufacturing Ltd)</div>
              <div style={{ fontSize: '12px', color: '#64748b', marginTop: '4px' }}>
                Professional Accounting &amp; Audit Services (SAC 998311) · Amount: ₹59,000 (Incl. 18% GST)
              </div>
            </div>

            <button
              onClick={async () => {
                alert('Invoice dispatch demo simulated: Email with Tax Invoice HTML/PDF attachment and WhatsApp alert queued for dispatch!')
              }}
              style={{ padding: '8px 18px', background: '#0f172a', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 700, fontSize: '13px', cursor: 'pointer' }}
            >
              ✉️ Send Invoice via Email &amp; WhatsApp
            </button>
          </div>
        </div>
      )}

      {/* TAB 5: LIVE OUTBOX & AUDIT LOG */}
      {activeTab === 'logs' && (
        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, margin: 0, color: '#0f172a' }}>
              Recent Communications &amp; Outbound Dispatches
            </h3>
            <span style={{ fontSize: '12px', color: '#64748b' }}>
              Showing latest {overview?.recentDispatches.length ?? 0} messages
            </span>
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
              <thead>
                <tr style={{ background: '#f8fafc', borderBottom: '1px solid #e2e8f0', textAlign: 'left' }}>
                  <th style={{ padding: '10px 12px', color: '#475569' }}>Timestamp</th>
                  <th style={{ padding: '10px 12px', color: '#475569' }}>Channel</th>
                  <th style={{ padding: '10px 12px', color: '#475569' }}>Recipient</th>
                  <th style={{ padding: '10px 12px', color: '#475569' }}>Template / Subject</th>
                  <th style={{ padding: '10px 12px', color: '#475569' }}>Status</th>
                </tr>
              </thead>
              <tbody>
                {overview?.recentDispatches && overview.recentDispatches.length > 0 ? (
                  overview.recentDispatches.map((n: NotificationItem) => (
                    <tr key={n.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                      <td style={{ padding: '10px 12px', color: '#64748b', whiteSpace: 'nowrap' }}>
                        {new Date(n.sentAt ?? n.scheduledAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                      </td>
                      <td style={{ padding: '10px 12px' }}>
                        <span style={{
                          padding: '2px 8px',
                          borderRadius: '4px',
                          fontSize: '11px',
                          fontWeight: 700,
                          background: n.channel === 'whatsapp' ? '#dcfce7' : '#e0e7ff',
                          color: n.channel === 'whatsapp' ? '#15803d' : '#3730a3'
                        }}>
                          {n.channel === 'whatsapp' ? '📱 WhatsApp' : '📧 Email'}
                        </span>
                      </td>
                      <td style={{ padding: '10px 12px' }}>
                        <div style={{ fontWeight: 600, color: '#0f172a' }}>{n.recipientName}</div>
                        <div style={{ fontSize: '11px', color: '#94a3b8' }}>{n.toAddress}</div>
                      </td>
                      <td style={{ padding: '10px 12px' }}>
                        <div style={{ fontWeight: 600, color: '#334155' }}>{n.templateKey}</div>
                        <div style={{ fontSize: '11px', color: '#64748b', maxWidth: '300px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                          {n.subject || n.body}
                        </div>
                      </td>
                      <td style={{ padding: '10px 12px' }}>
                        <span style={{
                          padding: '2px 8px',
                          borderRadius: '4px',
                          fontSize: '11px',
                          fontWeight: 700,
                          background: n.status === 'sent' ? '#f0fdf4' : n.status === 'failed' ? '#fef2f2' : '#f8fafc',
                          color: n.status === 'sent' ? '#16a34a' : n.status === 'failed' ? '#dc2626' : '#64748b'
                        }}>
                          {n.status.toUpperCase()}
                        </span>
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={5} style={{ padding: '24px', textAlign: 'center', color: '#94a3b8' }}>
                      No communications dispatched yet. Use the Test Console or Deadline Campaign to initiate messages.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  )
}
