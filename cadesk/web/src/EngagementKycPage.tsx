import React, { useState, useEffect, useCallback } from 'react'
import {
  api,
  engagementApi,
  type Client,
  type ClientPage,
  type EngagementLetterDto,
  type ClientKycRecordDto,
  type EngagementKycSummaryDto,
  type CreateEngagementLetterRequest,
  type AcceptEngagementLetterRequest,
  type SaveClientKycRequest,
  type GeneratedEngagementTemplate
} from './api'

export const EngagementKycPage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'letters' | 'kyc' | 'template'>('letters')
  const [summary, setSummary] = useState<EngagementKycSummaryDto | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [letters, setLetters] = useState<EngagementLetterDto[]>([])
  const [kycRecords, setKycRecords] = useState<ClientKycRecordDto[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Filters
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [selectedStatus, setSelectedStatus] = useState<string>('')

  // Modals
  const [showLetterModal, setShowLetterModal] = useState<boolean>(false)
  const [showAcceptModal, setShowAcceptModal] = useState<boolean>(false)
  const [showKycModal, setShowKycModal] = useState<boolean>(false)
  const [selectedLetterForAccept, setSelectedLetterForAccept] = useState<EngagementLetterDto | null>(null)
  const [selectedLetterForView, setSelectedLetterForView] = useState<EngagementLetterDto | null>(null)

  // Letter Form State
  const [letterForm, setLetterForm] = useState<CreateEngagementLetterRequest>({
    clientId: '',
    engagementType: 'StatutoryAudit',
    financialYear: '2024-25',
    feeStructure: 'FixedFee',
    agreedFeeAmount: 250000,
    outOfPocketIncluded: false,
    scopeOfWork: '',
    managementResponsibilities: '',
    auditorResponsibilities: '',
    letterContent: ''
  })

  // Accept Form State
  const [acceptForm, setAcceptForm] = useState<AcceptEngagementLetterRequest>({
    signatoryName: 'Arun Kumar',
    signatoryDesignation: 'Director',
    ipAddress: '127.0.0.1'
  })

  // KYC Form State
  const [kycForm, setKycForm] = useState<SaveClientKycRequest>({
    clientId: '',
    riskRating: 'Low',
    verificationStatus: 'Verified',
    constitutionDocumentType: 'CertificateOfIncorporation',
    constitutionDocumentNumber: 'U72200MH2021PTC123456',
    panVerified: true,
    gstinVerified: true,
    addressProofType: 'ElectricityBill',
    addressProofNumber: 'EB-2024-9988',
    uboName: 'Arun Kumar',
    uboShareholdingPercentage: 51.0,
    uboPan: 'ABCPS1234K',
    sanctionsCheckCleared: true,
    adverseMediaCheckCleared: true,
    riskRationale: 'Operating private limited company with verified directors and standard turnover.'
  })

  // Template Preview State
  const [templateForm, setTemplateForm] = useState({
    engagementType: 'StatutoryAudit',
    clientName: 'Reliance Retail Limited',
    financialYear: '2024-25',
    feeStructure: 'FixedFee',
    agreedFeeAmount: 350000
  })
  const [generatedTemplate, setGeneratedTemplate] = useState<GeneratedEngagementTemplate | null>(null)

  const loadData = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)
      const [sum, clientRes, lettrs, kycs] = await Promise.all([
        engagementApi.summary(),
        api<ClientPage>('/api/clients?take=100'),
        engagementApi.listLetters(selectedClientId || undefined, undefined, selectedStatus || undefined),
        engagementApi.listKyc(selectedClientId || undefined)
      ])
      setSummary(sum)
      const clientList = clientRes.items || []
      setClients(clientList)
      setLetters(lettrs)
      setKycRecords(kycs)

      if (clientList.length > 0) {
        setLetterForm(prev => (prev.clientId ? prev : { ...prev, clientId: clientList[0].id }))
        setKycForm(prev => (prev.clientId ? prev : { ...prev, clientId: clientList[0].id }))
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load engagement data')
    } finally {
      setLoading(false)
    }
  }, [selectedClientId, selectedStatus])

  useEffect(() => {
    loadData()
  }, [loadData])

  const handleCreateLetter = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      setLoading(true)
      await engagementApi.createLetter(letterForm)
      setSuccess('Engagement Letter generated successfully')
      setShowLetterModal(false)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to create engagement letter')
    } finally {
      setLoading(false)
    }
  }

  const handleSendLetter = async (id: string) => {
    try {
      setLoading(true)
      await engagementApi.sendLetter(id)
      setSuccess('Engagement letter marked as sent to client')
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to send letter')
    } finally {
      setLoading(false)
    }
  }

  const handleAcceptLetter = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedLetterForAccept) return
    try {
      setLoading(true)
      await engagementApi.acceptLetter(selectedLetterForAccept.id, acceptForm)
      setSuccess('Digital acceptance recorded with audit trail timestamp')
      setShowAcceptModal(false)
      setSelectedLetterForAccept(null)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to record acceptance')
    } finally {
      setLoading(false)
    }
  }

  const handleSaveKyc = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      setLoading(true)
      await engagementApi.saveKyc(kycForm)
      setSuccess('Client KYC and PMLA risk profile updated')
      setShowKycModal(false)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to save KYC')
    } finally {
      setLoading(false)
    }
  }

  const handlePreviewTemplate = async () => {
    try {
      const res = await engagementApi.generateTemplate(templateForm)
      setGeneratedTemplate(res)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Template preview failed')
    }
  }

  return (
    <div style={{ padding: '24px', maxWidth: '1400px', margin: '0 auto' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 700, margin: 0, color: 'var(--text)' }}>
            Engagement Letters (SA 210) & AML / KYC Compliance
          </h1>
          <p style={{ margin: '4px 0 0', color: 'var(--muted)', fontSize: '14px' }}>
            ICAI Standards on Auditing SA 210 terms, digital acceptance workflows & PMLA beneficial ownership (UBO) register
          </p>
        </div>
        <div style={{ display: 'flex', gap: '8px' }}>
          <button
            onClick={() => setShowLetterModal(true)}
            style={{
              padding: '8px 16px',
              backgroundColor: '#2563eb',
              color: '#fff',
              border: 'none',
              borderRadius: '6px',
              fontWeight: 500,
              cursor: 'pointer'
            }}
          >
            + Issue Engagement Letter
          </button>
          <button
            onClick={() => setShowKycModal(true)}
            style={{
              padding: '8px 16px',
              backgroundColor: '#059669',
              color: '#fff',
              border: 'none',
              borderRadius: '6px',
              fontWeight: 500,
              cursor: 'pointer'
            }}
          >
            + Add / Update Client KYC
          </button>
        </div>
      </div>

      {/* Notifications */}
      {error && (
        <div style={{ padding: '12px 16px', backgroundColor: 'var(--error-bg)', border: '1px solid #f87171', borderRadius: '6px', color: 'var(--error)', marginBottom: '16px' }}>
          {error}
        </div>
      )}
      {success && (
        <div style={{ padding: '12px 16px', backgroundColor: 'var(--success-bg)', border: '1px solid #34d399', borderRadius: '6px', color: 'var(--success-text)', marginBottom: '16px' }}>
          {success}
        </div>
      )}

      {/* KPI Cards */}
      {summary && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          <div style={{ backgroundColor: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
            <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--muted)', textTransform: 'uppercase' }}>Engagement Letters</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--text)', marginTop: '4px' }}>{summary.totalLettersCount}</div>
            <div style={{ fontSize: '13px', color: 'var(--success)', marginTop: '4px' }}>{summary.acceptedLettersCount} Digitally Accepted</div>
          </div>
          <div style={{ backgroundColor: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
            <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--accent)', textTransform: 'uppercase' }}>Contracted Value</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--accent)', marginTop: '4px' }}>₹{summary.totalContractedFeeValue.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</div>
            <div style={{ fontSize: '13px', color: 'var(--muted)', marginTop: '4px' }}>Agreed audit & advisory fees</div>
          </div>
          <div style={{ backgroundColor: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
            <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--success)', textTransform: 'uppercase' }}>PMLA KYC Profiles</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--success)', marginTop: '4px' }}>{summary.totalKycRecordsCount}</div>
            <div style={{ fontSize: '13px', color: 'var(--muted)', marginTop: '4px' }}>{summary.verifiedKycCount} Fully Verified</div>
          </div>
          <div style={{ backgroundColor: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
            <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--error)', textTransform: 'uppercase' }}>High Risk / PEP Clients</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--error)', marginTop: '4px' }}>{summary.highRiskKycCount}</div>
            <div style={{ fontSize: '13px', color: 'var(--muted)', marginTop: '4px' }}>Requires annual PMLA review</div>
          </div>
        </div>
      )}

      {/* Global Filter Bar */}
      <div style={{ display: 'flex', gap: '12px', alignItems: 'center', backgroundColor: 'var(--card-subtle)', padding: '12px 16px', borderRadius: '8px', marginBottom: '20px', border: '1px solid var(--border)' }}>
        <div>
          <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Client:</label>
          <select
            value={selectedClientId}
            onChange={(e) => setSelectedClientId(e.target.value)}
            style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '14px' }}
          >
            <option value="">All Clients</option>
            {clients.map(c => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </div>
        <div>
          <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Letter Status:</label>
          <select
            value={selectedStatus}
            onChange={(e) => setSelectedStatus(e.target.value)}
            style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '14px' }}
          >
            <option value="">All Statuses</option>
            <option value="Draft">Draft</option>
            <option value="SentToClient">Sent To Client</option>
            <option value="Accepted">Accepted</option>
          </select>
        </div>
        <div style={{ marginLeft: 'auto' }}>
          <button
            onClick={loadData}
            style={{
              padding: '6px 12px',
              backgroundColor: 'var(--card)',
              color: 'var(--text)',
              border: '1px solid var(--border-subtle)',
              borderRadius: '6px',
              fontSize: '13px',
              cursor: 'pointer'
            }}
          >
            Refresh
          </button>
        </div>
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', borderBottom: '1px solid var(--border)', marginBottom: '20px' }}>
        <button
          onClick={() => setActiveTab('letters')}
          style={{
            padding: '10px 20px',
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'letters' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'letters' ? 'var(--accent)' : 'var(--muted)',
            fontWeight: activeTab === 'letters' ? 600 : 500,
            cursor: 'pointer'
          }}
        >
          Engagement Letters ({letters.length})
        </button>
        <button
          onClick={() => setActiveTab('kyc')}
          style={{
            padding: '10px 20px',
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'kyc' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'kyc' ? 'var(--accent)' : 'var(--muted)',
            fontWeight: activeTab === 'kyc' ? 600 : 500,
            cursor: 'pointer'
          }}
        >
          AML / PMLA KYC Register ({kycRecords.length})
        </button>
        <button
          onClick={() => setActiveTab('template')}
          style={{
            padding: '10px 20px',
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'template' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'template' ? 'var(--accent)' : 'var(--muted)',
            fontWeight: activeTab === 'template' ? 600 : 500,
            cursor: 'pointer'
          }}
        >
          ICAI SA 210 Letter Generator
        </button>
      </div>

      {/* Tab 1: Engagement Letters */}
      {activeTab === 'letters' && (
        <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead style={{ backgroundColor: 'var(--card-subtle)', borderBottom: '1px solid var(--border)' }}>
                <tr>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Letter No / FY</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Client</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Engagement Type</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Agreed Fee</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Status</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Acceptance Details</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {letters.length === 0 ? (
                  <tr>
                    <td colSpan={7} style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>
                      No engagement letters created. Click "+ Issue Engagement Letter" to generate a contract.
                    </td>
                  </tr>
                ) : (
                  letters.map((l) => (
                    <tr key={l.id} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 600, color: 'var(--text)' }}>{l.letterNumber}</div>
                        <div style={{ fontSize: '11px', color: 'var(--muted)' }}>FY {l.financialYear}</div>
                      </td>
                      <td style={{ padding: '12px', fontWeight: 500, color: 'var(--text-secondary)' }}>
                        {l.clientName || '-'}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span style={{ padding: '2px 8px', borderRadius: '4px', backgroundColor: 'var(--accent-subtle)', color: 'var(--accent)', fontSize: '11px', fontWeight: 600 }}>
                          {l.engagementType}
                        </span>
                        <div style={{ fontSize: '11px', color: 'var(--muted)', marginTop: '2px' }}>{l.feeStructure}</div>
                      </td>
                      <td style={{ padding: '12px', fontWeight: 700, color: 'var(--text)' }}>
                        ₹{l.agreedFeeAmount.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span
                          style={{
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 600,
                            backgroundColor: l.status === 'Accepted' ? 'var(--success-bg)' : l.status === 'SentToClient' ? 'var(--accent-subtle)' : 'var(--warning-bg)',
                            color: l.status === 'Accepted' ? 'var(--success)' : l.status === 'SentToClient' ? 'var(--accent)' : 'var(--warning)'
                          }}
                        >
                          {l.status}
                        </span>
                      </td>
                      <td style={{ padding: '12px' }}>
                        {l.acceptedDate ? (
                          <div>
                            <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--success)' }}>{l.clientSignatoryName}</div>
                            <div style={{ fontSize: '10px', color: 'var(--muted)' }}>
                              {new Date(l.acceptedDate).toLocaleDateString()} ({l.acceptanceIp})
                            </div>
                          </div>
                        ) : (
                          <span style={{ color: 'var(--muted)', fontSize: '12px' }}>Pending Acceptance</span>
                        )}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <div style={{ display: 'flex', gap: '6px' }}>
                          <button
                            onClick={() => setSelectedLetterForView(l)}
                            style={{ padding: '4px 8px', borderRadius: '4px', border: '1px solid var(--border-subtle)', background: 'var(--card)', fontSize: '11px', cursor: 'pointer' }}
                          >
                            View
                          </button>
                          {l.status === 'Draft' && (
                            <button
                              onClick={() => handleSendLetter(l.id)}
                              style={{ padding: '4px 8px', borderRadius: '4px', border: 'none', background: '#0284c7', color: '#fff', fontSize: '11px', cursor: 'pointer' }}
                            >
                              Send
                            </button>
                          )}
                          {l.status === 'SentToClient' && (
                            <button
                              onClick={() => {
                                setSelectedLetterForAccept(l)
                                setShowAcceptModal(true)
                              }}
                              style={{ padding: '4px 8px', borderRadius: '4px', border: 'none', background: '#059669', color: '#fff', fontSize: '11px', cursor: 'pointer' }}
                            >
                              Accept
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Tab 2: KYC & UBO Register */}
      {activeTab === 'kyc' && (
        <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead style={{ backgroundColor: 'var(--card-subtle)', borderBottom: '1px solid var(--border)' }}>
                <tr>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Client</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>PMLA Risk Tier</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Verification</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Constitution Doc</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Statutory Checks</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Beneficial Owner (UBO)</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Review Due Date</th>
                </tr>
              </thead>
              <tbody>
                {kycRecords.length === 0 ? (
                  <tr>
                    <td colSpan={7} style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>
                      No client AML / KYC records logged yet. Click "+ Add / Update Client KYC" to record risk profiles.
                    </td>
                  </tr>
                ) : (
                  kycRecords.map((k) => (
                    <tr key={k.id} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '12px', fontWeight: 600, color: 'var(--text)' }}>
                        {k.clientName || '-'}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span
                          style={{
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 600,
                            backgroundColor: k.riskRating === 'Low' ? 'var(--success-bg)' : k.riskRating === 'Medium' ? 'var(--warning-bg)' : 'var(--error-bg)',
                            color: k.riskRating === 'Low' ? 'var(--success)' : k.riskRating === 'Medium' ? 'var(--warning)' : 'var(--error)'
                          }}
                        >
                          {k.riskRating}
                        </span>
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span style={{ fontWeight: 600, color: k.verificationStatus === 'Verified' ? 'var(--success)' : 'var(--warning)' }}>
                          {k.verificationStatus}
                        </span>
                      </td>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 500 }}>{k.constitutionDocumentType}</div>
                        <div style={{ fontSize: '11px', color: 'var(--muted)' }}>{k.constitutionDocumentNumber || 'No ID'}</div>
                      </td>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontSize: '11px' }}>
                          PAN: {k.panVerified ? <span style={{ color: 'var(--success)' }}>✓ Verified</span> : <span style={{ color: 'var(--error)' }}>Pending</span>}
                        </div>
                        <div style={{ fontSize: '11px' }}>
                          GSTIN: {k.gstinVerified ? <span style={{ color: 'var(--success)' }}>✓ Verified</span> : <span style={{ color: 'var(--error)' }}>Pending</span>}
                        </div>
                      </td>
                      <td style={{ padding: '12px' }}>
                        {k.uboName ? (
                          <div>
                            <div style={{ fontWeight: 600 }}>{k.uboName}</div>
                            <div style={{ fontSize: '11px', color: 'var(--muted)' }}>Holding: {k.uboShareholdingPercentage}%</div>
                          </div>
                        ) : (
                          <span style={{ color: 'var(--muted)' }}>None identified</span>
                        )}
                      </td>
                      <td style={{ padding: '12px', color: 'var(--text-secondary)' }}>
                        {k.nextReviewDueDate ? new Date(k.nextReviewDueDate).toLocaleDateString() : '-'}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Tab 3: Template Generator */}
      {activeTab === 'template' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: '24px' }}>
          <div style={{ backgroundColor: 'var(--card)', padding: '24px', borderRadius: '8px', border: '1px solid var(--border)' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, marginTop: 0, color: 'var(--text)' }}>
              ICAI Standard (SA 210) Clause Generator
            </h3>
            <p style={{ fontSize: '13px', color: 'var(--muted)', marginBottom: '20px' }}>
              Generates legal terms conforming to ICAI Standards on Auditing SA 210 and SQC 1 quality control policies.
            </p>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Engagement Type:</label>
                <select
                  value={templateForm.engagementType}
                  onChange={(e) => setTemplateForm(prev => ({ ...prev, engagementType: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  <option value="StatutoryAudit">Statutory Audit (Sec 139 Companies Act)</option>
                  <option value="TaxAudit">Tax Audit (Sec 44AB Income Tax Act)</option>
                  <option value="GstAudit">GST Audit & Annual Return (Sec 44 GSTR-9C)</option>
                  <option value="DirectTaxFiling">Direct Tax Computation & ITR Filing</option>
                  <option value="InternalAudit">Internal Audit & Internal Financial Controls</option>
                </select>
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Client Legal Name:</label>
                <input
                  type="text"
                  value={templateForm.clientName}
                  onChange={(e) => setTemplateForm(prev => ({ ...prev, clientName: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Financial Year:</label>
                <input
                  type="text"
                  value={templateForm.financialYear}
                  onChange={(e) => setTemplateForm(prev => ({ ...prev, financialYear: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Fee Amount (₹):</label>
                <input
                  type="number"
                  value={templateForm.agreedFeeAmount}
                  onChange={(e) => setTemplateForm(prev => ({ ...prev, agreedFeeAmount: Number(e.target.value) }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <button
                onClick={handlePreviewTemplate}
                style={{
                  padding: '10px 16px',
                  backgroundColor: '#2563eb',
                  color: '#fff',
                  border: 'none',
                  borderRadius: '6px',
                  fontWeight: 600,
                  cursor: 'pointer',
                  marginTop: '8px'
                }}
              >
                Generate SA 210 Letter Text
              </button>
            </div>
          </div>

          <div style={{ backgroundColor: 'var(--card)', padding: '24px', borderRadius: '8px', border: '1px solid var(--border)' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, marginTop: 0, color: 'var(--text)' }}>
              Engagement Letter Preview
            </h3>
            {generatedTemplate ? (
              <div style={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: '12px', backgroundColor: 'var(--card-subtle)', padding: '16px', borderRadius: '6px', border: '1px solid var(--border)', maxHeight: '550px', overflowY: 'auto', lineHeight: '1.5' }}>
                {generatedTemplate.fullLetterContent}
              </div>
            ) : (
              <div style={{ textAlign: 'center', padding: '40px 20px', color: 'var(--muted)' }}>
                Click "Generate SA 210 Letter Text" to assemble standardized statutory engagement terms.
              </div>
            )}
          </div>
        </div>
      )}

      {/* Modal: New Engagement Letter */}
      {showLetterModal && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', width: '600px', maxWidth: '90%', maxHeight: '90vh', overflowY: 'auto', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--text)' }}>Issue Digital Engagement Letter</h3>
            <form onSubmit={handleCreateLetter} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Client:</label>
                <select
                  required
                  value={letterForm.clientId}
                  onChange={(e) => setLetterForm(prev => ({ ...prev, clientId: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  {clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Engagement Type:</label>
                  <select
                    value={letterForm.engagementType}
                    onChange={(e) => setLetterForm(prev => ({ ...prev, engagementType: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="StatutoryAudit">Statutory Audit (Sec 139)</option>
                    <option value="TaxAudit">Tax Audit (Sec 44AB)</option>
                    <option value="GstAudit">GST Audit (GSTR-9C)</option>
                    <option value="DirectTaxFiling">Direct Tax Computation</option>
                    <option value="InternalAudit">Internal Audit & IFC</option>
                    <option value="AccountingBookkeeping">Accounting & Bookkeeping</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Financial Year:</label>
                  <input
                    type="text"
                    required
                    value={letterForm.financialYear}
                    placeholder="e.g. 2024-25"
                    onChange={(e) => setLetterForm(prev => ({ ...prev, financialYear: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Fee Structure:</label>
                  <select
                    value={letterForm.feeStructure}
                    onChange={(e) => setLetterForm(prev => ({ ...prev, feeStructure: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="FixedFee">Fixed Fee</option>
                    <option value="TimeBased">Time Based / Hourly</option>
                    <option value="MonthlyRetainer">Monthly Retainer</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Agreed Fee Amount (₹):</label>
                  <input
                    type="number"
                    required
                    value={letterForm.agreedFeeAmount}
                    onChange={(e) => setLetterForm(prev => ({ ...prev, agreedFeeAmount: Number(e.target.value) }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
              </div>

              <div style={{ padding: '8px 0' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={letterForm.outOfPocketIncluded ?? false}
                    onChange={(e) => setLetterForm(prev => ({ ...prev, outOfPocketIncluded: e.target.checked }))}
                  />
                  <span>Out of pocket expenses included in agreed fee</span>
                </label>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowLetterModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border-subtle)', background: 'var(--card)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: '#2563eb', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  Generate & Save Letter
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Digital Acceptance */}
      {showAcceptModal && selectedLetterForAccept && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', width: '500px', maxWidth: '90%', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--text)' }}>
              Confirm Client Digital Acceptance
            </h3>
            <p style={{ fontSize: '13px', color: 'var(--muted)', marginBottom: '16px' }}>
              Letter {selectedLetterForAccept.letterNumber} ({selectedLetterForAccept.engagementType}) for {selectedLetterForAccept.clientName}
            </p>
            <form onSubmit={handleAcceptLetter} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Signatory Name:</label>
                <input
                  type="text"
                  required
                  value={acceptForm.signatoryName}
                  onChange={(e) => setAcceptForm(prev => ({ ...prev, signatoryName: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Designation:</label>
                <input
                  type="text"
                  required
                  value={acceptForm.signatoryDesignation}
                  onChange={(e) => setAcceptForm(prev => ({ ...prev, signatoryDesignation: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowAcceptModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border-subtle)', background: 'var(--card)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: '#059669', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  Record Acceptance
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: View Letter */}
      {selectedLetterForView && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', width: '700px', maxWidth: '90%', maxHeight: '90vh', overflowY: 'auto', padding: '24px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h3 style={{ fontSize: '18px', fontWeight: 700, margin: 0, color: 'var(--text)' }}>
                Engagement Letter: {selectedLetterForView.letterNumber}
              </h3>
              <button
                onClick={() => setSelectedLetterForView(null)}
                style={{ padding: '4px 8px', border: '1px solid var(--border-subtle)', background: 'var(--card)', borderRadius: '4px', cursor: 'pointer' }}
              >
                Close
              </button>
            </div>
            <div style={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: '12px', backgroundColor: 'var(--card-subtle)', padding: '16px', borderRadius: '6px', border: '1px solid var(--border)', lineHeight: '1.5' }}>
              {selectedLetterForView.letterContent}
            </div>
          </div>
        </div>
      )}

      {/* Modal: Add/Update KYC */}
      {showKycModal && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', width: '600px', maxWidth: '90%', maxHeight: '90vh', overflowY: 'auto', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--text)' }}>Client AML / PMLA KYC Profile</h3>
            <form onSubmit={handleSaveKyc} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Client:</label>
                <select
                  required
                  value={kycForm.clientId}
                  onChange={(e) => setKycForm(prev => ({ ...prev, clientId: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  {clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>PMLA Risk Rating:</label>
                  <select
                    value={kycForm.riskRating}
                    onChange={(e) => setKycForm(prev => ({ ...prev, riskRating: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="Low">Low Risk (3 Year Review)</option>
                    <option value="Medium">Medium Risk (2 Year Review)</option>
                    <option value="High">High Risk (Annual Review)</option>
                    <option value="PoliticallyExposedPerson">PEP (Politically Exposed Person)</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Verification Status:</label>
                  <select
                    value={kycForm.verificationStatus}
                    onChange={(e) => setKycForm(prev => ({ ...prev, verificationStatus: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="Verified">Verified</option>
                    <option value="PendingVerification">Pending Verification</option>
                    <option value="Rejected">Rejected</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Constitution Document:</label>
                  <input
                    type="text"
                    required
                    value={kycForm.constitutionDocumentType}
                    placeholder="e.g. CertificateOfIncorporation"
                    onChange={(e) => setKycForm(prev => ({ ...prev, constitutionDocumentType: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Doc Number / CIN / Reg No:</label>
                  <input
                    type="text"
                    value={kycForm.constitutionDocumentNumber || ''}
                    onChange={(e) => setKycForm(prev => ({ ...prev, constitutionDocumentNumber: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Ultimate Beneficial Owner (UBO):</label>
                  <input
                    type="text"
                    value={kycForm.uboName || ''}
                    placeholder="e.g. Arun Kumar"
                    onChange={(e) => setKycForm(prev => ({ ...prev, uboName: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>UBO Shareholding (%):</label>
                  <input
                    type="number"
                    step="0.01"
                    value={kycForm.uboShareholdingPercentage || ''}
                    placeholder="e.g. 51"
                    onChange={(e) => setKycForm(prev => ({ ...prev, uboShareholdingPercentage: Number(e.target.value) }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', gap: '20px', padding: '8px 0' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={kycForm.panVerified ?? true}
                    onChange={(e) => setKycForm(prev => ({ ...prev, panVerified: e.target.checked }))}
                  />
                  <span>PAN Verified</span>
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={kycForm.gstinVerified ?? true}
                    onChange={(e) => setKycForm(prev => ({ ...prev, gstinVerified: e.target.checked }))}
                  />
                  <span>GSTIN Verified</span>
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={kycForm.sanctionsCheckCleared ?? true}
                    onChange={(e) => setKycForm(prev => ({ ...prev, sanctionsCheckCleared: e.target.checked }))}
                  />
                  <span>Sanctions Cleared</span>
                </label>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowKycModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border-subtle)', background: 'var(--card)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: '#059669', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  Save KYC Record
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
export default EngagementKycPage
