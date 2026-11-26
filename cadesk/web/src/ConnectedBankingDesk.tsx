import { useState, useEffect } from 'react'
import {
  connectedBankingApi,
  errorMessage,
  type ConnectedBankSummary,
  type ConnectBankRequest,
  type PayoutBatch,
  type CreatePayoutItemInput,
  type BankAccountDto
} from './api'

interface ConnectedBankingDeskProps {
  accounts: BankAccountDto[]
  onRefreshAccounts?: () => void
}

type DeskTab = 'feeds' | 'payouts' | 'challans'

export default function ConnectedBankingDesk({ accounts, onRefreshAccounts }: ConnectedBankingDeskProps) {
  const [activeTab, setActiveTab] = useState<DeskTab>('feeds')
  const [connectedList, setConnectedList] = useState<ConnectedBankSummary[]>([])
  const [payouts, setPayouts] = useState<PayoutBatch[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [toast, setToast] = useState<string | null>(null)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)

  // Connect Bank Modal
  const [showConnectModal, setShowConnectModal] = useState<boolean>(false)
  const [connectForm, setConnectForm] = useState<ConnectBankRequest>({
    bankAccountId: '',
    provider: 'ICICI_Corporate',
    corporateId: '',
    userId: '',
    apiKey: '',
    apiSecret: '',
    webhookSecret: ''
  })

  // Create Payout Batch Modal
  const [showPayoutModal, setShowPayoutModal] = useState<boolean>(false)
  const [payoutTitle, setPayoutTitle] = useState<string>('')
  const [selectedDebitAccountId, setSelectedDebitAccountId] = useState<string>('')
  const [payoutRemarks, setPayoutRemarks] = useState<string>('')
  const [payoutItems, setPayoutItems] = useState<CreatePayoutItemInput[]>([
    {
      payoutType: 'VendorInvoice',
      beneficiaryName: '',
      beneficiaryAccountNumber: '',
      beneficiaryIfsc: '',
      amount: 0,
      paymentMode: 'NEFT',
      narration: 'Vendor Settlement',
      invoiceOrChallanRef: ''
    }
  ])

  // Authorize Payout Modal (Checker)
  const [authorizingBatch, setAuthorizingBatch] = useState<PayoutBatch | null>(null)
  const [authMethod, setAuthMethod] = useState<'OTP' | 'DSC_SHA256' | 'Biometric'>('OTP')
  const [otpToken, setOtpToken] = useState<string>('849201')
  const [partnerRemarks, setPartnerRemarks] = useState<string>('Authorized by Managing Partner after invoice inspection.')

  // Reject Batch Modal
  const [rejectingBatch, setRejectingBatch] = useState<PayoutBatch | null>(null)
  const [rejectReason, setRejectReason] = useState<string>('')

  // Tax Challan Form
  const [challanAccount, setChallanAccount] = useState<string>('')
  const [challanType, setChallanType] = useState<'AdvanceTax_ITNS280' | 'TDS_ITNS281' | 'GST_PMT06'>('AdvanceTax_ITNS280')
  const [fy, setFy] = useState<string>('2024-25')
  const [ay, setAy] = useState<string>('2025-26')
  const [majorHead, setMajorHead] = useState<string>('0020') // 0020 Company
  const [minorHead, setMinorHead] = useState<string>('100') // 100 Advance Tax
  const [panTanGstin, setPanTanGstin] = useState<string>('AAACA1234F')
  const [taxAmount, setTaxAmount] = useState<number>(150000)
  const [surcharge, setSurcharge] = useState<number>(0)
  const [cess, setCess] = useState<number>(6000)
  const [interest, setInterest] = useState<number>(0)
  const [feePenalty, setFeePenalty] = useState<number>(0)

  const showToast = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(null), 3500)
  }

  const loadData = async () => {
    try {
      setLoading(true)
      const [accs, batches] = await Promise.all([
        connectedBankingApi.getAccounts(),
        connectedBankingApi.getPayouts()
      ])
      setConnectedList(accs)
      setPayouts(batches)
      setErrorMsg(null)
    } catch (err: unknown) {
      setErrorMsg(errorMessage(err, 'Failed to load connected banking data.'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadData()
  }, [])

  // Handle Connect Bank
  const handleConnectBank = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!connectForm.bankAccountId) {
      alert('Please select a bank account to connect.')
      return
    }
    try {
      await connectedBankingApi.connectAccount(connectForm)
      showToast(`Successfully connected ${connectForm.provider} API!`)
      setShowConnectModal(false)
      loadData()
    } catch (err: unknown) {
      alert(errorMessage(err, 'Failed to connect bank API.'))
    }
  }

  // Handle 1-Click Live Sync
  const handleSyncFeed = async (accId: string, bankName: string) => {
    try {
      const res = await connectedBankingApi.syncFeed(accId)
      showToast(res.message || `Synchronized live transactions for ${bankName}.`)
      loadData()
      if (onRefreshAccounts) onRefreshAccounts()
    } catch (err: unknown) {
      alert(errorMessage(err, 'Live synchronization failed.'))
    }
  }

  // Add Item to Payout Batch
  const handleAddPayoutItem = () => {
    setPayoutItems([
      ...payoutItems,
      {
        payoutType: 'VendorInvoice',
        beneficiaryName: '',
        beneficiaryAccountNumber: '',
        beneficiaryIfsc: '',
        amount: 0,
        paymentMode: 'NEFT',
        narration: 'Vendor Invoice Settlement',
        invoiceOrChallanRef: ''
      }
    ])
  }

  // Handle Create Payout Batch
  const handleCreatePayoutBatch = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedDebitAccountId) {
      alert('Please select a debit bank account.')
      return
    }

    try {
      await connectedBankingApi.createPayoutBatch({
        bankAccountId: selectedDebitAccountId,
        title: payoutTitle || 'Vendor & Salary Disbursement Batch',
        makerRemarks: payoutRemarks,
        items: payoutItems
      })
      showToast('Payout batch created successfully in Draft status.')
      setShowPayoutModal(false)
      loadData()
    } catch (err: unknown) {
      alert(errorMessage(err, 'Failed to create payout batch.'))
    }
  }

  // Submit Payout Batch for Partner Review
  const handleSubmitBatch = async (batchId: string) => {
    try {
      await connectedBankingApi.submitPayoutBatch(batchId)
      showToast('Payout batch submitted for Managing Partner authorization.')
      loadData()
    } catch (err: unknown) {
      alert(errorMessage(err, 'Failed to submit batch.'))
    }
  }

  // Authorize Payout Batch (Partner Checker)
  const handleAuthorizeBatch = async () => {
    if (!authorizingBatch) return
    try {
      const res = await connectedBankingApi.authorizePayoutBatch(authorizingBatch.id, {
        authMethod,
        otpOrToken: otpToken,
        partnerRemarks
      })
      showToast(`Batch #${res.batchNumber} authorized & disbursed! Ref: ${res.authReference}`)
      setAuthorizingBatch(null)
      loadData()
      if (onRefreshAccounts) onRefreshAccounts()
    } catch (err: unknown) {
      alert(errorMessage(err, 'Authorization failed.'))
    }
  }

  // Reject Payout Batch
  const handleRejectBatch = async () => {
    if (!rejectingBatch) return
    if (!rejectReason) {
      alert('Please provide a rejection reason.')
      return
    }
    try {
      await connectedBankingApi.rejectPayoutBatch(rejectingBatch.id, { reason: rejectReason })
      showToast(`Batch #${rejectingBatch.batchNumber} rejected.`)
      setRejectingBatch(null)
      setRejectReason('')
      loadData()
    } catch (err: unknown) {
      alert(errorMessage(err, 'Rejection failed.'))
    }
  }

  // Schedule Statutory Tax Challan
  const handleScheduleTaxChallan = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!challanAccount) {
      alert('Please select a debit bank account for challan e-payment.')
      return
    }

    try {
      const res = await connectedBankingApi.generateTaxChallan({
        bankAccountId: challanAccount,
        challanType,
        financialYear: fy,
        assessmentYear: ay,
        majorHeadCode: majorHead,
        minorHeadCode: minorHead,
        taxAmount,
        surchargeAmount: surcharge,
        cessAmount: cess,
        interestAmount: interest,
        feePenaltyAmount: feePenalty,
        panOrTanOrGstin: panTanGstin
      })
      showToast(`Statutory ${challanType} scheduled in Payout Batch #${res.batchNumber}! Ready for approval.`)
      setActiveTab('payouts')
      loadData()
    } catch (err: unknown) {
      alert(errorMessage(err, 'Failed to schedule tax challan.'))
    }
  }

  return (
    <div style={{ marginTop: '1.5rem', color: 'var(--text)' }}>
      {/* Toast */}
      {toast && (
        <div style={{
          position: 'fixed',
          top: '1rem',
          right: '1rem',
          backgroundColor: '#10b981',
          color: '#ffffff',
          padding: '0.75rem 1.25rem',
          borderRadius: '8px',
          fontWeight: 600,
          boxShadow: '0 4px 12px rgba(0,0,0,0.3)',
          zIndex: 9999
        }}>
          ✓ {toast}
        </div>
      )}

      {errorMsg && (
        <div style={{ backgroundColor: 'rgba(239, 68, 68, 0.2)', border: '1px solid #ef4444', padding: '0.75rem', borderRadius: '8px', marginBottom: '1rem', color: '#fca5a5' }}>
          ⚠️ {errorMsg}
        </div>
      )}

      {/* Header Banner */}
      <div style={{
        backgroundColor: 'var(--card)',
        border: '1px solid var(--border)',
        borderRadius: '16px',
        padding: '1.25rem',
        marginBottom: '1.5rem',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: '1rem'
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          <div style={{
            width: '48px',
            height: '48px',
            borderRadius: '12px',
            backgroundColor: '#0284c7',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: '1.5rem'
          }}>
            🏛️
          </div>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <h2 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 700 }}>Direct Connected Banking &amp; Payout Desk</h2>
              <span style={{ backgroundColor: 'rgba(2, 132, 199, 0.15)', color: 'var(--accent)', padding: '2px 8px', borderRadius: '9999px', fontSize: '0.7rem', fontWeight: 600 }}>
                API Gateway Live
              </span>
            </div>
            <p style={{ margin: '2px 0 0', color: 'var(--muted)', fontSize: '0.85rem' }}>
              Open Banking real-time statement syncs &amp; Maker-Checker payment disbursements (ICICI / HDFC / Axis / Open)
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button
            onClick={() => setShowConnectModal(true)}
            style={{
              backgroundColor: '#0284c7',
              color: '#ffffff',
              border: 'none',
              padding: '0.55rem 1rem',
              borderRadius: '8px',
              fontSize: '0.85rem',
              fontWeight: 600,
              cursor: 'pointer'
            }}
          >
            + Connect Bank API
          </button>
          <button
            onClick={() => setShowPayoutModal(true)}
            style={{
              backgroundColor: '#2563eb',
              color: '#ffffff',
              border: 'none',
              padding: '0.55rem 1rem',
              borderRadius: '8px',
              fontSize: '0.85rem',
              fontWeight: 600,
              cursor: 'pointer'
            }}
          >
            + New Payout Batch
          </button>
          <button
            onClick={loadData}
            disabled={loading}
            style={{
              backgroundColor: 'var(--card-subtle)',
              color: 'var(--text)',
              border: '1px solid var(--border-subtle)',
              padding: '0.55rem 0.9rem',
              borderRadius: '8px',
              fontSize: '0.85rem',
              cursor: 'pointer'
            }}
          >
            {loading ? '🔄 Syncing...' : '🔄 Refresh'}
          </button>
        </div>
      </div>

      {/* Sub Tabs */}
      <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1.25rem', borderBottom: '1px solid var(--border)', paddingBottom: '0.5rem' }}>
        <button
          onClick={() => setActiveTab('feeds')}
          style={{
            backgroundColor: activeTab === 'feeds' ? '#0284c7' : 'transparent',
            color: activeTab === 'feeds' ? '#ffffff' : 'var(--muted)',
            border: 'none',
            padding: '0.5rem 1rem',
            borderRadius: '8px',
            fontSize: '0.85rem',
            fontWeight: 600,
            cursor: 'pointer'
          }}
        >
          🌐 Connected Bank Feeds ({connectedList.length})
        </button>
        <button
          onClick={() => setActiveTab('payouts')}
          style={{
            backgroundColor: activeTab === 'payouts' ? '#0284c7' : 'transparent',
            color: activeTab === 'payouts' ? '#ffffff' : 'var(--muted)',
            border: 'none',
            padding: '0.5rem 1rem',
            borderRadius: '8px',
            fontSize: '0.85rem',
            fontWeight: 600,
            cursor: 'pointer'
          }}
        >
          💸 Maker-Checker Payout Batches ({payouts.length})
        </button>
        <button
          onClick={() => setActiveTab('challans')}
          style={{
            backgroundColor: activeTab === 'challans' ? '#0284c7' : 'transparent',
            color: activeTab === 'challans' ? '#ffffff' : 'var(--muted)',
            border: 'none',
            padding: '0.5rem 1rem',
            borderRadius: '8px',
            fontSize: '0.85rem',
            fontWeight: 600,
            cursor: 'pointer'
          }}
        >
          🏛️ 1-Click Tax Challan Payments (ITNS 280 / 281 / PMT-06)
        </button>
      </div>

      {/* TAB 1: CONNECTED BANK FEEDS */}
      {activeTab === 'feeds' && (
        <div>
          {connectedList.length === 0 ? (
            <div style={{ backgroundColor: 'var(--card)', border: '1px solid var(--border)', borderRadius: '14px', padding: '3rem', textAlign: 'center', color: 'var(--muted)' }}>
              <div style={{ fontSize: '2rem', marginBottom: '0.5rem' }}>🏛️</div>
              <h3 style={{ color: 'var(--text)', margin: '0 0 0.5rem' }}>No Bank Accounts Connected via API</h3>
              <p style={{ margin: '0 0 1.25rem', fontSize: '0.9rem' }}>
                Connect client accounts to ICICI Corporate, HDFC SmartHub, Axis Open, or Open Neobank for real-time transaction streaming.
              </p>
              <button
                onClick={() => setShowConnectModal(true)}
                style={{ backgroundColor: '#0284c7', color: '#ffffff', border: 'none', padding: '0.6rem 1.25rem', borderRadius: '8px', fontWeight: 600, cursor: 'pointer' }}
              >
                Connect First Bank Account
              </button>
            </div>
          ) : (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '1rem' }}>
              {connectedList.map(c => (
                <div
                  key={c.id}
                  style={{
                    backgroundColor: 'var(--card)',
                    border: '1px solid var(--border)',
                    borderRadius: '14px',
                    padding: '1.25rem',
                    display: 'flex',
                    flexDirection: 'column',
                    justifyContent: 'space-between'
                  }}
                >
                  <div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '0.5rem' }}>
                      <div>
                        <div style={{ fontWeight: 700, fontSize: '1.05rem', color: 'var(--text)' }}>{c.bankName}</div>
                        <div style={{ color: 'var(--muted)', fontSize: '0.8rem' }}>{c.clientName} • {c.accountNumber}</div>
                      </div>
                      <span style={{
                        backgroundColor: 'rgba(16, 185, 129, 0.15)',
                        color: '#10b981',
                        padding: '2px 8px',
                        borderRadius: '9999px',
                        fontSize: '0.7rem',
                        fontWeight: 700
                      }}>
                        {c.status}
                      </span>
                    </div>

                    <div style={{ backgroundColor: 'var(--card-subtle)', borderRadius: '8px', padding: '0.75rem', margin: '0.75rem 0' }}>
                      <div style={{ fontSize: '0.75rem', color: 'var(--muted)' }}>Live Bank Balance</div>
                      <div style={{ fontSize: '1.4rem', fontWeight: 800, color: '#10b981' }}>
                        ₹{c.currentBalance.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                      </div>
                      <div style={{ fontSize: '0.7rem', color: 'var(--muted)', marginTop: '2px' }}>
                        Gateway: <strong style={{ color: 'var(--accent)' }}>{c.provider}</strong> • Corp ID: {c.corporateId}
                      </div>
                    </div>

                    <div style={{ fontSize: '0.75rem', color: 'var(--muted)' }}>
                      Last API Sync: {c.lastSyncAt ? new Date(c.lastSyncAt).toLocaleString() : 'Never'}
                    </div>
                  </div>

                  <div style={{ marginTop: '1rem', display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
                    <button
                      onClick={() => handleSyncFeed(c.bankAccountId, c.bankName)}
                      style={{
                        backgroundColor: 'var(--card-subtle)',
                        color: 'var(--accent)',
                        border: '1px solid var(--border-subtle)',
                        padding: '0.45rem 0.9rem',
                        borderRadius: '6px',
                        fontSize: '0.8rem',
                        fontWeight: 600,
                        cursor: 'pointer',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.4rem'
                      }}
                    >
                      <span>🔄</span> Sync Live Transactions
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* TAB 2: PAYOUT BATCHES (MAKER-CHECKER) */}
      {activeTab === 'payouts' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          {payouts.length === 0 ? (
            <div style={{ backgroundColor: 'var(--card)', border: '1px solid var(--border)', borderRadius: '14px', padding: '3rem', textAlign: 'center', color: 'var(--muted)' }}>
              ✓ No payout batches created yet. Click "+ New Payout Batch" to draft vendor disbursements or salary batches.
            </div>
          ) : (
            payouts.map(b => (
              <div
                key={b.id}
                style={{
                  backgroundColor: 'var(--card)',
                  border: '1px solid var(--border)',
                  borderRadius: '14px',
                  padding: '1.25rem',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '0.75rem',
                  borderLeft: `4px solid ${
                    b.status === 'Completed' ? '#10b981' :
                    b.status === 'PendingApproval' ? '#f59e0b' :
                    b.status === 'Rejected' ? '#ef4444' : '#3b82f6'
                  }`
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '0.5rem' }}>
                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                      <span style={{ fontWeight: 800, fontSize: '1rem', color: 'var(--accent)' }}>#{b.batchNumber}</span>
                      <span style={{ fontWeight: 700, fontSize: '1rem' }}>{b.title}</span>
                    </div>
                    <div style={{ color: 'var(--muted)', fontSize: '0.8rem', marginTop: '2px' }}>
                      Debit Bank: <strong>{b.bankName}</strong> ({b.accountNumber}) • Client: {b.clientName}
                    </div>
                    <div style={{ color: 'var(--muted)', fontSize: '0.75rem', marginTop: '2px' }}>
                      Maker: {b.makerName} • Created: {new Date(b.createdAt).toLocaleDateString()}
                      {b.checkerName && <span> • Checker (Partner): <strong style={{ color: '#10b981' }}>{b.checkerName}</strong></span>}
                    </div>
                  </div>

                  <div style={{ textAlign: 'right' }}>
                    <div style={{ fontSize: '1.3rem', fontWeight: 800, color: '#10b981' }}>
                      ₹{b.totalAmount.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                    </div>
                    <span style={{
                      backgroundColor: b.status === 'Completed' ? 'rgba(16, 185, 129, 0.15)' :
                                       b.status === 'PendingApproval' ? 'rgba(245, 158, 11, 0.15)' :
                                       b.status === 'Rejected' ? 'rgba(239, 68, 68, 0.15)' : 'rgba(59, 130, 246, 0.15)',
                      color: b.status === 'Completed' ? '#10b981' :
                             b.status === 'PendingApproval' ? '#f59e0b' :
                             b.status === 'Rejected' ? '#ef4444' : '#3b82f6',
                      padding: '2px 8px',
                      borderRadius: '9999px',
                      fontSize: '0.7rem',
                      fontWeight: 700,
                      textTransform: 'uppercase'
                    }}>
                      {b.status === 'PendingApproval' ? 'Awaiting Partner Auth' : b.status}
                    </span>
                  </div>
                </div>

                {/* Items preview table */}
                <div style={{ backgroundColor: 'var(--card-subtle)', borderRadius: '8px', padding: '0.6rem 0.8rem', overflowX: 'auto' }}>
                  <table style={{ width: '100%', fontSize: '0.8rem', borderCollapse: 'collapse' }}>
                    <thead>
                      <tr style={{ color: 'var(--muted)', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>
                        <th style={{ paddingBottom: '4px' }}>Beneficiary</th>
                        <th style={{ paddingBottom: '4px' }}>A/C &amp; IFSC</th>
                        <th style={{ paddingBottom: '4px' }}>Mode</th>
                        <th style={{ paddingBottom: '4px' }}>Ref / Narration</th>
                        <th style={{ paddingBottom: '4px', textAlign: 'right' }}>Amount</th>
                        <th style={{ paddingBottom: '4px', textAlign: 'right' }}>UTR Ref</th>
                      </tr>
                    </thead>
                    <tbody>
                      {b.items.map(item => (
                        <tr key={item.id} style={{ borderBottom: '1px solid rgba(51, 65, 85, 0.4)' }}>
                          <td style={{ padding: '6px 0', fontWeight: 600 }}>{item.beneficiaryName}</td>
                          <td style={{ padding: '6px 0', color: 'var(--muted)' }}>{item.beneficiaryAccountNumber} • {item.beneficiaryIfsc}</td>
                          <td style={{ padding: '6px 0' }}><span style={{ backgroundColor: 'var(--card)', padding: '1px 6px', borderRadius: '4px', fontSize: '0.7rem' }}>{item.paymentMode}</span></td>
                          <td style={{ padding: '6px 0', color: 'var(--text-secondary)' }}>{item.narration}</td>
                          <td style={{ padding: '6px 0', textAlign: 'right', fontWeight: 700, color: 'var(--text)' }}>₹{item.amount.toLocaleString('en-IN')}</td>
                          <td style={{ padding: '6px 0', textAlign: 'right', color: 'var(--accent)', fontFamily: 'monospace', fontSize: '0.75rem' }}>{item.utrNumber || '—'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {/* Actions row */}
                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', marginTop: '0.25rem' }}>
                  {b.status === 'Draft' && (
                    <button
                      onClick={() => handleSubmitBatch(b.id)}
                      style={{
                        backgroundColor: '#2563eb',
                        color: '#ffffff',
                        border: 'none',
                        padding: '0.45rem 1rem',
                        borderRadius: '6px',
                        fontSize: '0.8rem',
                        fontWeight: 600,
                        cursor: 'pointer'
                      }}
                    >
                      Submit for Partner Approval →
                    </button>
                  )}

                  {b.status === 'PendingApproval' && (
                    <>
                      <button
                        onClick={() => {
                          setRejectingBatch(b)
                          setRejectReason('')
                        }}
                        style={{
                          backgroundColor: 'transparent',
                          color: '#ef4444',
                          border: '1px solid #ef4444',
                          padding: '0.45rem 0.9rem',
                          borderRadius: '6px',
                          fontSize: '0.8rem',
                          fontWeight: 600,
                          cursor: 'pointer'
                        }}
                      >
                        Reject Batch
                      </button>
                      <button
                        onClick={() => {
                          setAuthorizingBatch(b)
                          setOtpToken(Math.floor(100000 + Math.random() * 900000).toString())
                        }}
                        style={{
                          backgroundColor: '#10b981',
                          color: '#ffffff',
                          border: 'none',
                          padding: '0.45rem 1.1rem',
                          borderRadius: '6px',
                          fontSize: '0.8rem',
                          fontWeight: 700,
                          cursor: 'pointer',
                          display: 'flex',
                          alignItems: 'center',
                          gap: '0.4rem'
                        }}
                      >
                        <span>🔐</span> Authorize &amp; Disburse (Partner)
                      </button>
                    </>
                  )}
                </div>
              </div>
            ))
          )}
        </div>
      )}

      {/* TAB 3: 1-CLICK STATUTORY TAX CHALLAN PAYMENTS */}
      {activeTab === 'challans' && (
        <div style={{ backgroundColor: 'var(--card)', border: '1px solid var(--border)', borderRadius: '16px', padding: '1.5rem', maxWidth: '800px' }}>
          <h3 style={{ margin: '0 0 0.5rem', fontSize: '1.15rem', fontWeight: 700 }}>
            1-Click Statutory Tax Challan Direct Debit (ITNS 280 / 281 / PMT-06)
          </h3>
          <p style={{ color: 'var(--muted)', fontSize: '0.85rem', margin: '0 0 1.25rem' }}>
            Compiles a statutory tax challan directly into the Maker-Checker payout queue for automatic direct debit from client corporate bank account.
          </p>

          <form onSubmit={handleScheduleTaxChallan}>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem', marginBottom: '1rem' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--muted)', marginBottom: '4px' }}>
                  Debit Client Bank Account *
                </label>
                <select
                  value={challanAccount}
                  onChange={e => setChallanAccount(e.target.value)}
                  required
                  style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                >
                  <option value="">-- Select Bank Account --</option>
                  {accounts.map(a => (
                    <option key={a.id} value={a.id}>{a.bankName} - {a.accountNumber} ({a.clientName})</option>
                  ))}
                </select>
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--muted)', marginBottom: '4px' }}>
                  Statutory Challan Type *
                </label>
                <select
                  value={challanType}
                  onChange={e => {
                    const ct = e.target.value as 'AdvanceTax_ITNS280' | 'TDS_ITNS281' | 'GST_PMT06'
                    setChallanType(ct)
                    if (ct === 'AdvanceTax_ITNS280') {
                      setMinorHead('100')
                    } else if (ct === 'TDS_ITNS281') {
                      setMinorHead('200')
                    } else {
                      setMajorHead('0005')
                    }
                  }}
                  style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                >
                  <option value="AdvanceTax_ITNS280">ITNS 280 — Advance Tax / Self-Assessment</option>
                  <option value="TDS_ITNS281">ITNS 281 — TDS / TCS Withholding Tax</option>
                  <option value="GST_PMT06">GST PMT-06 — Electronic Cash Ledger Top-up</option>
                </select>
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '0.75rem', marginBottom: '1rem' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--muted)', marginBottom: '4px' }}>Financial Year</label>
                <input
                  type="text"
                  value={fy}
                  onChange={e => setFy(e.target.value)}
                  style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--muted)', marginBottom: '4px' }}>Assessment Year</label>
                <input
                  type="text"
                  value={ay}
                  onChange={e => setAy(e.target.value)}
                  style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--muted)', marginBottom: '4px' }}>Major Head</label>
                <input
                  type="text"
                  value={majorHead}
                  onChange={e => setMajorHead(e.target.value)}
                  style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '0.75rem', color: 'var(--muted)', marginBottom: '4px' }}>PAN / TAN / GSTIN</label>
                <input
                  type="text"
                  value={panTanGstin}
                  onChange={e => setPanTanGstin(e.target.value)}
                  style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--accent)', fontWeight: 600, padding: '0.6rem', borderRadius: '8px' }}
                />
              </div>
            </div>

            {/* Tax Amount Breakup */}
            <div style={{ backgroundColor: 'var(--card-subtle)', borderRadius: '8px', padding: '1rem', marginBottom: '1rem' }}>
              <div style={{ fontSize: '0.8rem', fontWeight: 700, color: 'var(--text)', marginBottom: '0.5rem' }}>Tax Breakup (₹)</div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '0.75rem' }}>
                <div>
                  <label style={{ fontSize: '0.7rem', color: 'var(--muted)' }}>Tax Amount</label>
                  <input
                    type="number"
                    value={taxAmount}
                    onChange={e => setTaxAmount(parseFloat(e.target.value) || 0)}
                    style={{ width: '100%', backgroundColor: 'var(--card)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.5rem', borderRadius: '6px' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '0.7rem', color: 'var(--muted)' }}>Surcharge</label>
                  <input
                    type="number"
                    value={surcharge}
                    onChange={e => setSurcharge(parseFloat(e.target.value) || 0)}
                    style={{ width: '100%', backgroundColor: 'var(--card)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.5rem', borderRadius: '6px' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '0.7rem', color: 'var(--muted)' }}>Health &amp; Edu Cess (4%)</label>
                  <input
                    type="number"
                    value={cess}
                    onChange={e => setCess(parseFloat(e.target.value) || 0)}
                    style={{ width: '100%', backgroundColor: 'var(--card)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.5rem', borderRadius: '6px' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '0.7rem', color: 'var(--muted)' }}>Interest (234A/B/C or 201(1A))</label>
                  <input
                    type="number"
                    value={interest}
                    onChange={e => setInterest(parseFloat(e.target.value) || 0)}
                    style={{ width: '100%', backgroundColor: 'var(--card)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.5rem', borderRadius: '6px' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '0.7rem', color: 'var(--muted)' }}>Fee / Penalty</label>
                  <input
                    type="number"
                    value={feePenalty}
                    onChange={e => setFeePenalty(parseFloat(e.target.value) || 0)}
                    style={{ width: '100%', backgroundColor: 'var(--card)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.5rem', borderRadius: '6px' }}
                  />
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
                  <div style={{ fontSize: '0.75rem', color: 'var(--muted)' }}>Total Challan Debit</div>
                  <div style={{ fontSize: '1.25rem', fontWeight: 800, color: '#10b981' }}>
                    ₹{(taxAmount + surcharge + cess + interest + feePenalty).toLocaleString('en-IN')}
                  </div>
                </div>
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
              <button
                type="submit"
                style={{
                  backgroundColor: '#0284c7',
                  color: '#ffffff',
                  border: 'none',
                  padding: '0.65rem 1.25rem',
                  borderRadius: '8px',
                  fontWeight: 700,
                  fontSize: '0.85rem',
                  cursor: 'pointer'
                }}
              >
                Schedule Statutory Direct Debit Payout →
              </button>
            </div>
          </form>
        </div>
      )}

      {/* MODAL: CONNECT BANK ACCOUNT */}
      {showConnectModal && (
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
          <div style={{ backgroundColor: 'var(--card)', border: '1px solid var(--border-subtle)', borderRadius: '16px', maxWidth: '500px', width: '100%', padding: '1.5rem', color: 'var(--text)' }}>
            <h3 style={{ fontSize: '1.25rem', fontWeight: 700, margin: '0 0 0.5rem' }}>
              Connect Corporate Bank API Feed
            </h3>
            <p style={{ color: 'var(--muted)', fontSize: '0.85rem', margin: '0 0 1rem' }}>
              Register API gateway credentials for real-time statement fetch and Maker-Checker payouts.
            </p>

            <form onSubmit={handleConnectBank}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', marginBottom: '1.25rem' }}>
                <div>
                  <label style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>Bank Account *</label>
                  <select
                    value={connectForm.bankAccountId}
                    onChange={e => setConnectForm({ ...connectForm, bankAccountId: e.target.value })}
                    required
                    style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                  >
                    <option value="">-- Select Bank Account --</option>
                    {accounts.map(a => (
                      <option key={a.id} value={a.id}>{a.bankName} - {a.accountNumber} ({a.clientName})</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>Banking Gateway / Provider *</label>
                  <select
                    value={connectForm.provider}
                    onChange={e => setConnectForm({ ...connectForm, provider: e.target.value })}
                    style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                  >
                    <option value="ICICI_Corporate">ICICI Bank Corporate API / Eazypay</option>
                    <option value="HDFC_SmartHub">HDFC Bank SmartHub / Corporate Banking</option>
                    <option value="Axis_Open">Axis Bank Open Corporate Gateway</option>
                    <option value="Open_Money">Open Financial Technologies Neobanking</option>
                  </select>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                  <div>
                    <label style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>Corporate ID *</label>
                    <input
                      type="text"
                      required
                      placeholder="e.g. CORP100452"
                      value={connectForm.corporateId}
                      onChange={e => setConnectForm({ ...connectForm, corporateId: e.target.value })}
                      style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                    />
                  </div>
                  <div>
                    <label style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>User ID *</label>
                    <input
                      type="text"
                      required
                      placeholder="e.g. USER_AUDIT"
                      value={connectForm.userId}
                      onChange={e => setConnectForm({ ...connectForm, userId: e.target.value })}
                      style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                    />
                  </div>
                </div>

                <div>
                  <label style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>API Client Key *</label>
                  <input
                    type="password"
                    required
                    placeholder="Enter API Key"
                    value={connectForm.apiKey}
                    onChange={e => setConnectForm({ ...connectForm, apiKey: e.target.value })}
                    style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                  />
                </div>

                <div>
                  <label style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>API Secret / RSA Key *</label>
                  <input
                    type="password"
                    required
                    placeholder="Enter API Secret"
                    value={connectForm.apiSecret}
                    onChange={e => setConnectForm({ ...connectForm, apiSecret: e.target.value })}
                    style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
                <button
                  type="button"
                  onClick={() => setShowConnectModal(false)}
                  style={{ backgroundColor: 'transparent', color: 'var(--muted)', border: '1px solid var(--border-subtle)', padding: '0.6rem 1rem', borderRadius: '8px', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  style={{ backgroundColor: '#0284c7', color: '#ffffff', border: 'none', padding: '0.6rem 1.25rem', borderRadius: '8px', fontWeight: 700, cursor: 'pointer' }}
                >
                  Verify &amp; Connect API
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: CREATE PAYOUT BATCH */}
      {showPayoutModal && (
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
          <div style={{ backgroundColor: 'var(--card)', border: '1px solid var(--border-subtle)', borderRadius: '16px', maxWidth: '750px', width: '100%', maxHeight: '90vh', overflowY: 'auto', padding: '1.5rem', color: 'var(--text)' }}>
            <h3 style={{ fontSize: '1.25rem', fontWeight: 700, margin: '0 0 0.5rem' }}>
              Create Disbursement Payout Batch (Maker)
            </h3>
            <p style={{ color: 'var(--muted)', fontSize: '0.85rem', margin: '0 0 1rem' }}>
              Bundle vendor invoices or employee salaries for partner approval and direct corporate banking debit.
            </p>

            <form onSubmit={handleCreatePayoutBatch}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem', marginBottom: '1rem' }}>
                <div>
                  <label style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>Debit Bank Account *</label>
                  <select
                    value={selectedDebitAccountId}
                    onChange={e => setSelectedDebitAccountId(e.target.value)}
                    required
                    style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                  >
                    <option value="">-- Select Bank Account --</option>
                    {accounts.map(a => (
                      <option key={a.id} value={a.id}>{a.bankName} - {a.accountNumber} ({a.clientName})</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>Batch Title</label>
                  <input
                    type="text"
                    value={payoutTitle}
                    onChange={e => setPayoutTitle(e.target.value)}
                    placeholder="e.g. Apex Global Vendor Batch Q3"
                    style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                  />
                </div>
                <div style={{ gridColumn: 'span 2' }}>
                  <label style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>Maker Remarks</label>
                  <input
                    type="text"
                    value={payoutRemarks}
                    onChange={e => setPayoutRemarks(e.target.value)}
                    placeholder="e.g. Verified against purchase invoice register and GST 2B ITC."
                    style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                  />
                </div>
              </div>

              {/* Items List */}
              <div style={{ marginBottom: '1rem' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
                  <span style={{ fontSize: '0.85rem', fontWeight: 700 }}>Disbursement Line Items ({payoutItems.length})</span>
                  <button
                    type="button"
                    onClick={handleAddPayoutItem}
                    style={{ backgroundColor: 'var(--card-subtle)', color: 'var(--accent)', border: '1px solid var(--border-subtle)', padding: '2px 8px', borderRadius: '6px', fontSize: '0.75rem', cursor: 'pointer' }}
                  >
                    + Add Item
                  </button>
                </div>

                {payoutItems.map((item, idx) => (
                  <div key={idx} style={{ backgroundColor: 'var(--card-subtle)', borderRadius: '8px', padding: '0.75rem', marginBottom: '0.5rem', display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '0.5rem' }}>
                    <div>
                      <label style={{ fontSize: '0.7rem', color: 'var(--muted)' }}>Beneficiary Name *</label>
                      <input
                        type="text"
                        required
                        value={item.beneficiaryName}
                        onChange={e => {
                          const copy = [...payoutItems]
                          copy[idx].beneficiaryName = e.target.value
                          setPayoutItems(copy)
                        }}
                        placeholder="Vendor / Employee"
                        style={{ width: '100%', backgroundColor: 'var(--card)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.4rem', borderRadius: '6px', fontSize: '0.8rem' }}
                      />
                    </div>
                    <div>
                      <label style={{ fontSize: '0.7rem', color: 'var(--muted)' }}>Account Number *</label>
                      <input
                        type="text"
                        required
                        value={item.beneficiaryAccountNumber}
                        onChange={e => {
                          const copy = [...payoutItems]
                          copy[idx].beneficiaryAccountNumber = e.target.value
                          setPayoutItems(copy)
                        }}
                        placeholder="11-18 digits"
                        style={{ width: '100%', backgroundColor: 'var(--card)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.4rem', borderRadius: '6px', fontSize: '0.8rem' }}
                      />
                    </div>
                    <div>
                      <label style={{ fontSize: '0.7rem', color: 'var(--muted)' }}>IFSC Code *</label>
                      <input
                        type="text"
                        required
                        value={item.beneficiaryIfsc}
                        onChange={e => {
                          const copy = [...payoutItems]
                          copy[idx].beneficiaryIfsc = e.target.value.toUpperCase()
                          setPayoutItems(copy)
                        }}
                        placeholder="e.g. HDFC0000123"
                        style={{ width: '100%', backgroundColor: 'var(--card)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.4rem', borderRadius: '6px', fontSize: '0.8rem' }}
                      />
                    </div>
                    <div>
                      <label style={{ fontSize: '0.7rem', color: 'var(--muted)' }}>Amount (₹) *</label>
                      <input
                        type="number"
                        required
                        value={item.amount || ''}
                        onChange={e => {
                          const copy = [...payoutItems]
                          copy[idx].amount = parseFloat(e.target.value) || 0
                          setPayoutItems(copy)
                        }}
                        style={{ width: '100%', backgroundColor: 'var(--card)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.4rem', borderRadius: '6px', fontSize: '0.8rem' }}
                      />
                    </div>
                    <div>
                      <label style={{ fontSize: '0.7rem', color: 'var(--muted)' }}>Payment Mode</label>
                      <select
                        value={item.paymentMode}
                        onChange={e => {
                          const copy = [...payoutItems]
                          copy[idx].paymentMode = e.target.value
                          setPayoutItems(copy)
                        }}
                        style={{ width: '100%', backgroundColor: 'var(--card)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.4rem', borderRadius: '6px', fontSize: '0.8rem' }}
                      >
                        <option value="NEFT">NEFT</option>
                        <option value="RTGS">RTGS (&ge; ₹2,00,000)</option>
                        <option value="IMPS">IMPS (&le; ₹5,00,000)</option>
                        <option value="UPI">UPI (&le; ₹1,00,000)</option>
                      </select>
                    </div>
                    <div>
                      <label style={{ fontSize: '0.7rem', color: 'var(--muted)' }}>Narration</label>
                      <input
                        type="text"
                        value={item.narration}
                        onChange={e => {
                          const copy = [...payoutItems]
                          copy[idx].narration = e.target.value
                          setPayoutItems(copy)
                        }}
                        placeholder="Invoice # or Salary"
                        style={{ width: '100%', backgroundColor: 'var(--card)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.4rem', borderRadius: '6px', fontSize: '0.8rem' }}
                      />
                    </div>
                  </div>
                ))}
              </div>

              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div style={{ fontSize: '0.9rem', fontWeight: 700, color: '#10b981' }}>
                  Total Batch Amount: ₹{payoutItems.reduce((acc, curr) => acc + (curr.amount || 0), 0).toLocaleString('en-IN')}
                </div>
                <div style={{ display: 'flex', gap: '0.75rem' }}>
                  <button
                    type="button"
                    onClick={() => setShowPayoutModal(false)}
                    style={{ backgroundColor: 'transparent', color: 'var(--muted)', border: '1px solid var(--border-subtle)', padding: '0.6rem 1rem', borderRadius: '8px', cursor: 'pointer' }}
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    style={{ backgroundColor: '#2563eb', color: '#ffffff', border: 'none', padding: '0.6rem 1.25rem', borderRadius: '8px', fontWeight: 700, cursor: 'pointer' }}
                  >
                    Save Batch (Draft)
                  </button>
                </div>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: AUTHORIZE PAYOUT BATCH (PARTNER CHECKER) */}
      {authorizingBatch && (
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
          <div style={{ backgroundColor: 'var(--card)', border: '1px solid var(--border-subtle)', borderRadius: '16px', maxWidth: '520px', width: '100%', padding: '1.5rem', color: 'var(--text)' }}>
            <h3 style={{ fontSize: '1.25rem', fontWeight: 700, margin: '0 0 0.5rem' }}>
              Authorize Bank Payout Batch #{authorizingBatch.batchNumber}
            </h3>
            <p style={{ color: 'var(--muted)', fontSize: '0.85rem', margin: '0 0 1rem' }}>
              You are authorizing <strong>₹{authorizingBatch.totalAmount.toLocaleString('en-IN')}</strong> across {authorizingBatch.totalCount} payment(s) from <strong>{authorizingBatch.bankName}</strong>.
            </p>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', marginBottom: '1.25rem' }}>
              <div>
                <label style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>Authorization Method</label>
                <select
                  value={authMethod}
                  onChange={e => setAuthMethod(e.target.value as 'OTP' | 'DSC_SHA256' | 'Biometric')}
                  style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                >
                  <option value="OTP">Corporate Banking 6-Digit OTP</option>
                  <option value="DSC_SHA256">Class 3 USB DSC Cryptographic Attestation</option>
                  <option value="Biometric">Managing Partner Biometric Attestation</option>
                </select>
              </div>

              <div>
                <label style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>
                  {authMethod === 'OTP' ? 'Enter Bank Security OTP' : authMethod === 'DSC_SHA256' ? 'DSC Token PIN / SHA-256 Hash' : 'Biometric Sensor Credential'}
                </label>
                <input
                  type="text"
                  value={otpToken}
                  onChange={e => setOtpToken(e.target.value)}
                  style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--accent)', fontFamily: 'monospace', padding: '0.6rem', borderRadius: '8px' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '0.75rem', color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>Partner Audit Sign-off Remarks</label>
                <textarea
                  value={partnerRemarks}
                  onChange={e => setPartnerRemarks(e.target.value)}
                  rows={2}
                  style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px' }}
                />
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
              <button
                onClick={() => setAuthorizingBatch(null)}
                style={{ backgroundColor: 'transparent', color: 'var(--muted)', border: '1px solid var(--border-subtle)', padding: '0.6rem 1rem', borderRadius: '8px', cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                onClick={handleAuthorizeBatch}
                style={{ backgroundColor: '#10b981', color: '#ffffff', border: 'none', padding: '0.6rem 1.25rem', borderRadius: '8px', fontWeight: 700, cursor: 'pointer' }}
              >
                Sign &amp; Disburse Payments
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL: REJECT BATCH */}
      {rejectingBatch && (
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
          <div style={{ backgroundColor: 'var(--card)', border: '1px solid var(--border-subtle)', borderRadius: '16px', maxWidth: '440px', width: '100%', padding: '1.5rem', color: 'var(--text)' }}>
            <h3 style={{ fontSize: '1.25rem', fontWeight: 700, margin: '0 0 0.5rem', color: '#ef4444' }}>
              Reject Payout Batch #{rejectingBatch.batchNumber}
            </h3>
            <p style={{ color: 'var(--muted)', fontSize: '0.85rem', margin: '0 0 1rem' }}>
              Specify the reason for returning this batch to the staff maker.
            </p>

            <textarea
              value={rejectReason}
              onChange={e => setRejectReason(e.target.value)}
              placeholder="e.g. Vendor invoice not attached or IFSC mismatch."
              rows={3}
              style={{ width: '100%', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', color: 'var(--text)', padding: '0.6rem', borderRadius: '8px', marginBottom: '1rem' }}
            />

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
              <button
                onClick={() => setRejectingBatch(null)}
                style={{ backgroundColor: 'transparent', color: 'var(--muted)', border: '1px solid var(--border-subtle)', padding: '0.6rem 1rem', borderRadius: '8px', cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                onClick={handleRejectBatch}
                style={{ backgroundColor: '#ef4444', color: '#ffffff', border: 'none', padding: '0.6rem 1.25rem', borderRadius: '8px', fontWeight: 700, cursor: 'pointer' }}
              >
                Reject Batch
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
