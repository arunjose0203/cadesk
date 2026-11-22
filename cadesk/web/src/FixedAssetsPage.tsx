import React, { useState, useEffect } from 'react'
import { toError,
  fixedAssetApi,
  api,
  type FixedAssetDto,
  type AssetDepreciationEntryDto,
  type AssetPhysicalVerificationDto,
  type FixedAssetSummaryDto,
  type SaveFixedAssetRequest,
  type DisposeAssetRequest,
  type RunDualDepreciationRequest,
  type RecordPhysicalVerificationRequest,
  type AssetClass,
  type AssetStatus,
  type Client,
  type ClientPage
} from './api'

interface FixedAssetsPageProps {
  can?: (p: string) => boolean
}

const formatCurrency = (amount: number) => {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 2
  }).format(amount)
}

const formatAssetClass = (ac: AssetClass | string) => {
  switch (ac) {
    case 'Building': return '🏢 Building'
    case 'PlantAndMachinery': return '⚙️ Plant & Machinery'
    case 'ComputersAndSoftware': return '💻 Computers & Software'
    case 'FurnitureAndFixtures': return '🪑 Furniture & Fixtures'
    case 'OfficeEquipment': return '📠 Office Equipment'
    case 'Vehicles': return '🚗 Motor Vehicles'
    case 'Intangibles': return '📜 Intangibles'
    default: return String(ac)
  }
}

export const FixedAssetsPage: React.FC<FixedAssetsPageProps> = ({ can }) => {
  const [activeTab, setActiveTab] = useState<'assets' | 'depreciation' | 'verifications'>('assets')
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Filters & State
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [selectedFy, setSelectedFy] = useState<string>('2024-25')
  const [summary, setSummary] = useState<FixedAssetSummaryDto | null>(null)
  const [assets, setAssets] = useState<FixedAssetDto[]>([])
  const [deprEntries, setDeprEntries] = useState<AssetDepreciationEntryDto[]>([])
  const [verifications, setVerifications] = useState<AssetPhysicalVerificationDto[]>([])

  // Filter criteria for assets
  const [filterClass, setFilterClass] = useState<string>('')
  const [filterStatus, setFilterStatus] = useState<string>('')

  // Modals
  const [showAssetModal, setShowAssetModal] = useState<boolean>(false)
  const [showDeprModal, setShowDeprModal] = useState<boolean>(false)
  const [showVerifModal, setShowVerifModal] = useState<boolean>(false)
  const [showDisposeModal, setShowDisposeModal] = useState<boolean>(false)
  const [editingAssetId, setEditingAssetId] = useState<string | null>(null)
  const [selectedAsset, setSelectedAsset] = useState<FixedAssetDto | null>(null)

  // Asset Form
  const [assetForm, setAssetForm] = useState<SaveFixedAssetRequest>({
    clientId: '',
    assetTag: '',
    name: '',
    assetClass: 'PlantAndMachinery',
    purchaseDate: new Date().toISOString().split('T')[0],
    capitalizationDate: new Date().toISOString().split('T')[0],
    purchaseCost: 100000,
    inwardFreight: 5000,
    installationCost: 10000,
    preOperatingCost: 0,
    gstItcClaimed: 18000,
    salvageValue: 4850,
    usefulLifeYears: 10,
    companyDepreciationMethod: 'SLM',
    shiftType: 'SingleShift',
    incomeTaxBlockRatePercent: 15,
    isEligibleForAdditionalDepreciation: false,
    location: 'Main Factory',
    department: 'Operations',
    custodianName: 'Vikram Joshi'
  })

  // Run Depreciation Form
  const [deprForm, setDeprForm] = useState<RunDualDepreciationRequest>({
    clientId: '',
    financialYear: '2024-25',
    assetId: null,
    corporateTaxRate: 0.2517
  })

  // Physical Verification Form
  const [verifForm, setVerifForm] = useState<RecordPhysicalVerificationRequest>({
    verificationDate: new Date().toISOString().split('T')[0],
    verifiedBy: 'Audit Team',
    status: 'FoundAndGood',
    remarks: 'Verified in working condition',
    locationVerified: ''
  })

  // Dispose Form
  const [disposeForm, setDisposeForm] = useState<DisposeAssetRequest>({
    disposalDate: new Date().toISOString().split('T')[0],
    disposalProceeds: 0,
    remarks: 'Sold in secondary market'
  })

  useEffect(() => {
    loadClients()
  }, [])

  useEffect(() => {
    loadData(selectedClientId || undefined, selectedFy)
  }, [selectedClientId, selectedFy])

  const loadClients = async () => {
    try {
      const res = await api<ClientPage>('/api/clients?pageSize=100')
      if (res && res.items) {
        setClients(res.items)
        if (res.items.length > 0 && !selectedClientId) {
          setAssetForm(f => ({ ...f, clientId: res.items[0].id }))
          setDeprForm(f => ({ ...f, clientId: res.items[0].id }))
        }
      }
    } catch {
      // Non-fatal if clients cannot be loaded immediately
    }
  }

  const loadData = async (cId?: string, fy?: string) => {
    setLoading(true)
    setError(null)
    try {
      const [sum, assetList, deprList, verifList] = await Promise.all([
        fixedAssetApi.getSummary(cId, fy),
        fixedAssetApi.getAssets(cId, (filterClass as AssetClass) || undefined, (filterStatus as AssetStatus) || undefined),
        fixedAssetApi.getDepreciationEntries(undefined, cId, fy),
        fixedAssetApi.getVerifications(undefined, cId)
      ])
      setSummary(sum)
      setAssets(assetList)
      setDeprEntries(deprList)
      setVerifications(verifList)
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to load fixed asset data')
    } finally {
      setLoading(false)
    }
  }

  const handleOpenCreateAsset = () => {
    const defaultClient = clients.find(c => c.id === (selectedClientId || clients[0]?.id))
    const randomTag = 'FA-' + new Date().getFullYear() + '-' + Math.floor(100 + Math.random() * 900)
    setEditingAssetId(null)
    setAssetForm({
      clientId: defaultClient?.id || '',
      assetTag: randomTag,
      name: '',
      assetClass: 'PlantAndMachinery',
      purchaseDate: new Date().toISOString().split('T')[0],
      capitalizationDate: new Date().toISOString().split('T')[0],
      purchaseCost: 100000,
      inwardFreight: 5000,
      installationCost: 10000,
      preOperatingCost: 0,
      gstItcClaimed: 18000,
      salvageValue: 4850,
      usefulLifeYears: 10,
      companyDepreciationMethod: 'SLM',
      shiftType: 'SingleShift',
      incomeTaxBlockRatePercent: 15,
      isEligibleForAdditionalDepreciation: false,
      location: 'Main Factory',
      department: 'Operations',
      custodianName: 'Staff In-charge'
    })
    setShowAssetModal(true)
  }

  const handleOpenEditAsset = (a: FixedAssetDto) => {
    setEditingAssetId(a.id)
    setAssetForm({
      clientId: a.clientId,
      assetTag: a.assetTag,
      name: a.name,
      assetClass: a.assetClass,
      purchaseDate: a.purchaseDate,
      capitalizationDate: a.capitalizationDate,
      purchaseCost: a.purchaseCost,
      inwardFreight: a.inwardFreight,
      installationCost: a.installationCost,
      preOperatingCost: a.preOperatingCost,
      gstItcClaimed: a.gstItcClaimed,
      salvageValue: a.salvageValue,
      usefulLifeYears: a.usefulLifeYears,
      companyDepreciationMethod: a.companyDepreciationMethod,
      shiftType: a.shiftType,
      incomeTaxBlockRatePercent: a.incomeTaxBlockRatePercent,
      isEligibleForAdditionalDepreciation: a.isEligibleForAdditionalDepreciation,
      location: a.location || '',
      department: a.department || '',
      custodianName: a.custodianName || ''
    })
    setShowAssetModal(true)
  }

  const handleSaveAsset = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setSuccess(null)
    try {
      if (editingAssetId) {
        await fixedAssetApi.updateAsset(editingAssetId, assetForm)
        setSuccess(`Asset ${assetForm.assetTag} updated successfully!`)
      } else {
        await fixedAssetApi.createAsset(assetForm)
        setSuccess(`New Asset ${assetForm.assetTag} registered successfully!`)
      }
      setShowAssetModal(false)
      loadData(selectedClientId || undefined, selectedFy)
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to save fixed asset')
    }
  }

  const handleOpenRunDepr = () => {
    const curClient = clients.find(c => c.id === (selectedClientId || clients[0]?.id))
    setDeprForm({
      clientId: curClient?.id || '',
      financialYear: selectedFy,
      assetId: null,
      corporateTaxRate: 0.2517
    })
    setShowDeprModal(true)
  }

  const handleSubmitDepr = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setSuccess(null)
    try {
      const res = await fixedAssetApi.runDepreciation(deprForm)
      setSuccess(`Dual depreciation calculated for ${res.length} asset(s) for FY ${deprForm.financialYear}!`)
      setShowDeprModal(false)
      loadData(selectedClientId || undefined, selectedFy)
      setActiveTab('depreciation')
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to run dual depreciation')
    }
  }

  const handleOpenVerification = (a: FixedAssetDto) => {
    setSelectedAsset(a)
    setVerifForm({
      verificationDate: new Date().toISOString().split('T')[0],
      verifiedBy: 'Audit Team',
      status: 'FoundAndGood',
      remarks: 'Verified in working condition',
      locationVerified: a.location || ''
    })
    setShowVerifModal(true)
  }

  const handleSubmitVerification = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedAsset) return
    setError(null)
    setSuccess(null)
    try {
      await fixedAssetApi.recordVerification(selectedAsset.id, verifForm)
      setSuccess(`Physical verification recorded for ${selectedAsset.assetTag}!`)
      setShowVerifModal(false)
      loadData(selectedClientId || undefined, selectedFy)
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to record physical verification')
    }
  }

  const handleOpenDispose = (a: FixedAssetDto) => {
    setSelectedAsset(a)
    setDisposeForm({
      disposalDate: new Date().toISOString().split('T')[0],
      disposalProceeds: a.latestBookValue,
      remarks: 'Sold in secondary market'
    })
    setShowDisposeModal(true)
  }

  const handleSubmitDispose = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedAsset) return
    setError(null)
    setSuccess(null)
    try {
      await fixedAssetApi.disposeAsset(selectedAsset.id, disposeForm)
      setSuccess(`Asset ${selectedAsset.assetTag} marked as Disposed!`)
      setShowDisposeModal(false)
      loadData(selectedClientId || undefined, selectedFy)
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to dispose asset')
    }
  }

  // Real-time calculation of gross cost in asset form
  const computedGrossCost = Math.max(
    0,
    Number(assetForm.purchaseCost || 0) +
    Number(assetForm.inwardFreight || 0) +
    Number(assetForm.installationCost || 0) +
    Number(assetForm.preOperatingCost || 0) -
    Number(assetForm.gstItcClaimed || 0)
  )

  return (
    <div style={{ padding: '24px', maxWidth: '1440px', margin: '0 auto' }}>
      {/* Top Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px', flexWrap: 'wrap', gap: '16px' }}>
        <div>
          <h1 style={{ margin: 0, fontSize: '26px', fontWeight: 700, color: '#1a237e' }}>
            Fixed Asset Register & Dual Depreciation Desk
          </h1>
          <p style={{ margin: '4px 0 0 0', color: '#555', fontSize: '14px' }}>
            Companies Act 2013 Sch II (Useful Life / SLM / WDV) vs. Income Tax Act Sec 32 (Block of Assets & 180-Day Rule) & AS 22 Deferred Tax Engine.
          </p>
        </div>

        <div style={{ display: 'flex', gap: '12px', alignItems: 'center', flexWrap: 'wrap' }}>
          {/* Client Filter */}
          <select
            value={selectedClientId}
            onChange={e => setSelectedClientId(e.target.value)}
            style={{
              padding: '8px 12px',
              borderRadius: '6px',
              border: '1px solid #ccc',
              fontSize: '14px',
              background: '#fff',
              minWidth: '200px'
            }}
          >
            <option value="">All Clients</option>
            {clients.map(c => (
              <option key={c.id} value={c.id}>
                {c.name} ({c.code})
              </option>
            ))}
          </select>

          {/* FY Filter */}
          <select
            value={selectedFy}
            onChange={e => setSelectedFy(e.target.value)}
            style={{
              padding: '8px 12px',
              borderRadius: '6px',
              border: '1px solid #ccc',
              fontSize: '14px',
              background: '#fff',
              fontWeight: 600
            }}
          >
            <option value="2024-25">FY 2024-25</option>
            <option value="2023-24">FY 2023-24</option>
            <option value="2025-26">FY 2025-26</option>
          </select>

          <button
            onClick={() => loadData(selectedClientId || undefined, selectedFy)}
            disabled={loading}
            style={{
              padding: '8px 14px',
              borderRadius: '6px',
              border: '1px solid #1a237e',
              background: '#fff',
              color: '#1a237e',
              fontWeight: 600,
              cursor: 'pointer'
            }}
          >
            {loading ? 'Refreshing...' : '↻ Refresh'}
          </button>

          {(!can || can('assets.manage')) && (
            <button
              onClick={handleOpenCreateAsset}
              style={{
                padding: '8px 16px',
                borderRadius: '6px',
                border: 'none',
                background: '#1a237e',
                color: '#fff',
                fontWeight: 600,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '6px'
              }}
            >
              <span>+</span> Register Fixed Asset
            </button>
          )}

          {(!can || can('assets.depreciate')) && (
            <button
              onClick={handleOpenRunDepr}
              style={{
                padding: '8px 16px',
                borderRadius: '6px',
                border: 'none',
                background: '#0d47a1',
                color: '#fff',
                fontWeight: 600,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '6px'
              }}
            >
              <span>⚡</span> Run Dual Depreciation
            </button>
          )}
        </div>
      </div>

      {/* Notifications */}
      {error && (
        <div style={{
          padding: '12px 16px',
          background: '#ffebee',
          color: '#c62828',
          borderRadius: '6px',
          marginBottom: '20px',
          border: '1px solid #ffcdd2',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center'
        }}>
          <span><strong>Error:</strong> {error}</span>
          <button onClick={() => setError(null)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#c62828', fontWeight: 'bold' }}>✕</button>
        </div>
      )}

      {success && (
        <div style={{
          padding: '12px 16px',
          background: '#e8f5e9',
          color: '#2e7d32',
          borderRadius: '6px',
          marginBottom: '20px',
          border: '1px solid #c8e6c9',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center'
        }}>
          <span><strong>Success:</strong> {success}</span>
          <button onClick={() => setSuccess(null)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#2e7d32', fontWeight: 'bold' }}>✕</button>
        </div>
      )}

      {/* Summary KPI Cards */}
      {summary && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(210px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', borderLeft: '4px solid #1a237e' }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Total Gross Block</div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: '#1a237e', marginTop: '4px' }}>
              {formatCurrency(summary.totalGrossBlock)}
            </div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>
              Assets: <strong>{summary.totalAssets}</strong> ({summary.activeAssets} active)
            </div>
          </div>

          <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', borderLeft: '4px solid #2e7d32' }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Net Book Value (Sch II)</div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: '#2e7d32', marginTop: '4px' }}>
              {formatCurrency(summary.totalNetBookValue)}
            </div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>
              Accum Depr: {formatCurrency(summary.totalBookDepreciation)}
            </div>
          </div>

          <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', borderLeft: '4px solid #00838f' }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Tax Block WDV (Sec 32)</div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: '#00838f', marginTop: '4px' }}>
              {formatCurrency(summary.totalTaxWdv)}
            </div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>
              FY Tax Depr: {formatCurrency(summary.totalTaxDepreciation)}
            </div>
          </div>

          <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', borderLeft: '4px solid #f57f17' }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Timing Difference</div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: summary.totalTimingDifference >= 0 ? '#f57f17' : '#c62828', marginTop: '4px' }}>
              {formatCurrency(summary.totalTimingDifference)}
            </div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>
              Tax Depr - Book Depr
            </div>
          </div>

          <div style={{
            background: '#fff',
            padding: '16px',
            borderRadius: '8px',
            boxShadow: '0 2px 4px rgba(0,0,0,0.05)',
            borderLeft: summary.netDeferredTaxLiability >= 0 ? '4px solid #d32f2f' : '4px solid #388e3c'
          }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>
              AS 22 Deferred Tax {summary.netDeferredTaxLiability >= 0 ? '(DTL)' : '(DTA)'}
            </div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: summary.netDeferredTaxLiability >= 0 ? '#d32f2f' : '#388e3c', marginTop: '4px' }}>
              {formatCurrency(Math.abs(summary.netDeferredTaxLiability))}
            </div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>
              {summary.netDeferredTaxLiability >= 0 ? 'Deferred Tax Liability' : 'Deferred Tax Asset'}
            </div>
          </div>

          <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', borderLeft: '4px solid #4527a0' }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Physical Verification</div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: '#4527a0', marginTop: '4px' }}>
              {summary.verificationCompliancePercent}%
            </div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>
              {summary.verifiedInLast12Months} of {summary.totalAssets} verified in 12m
            </div>
          </div>
        </div>
      )}

      {/* Tabs */}
      <div style={{ display: 'flex', borderBottom: '2px solid #e0e0e0', marginBottom: '20px' }}>
        <button
          onClick={() => setActiveTab('assets')}
          style={{
            padding: '12px 24px',
            fontSize: '15px',
            fontWeight: 600,
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'assets' ? '3px solid #1a237e' : 'none',
            color: activeTab === 'assets' ? '#1a237e' : '#666',
            cursor: 'pointer'
          }}
        >
          🏷️ Asset Register & Master ({assets.length})
        </button>

        <button
          onClick={() => setActiveTab('depreciation')}
          style={{
            padding: '12px 24px',
            fontSize: '15px',
            fontWeight: 600,
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'depreciation' ? '3px solid #1a237e' : 'none',
            color: activeTab === 'depreciation' ? '#1a237e' : '#666',
            cursor: 'pointer'
          }}
        >
          ⚖️ Dual Depreciation & AS 22 Schedule ({deprEntries.length})
        </button>

        <button
          onClick={() => setActiveTab('verifications')}
          style={{
            padding: '12px 24px',
            fontSize: '15px',
            fontWeight: 600,
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'verifications' ? '3px solid #1a237e' : 'none',
            color: activeTab === 'verifications' ? '#1a237e' : '#666',
            cursor: 'pointer'
          }}
        >
          📋 Physical Verification Audits ({verifications.length})
        </button>
      </div>

      {/* TAB 1: Asset Register & Master */}
      {activeTab === 'assets' && (
        <div style={{ background: '#fff', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', overflow: 'hidden' }}>
          <div style={{ padding: '16px', borderBottom: '1px solid #eee', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '12px' }}>
            <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 600 }}>Fixed Asset Master Register</h3>
            <div style={{ display: 'flex', gap: '8px' }}>
              <select
                value={filterClass}
                onChange={e => {
                  setFilterClass(e.target.value)
                  loadData(selectedClientId || undefined, selectedFy)
                }}
                style={{ padding: '6px 10px', borderRadius: '4px', border: '1px solid #ccc', fontSize: '12px' }}
              >
                <option value="">All Asset Classes</option>
                <option value="Building">Buildings</option>
                <option value="PlantAndMachinery">Plant & Machinery</option>
                <option value="ComputersAndSoftware">Computers & Software</option>
                <option value="FurnitureAndFixtures">Furniture & Fixtures</option>
                <option value="OfficeEquipment">Office Equipment</option>
                <option value="Vehicles">Motor Vehicles</option>
                <option value="Intangibles">Intangibles</option>
              </select>

              <select
                value={filterStatus}
                onChange={e => {
                  setFilterStatus(e.target.value)
                  loadData(selectedClientId || undefined, selectedFy)
                }}
                style={{ padding: '6px 10px', borderRadius: '4px', border: '1px solid #ccc', fontSize: '12px' }}
              >
                <option value="">All Statuses</option>
                <option value="Active">Active</option>
                <option value="Disposed">Disposed</option>
                <option value="WrittenOff">Written Off</option>
              </select>
            </div>
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead>
                <tr style={{ background: '#f8f9fa', color: '#555', borderBottom: '2px solid #eee' }}>
                  <th style={{ padding: '12px' }}>Tag & Asset Name</th>
                  <th style={{ padding: '12px' }}>Class & Location</th>
                  <th style={{ padding: '12px' }}>Put to Use Date</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Gross Block</th>
                  <th style={{ padding: '12px' }}>Sch II Life / Method</th>
                  <th style={{ padding: '12px' }}>Sec 32 Rate</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Latest Net Book</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Latest Tax WDV</th>
                  <th style={{ padding: '12px' }}>Status</th>
                  <th style={{ padding: '12px', textAlign: 'center' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {assets.length === 0 ? (
                  <tr>
                    <td colSpan={10} style={{ padding: '32px', textAlign: 'center', color: '#999' }}>
                      No fixed assets found. Click "+ Register Fixed Asset" to add capital assets to the register.
                    </td>
                  </tr>
                ) : (
                  assets.map(a => {
                    const isActive = a.status === 'Active'
                    const isDisposed = a.status === 'Disposed'

                    return (
                      <tr key={a.id} style={{ borderBottom: '1px solid #eee', background: isDisposed ? '#fafafa' : '#fff' }}>
                        <td style={{ padding: '12px' }}>
                          <div style={{ fontWeight: 700, fontFamily: 'monospace', color: '#1a237e' }}>{a.assetTag}</div>
                          <div style={{ fontWeight: 600 }}>{a.name}</div>
                          <div style={{ fontSize: '11px', color: '#666' }}>Client: {a.clientName}</div>
                        </td>
                        <td style={{ padding: '12px' }}>
                          <div style={{ fontSize: '12px', fontWeight: 600 }}>{formatAssetClass(a.assetClass)}</div>
                          <div style={{ fontSize: '11px', color: '#666' }}>
                            {a.location || 'HQ'} | {a.department || 'General'}
                          </div>
                          {a.custodianName && (
                            <div style={{ fontSize: '10px', color: '#888' }}>Custodian: {a.custodianName}</div>
                          )}
                        </td>
                        <td style={{ padding: '12px' }}>
                          <div style={{ fontWeight: 600 }}>{a.capitalizationDate}</div>
                          <div style={{ fontSize: '11px', color: '#666' }}>Pur: {a.purchaseDate}</div>
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 700, color: '#1a237e' }}>
                          {formatCurrency(a.grossCost)}
                          <div style={{ fontSize: '10px', color: '#777' }}>
                            Sal: {formatCurrency(a.salvageValue)}
                          </div>
                        </td>
                        <td style={{ padding: '12px' }}>
                          <div style={{ fontWeight: 600 }}>{a.usefulLifeYears} yrs ({a.companyDepreciationMethod})</div>
                          <div style={{ fontSize: '11px', color: '#555' }}>
                            Shift: <span style={{ fontWeight: 600, color: a.shiftType === 'SingleShift' ? '#555' : '#1565c0' }}>{a.shiftType}</span>
                          </div>
                        </td>
                        <td style={{ padding: '12px' }}>
                          <div style={{ fontWeight: 600 }}>{a.incomeTaxBlockRatePercent}% Block</div>
                          {a.isEligibleForAdditionalDepreciation && (
                            <span style={{
                              display: 'inline-block',
                              padding: '2px 5px',
                              borderRadius: '3px',
                              background: '#e8f5e9',
                              color: '#2e7d32',
                              fontSize: '10px',
                              fontWeight: 700,
                              marginTop: '2px'
                            }}>
                              +20% Addl Depr
                            </span>
                          )}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: '#2e7d32' }}>
                          {formatCurrency(a.latestBookValue)}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: '#00838f' }}>
                          {formatCurrency(a.latestTaxWdv)}
                        </td>
                        <td style={{ padding: '12px' }}>
                          <span style={{
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 700,
                            background: isActive ? '#e8f5e9' : isDisposed ? '#fff3e0' : '#ffebee',
                            color: isActive ? '#2e7d32' : isDisposed ? '#e65100' : '#c62828'
                          }}>
                            {a.status}
                          </span>
                        </td>
                        <td style={{ padding: '12px', textAlign: 'center' }}>
                          <div style={{ display: 'flex', gap: '6px', justifyContent: 'center' }}>
                            {(!can || can('assets.manage')) && (
                              <button
                                onClick={() => handleOpenEditAsset(a)}
                                title="Edit Asset Details"
                                style={{
                                  padding: '4px 8px',
                                  borderRadius: '4px',
                                  border: '1px solid #1a237e',
                                  background: '#e8eaf6',
                                  color: '#1a237e',
                                  fontSize: '11px',
                                  fontWeight: 600,
                                  cursor: 'pointer'
                                }}
                              >
                                Edit
                              </button>
                            )}

                            {(!can || can('assets.manage')) && (
                              <button
                                onClick={() => handleOpenVerification(a)}
                                title="Record Physical Verification Audit"
                                style={{
                                  padding: '4px 8px',
                                  borderRadius: '4px',
                                  border: '1px solid #4527a0',
                                  background: '#ede7f6',
                                  color: '#4527a0',
                                  fontSize: '11px',
                                  fontWeight: 600,
                                  cursor: 'pointer'
                                }}
                              >
                                Audit
                              </button>
                            )}

                            {isActive && (!can || can('assets.manage')) && (
                              <button
                                onClick={() => handleOpenDispose(a)}
                                title="Dispose / Sell Asset"
                                style={{
                                  padding: '4px 8px',
                                  borderRadius: '4px',
                                  border: '1px solid #d32f2f',
                                  background: '#ffebee',
                                  color: '#d32f2f',
                                  fontSize: '11px',
                                  fontWeight: 600,
                                  cursor: 'pointer'
                                }}
                              >
                                Dispose
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    )
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* TAB 2: Dual Depreciation & AS 22 Schedule */}
      {activeTab === 'depreciation' && (
        <div style={{ background: '#fff', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', overflow: 'hidden' }}>
          <div style={{ padding: '16px', borderBottom: '1px solid #eee', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div>
              <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 600 }}>
                Dual Depreciation & Deferred Tax (AS 22 / Ind AS 12) Register
              </h3>
              <p style={{ margin: '2px 0 0 0', fontSize: '12px', color: '#666' }}>
                Comparing Book Depreciation (Schedule II) vs. Tax Depreciation (Section 32) for FY {selectedFy}
              </p>
            </div>
            <button
              onClick={handleOpenRunDepr}
              style={{
                padding: '6px 14px',
                borderRadius: '4px',
                border: 'none',
                background: '#0d47a1',
                color: '#fff',
                fontSize: '12px',
                fontWeight: 600,
                cursor: 'pointer'
              }}
            >
              ⚡ Re-calculate Dual Depreciation
            </button>
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead>
                <tr style={{ background: '#f8f9fa', color: '#555', borderBottom: '2px solid #eee' }}>
                  <th style={{ padding: '12px' }}>Asset Details</th>
                  <th style={{ padding: '12px' }}>FY</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Sch II Opening</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Sch II Depr</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Sch II Closing</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Sec 32 Opening</th>
                  <th style={{ padding: '12px', textAlign: 'center' }}>180-Day Rule</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Sec 32 Depr</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Sec 32 Closing</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Timing Diff</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Deferred Tax</th>
                </tr>
              </thead>
              <tbody>
                {deprEntries.length === 0 ? (
                  <tr>
                    <td colSpan={11} style={{ padding: '32px', textAlign: 'center', color: '#999' }}>
                      No depreciation entries computed for FY {selectedFy}. Click "⚡ Run Dual Depreciation" to compute statutory schedules.
                    </td>
                  </tr>
                ) : (
                  deprEntries.map(e => {
                    const isDtl = e.deferredTaxImpact >= 0

                    return (
                      <tr key={e.id} style={{ borderBottom: '1px solid #eee' }}>
                        <td style={{ padding: '12px' }}>
                          <div style={{ fontWeight: 700, fontFamily: 'monospace', color: '#1a237e' }}>{e.assetTag}</div>
                          <div style={{ fontWeight: 600 }}>{e.assetName}</div>
                          <div style={{ fontSize: '11px', color: '#666' }}>{formatAssetClass(e.assetClass)}</div>
                        </td>
                        <td style={{ padding: '12px', fontWeight: 600 }}>{e.financialYear}</td>
                        <td style={{ padding: '12px', textAlign: 'right' }}>{formatCurrency(e.openingBookValue)}</td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: '#1565c0' }}>
                          {formatCurrency(e.bookDepreciation)}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: '#2e7d32' }}>
                          {formatCurrency(e.closingBookValue)}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right' }}>{formatCurrency(e.openingTaxWdv)}</td>
                        <td style={{ padding: '12px', textAlign: 'center' }}>
                          {e.isUnder180Days ? (
                            <span style={{
                              padding: '2px 6px',
                              borderRadius: '4px',
                              fontSize: '10px',
                              fontWeight: 700,
                              background: '#fff3e0',
                              color: '#e65100'
                            }}>
                              &lt; 180 Days (50%)
                            </span>
                          ) : (
                            <span style={{
                              padding: '2px 6px',
                              borderRadius: '4px',
                              fontSize: '10px',
                              fontWeight: 700,
                              background: '#e8f5e9',
                              color: '#2e7d32'
                            }}>
                              &ge; 180 Days (100%)
                            </span>
                          )}
                          <div style={{ fontSize: '10px', color: '#777', marginTop: '2px' }}>
                            {e.daysPutToUseInYear} days
                          </div>
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: '#00838f' }}>
                          {formatCurrency(e.taxDepreciation)}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600 }}>
                          {formatCurrency(e.closingTaxWdv)}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 700, color: e.timingDifference >= 0 ? '#e65100' : '#c62828' }}>
                          {formatCurrency(e.timingDifference)}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right' }}>
                          <span style={{
                            padding: '3px 8px',
                            borderRadius: '4px',
                            fontSize: '11px',
                            fontWeight: 700,
                            background: isDtl ? '#ffebee' : '#e8f5e9',
                            color: isDtl ? '#c62828' : '#2e7d32'
                          }}>
                            {formatCurrency(Math.abs(e.deferredTaxImpact))} {isDtl ? 'DTL' : 'DTA'}
                          </span>
                        </td>
                      </tr>
                    )
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* TAB 3: Physical Verification Audits */}
      {activeTab === 'verifications' && (
        <div style={{ background: '#fff', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', overflow: 'hidden' }}>
          <div style={{ padding: '16px', borderBottom: '1px solid #eee', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 600 }}>Physical Verification Audit Trail</h3>
            <span style={{ fontSize: '12px', color: '#666' }}>Showing {verifications.length} verification events</span>
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead>
                <tr style={{ background: '#f8f9fa', color: '#555', borderBottom: '2px solid #eee' }}>
                  <th style={{ padding: '12px' }}>Asset Tag & Name</th>
                  <th style={{ padding: '12px' }}>Verification Date</th>
                  <th style={{ padding: '12px' }}>Verified By</th>
                  <th style={{ padding: '12px' }}>Status</th>
                  <th style={{ padding: '12px' }}>Verified Location</th>
                  <th style={{ padding: '12px' }}>Remarks</th>
                </tr>
              </thead>
              <tbody>
                {verifications.length === 0 ? (
                  <tr>
                    <td colSpan={6} style={{ padding: '32px', textAlign: 'center', color: '#999' }}>
                      No physical verification records yet. Use the "Audit" action on any asset in the master register to log physical inspection.
                    </td>
                  </tr>
                ) : (
                  verifications.map(v => (
                    <tr key={v.id} style={{ borderBottom: '1px solid #eee' }}>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 700, fontFamily: 'monospace', color: '#1a237e' }}>{v.assetTag}</div>
                        <div style={{ fontWeight: 600 }}>{v.assetName}</div>
                      </td>
                      <td style={{ padding: '12px', fontWeight: 600 }}>{v.verificationDate}</td>
                      <td style={{ padding: '12px' }}>{v.verifiedBy}</td>
                      <td style={{ padding: '12px' }}>
                        <span style={{
                          padding: '3px 8px',
                          borderRadius: '12px',
                          fontSize: '11px',
                          fontWeight: 700,
                          background: v.status === 'FoundAndGood' ? '#e8f5e9' : v.status === 'NeedsMaintenance' ? '#fff3e0' : '#ffebee',
                          color: v.status === 'FoundAndGood' ? '#2e7d32' : v.status === 'NeedsMaintenance' ? '#e65100' : '#c62828'
                        }}>
                          {v.status}
                        </span>
                      </td>
                      <td style={{ padding: '12px', color: '#555' }}>{v.locationVerified || '-'}</td>
                      <td style={{ padding: '12px', color: '#444' }}>{v.remarks || '-'}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* MODAL 1: Register / Edit Asset */}
      {showAssetModal && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '8px',
            width: '100%',
            maxWidth: '720px',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            boxShadow: '0 4px 20px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #eee', paddingBottom: '12px' }}>
              <div>
                <h2 style={{ margin: 0, fontSize: '18px', color: '#1a237e' }}>
                  {editingAssetId ? 'Edit Fixed Asset' : 'Register New Fixed Asset'}
                </h2>
                <div style={{ fontSize: '12px', color: '#666' }}>Capital asset master with dual depreciation parameters</div>
              </div>
              <button onClick={() => setShowAssetModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#666' }}>✕</button>
            </div>

            <form onSubmit={handleSaveAsset}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '14px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Client</label>
                  <select
                    value={assetForm.clientId}
                    onChange={e => setAssetForm({ ...assetForm, clientId: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    {clients.map(c => (
                      <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Asset Tag / Identifier</label>
                  <input
                    type="text"
                    value={assetForm.assetTag}
                    onChange={e => setAssetForm({ ...assetForm, assetTag: e.target.value.toUpperCase() })}
                    required
                    placeholder="e.g. FA-2024-001"
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc', fontFamily: 'monospace' }}
                  />
                </div>

                <div style={{ gridColumn: 'span 2' }}>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Asset Name / Description</label>
                  <input
                    type="text"
                    value={assetForm.name}
                    onChange={e => setAssetForm({ ...assetForm, name: e.target.value })}
                    required
                    placeholder="e.g. Dell PowerEdge R750 Rack Server"
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Asset Class</label>
                  <select
                    value={assetForm.assetClass}
                    onChange={e => {
                      const ac = e.target.value as AssetClass
                      // Default useful life and IT block rate based on class
                      let life = 10
                      let rate = 15
                      if (ac === 'Building') { life = 30; rate = 10 }
                      else if (ac === 'ComputersAndSoftware') { life = 3; rate = 40 }
                      else if (ac === 'FurnitureAndFixtures') { life = 10; rate = 10 }
                      else if (ac === 'OfficeEquipment') { life = 5; rate = 15 }
                      else if (ac === 'Vehicles') { life = 8; rate = 15 }
                      else if (ac === 'Intangibles') { life = 5; rate = 25 }

                      setAssetForm({
                        ...assetForm,
                        assetClass: ac,
                        usefulLifeYears: life,
                        incomeTaxBlockRatePercent: rate
                      })
                    }}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    <option value="Building">Building</option>
                    <option value="PlantAndMachinery">Plant & Machinery</option>
                    <option value="ComputersAndSoftware">Computers & Software</option>
                    <option value="FurnitureAndFixtures">Furniture & Fixtures</option>
                    <option value="OfficeEquipment">Office Equipment</option>
                    <option value="Vehicles">Motor Vehicles</option>
                    <option value="Intangibles">Intangibles</option>
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Purchase Date</label>
                  <input
                    type="date"
                    value={assetForm.purchaseDate}
                    onChange={e => setAssetForm({ ...assetForm, purchaseDate: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Capitalization (Put to Use) Date</label>
                  <input
                    type="date"
                    value={assetForm.capitalizationDate}
                    onChange={e => setAssetForm({ ...assetForm, capitalizationDate: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Purchase Cost (₹)</label>
                  <input
                    type="number"
                    step="0.01"
                    value={assetForm.purchaseCost}
                    onChange={e => {
                      const cost = parseFloat(e.target.value) || 0
                      setAssetForm({ ...assetForm, purchaseCost: cost, salvageValue: Math.round(cost * 0.05) })
                    }}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Inward Freight & Transport (₹)</label>
                  <input
                    type="number"
                    step="0.01"
                    value={assetForm.inwardFreight}
                    onChange={e => setAssetForm({ ...assetForm, inwardFreight: parseFloat(e.target.value) || 0 })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Installation & Testing (₹)</label>
                  <input
                    type="number"
                    step="0.01"
                    value={assetForm.installationCost}
                    onChange={e => setAssetForm({ ...assetForm, installationCost: parseFloat(e.target.value) || 0 })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Less: GST Input Tax Credit Availed (₹)</label>
                  <input
                    type="number"
                    step="0.01"
                    value={assetForm.gstItcClaimed}
                    onChange={e => setAssetForm({ ...assetForm, gstItcClaimed: parseFloat(e.target.value) || 0 })}
                    placeholder="Reduces cost if claimed"
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Salvage Value (₹) (Default 5%)</label>
                  <input
                    type="number"
                    step="0.01"
                    value={assetForm.salvageValue || ''}
                    onChange={e => setAssetForm({ ...assetForm, salvageValue: parseFloat(e.target.value) || 0 })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                {/* Companies Act Schedule II Settings */}
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Sch II Useful Life (Years)</label>
                  <input
                    type="number"
                    step="0.5"
                    min="0.5"
                    value={assetForm.usefulLifeYears}
                    onChange={e => setAssetForm({ ...assetForm, usefulLifeYears: parseFloat(e.target.value) || 1 })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Sch II Depr Method</label>
                  <select
                    value={assetForm.companyDepreciationMethod}
                    onChange={e => setAssetForm({ ...assetForm, companyDepreciationMethod: e.target.value as (typeof assetForm)['companyDepreciationMethod'] })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    <option value="SLM">Straight Line Method (SLM)</option>
                    <option value="WDV">Written Down Value (WDV)</option>
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Shift Working</label>
                  <select
                    value={assetForm.shiftType}
                    onChange={e => setAssetForm({ ...assetForm, shiftType: e.target.value as (typeof assetForm)['shiftType'] })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    <option value="SingleShift">Single Shift (1.0x)</option>
                    <option value="DoubleShift">Double Shift (+50% extra depr)</option>
                    <option value="TripleShift">Triple Shift (+100% extra depr)</option>
                  </select>
                </div>

                {/* Income Tax Act Section 32 Settings */}
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Income Tax Block Rate (%)</label>
                  <input
                    type="number"
                    step="1"
                    min="1"
                    max="100"
                    value={assetForm.incomeTaxBlockRatePercent}
                    onChange={e => setAssetForm({ ...assetForm, incomeTaxBlockRatePercent: parseFloat(e.target.value) || 15 })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div style={{ display: 'flex', alignItems: 'center', marginTop: '20px' }}>
                  <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', fontWeight: 600, cursor: 'pointer' }}>
                    <input
                      type="checkbox"
                      checked={assetForm.isEligibleForAdditionalDepreciation}
                      onChange={e => setAssetForm({ ...assetForm, isEligibleForAdditionalDepreciation: e.target.checked })}
                    />
                    Eligible for Additional Depreciation u/s 32(1)(iia) (20%)
                  </label>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Location / Facility</label>
                  <input
                    type="text"
                    value={assetForm.location || ''}
                    onChange={e => setAssetForm({ ...assetForm, location: e.target.value })}
                    placeholder="e.g. Plant 1, Floor 2"
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Custodian / User</label>
                  <input
                    type="text"
                    value={assetForm.custodianName || ''}
                    onChange={e => setAssetForm({ ...assetForm, custodianName: e.target.value })}
                    placeholder="e.g. Employee Name"
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              {/* Computed Gross Block Banner */}
              <div style={{ background: '#f5f5f5', padding: '12px', borderRadius: '6px', marginBottom: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ fontSize: '14px', fontWeight: 600 }}>Calculated Gross Block (Historical Cost):</span>
                <span style={{ fontSize: '18px', fontWeight: 700, color: '#1a237e' }}>
                  {formatCurrency(computedGrossCost)}
                </span>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowAssetModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid #ccc', background: '#fff', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 20px', borderRadius: '4px', border: 'none', background: '#1a237e', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  {loading ? 'Saving...' : editingAssetId ? 'Update Asset' : 'Register Asset'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 2: Run Dual Depreciation */}
      {showDeprModal && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '8px',
            width: '100%',
            maxWidth: '520px',
            padding: '24px',
            boxShadow: '0 4px 20px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #eee', paddingBottom: '12px' }}>
              <div>
                <h2 style={{ margin: 0, fontSize: '18px', color: '#0d47a1' }}>Run Dual Depreciation Engine</h2>
                <div style={{ fontSize: '12px', color: '#666' }}>Compute Companies Act Sch II vs. Sec 32 and Deferred Tax</div>
              </div>
              <button onClick={() => setShowDeprModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#666' }}>✕</button>
            </div>

            <form onSubmit={handleSubmitDepr}>
              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Client</label>
                <select
                  value={deprForm.clientId}
                  onChange={e => setDeprForm({ ...deprForm, clientId: e.target.value })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                >
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                  ))}
                </select>
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Financial Year</label>
                <select
                  value={deprForm.financialYear}
                  onChange={e => setDeprForm({ ...deprForm, financialYear: e.target.value })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                >
                  <option value="2024-25">FY 2024-25 (01-Apr-2024 to 31-Mar-2025)</option>
                  <option value="2023-24">FY 2023-24 (01-Apr-2023 to 31-Mar-2024)</option>
                  <option value="2025-26">FY 2025-26 (01-Apr-2025 to 31-Mar-2026)</option>
                </select>
              </div>

              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Corporate Tax Rate (for AS 22 DTA/DTL)</label>
                <select
                  value={deprForm.corporateTaxRate ?? 0.2517}
                  onChange={e => setDeprForm({ ...deprForm, corporateTaxRate: parseFloat(e.target.value) })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                >
                  <option value={0.2517}>25.17% (Section 115BAA Domestic Corporate)</option>
                  <option value={0.22}>22.00% (Base Corporate Rate)</option>
                  <option value={0.30}>30.00% (Regular Rate / Firms / LLPs)</option>
                  <option value={0.312}>31.20% (Firm / LLP with 4% cess)</option>
                </select>
              </div>

              <div style={{ background: '#e3f2fd', padding: '12px', borderRadius: '6px', marginBottom: '16px', borderLeft: '4px solid #1565c0' }}>
                <div style={{ fontSize: '12px', fontWeight: 700, color: '#0d47a1' }}>
                  Statutory Rule 180-Day Handling
                </div>
                <div style={{ fontSize: '11px', color: '#444', marginTop: '2px' }}>
                  Assets put to use for less than 180 days in the financial year will automatically be restricted to 50% of the normal Income Tax block rate under Section 32(1).
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowDeprModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid #ccc', background: '#fff', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 20px', borderRadius: '4px', border: 'none', background: '#0d47a1', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  {loading ? 'Processing...' : 'Run Dual Depreciation'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 3: Physical Verification */}
      {showVerifModal && selectedAsset && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '8px',
            width: '100%',
            maxWidth: '480px',
            padding: '24px',
            boxShadow: '0 4px 20px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #eee', paddingBottom: '12px' }}>
              <div>
                <h2 style={{ margin: 0, fontSize: '18px', color: '#4527a0' }}>Record Physical Verification</h2>
                <div style={{ fontSize: '12px', color: '#666' }}>{selectedAsset.assetTag} - {selectedAsset.name}</div>
              </div>
              <button onClick={() => setShowVerifModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#666' }}>✕</button>
            </div>

            <form onSubmit={handleSubmitVerification}>
              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Verification Date</label>
                <input
                  type="date"
                  value={verifForm.verificationDate}
                  onChange={e => setVerifForm({ ...verifForm, verificationDate: e.target.value })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Verified By (Auditor / Staff)</label>
                <input
                  type="text"
                  value={verifForm.verifiedBy}
                  onChange={e => setVerifForm({ ...verifForm, verifiedBy: e.target.value })}
                  required
                  placeholder="e.g. CA Sandeep Kumar"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Condition Status</label>
                <select
                  value={verifForm.status}
                  onChange={e => setVerifForm({ ...verifForm, status: e.target.value as (typeof verifForm)['status'] })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                >
                  <option value="FoundAndGood">Found & Good Condition</option>
                  <option value="NeedsMaintenance">Needs Maintenance / Calibration</option>
                  <option value="Damaged">Damaged / Non-operational</option>
                  <option value="Missing">Missing / Untraceable (Auto Write-off)</option>
                </select>
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Location Verified</label>
                <input
                  type="text"
                  value={verifForm.locationVerified || ''}
                  onChange={e => setVerifForm({ ...verifForm, locationVerified: e.target.value })}
                  placeholder="e.g. Unit 3 Floor 1"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Audit Remarks</label>
                <input
                  type="text"
                  value={verifForm.remarks || ''}
                  onChange={e => setVerifForm({ ...verifForm, remarks: e.target.value })}
                  placeholder="e.g. Barcode tag scanned, verified with serial number"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowVerifModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid #ccc', background: '#fff', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 20px', borderRadius: '4px', border: 'none', background: '#4527a0', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  {loading ? 'Saving...' : 'Record Verification'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 4: Dispose Asset */}
      {showDisposeModal && selectedAsset && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '8px',
            width: '100%',
            maxWidth: '480px',
            padding: '24px',
            boxShadow: '0 4px 20px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #eee', paddingBottom: '12px' }}>
              <div>
                <h2 style={{ margin: 0, fontSize: '18px', color: '#c62828' }}>Dispose / Sell Fixed Asset</h2>
                <div style={{ fontSize: '12px', color: '#666' }}>{selectedAsset.assetTag} - {selectedAsset.name}</div>
              </div>
              <button onClick={() => setShowDisposeModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#666' }}>✕</button>
            </div>

            <form onSubmit={handleSubmitDispose}>
              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Disposal Date</label>
                <input
                  type="date"
                  value={disposeForm.disposalDate}
                  onChange={e => setDisposeForm({ ...disposeForm, disposalDate: e.target.value })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Disposal Proceeds / Sale Value (₹)</label>
                <input
                  type="number"
                  step="0.01"
                  value={disposeForm.disposalProceeds}
                  onChange={e => setDisposeForm({ ...disposeForm, disposalProceeds: parseFloat(e.target.value) || 0 })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Remarks / Reason for Disposal</label>
                <input
                  type="text"
                  value={disposeForm.remarks || ''}
                  onChange={e => setDisposeForm({ ...disposeForm, remarks: e.target.value })}
                  placeholder="e.g. Scrapped due to obsolescence, sold to vendor"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              {/* Profit / Loss on Sale Preview */}
              <div style={{ background: '#f5f5f5', padding: '12px', borderRadius: '6px', marginBottom: '16px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px' }}>
                  <span>Current Net Book Value:</span>
                  <strong>{formatCurrency(selectedAsset.latestBookValue)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', marginTop: '4px' }}>
                  <span>Estimated Profit / (Loss) on Disposal:</span>
                  <strong style={{
                    color: Number(disposeForm.disposalProceeds) >= selectedAsset.latestBookValue ? '#2e7d32' : '#c62828'
                  }}>
                    {formatCurrency(Number(disposeForm.disposalProceeds) - selectedAsset.latestBookValue)}
                  </strong>
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowDisposeModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid #ccc', background: '#fff', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 20px', borderRadius: '4px', border: 'none', background: '#c62828', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  {loading ? 'Disposing...' : 'Confirm Disposal'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
