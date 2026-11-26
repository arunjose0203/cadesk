import React, { useState, useEffect, useCallback } from 'react'
import {
  api,
  mcaApi,
  type Client,
  type ClientPage,
  type CompanyMasterDto,
  type DirectorDinDto,
  type McaFilingDto,
  type BoardResolutionDto,
  type McaComplianceSummaryDto,
  type McaLateFeeCalculationResult,
  type CreateCompanyRequest,
  type CreateDirectorRequest,
  type CreateFilingRequest,
  type CreateResolutionRequest
} from './api'

export const McaCompliancePage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'companies' | 'directors' | 'filings' | 'resolutions' | 'calculator'>('companies')
  const [summary, setSummary] = useState<McaComplianceSummaryDto | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [companies, setCompanies] = useState<CompanyMasterDto[]>([])
  const [directors, setDirectors] = useState<DirectorDinDto[]>([])
  const [filings, setFilings] = useState<McaFilingDto[]>([])
  const [resolutions, setResolutions] = useState<BoardResolutionDto[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Filters
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [selectedStatus, setSelectedStatus] = useState<string>('')

  // Modals
  const [showCompanyModal, setShowCompanyModal] = useState<boolean>(false)
  const [showDirectorModal, setShowDirectorModal] = useState<boolean>(false)
  const [showFilingModal, setShowFilingModal] = useState<boolean>(false)
  const [showResolutionModal, setShowResolutionModal] = useState<boolean>(false)

  // Company Form State
  const [companyForm, setCompanyForm] = useState<CreateCompanyRequest>({
    clientId: '',
    cin: 'U72200MH2021PTC123456',
    companyName: 'Apex Technologies Private Limited',
    companyClass: 'Private',
    authorizedCapital: 1000000,
    paidUpCapital: 500000,
    registeredOfficeAddress: '101 Technopark, Andheri East, Mumbai 400069',
    rocJurisdiction: 'RoC-Mumbai',
    incorporationDate: '2021-04-15',
    companyStatus: 'Active',
    lastAgmDate: '2024-09-28',
    financialYearEndMonth: 3
  })

  // Director Form State
  const [directorForm, setDirectorForm] = useState<CreateDirectorRequest>({
    companyId: '',
    din: '01234567',
    directorName: 'Arun Kumar Sharma',
    pan: 'ABCPS1234K',
    designation: 'Director',
    appointmentDate: '2021-04-15',
    cessationDate: null,
    dscExpiryDate: '2026-10-15',
    dir3KycStatus: 'Complied',
    dir3KycLastFiledDate: '2024-09-12',
    isSignatory: true
  })

  // Filing Form State
  const [filingForm, setFilingForm] = useState<CreateFilingRequest>({
    companyId: '',
    financialYear: '2023-24',
    formType: 'AOC-4',
    dueDate: '2024-10-30',
    filingDate: new Date().toISOString().split('T')[0],
    srn: 'AA1234567',
    status: 'Approved',
    normalFee: 400,
    remarks: 'Financial statements with director report'
  })

  // Resolution Form State
  const [resolutionForm, setResolutionForm] = useState<CreateResolutionRequest>({
    companyId: '',
    resolutionNumber: 'BR/2024-25/001',
    meetingType: 'BoardMeeting',
    meetingDate: new Date().toISOString().split('T')[0],
    category: 'Ordinary',
    subject: 'Approval of Annual Accounts and Statutory Audit Report',
    verbiage: 'RESOLVED THAT the audited balance sheet, profit and loss account, and auditors report for the year ended March 31, 2024 be and are hereby approved.',
    passedUnanimously: true,
    signatoryDin: '01234567',
    signatoryName: 'Arun Kumar Sharma'
  })

  // Calculator State
  const [calcForm, setCalcForm] = useState({
    formType: 'AOC-4',
    dueDate: '2024-10-30',
    filingDate: '2024-11-20',
    paidUpCapital: 500000
  })
  const [calcResult, setCalcResult] = useState<McaLateFeeCalculationResult | null>(null)

  const loadData = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)
      const [sum, clientRes, comps, dirs, flgs, resls] = await Promise.all([
        mcaApi.summary(),
        api<ClientPage>('/api/clients?take=100'),
        mcaApi.listCompanies(selectedClientId || undefined, selectedStatus || undefined),
        mcaApi.listDirectors(),
        mcaApi.listFilings(),
        mcaApi.listResolutions()
      ])
      setSummary(sum)
      const clientList = clientRes.items || []
      setClients(clientList)
      setCompanies(comps)
      setDirectors(dirs)
      setFilings(flgs)
      setResolutions(resls)

      if (clientList.length > 0) {
        setCompanyForm(prev => (prev.clientId ? prev : { ...prev, clientId: clientList[0].id }))
      }
      if (comps.length > 0) {
        setDirectorForm(prev => (prev.companyId ? prev : { ...prev, companyId: comps[0].id }))
        setFilingForm(prev => (prev.companyId ? prev : { ...prev, companyId: comps[0].id }))
        setResolutionForm(prev => (prev.companyId ? prev : { ...prev, companyId: comps[0].id }))
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load MCA data')
    } finally {
      setLoading(false)
    }
  }, [selectedClientId, selectedStatus])

  useEffect(() => {
    loadData()
  }, [loadData])

  const handleCreateCompany = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      setLoading(true)
      await mcaApi.createCompany(companyForm)
      setSuccess('Company Master registered successfully')
      setShowCompanyModal(false)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to register company')
    } finally {
      setLoading(false)
    }
  }

  const handleCreateDirector = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      setLoading(true)
      await mcaApi.createDirector(directorForm)
      setSuccess('Director DIN record saved successfully')
      setShowDirectorModal(false)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to add director')
    } finally {
      setLoading(false)
    }
  }

  const handleCreateFiling = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      setLoading(true)
      await mcaApi.createFiling(filingForm)
      setSuccess('MCA Filing logged successfully')
      setShowFilingModal(false)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to log filing')
    } finally {
      setLoading(false)
    }
  }

  const handleCreateResolution = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      setLoading(true)
      await mcaApi.createResolution(resolutionForm)
      setSuccess('Board resolution recorded successfully')
      setShowResolutionModal(false)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to save resolution')
    } finally {
      setLoading(false)
    }
  }

  const handleCalculateLateFee = async () => {
    try {
      const res = await mcaApi.calculateLateFee({
        formType: calcForm.formType,
        dueDate: calcForm.dueDate,
        filingDate: calcForm.filingDate,
        paidUpCapital: Number(calcForm.paidUpCapital)
      })
      setCalcResult(res)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Calculation failed')
    }
  }

  return (
    <div style={{ padding: '24px', maxWidth: '1400px', margin: '0 auto' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 700, margin: 0, color: 'var(--text)' }}>
            ROC & MCA Corporate Governance Studio
          </h1>
          <p style={{ margin: '4px 0 0', color: 'var(--muted)', fontSize: '14px' }}>
            Company Master, Director DIN / DIR-3 KYC tracking, Section 403 statutory fee engine & Secretarial resolutions
          </p>
        </div>
        <div style={{ display: 'flex', gap: '8px' }}>
          <button
            onClick={() => setShowCompanyModal(true)}
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
            + New Company
          </button>
          <button
            onClick={() => setShowDirectorModal(true)}
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
            + Add Director (DIN)
          </button>
          <button
            onClick={() => setShowFilingModal(true)}
            style={{
              padding: '8px 16px',
              backgroundColor: '#7c3aed',
              color: '#fff',
              border: 'none',
              borderRadius: '6px',
              fontWeight: 500,
              cursor: 'pointer'
            }}
          >
            + Log MCA Filing
          </button>
          <button
            onClick={() => setShowResolutionModal(true)}
            style={{
              padding: '8px 16px',
              backgroundColor: '#0284c7',
              color: '#fff',
              border: 'none',
              borderRadius: '6px',
              fontWeight: 500,
              cursor: 'pointer'
            }}
          >
            + Draft Resolution
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
            <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--muted)', textTransform: 'uppercase' }}>Total Companies</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--text)', marginTop: '4px' }}>{summary.totalCompaniesCount}</div>
            <div style={{ fontSize: '13px', color: 'var(--success)', marginTop: '4px' }}>{summary.activeCompaniesCount} Active Entities</div>
          </div>
          <div style={{ backgroundColor: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
            <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--accent)', textTransform: 'uppercase' }}>Directors Enrolled</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--accent)', marginTop: '4px' }}>{summary.totalDirectorsCount}</div>
            <div style={{ fontSize: '13px', color: summary.pendingKycCount > 0 ? 'var(--error)' : 'var(--success)', marginTop: '4px' }}>
              {summary.pendingKycCount} Pending DIR-3 KYC
            </div>
          </div>
          <div style={{ backgroundColor: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
            <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--warning)', textTransform: 'uppercase' }}>Statutory Filings</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--warning)', marginTop: '4px' }}>{summary.pendingFilingsCount} Pending</div>
            <div style={{ fontSize: '13px', color: 'var(--success)', marginTop: '4px' }}>{summary.approvedFilingsCount} Approved Returns</div>
          </div>
          <div style={{ backgroundColor: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
            <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--error)', textTransform: 'uppercase' }}>Accrued Late Fees</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--error)', marginTop: '4px' }}>₹{summary.totalLateFeesAccrued.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</div>
            <div style={{ fontSize: '13px', color: 'var(--muted)', marginTop: '4px' }}>Section 403 second proviso</div>
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
          <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Company Status:</label>
          <select
            value={selectedStatus}
            onChange={(e) => setSelectedStatus(e.target.value)}
            style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '14px' }}
          >
            <option value="">All Statuses</option>
            <option value="Active">Active</option>
            <option value="StrikeOff">Strike Off</option>
            <option value="Dormant">Dormant</option>
            <option value="Amalgamated">Amalgamated</option>
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
          onClick={() => setActiveTab('companies')}
          style={{
            padding: '10px 20px',
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'companies' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'companies' ? 'var(--accent)' : 'var(--muted)',
            fontWeight: activeTab === 'companies' ? 600 : 500,
            cursor: 'pointer'
          }}
        >
          Company Master ({companies.length})
        </button>
        <button
          onClick={() => setActiveTab('directors')}
          style={{
            padding: '10px 20px',
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'directors' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'directors' ? 'var(--accent)' : 'var(--muted)',
            fontWeight: activeTab === 'directors' ? 600 : 500,
            cursor: 'pointer'
          }}
        >
          Directors & DIN KYC ({directors.length})
        </button>
        <button
          onClick={() => setActiveTab('filings')}
          style={{
            padding: '10px 20px',
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'filings' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'filings' ? 'var(--accent)' : 'var(--muted)',
            fontWeight: activeTab === 'filings' ? 600 : 500,
            cursor: 'pointer'
          }}
        >
          Statutory Filings ({filings.length})
        </button>
        <button
          onClick={() => setActiveTab('resolutions')}
          style={{
            padding: '10px 20px',
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'resolutions' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'resolutions' ? 'var(--accent)' : 'var(--muted)',
            fontWeight: activeTab === 'resolutions' ? 600 : 500,
            cursor: 'pointer'
          }}
        >
          Board Resolutions ({resolutions.length})
        </button>
        <button
          onClick={() => setActiveTab('calculator')}
          style={{
            padding: '10px 20px',
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'calculator' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'calculator' ? 'var(--accent)' : 'var(--muted)',
            fontWeight: activeTab === 'calculator' ? 600 : 500,
            cursor: 'pointer'
          }}
        >
          Section 403 Fee Simulator
        </button>
      </div>

      {/* Tab 1: Companies */}
      {activeTab === 'companies' && (
        <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead style={{ backgroundColor: 'var(--card-subtle)', borderBottom: '1px solid var(--border)' }}>
                <tr>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Company Name / CIN</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Class / Jurisdiction</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Incorporation</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Capital (Auth / Paid-Up)</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Directors</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Pending Filings</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Status</th>
                </tr>
              </thead>
              <tbody>
                {companies.length === 0 ? (
                  <tr>
                    <td colSpan={7} style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>
                      No companies enrolled yet. Click "+ New Company" to register your first corporate client.
                    </td>
                  </tr>
                ) : (
                  companies.map((c) => (
                    <tr key={c.id} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 600, color: 'var(--text)' }}>{c.companyName}</div>
                        <div style={{ fontSize: '11px', fontFamily: 'monospace', color: 'var(--accent)' }}>{c.cin}</div>
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span style={{ display: 'inline-block', padding: '2px 8px', borderRadius: '4px', backgroundColor: 'var(--bg-subtle)', fontSize: '11px', fontWeight: 600 }}>
                          {c.companyClass}
                        </span>
                        <div style={{ fontSize: '11px', color: 'var(--muted)', marginTop: '2px' }}>{c.rocJurisdiction}</div>
                      </td>
                      <td style={{ padding: '12px', color: 'var(--text-secondary)' }}>
                        {c.incorporationDate ? new Date(c.incorporationDate).toLocaleDateString() : '-'}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 600 }}>₹{c.paidUpCapital.toLocaleString('en-IN')}</div>
                        <div style={{ fontSize: '11px', color: 'var(--muted)' }}>Auth: ₹{c.authorizedCapital.toLocaleString('en-IN')}</div>
                      </td>
                      <td style={{ padding: '12px', fontWeight: 600, color: 'var(--text)' }}>
                        {c.directorsCount} Directors
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span
                          style={{
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 600,
                            backgroundColor: c.pendingFilingsCount > 0 ? 'var(--warning-bg)' : 'var(--success-bg)',
                            color: c.pendingFilingsCount > 0 ? 'var(--warning)' : 'var(--success)'
                          }}
                        >
                          {c.pendingFilingsCount} Pending
                        </span>
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span
                          style={{
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 600,
                            backgroundColor: c.companyStatus === 'Active' ? 'var(--success-bg)' : 'var(--error-bg)',
                            color: c.companyStatus === 'Active' ? 'var(--success)' : 'var(--error)'
                          }}
                        >
                          {c.companyStatus}
                        </span>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Tab 2: Directors & DIN */}
      {activeTab === 'directors' && (
        <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead style={{ backgroundColor: 'var(--card-subtle)', borderBottom: '1px solid var(--border)' }}>
                <tr>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Director / DIN</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Company</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Designation</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Appointment Date</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>DSC Expiry</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>DIR-3 KYC Status</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Signatory</th>
                </tr>
              </thead>
              <tbody>
                {directors.length === 0 ? (
                  <tr>
                    <td colSpan={7} style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>
                      No director DIN records found. Add directors to track DIR-3 KYC compliance.
                    </td>
                  </tr>
                ) : (
                  directors.map((d) => (
                    <tr key={d.id} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 600, color: 'var(--text)' }}>{d.directorName}</div>
                        <div style={{ fontSize: '11px', fontFamily: 'monospace', color: 'var(--muted)' }}>DIN: {d.din}</div>
                      </td>
                      <td style={{ padding: '12px', color: 'var(--text-secondary)' }}>
                        {d.companyName || '-'}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span style={{ padding: '2px 8px', borderRadius: '4px', backgroundColor: 'var(--accent-subtle)', color: 'var(--accent)', fontSize: '11px', fontWeight: 600 }}>
                          {d.designation}
                        </span>
                      </td>
                      <td style={{ padding: '12px', color: 'var(--text-secondary)' }}>
                        {d.appointmentDate ? new Date(d.appointmentDate).toLocaleDateString() : '-'}
                      </td>
                      <td style={{ padding: '12px', color: 'var(--text-secondary)' }}>
                        {d.dscExpiryDate ? new Date(d.dscExpiryDate).toLocaleDateString() : 'No DSC'}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span
                          style={{
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 600,
                            backgroundColor: d.dir3KycStatus === 'Complied' ? 'var(--success-bg)' : 'var(--error-bg)',
                            color: d.dir3KycStatus === 'Complied' ? 'var(--success)' : 'var(--error)'
                          }}
                        >
                          {d.dir3KycStatus}
                        </span>
                      </td>
                      <td style={{ padding: '12px' }}>
                        {d.isSignatory ? (
                          <span style={{ color: 'var(--success)', fontWeight: 600 }}>Yes</span>
                        ) : (
                          <span style={{ color: 'var(--muted)' }}>No</span>
                        )}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Tab 3: Statutory Filings */}
      {activeTab === 'filings' && (
        <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead style={{ backgroundColor: 'var(--card-subtle)', borderBottom: '1px solid var(--border)' }}>
                <tr>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Form / FY</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Company</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Due Date</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Filing Date</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>SRN</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Fees (Normal / Late)</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Total Fee</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Status</th>
                </tr>
              </thead>
              <tbody>
                {filings.length === 0 ? (
                  <tr>
                    <td colSpan={8} style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>
                      No statutory filings logged yet. Click "+ Log MCA Filing" to record returns.
                    </td>
                  </tr>
                ) : (
                  filings.map((f) => (
                    <tr key={f.id} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '12px' }}>
                        <span style={{ padding: '2px 8px', borderRadius: '4px', backgroundColor: 'var(--purple-bg)', color: '#6b21a8', fontWeight: 600, fontSize: '12px' }}>
                          {f.formType}
                        </span>
                        <div style={{ fontSize: '11px', color: 'var(--muted)', marginTop: '2px' }}>FY {f.financialYear}</div>
                      </td>
                      <td style={{ padding: '12px', fontWeight: 500, color: 'var(--text)' }}>
                        {f.companyName || '-'}
                      </td>
                      <td style={{ padding: '12px', color: 'var(--text-secondary)' }}>
                        {f.dueDate ? new Date(f.dueDate).toLocaleDateString() : '-'}
                      </td>
                      <td style={{ padding: '12px', color: 'var(--text-secondary)' }}>
                        {f.filingDate ? new Date(f.filingDate).toLocaleDateString() : '-'}
                        {f.daysDelayed > 0 && (
                          <div style={{ fontSize: '10px', color: 'var(--error)', fontWeight: 600 }}>
                            {f.daysDelayed} days late
                          </div>
                        )}
                      </td>
                      <td style={{ padding: '12px', fontFamily: 'monospace', color: 'var(--accent)' }}>
                        {f.srn || '-'}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <div style={{ color: 'var(--text-secondary)' }}>Norm: ₹{f.normalFee.toLocaleString('en-IN')}</div>
                        {f.lateFee > 0 && (
                          <div style={{ color: 'var(--error)', fontWeight: 600 }}>Late: ₹{f.lateFee.toLocaleString('en-IN')}</div>
                        )}
                      </td>
                      <td style={{ padding: '12px', fontWeight: 700, color: 'var(--text)' }}>
                        ₹{f.totalFee.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span
                          style={{
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 600,
                            backgroundColor: f.status === 'Approved' ? 'var(--success-bg)' : 'var(--warning-bg)',
                            color: f.status === 'Approved' ? 'var(--success)' : 'var(--warning)'
                          }}
                        >
                          {f.status}
                        </span>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Tab 4: Board Resolutions */}
      {activeTab === 'resolutions' && (
        <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead style={{ backgroundColor: 'var(--card-subtle)', borderBottom: '1px solid var(--border)' }}>
                <tr>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Resolution No / Date</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Company</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Meeting Type</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Subject</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Verbiage Preview</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>Signatory</th>
                </tr>
              </thead>
              <tbody>
                {resolutions.length === 0 ? (
                  <tr>
                    <td colSpan={6} style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>
                      No board or committee resolutions logged. Draft resolutions for corporate records.
                    </td>
                  </tr>
                ) : (
                  resolutions.map((r) => (
                    <tr key={r.id} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 600, color: 'var(--text)' }}>{r.resolutionNumber}</div>
                        <div style={{ fontSize: '11px', color: 'var(--muted)' }}>
                          {r.meetingDate ? new Date(r.meetingDate).toLocaleDateString() : '-'}
                        </div>
                      </td>
                      <td style={{ padding: '12px', fontWeight: 500, color: 'var(--text-secondary)' }}>
                        {r.companyName || '-'}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span style={{ padding: '2px 8px', borderRadius: '4px', backgroundColor: 'var(--bg-subtle)', fontSize: '11px', fontWeight: 600 }}>
                          {r.meetingType}
                        </span>
                        <div style={{ fontSize: '10px', color: 'var(--muted)', marginTop: '2px' }}>{r.category} Resolution</div>
                      </td>
                      <td style={{ padding: '12px', fontWeight: 600, color: 'var(--text)' }}>
                        {r.subject}
                      </td>
                      <td style={{ padding: '12px', color: 'var(--muted)', maxWidth: '300px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {r.verbiage}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 500 }}>{r.signatoryName || 'Board Chair'}</div>
                        {r.signatoryDin && <div style={{ fontSize: '10px', color: 'var(--muted)' }}>DIN: {r.signatoryDin}</div>}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Tab 5: Fee Simulator */}
      {activeTab === 'calculator' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '24px' }}>
          <div style={{ backgroundColor: 'var(--card)', padding: '24px', borderRadius: '8px', border: '1px solid var(--border)' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, marginTop: 0, color: 'var(--text)' }}>
              Section 403 MCA Statutory Fee & Late Fee Simulator
            </h3>
            <p style={{ fontSize: '13px', color: 'var(--muted)', marginBottom: '20px' }}>
              Calculates normal filing fees and daily Section 403 late fees for AOC-4, MGT-7, and event-based forms under Companies Act, 2013.
            </p>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>MCA Form Type:</label>
                <select
                  value={calcForm.formType}
                  onChange={(e) => setCalcForm(prev => ({ ...prev, formType: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  <option value="AOC-4">AOC-4 (Financial Statements - ₹100/day)</option>
                  <option value="AOC-4_XBRL">AOC-4 XBRL (XBRL Filing - ₹100/day)</option>
                  <option value="MGT-7">MGT-7 (Annual Return - ₹100/day)</option>
                  <option value="MGT-7A">MGT-7A (Abridged Annual Return - ₹100/day)</option>
                  <option value="DIR-3_KYC">DIR-3 KYC (Director KYC - ₹5,000 flat late fee)</option>
                  <option value="DIR-12">DIR-12 (Appointment/Cessation - Slab Multipliers)</option>
                  <option value="MGT-14">MGT-14 (Filing of Resolutions - Slab Multipliers)</option>
                  <option value="ADT-1">ADT-1 (Auditor Appointment - Slab Multipliers)</option>
                  <option value="DPT-3">DPT-3 (Return of Deposits - Slab Multipliers)</option>
                  <option value="MSME-1">MSME-1 (Half Yearly Return - Slab Multipliers)</option>
                </select>
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Paid-up Capital (₹):</label>
                <input
                  type="number"
                  value={calcForm.paidUpCapital}
                  onChange={(e) => setCalcForm(prev => ({ ...prev, paidUpCapital: Number(e.target.value) }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Statutory Due Date:</label>
                <input
                  type="date"
                  value={calcForm.dueDate}
                  onChange={(e) => setCalcForm(prev => ({ ...prev, dueDate: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Actual / Projected Filing Date:</label>
                <input
                  type="date"
                  value={calcForm.filingDate}
                  onChange={(e) => setCalcForm(prev => ({ ...prev, filingDate: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <button
                onClick={handleCalculateLateFee}
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
                Compute Section 403 Fees
              </button>
            </div>
          </div>

          <div style={{ backgroundColor: 'var(--card)', padding: '24px', borderRadius: '8px', border: '1px solid var(--border)' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, marginTop: 0, color: 'var(--text)' }}>
              Fee Computation Summary
            </h3>
            {calcResult ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                <div style={{ padding: '16px', backgroundColor: 'var(--card-subtle)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
                    <span style={{ color: 'var(--muted)' }}>Normal Statutory Fee:</span>
                    <span style={{ fontWeight: 600, color: 'var(--text)' }}>₹{calcResult.normalFee.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
                    <span style={{ color: 'var(--muted)' }}>Days Delayed:</span>
                    <span style={{ fontWeight: 700, color: calcResult.daysDelayed > 0 ? 'var(--error)' : 'var(--success)' }}>
                      {calcResult.daysDelayed} days
                    </span>
                  </div>
                </div>

                <div style={{ padding: '16px', backgroundColor: 'var(--error-bg)', borderRadius: '8px', border: '1px solid var(--error-border)' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '15px', fontWeight: 700, color: 'var(--error)' }}>
                    <span>Section 403 Additional / Late Fee:</span>
                    <span>₹{calcResult.lateFee.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</span>
                  </div>
                </div>

                <div style={{ padding: '16px', backgroundColor: 'var(--success-bg)', borderRadius: '8px', border: '1px solid var(--success-border)' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '18px', fontWeight: 800, color: 'var(--success-text)' }}>
                    <span>Total Fee Payable at MCA:</span>
                    <span>₹{calcResult.totalFee.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</span>
                  </div>
                </div>

                <div style={{ padding: '14px', backgroundColor: 'var(--accent-subtle)', borderRadius: '8px', border: '1px solid var(--accent)', fontSize: '12px', color: 'var(--accent)' }}>
                  <strong>Legal Rule:</strong> {calcResult.legalProvisionDescription}
                </div>
              </div>
            ) : (
              <div style={{ textAlign: 'center', padding: '40px 20px', color: 'var(--muted)' }}>
                Enter the filing details on the left and click "Compute Section 403 Fees" to preview official MCA challan amounts.
              </div>
            )}
          </div>
        </div>
      )}

      {/* Modal: New Company Master */}
      {showCompanyModal && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', width: '620px', maxWidth: '90%', maxHeight: '90vh', overflowY: 'auto', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--text)' }}>Enroll Company Master</h3>
            <form onSubmit={handleCreateCompany} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Client:</label>
                <select
                  required
                  value={companyForm.clientId}
                  onChange={(e) => setCompanyForm(prev => ({ ...prev, clientId: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  {clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>CIN (21 characters):</label>
                  <input
                    type="text"
                    required
                    value={companyForm.cin}
                    placeholder="e.g. U72200MH2021PTC123456"
                    onChange={(e) => setCompanyForm(prev => ({ ...prev, cin: e.target.value.toUpperCase() }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Company Class:</label>
                  <select
                    value={companyForm.companyClass}
                    onChange={(e) => setCompanyForm(prev => ({ ...prev, companyClass: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="Private">Private Limited</option>
                    <option value="Public">Public Limited</option>
                    <option value="OnePersonCompany">One Person Company (OPC)</option>
                    <option value="Section8">Section 8 (Non-Profit)</option>
                    <option value="LLP">Limited Liability Partnership (LLP)</option>
                  </select>
                </div>
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Full Company Name:</label>
                <input
                  type="text"
                  required
                  value={companyForm.companyName}
                  onChange={(e) => setCompanyForm(prev => ({ ...prev, companyName: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Authorized Capital (₹):</label>
                  <input
                    type="number"
                    required
                    value={companyForm.authorizedCapital}
                    onChange={(e) => setCompanyForm(prev => ({ ...prev, authorizedCapital: Number(e.target.value) }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Paid-up Capital (₹):</label>
                  <input
                    type="number"
                    required
                    value={companyForm.paidUpCapital}
                    onChange={(e) => setCompanyForm(prev => ({ ...prev, paidUpCapital: Number(e.target.value) }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Incorporation Date:</label>
                  <input
                    type="date"
                    required
                    value={companyForm.incorporationDate}
                    onChange={(e) => setCompanyForm(prev => ({ ...prev, incorporationDate: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>RoC Jurisdiction:</label>
                  <input
                    type="text"
                    required
                    value={companyForm.rocJurisdiction}
                    onChange={(e) => setCompanyForm(prev => ({ ...prev, rocJurisdiction: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Registered Office Address:</label>
                <textarea
                  required
                  rows={2}
                  value={companyForm.registeredOfficeAddress}
                  onChange={(e) => setCompanyForm(prev => ({ ...prev, registeredOfficeAddress: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowCompanyModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border-subtle)', background: 'var(--card)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: '#2563eb', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  Save Company
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: New Director (DIN) */}
      {showDirectorModal && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', width: '560px', maxWidth: '90%', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--text)' }}>Enroll Director (DIN)</h3>
            <form onSubmit={handleCreateDirector} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Company:</label>
                <select
                  required
                  value={directorForm.companyId}
                  onChange={(e) => setDirectorForm(prev => ({ ...prev, companyId: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  {companies.map(c => <option key={c.id} value={c.id}>{c.companyName} ({c.cin})</option>)}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>DIN (8 Digits):</label>
                  <input
                    type="text"
                    required
                    value={directorForm.din}
                    placeholder="e.g. 01234567"
                    onChange={(e) => setDirectorForm(prev => ({ ...prev, din: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Director Name:</label>
                  <input
                    type="text"
                    required
                    value={directorForm.directorName}
                    onChange={(e) => setDirectorForm(prev => ({ ...prev, directorName: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Designation:</label>
                  <select
                    value={directorForm.designation}
                    onChange={(e) => setDirectorForm(prev => ({ ...prev, designation: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="Director">Director</option>
                    <option value="ManagingDirector">Managing Director</option>
                    <option value="WholeTimeDirector">Whole-time Director</option>
                    <option value="IndependentDirector">Independent Director</option>
                    <option value="AdditionalDirector">Additional Director</option>
                    <option value="DesignatedPartner">Designated Partner (LLP)</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>DIR-3 KYC Status:</label>
                  <select
                    value={directorForm.dir3KycStatus}
                    onChange={(e) => setDirectorForm(prev => ({ ...prev, dir3KycStatus: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="Complied">Complied</option>
                    <option value="Pending">Pending</option>
                    <option value="DeactivatedDueToNonFiling">Deactivated</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Appointment Date:</label>
                  <input
                    type="date"
                    required
                    value={directorForm.appointmentDate}
                    onChange={(e) => setDirectorForm(prev => ({ ...prev, appointmentDate: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>DSC Expiry Date:</label>
                  <input
                    type="date"
                    value={directorForm.dscExpiryDate || ''}
                    onChange={(e) => setDirectorForm(prev => ({ ...prev, dscExpiryDate: e.target.value || null }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
              </div>

              <div style={{ padding: '8px 0' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={directorForm.isSignatory ?? true}
                    onChange={(e) => setDirectorForm(prev => ({ ...prev, isSignatory: e.target.checked }))}
                  />
                  <span>Authorized Signatory on MCA Portal</span>
                </label>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowDirectorModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border-subtle)', background: 'var(--card)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: '#059669', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  Save Director
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Log Filing */}
      {showFilingModal && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', width: '560px', maxWidth: '90%', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--text)' }}>Log MCA Statutory Filing</h3>
            <form onSubmit={handleCreateFiling} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Company:</label>
                <select
                  required
                  value={filingForm.companyId}
                  onChange={(e) => setFilingForm(prev => ({ ...prev, companyId: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  {companies.map(c => <option key={c.id} value={c.id}>{c.companyName}</option>)}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Form Type:</label>
                  <select
                    value={filingForm.formType}
                    onChange={(e) => setFilingForm(prev => ({ ...prev, formType: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="AOC-4">AOC-4 (Financial Statements)</option>
                    <option value="AOC-4_XBRL">AOC-4 XBRL (XBRL)</option>
                    <option value="MGT-7">MGT-7 (Annual Return)</option>
                    <option value="MGT-7A">MGT-7A (Abridged Annual Return)</option>
                    <option value="DIR-12">DIR-12 (Director Appointment/Cessation)</option>
                    <option value="MGT-14">MGT-14 (Filing of Resolutions)</option>
                    <option value="ADT-1">ADT-1 (Auditor Appointment)</option>
                    <option value="DPT-3">DPT-3 (Return of Deposits)</option>
                    <option value="MSME-1">MSME-1 (Half Yearly Return)</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Financial Year:</label>
                  <input
                    type="text"
                    required
                    value={filingForm.financialYear}
                    placeholder="e.g. 2023-24"
                    onChange={(e) => setFilingForm(prev => ({ ...prev, financialYear: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Due Date:</label>
                  <input
                    type="date"
                    required
                    value={filingForm.dueDate}
                    onChange={(e) => setFilingForm(prev => ({ ...prev, dueDate: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Actual Filing Date:</label>
                  <input
                    type="date"
                    value={filingForm.filingDate || ''}
                    onChange={(e) => setFilingForm(prev => ({ ...prev, filingDate: e.target.value || null }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>SRN (Service Request No):</label>
                  <input
                    type="text"
                    value={filingForm.srn || ''}
                    placeholder="e.g. AA1234567"
                    onChange={(e) => setFilingForm(prev => ({ ...prev, srn: e.target.value.toUpperCase() }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Status:</label>
                  <select
                    value={filingForm.status}
                    onChange={(e) => setFilingForm(prev => ({ ...prev, status: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="Pending">Pending</option>
                    <option value="ChallanGenerated">Challan Generated</option>
                    <option value="Uploaded">Uploaded</option>
                    <option value="Approved">Approved</option>
                    <option value="Defective">Defective</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowFilingModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border-subtle)', background: 'var(--card)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: '#7c3aed', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  Record Filing
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Draft Resolution */}
      {showResolutionModal && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', width: '600px', maxWidth: '90%', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--text)' }}>Draft Board / Meeting Resolution</h3>
            <form onSubmit={handleCreateResolution} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Company:</label>
                <select
                  required
                  value={resolutionForm.companyId}
                  onChange={(e) => setResolutionForm(prev => ({ ...prev, companyId: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  {companies.map(c => <option key={c.id} value={c.id}>{c.companyName}</option>)}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Resolution No:</label>
                  <input
                    type="text"
                    required
                    value={resolutionForm.resolutionNumber}
                    onChange={(e) => setResolutionForm(prev => ({ ...prev, resolutionNumber: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Meeting Type:</label>
                  <select
                    value={resolutionForm.meetingType}
                    onChange={(e) => setResolutionForm(prev => ({ ...prev, meetingType: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="BoardMeeting">Board Meeting</option>
                    <option value="AGM">Annual General Meeting (AGM)</option>
                    <option value="EGM">Extraordinary General Meeting (EGM)</option>
                    <option value="CommitteeMeeting">Audit Committee Meeting</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Meeting Date:</label>
                  <input
                    type="date"
                    required
                    value={resolutionForm.meetingDate}
                    onChange={(e) => setResolutionForm(prev => ({ ...prev, meetingDate: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Category:</label>
                  <select
                    value={resolutionForm.category}
                    onChange={(e) => setResolutionForm(prev => ({ ...prev, category: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="Ordinary">Ordinary Resolution</option>
                    <option value="Special">Special Resolution (75% majority)</option>
                    <option value="Unanimous">Unanimous Resolution</option>
                  </select>
                </div>
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Subject / Agenda:</label>
                <input
                  type="text"
                  required
                  value={resolutionForm.subject}
                  onChange={(e) => setResolutionForm(prev => ({ ...prev, subject: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Resolution Text / Verbiage:</label>
                <textarea
                  required
                  rows={4}
                  value={resolutionForm.verbiage}
                  onChange={(e) => setResolutionForm(prev => ({ ...prev, verbiage: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Signatory Name:</label>
                  <input
                    type="text"
                    value={resolutionForm.signatoryName || ''}
                    onChange={(e) => setResolutionForm(prev => ({ ...prev, signatoryName: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Signatory DIN:</label>
                  <input
                    type="text"
                    value={resolutionForm.signatoryDin || ''}
                    onChange={(e) => setResolutionForm(prev => ({ ...prev, signatoryDin: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowResolutionModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border-subtle)', background: 'var(--card)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: '#0284c7', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  Save Resolution
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
export default McaCompliancePage
