import { useEffect, useState } from 'react'
import {
  type SubscriptionStatusDto,
  type PlanPricingOptionDto,
  type SubscriptionInvoiceDto,
  type UpgradePlanRequest,
  subscriptionApi
} from './api'

const fmtInr = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n || 0)

export function SubscriptionsPage({ onDemoSeeded }: { onDemoSeeded?: () => void }) {
  const [status, setStatus] = useState<SubscriptionStatusDto | null>(null)
  const [plans, setPlans] = useState<PlanPricingOptionDto[]>([])
  const [invoices, setInvoices] = useState<SubscriptionInvoiceDto[]>([])
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)
  const [billingInterval, setBillingInterval] = useState<'Monthly' | 'Annual'>('Monthly')

  // Checkout modal
  const [selectedPlanForUpgrade, setSelectedPlanForUpgrade] = useState<PlanPricingOptionDto | null>(null)
  const [firmGstin, setFirmGstin] = useState<string>('')
  const [paymentMethod, setPaymentMethod] = useState<string>('UPI AutoPay')
  const [upgrading, setUpgrading] = useState<boolean>(false)

  // Demo seeder
  const [seedingDemo, setSeedingDemo] = useState<boolean>(false)
  const [purgingDemo, setPurgingDemo] = useState<boolean>(false)
  const [demoMessage, setDemoMessage] = useState<string | null>(null)

  useEffect(() => {
    loadSubscriptionData()
  }, [])

  async function loadSubscriptionData() {
    try {
      setLoading(true)
      const [s, p, inv] = await Promise.all([
        subscriptionApi.getStatus(),
        subscriptionApi.getPlans(),
        subscriptionApi.getInvoices().catch(() => [])
      ])
      setStatus(s)
      setPlans(p || [])
      setInvoices(inv || [])
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load subscription details.')
    } finally {
      setLoading(false)
    }
  }

  async function handleUpgrade() {
    if (!selectedPlanForUpgrade) return
    try {
      setUpgrading(true)
      const req: UpgradePlanRequest = {
        targetTier: selectedPlanForUpgrade.tierCode,
        billingInterval,
        paymentMethod,
        firmGstin: firmGstin.trim() || null
      }
      const res = await subscriptionApi.upgradePlan(req)
      alert(`🎉 ${res.message}\nInvoice: ${res.invoiceNumber}\nAmount: ${fmtInr(res.totalAmountPaid)} (Includes 18% GST under SAC 998311)`)
      setSelectedPlanForUpgrade(null)
      await loadSubscriptionData()
    } catch (err: unknown) {
      alert(`Upgrade failed: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setUpgrading(false)
    }
  }

  async function handleSeedDemoData() {
    if (!confirm('This will populate your firm workspace with 3 realistic sample audit clients (Apex Global, Bharat Steel, Vanguard Diagnostics) with trial balances, lead schedules, and bank forensics. Continue?')) return
    try {
      setSeedingDemo(true)
      const res = await subscriptionApi.seedDemoData()
      setDemoMessage(res.message)
      await loadSubscriptionData()
      onDemoSeeded?.()
    } catch (err: unknown) {
      alert(`Demo seeding failed: ${err instanceof Error ? err.message : 'Failed'}`)
    } finally {
      setSeedingDemo(false)
    }
  }

  async function handlePurgeDemoData() {
    if (!confirm('Are you sure you want to remove all pre-loaded sample demo clients and test audit files?')) return
    try {
      setPurgingDemo(true)
      const res = await subscriptionApi.purgeDemoData()
      setDemoMessage(res.message)
      await loadSubscriptionData()
      onDemoSeeded?.()
    } catch (err: unknown) {
      alert(`Purge failed: ${err instanceof Error ? err.message : 'Failed'}`)
    } finally {
      setPurgingDemo(false)
    }
  }

  if (loading) {
    return <div style={{ padding: '40px', textAlign: 'center', color: 'var(--muted)' }}>Loading subscription details...</div>
  }

  return (
    <div style={{ padding: '28px', maxWidth: '1360px', margin: '0 auto', color: 'var(--text)' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px', flexWrap: 'wrap', gap: '14px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 4px 0', color: 'var(--text)' }}>
            💳 Subscription, Licensing &amp; Firm Plan
          </h1>
          <p style={{ fontSize: '13px', color: 'var(--muted)', margin: 0 }}>
            Manage your firm's CaDesk SaaS subscription tier, member capacities, and official GST tax invoices (SAC 998311).
          </p>
        </div>
        <div style={{ display: 'flex', gap: '10px' }}>
          {status?.isDemoDataSeeded ? (
            <button
              onClick={handlePurgeDemoData}
              disabled={purgingDemo}
              style={{ padding: '8px 16px', background: 'var(--error-bg)', color: 'var(--error)', border: '1px solid var(--error-border)', borderRadius: '6px', fontWeight: 600, fontSize: '13px', cursor: 'pointer' }}
            >
              {purgingDemo ? 'Clearing...' : '🗑️ Clear Sample Demo Clients'}
            </button>
          ) : (
            <button
              onClick={handleSeedDemoData}
              disabled={seedingDemo}
              style={{ padding: '8px 16px', background: 'var(--success-bg)', color: 'var(--success-text)', border: '1px solid var(--success-border)', borderRadius: '6px', fontWeight: 600, fontSize: '13px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '6px' }}
            >
              <span>{seedingDemo ? 'Seeding Workspace...' : '⚡ Pre-load Sample CA Firm (1-Click Demo)'}</span>
            </button>
          )}
        </div>
      </div>

      {/* Error notification banner */}
      {error && (
        <div style={{ padding: '12px 16px', background: 'var(--error-bg)', border: '1px solid var(--error-border)', borderRadius: '8px', marginBottom: '20px', color: 'var(--error-text)', fontSize: '13px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>⚠️ {error}</div>
          <button onClick={() => setError(null)} style={{ background: 'none', border: 'none', color: 'var(--error-text)', cursor: 'pointer', fontWeight: 700 }}>✕</button>
        </div>
      )}

      {/* Demo notification banner */}
      {demoMessage && (
        <div style={{ padding: '12px 16px', background: 'var(--success-bg)', border: '1px solid #86efac', borderRadius: '8px', marginBottom: '20px', color: 'var(--success-text)', fontSize: '13px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>{demoMessage}</div>
          <button onClick={() => setDemoMessage(null)} style={{ background: 'none', border: 'none', color: 'var(--success-text)', cursor: 'pointer', fontWeight: 700 }}>✕</button>
        </div>
      )}

      {/* Trial Countdown or Active Plan Banner */}
      {status && (
        <div style={{
          background: status.isTrialActive ? 'linear-gradient(135deg, #1e3a8a 0%, #3b82f6 100%)' : 'linear-gradient(135deg, #0f172a 0%, #1e293b 100%)',
          color: '#fff',
          padding: '24px',
          borderRadius: '10px',
          marginBottom: '24px',
          boxShadow: '0 4px 6px -1px rgba(0,0,0,0.1)'
        }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '16px' }}>
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '8px' }}>
                <span style={{ fontSize: '20px', fontWeight: 700 }}>{status.displayName}</span>
                <span style={{
                  padding: '3px 10px',
                  borderRadius: '12px',
                  fontSize: '11px',
                  fontWeight: 700,
                  textTransform: 'uppercase',
                  background: status.isTrialActive ? '#fef08a' : '#bbf7d0',
                  color: status.isTrialActive ? '#854d0e' : 'var(--success-text)'
                }}>
                  {status.isTrialActive ? `Free Trial • ${status.daysRemainingInTrial} Days Remaining` : status.status}
                </span>
              </div>
              <p style={{ margin: 0, fontSize: '13px', color: '#e2e8f0', maxWidth: '640px' }}>
                {status.isTrialActive
                  ? `Your 14-day full access trial gives you complete access to all audit workpapers, lead schedules, and forensic engines. Upgrade at any time to preserve your firm's data.`
                  : `Subscription active on ${status.billingInterval} billing. Next renewal date: ${new Date(status.currentPeriodEnd).toLocaleDateString('en-IN', { day: 'numeric', month: 'long', year: 'numeric' })}.`}
              </p>
            </div>
            {status.isTrialActive && (
              <div>
                <button
                  onClick={() => {
                    const g = plans.find(p => p.tierCode === 'GROWTH') || plans[1]
                    if (g) setSelectedPlanForUpgrade(g)
                  }}
                  style={{ padding: '12px 22px', background: '#22c55e', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 700, fontSize: '14px', cursor: 'pointer', boxShadow: '0 2px 4px rgba(0,0,0,0.2)' }}
                >
                  🚀 Activate Full Plan Now
                </button>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Usage vs Limits Grid */}
      {status && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '16px', marginBottom: '32px' }}>
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '18px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 600, textTransform: 'uppercase' }}>Licensed Staff Seats</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--text)', margin: '6px 0 2px 0' }}>
              {status.currentUsersCount} <span style={{ fontSize: '14px', color: 'var(--muted)', fontWeight: 400 }}>/ {status.userLimit === -1 ? 'Unlimited' : status.userLimit}</span>
            </div>
            <div style={{ fontSize: '11px', color: 'var(--muted)' }}>Active firm partners and article assistants</div>
          </div>

          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '18px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 600, textTransform: 'uppercase' }}>Client Entities Capacity</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--text)', margin: '6px 0 2px 0' }}>
              {status.currentClientsCount} <span style={{ fontSize: '14px', color: 'var(--muted)', fontWeight: 400 }}>/ {status.clientLimit === -1 ? 'Unlimited' : status.clientLimit}</span>
            </div>
            <div style={{ fontSize: '11px', color: 'var(--muted)' }}>Registered companies, LLPs, and firms</div>
          </div>

          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '18px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 600, textTransform: 'uppercase' }}>Active Statutory Audits</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--text)', margin: '6px 0 2px 0' }}>
              {status.activeAuditsCount} <span style={{ fontSize: '14px', color: 'var(--muted)', fontWeight: 400 }}>/ {status.activeAuditsLimit === -1 ? 'Unlimited' : status.activeAuditsLimit}</span>
            </div>
            <div style={{ fontSize: '11px', color: 'var(--muted)' }}>Statutory &amp; tax audit engagements</div>
          </div>

          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '18px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 600, textTransform: 'uppercase' }}>Sample Workspace</div>
            <div style={{ fontSize: '16px', fontWeight: 700, color: status.isDemoDataSeeded ? 'var(--success)' : 'var(--muted)', margin: '10px 0 2px 0' }}>
              {status.isDemoDataSeeded ? '✅ Demo Data Active' : 'Clean Slate (No Demo)'}
            </div>
            <div style={{ fontSize: '11px', color: 'var(--muted)' }}>Apex Global &amp; Bharat Steel sample audits</div>
          </div>
        </div>
      )}

      {/* Interval Switcher */}
      <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '12px', marginBottom: '24px' }}>
        <button
          onClick={() => setBillingInterval('Monthly')}
          style={{
            padding: '8px 18px',
            borderRadius: '20px',
            border: '1px solid var(--border-subtle)',
            background: billingInterval === 'Monthly' ? '#2563eb' : 'var(--card)',
            color: billingInterval === 'Monthly' ? '#fff' : 'var(--muted)',
            fontWeight: 600,
            fontSize: '13px',
            cursor: 'pointer'
          }}
        >
          Monthly Billing
        </button>
        <button
          onClick={() => setBillingInterval('Annual')}
          style={{
            padding: '8px 18px',
            borderRadius: '20px',
            border: '1px solid var(--border-subtle)',
            background: billingInterval === 'Annual' ? '#2563eb' : 'var(--card)',
            color: billingInterval === 'Annual' ? '#fff' : 'var(--muted)',
            fontWeight: 600,
            fontSize: '13px',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '6px'
          }}
        >
          <span>Annual Billing</span>
          <span style={{ fontSize: '11px', padding: '2px 6px', borderRadius: '10px', background: '#22c55e', color: '#fff' }}>SAVE 20%</span>
        </button>
      </div>

      {/* 3-Tier Pricing Plan Table */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '20px', marginBottom: '40px' }}>
        {plans.map(p => {
          const isCurrent = status?.planTier === p.tierCode
          const price = billingInterval === 'Annual' ? p.annualPrice : p.monthlyPrice
          const perMonthEquiv = billingInterval === 'Annual' ? Math.round(p.annualPrice / 12) : p.monthlyPrice

          return (
            <div
              key={p.tierCode}
              style={{
                background: 'var(--card)',
                border: p.isPopular ? '2px solid var(--accent)' : '1px solid var(--border)',
                borderRadius: '12px',
                padding: '24px',
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'space-between',
                position: 'relative',
                boxShadow: p.isPopular ? '0 10px 15px -3px rgba(37,99,235,0.1)' : 'none'
              }}
            >
              {p.isPopular && (
                <div style={{ position: 'absolute', top: '-12px', left: '50%', transform: 'translateX(-50%)', background: '#2563eb', color: '#fff', fontSize: '11px', fontWeight: 700, padding: '3px 12px', borderRadius: '12px', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                  ⭐ Most Popular for CA Firms
                </div>
              )}

              <div>
                <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 6px 0', color: 'var(--text)' }}>{p.displayName}</h3>
                <p style={{ fontSize: '12px', color: 'var(--muted)', minHeight: '36px', margin: '0 0 16px 0' }}>{p.description}</p>

                <div style={{ margin: '16px 0', borderBottom: '1px solid var(--border)', paddingBottom: '16px' }}>
                  <div style={{ fontSize: '32px', fontWeight: 800, color: 'var(--text)' }}>
                    {fmtInr(perMonthEquiv)}
                    <span style={{ fontSize: '13px', fontWeight: 400, color: 'var(--muted)' }}> / month</span>
                  </div>
                  {billingInterval === 'Annual' && (
                    <div style={{ fontSize: '11px', color: '#16a34a', fontWeight: 600, marginTop: '2px' }}>
                      Billed annually at {fmtInr(price)} + GST
                    </div>
                  )}
                </div>

                <div style={{ marginBottom: '20px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px' }}>Capacity Limits:</div>
                  <div style={{ fontSize: '12px', color: 'var(--text-secondary)', display: 'flex', flexDirection: 'column', gap: '4px' }}>
                    <div>👥 <strong>{p.userLimit === -1 ? 'Unlimited' : p.userLimit}</strong> staff &amp; partner seats</div>
                    <div>🏢 <strong>{p.clientLimit === -1 ? 'Unlimited' : p.clientLimit}</strong> client entities</div>
                    <div>📑 <strong>{p.activeAuditsLimit === -1 ? 'Unlimited' : p.activeAuditsLimit}</strong> concurrent audit engagements</div>
                  </div>
                </div>

                <div>
                  <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px' }}>Included Capabilities:</div>
                  <ul style={{ margin: 0, padding: 0, listStyle: 'none', display: 'flex', flexDirection: 'column', gap: '6px', fontSize: '12px', color: 'var(--text-secondary)' }}>
                    {p.includedFeatures.map((f, idx) => (
                      <li key={idx} style={{ display: 'flex', alignItems: 'flex-start', gap: '6px' }}>
                        <span style={{ color: '#16a34a', fontWeight: 700 }}>✓</span>
                        <span>{f}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              </div>

              <div style={{ marginTop: '24px' }}>
                <button
                  onClick={() => setSelectedPlanForUpgrade(p)}
                  disabled={isCurrent && !status?.isTrialActive}
                  style={{
                    width: '100%',
                    padding: '12px',
                    borderRadius: '6px',
                    border: 'none',
                    background: isCurrent && !status?.isTrialActive ? 'var(--card-subtle)' : (p.isPopular ? '#2563eb' : '#0f172a'),
                    color: isCurrent && !status?.isTrialActive ? 'var(--muted)' : '#fff',
                    fontWeight: 700,
                    fontSize: '13px',
                    cursor: isCurrent && !status?.isTrialActive ? 'default' : 'pointer'
                  }}
                >
                  {isCurrent && !status?.isTrialActive ? 'Current Active Plan' : `Upgrade to ${p.displayName.split(' ')[0]}`}
                </button>
              </div>
            </div>
          )
        })}
      </div>

      {/* Subscription Invoices History */}
      <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '20px' }}>
        <h3 style={{ fontSize: '16px', fontWeight: 600, margin: '0 0 14px 0', color: 'var(--text)' }}>
          📄 Tax Invoices &amp; Payment History (SAC 998311)
        </h3>
        {invoices.length === 0 ? (
          <div style={{ padding: '24px', textAlign: 'center', color: 'var(--muted)', fontSize: '13px' }}>
            No paid subscription invoices generated yet. Invoices with 18% GST credit will appear here upon plan upgrade.
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
              <thead>
                <tr style={{ background: 'var(--card-subtle)', borderBottom: '2px solid var(--border)', color: 'var(--text-secondary)', textAlign: 'left' }}>
                  <th style={{ padding: '10px 12px' }}>Invoice #</th>
                  <th style={{ padding: '10px 12px' }}>Date</th>
                  <th style={{ padding: '10px 12px' }}>Plan &amp; Interval</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Base Amount</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>GST (18%)</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Total Paid</th>
                  <th style={{ padding: '10px 12px' }}>Method</th>
                  <th style={{ padding: '10px 12px' }}>Status</th>
                </tr>
              </thead>
              <tbody>
                {invoices.map(inv => (
                  <tr key={inv.id} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td style={{ padding: '10px 12px', fontWeight: 600, fontFamily: 'monospace' }}>{inv.invoiceNumber}</td>
                    <td style={{ padding: '10px 12px' }}>{inv.invoiceDate}</td>
                    <td style={{ padding: '10px 12px' }}>{inv.planTier} ({inv.billingInterval})</td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>{fmtInr(inv.baseAmount)}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>{fmtInr(inv.cgstAmount + inv.sgstAmount + inv.igstAmount)}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 700, color: 'var(--text)' }}>{fmtInr(inv.totalAmount)}</td>
                    <td style={{ padding: '10px 12px' }}>{inv.paymentMethod}</td>
                    <td style={{ padding: '10px 12px' }}>
                      <span style={{ padding: '2px 8px', borderRadius: '10px', background: 'var(--success-bg)', color: 'var(--success)', fontWeight: 600, fontSize: '11px' }}>
                        {inv.paymentStatus}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Checkout / Upgrade Modal */}
      {selectedPlanForUpgrade && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: 'var(--card)', borderRadius: '10px', width: '520px', padding: '24px', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)' }}>
            <h3 style={{ margin: '0 0 6px 0', fontSize: '18px', color: '#1e3a8a' }}>
              Confirm Plan Upgrade: {selectedPlanForUpgrade.displayName}
            </h3>
            <p style={{ fontSize: '12px', color: 'var(--muted)', margin: '0 0 16px 0' }}>
              Select payment method to activate your firm's SaaS license with statutory GST tax invoice.
            </p>

            {(() => {
              const base = billingInterval === 'Annual' ? selectedPlanForUpgrade.annualPrice : selectedPlanForUpgrade.monthlyPrice
              const gst = Math.round(base * 0.18)
              const total = base + gst

              return (
                <div style={{ background: 'var(--card-subtle)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', marginBottom: '16px', fontSize: '13px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '6px' }}>
                    <span>Base Subscription ({billingInterval}):</span>
                    <strong>{fmtInr(base)}</strong>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '6px', color: 'var(--muted)' }}>
                    <span>GST @ 18% (SAC 998311 IT Software):</span>
                    <span>{fmtInr(gst)}</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', borderTop: '1px solid var(--border-subtle)', paddingTop: '8px', marginTop: '6px', fontSize: '15px' }}>
                    <span style={{ fontWeight: 700 }}>Total Payable:</span>
                    <strong style={{ color: '#1e3a8a' }}>{fmtInr(total)}</strong>
                  </div>
                </div>
              )
            })()}

            <div style={{ marginBottom: '14px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Payment Method</label>
              <select
                value={paymentMethod}
                onChange={e => setPaymentMethod(e.target.value)}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
              >
                <option value="UPI AutoPay">UPI AutoPay (Google Pay / PhonePe / Paytm) - Recommended</option>
                <option value="NetBanking">NetBanking (HDFC / ICICI / SBI / Axis)</option>
                <option value="Corporate Credit Card">Corporate Credit Card (Visa / Mastercard / RuPay)</option>
                <option value="Official Purchase Order">Firm Purchase Order &amp; Direct Bank Transfer</option>
              </select>
            </div>

            <div style={{ marginBottom: '18px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>
                Firm GSTIN (Optional - for Input Tax Credit)
              </label>
              <input
                type="text"
                maxLength={15}
                value={firmGstin}
                onChange={e => setFirmGstin(e.target.value.toUpperCase())}
                placeholder="27AABCS1234F1Z8"
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)', fontFamily: 'monospace' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button onClick={() => setSelectedPlanForUpgrade(null)} style={{ padding: '8px 16px', background: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', borderRadius: '4px', cursor: 'pointer' }}>Cancel</button>
              <button
                onClick={handleUpgrade}
                disabled={upgrading}
                style={{ padding: '8px 18px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}
              >
                {upgrading ? 'Processing...' : 'Complete Payment & Activate'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

export default SubscriptionsPage
