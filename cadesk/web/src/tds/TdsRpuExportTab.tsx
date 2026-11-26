import React, { useEffect, useRef, useState } from 'react'
import {
  tdsApi,
  type Client,
  type ParsedFvuErrEntry,
  type SaveTdsDeductorProfileRequest,
  type TdsReturnExportResult,
} from '../api'
import { download, errorText, saveGeneratedDocument } from '../gst/vault'
import {
  DEDUCTOR_TYPES,
  TDS_STATE_CODES,
  isValidFvuFileName,
  isValidPan,
  isValidTan,
  officialFormBadge,
  parseFvuErrClient,
  suggestedRpuFileName,
} from './tdsHelpers'

type Props = {
  clients: Client[]
  selectedClientId: string
  onSelectClient: (clientId: string) => void
  financialYear: string
  quarter: string
  onNotify: (msg: { error?: string; success?: string }) => void
}

const emptyProfile = (): SaveTdsDeductorProfileRequest => ({
  tan: '',
  pan: '',
  deductorType: 'K',
  deductorName: '',
  branchDivision: 'NA',
  gstin: '',
  flatBlock: '',
  premisesName: '',
  streetRoad: '',
  areaLocality: '',
  cityDistrict: '',
  stateCode: '16',
  pincode: '',
  countryName: 'INDIA',
  email: '',
  stdCode: '',
  phone: '',
  mobile: '',
  countryCallingCode: '91',
  respName: '',
  respDesignation: 'Director / Partner',
  respPan: '',
  respFlatBlock: '',
  respPremisesName: '',
  respStreetRoad: '',
  respAreaLocality: '',
  respCityDistrict: '',
  respStateCode: '16',
  respPincode: '',
  respCountryName: 'INDIA',
  respEmail: '',
  respMobile: '',
  respCountryCallingCode: '91',
  filedPreviousRegular: false,
  previousTokenNo: '',
})

export const TdsRpuExportTab: React.FC<Props> = ({
  clients,
  selectedClientId,
  onSelectClient,
  financialYear,
  quarter,
  onNotify,
}) => {
  const activeClientId = selectedClientId || clients[0]?.id || ''

  const [formType, setFormType] = useState<'24Q' | '26Q' | '27Q'>('26Q')
  const [fileDate, setFileDate] = useState<string>(() => new Date().toISOString().split('T')[0])
  const [profile, setProfile] = useState<SaveTdsDeductorProfileRequest>(emptyProfile)
  const [loadingProfile, setLoadingProfile] = useState(false)
  const [savingProfile, setSavingProfile] = useState(false)
  const [generating, setGenerating] = useState(false)
  const [savingVault, setSavingVault] = useState(false)
  const [exportResult, setExportResult] = useState<TdsReturnExportResult | null>(null)
  const [errContent, setErrContent] = useState('')
  const [errEntries, setErrEntries] = useState<ParsedFvuErrEntry[]>([])
  const [parsingErr, setParsingErr] = useState(false)
  const prevClientIdRef = useRef<string>('')

  useEffect(() => {
    if (!activeClientId) return
    let cancelled = false
    if (prevClientIdRef.current !== activeClientId) {
      prevClientIdRef.current = activeClientId
      setProfile(emptyProfile())
    }
    setLoadingProfile(true)
    setExportResult(null)
    tdsApi
      .getProfile(activeClientId)
      .then((dto) => {
        if (cancelled) return
        setProfile((prev) => {
          const base = emptyProfile()
          const pick = <K extends keyof SaveTdsDeductorProfileRequest>(
            key: K,
            serverVal: SaveTdsDeductorProfileRequest[K],
          ): SaveTdsDeductorProfileRequest[K] => (prev[key] !== base[key] ? prev[key] : serverVal)

          return {
            tan: pick('tan', dto.tan || ''),
            pan: pick('pan', dto.pan || ''),
            deductorType: pick('deductorType', dto.deductorType || 'K'),
            deductorName: pick('deductorName', dto.deductorName || ''),
            branchDivision: pick('branchDivision', dto.branchDivision || 'NA'),
            gstin: pick('gstin', dto.gstin || ''),
            flatBlock: pick('flatBlock', dto.flatBlock || ''),
            premisesName: pick('premisesName', dto.premisesName || ''),
            streetRoad: pick('streetRoad', dto.streetRoad || ''),
            areaLocality: pick('areaLocality', dto.areaLocality || ''),
            cityDistrict: pick('cityDistrict', dto.cityDistrict || ''),
            stateCode: pick('stateCode', dto.stateCode || '16'),
            pincode: pick('pincode', dto.pincode || ''),
            countryName: pick('countryName', dto.countryName || 'INDIA'),
            email: pick('email', dto.email || ''),
            stdCode: pick('stdCode', dto.stdCode || ''),
            phone: pick('phone', dto.phone || ''),
            mobile: pick('mobile', dto.mobile || ''),
            countryCallingCode: pick('countryCallingCode', dto.countryCallingCode || '91'),
            respName: pick('respName', dto.respName || ''),
            respDesignation: pick('respDesignation', dto.respDesignation || ''),
            respPan: pick('respPan', dto.respPan || ''),
            respFlatBlock: pick('respFlatBlock', dto.respFlatBlock || ''),
            respPremisesName: pick('respPremisesName', dto.respPremisesName || ''),
            respStreetRoad: pick('respStreetRoad', dto.respStreetRoad || ''),
            respAreaLocality: pick('respAreaLocality', dto.respAreaLocality || ''),
            respCityDistrict: pick('respCityDistrict', dto.respCityDistrict || ''),
            respStateCode: pick('respStateCode', dto.respStateCode || '16'),
            respPincode: pick('respPincode', dto.respPincode || ''),
            respCountryName: pick('respCountryName', dto.respCountryName || 'INDIA'),
            respEmail: pick('respEmail', dto.respEmail || ''),
            respMobile: pick('respMobile', dto.respMobile || ''),
            respCountryCallingCode: pick('respCountryCallingCode', dto.respCountryCallingCode || '91'),
            filedPreviousRegular: pick('filedPreviousRegular', dto.filedPreviousRegular),
            previousTokenNo: pick('previousTokenNo', dto.previousTokenNo || ''),
          }
        })
      })
      .catch((err: unknown) => {
        if (!cancelled) onNotify({ error: errorText(err) })
      })
      .finally(() => {
        if (!cancelled) setLoadingProfile(false)
      })
    return () => {
      cancelled = true
    }
  }, [activeClientId, onNotify])

  const badge = officialFormBadge(formType, financialYear)
  const expectedFile = suggestedRpuFileName(formType, quarter, financialYear)

  const handleSaveProfile = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!activeClientId) return
    try {
      setSavingProfile(true)
      onNotify({})
      const saved = await tdsApi.saveProfile(activeClientId, profile)
      setProfile((prev) => ({
        ...prev,
        tan: saved.tan,
        pan: saved.pan,
        deductorName: saved.deductorName,
      }))
      onNotify({ success: `Deductor & Responsible Person profile saved for ${saved.deductorName || saved.tan}.` })
    } catch (err: unknown) {
      onNotify({ error: errorText(err) })
    } finally {
      setSavingProfile(false)
    }
  }

  const copyDeductorAddrToResp = () => {
    setProfile((prev) => ({
      ...prev,
      respFlatBlock: prev.flatBlock,
      respPremisesName: prev.premisesName,
      respStreetRoad: prev.streetRoad,
      respAreaLocality: prev.areaLocality,
      respCityDistrict: prev.cityDistrict,
      respStateCode: prev.stateCode,
      respPincode: prev.pincode,
      respCountryName: prev.countryName,
      respEmail: prev.respEmail || prev.email || '',
      respMobile: prev.respMobile || prev.mobile || '',
    }))
  }

  const handleGenerate = async () => {
    if (!activeClientId) {
      onNotify({ error: 'Select a client before generating the RPU return file.' })
      return
    }
    try {
      setGenerating(true)
      onNotify({})
      const res = await tdsApi.exportReturn({
        clientId: activeClientId,
        formType,
        financialYear,
        quarter,
        fileCreationDate: fileDate || undefined,
      })
      setExportResult(res)
      if (res.validForFvu) {
        onNotify({ success: `Generated ${res.fileName} (Form ${res.officialFormNumber}) with 0 FVU blocking errors.` })
      } else {
        onNotify({ error: `${res.fileName} generated with FVU validation issue(s). Review the pre-check list before filing.` })
      }
    } catch (err: unknown) {
      onNotify({ error: errorText(err) })
    } finally {
      setGenerating(false)
    }
  }

  const handleSaveToVault = async () => {
    if (!exportResult || !activeClientId) return
    try {
      setSavingVault(true)
      onNotify({})
      const doc = await saveGeneratedDocument(activeClientId, {
        fileName: exportResult.fileName,
        content: exportResult.content,
        typeCode: 'TDS_RETURN',
        fy: exportResult.financialYear,
        name: `Form ${exportResult.officialFormNumber} ${exportResult.quarter} FY ${exportResult.financialYear} (${exportResult.fileName})`,
        notes: `${exportResult.actLabel} • ${exportResult.challanCount} challans • ${exportResult.deducteeCount} deductees`,
      })
      onNotify({ success: `Saved ${doc.name} to client document vault.` })
    } catch (err: unknown) {
      onNotify({ error: errorText(err) })
    } finally {
      setSavingVault(false)
    }
  }

  const handleParseErr = async () => {
    if (!errContent.trim()) return
    try {
      setParsingErr(true)
      const parsed = await tdsApi.parseErr(errContent)
      setErrEntries(parsed)
    } catch {
      setErrEntries(parseFvuErrClient(errContent))
    } finally {
      setParsingErr(false)
    }
  }

  const handleErrFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    const text = await file.text()
    setErrContent(text)
    try {
      const parsed = await tdsApi.parseErr(text)
      setErrEntries(parsed)
    } catch {
      setErrEntries(parseFvuErrClient(text))
    }
  }

  const inputStyle: React.CSSProperties = {
    width: '100%',
    padding: '7px 10px',
    borderRadius: '6px',
    border: '1px solid var(--border-subtle)',
    fontSize: '13px',
    boxSizing: 'border-box',
  }

  const labelStyle: React.CSSProperties = {
    fontSize: '11px',
    fontWeight: 600,
    color: 'var(--text-secondary)',
    display: 'block',
    marginBottom: '4px',
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      {/* Return Scope & Generator Bar */}
      <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '20px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '16px' }}>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
              <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 700, color: 'var(--text)' }}>
                Protean / NSDL e-TDS RPU `.txt` Generator
              </h3>
              <span style={{ padding: '2px 10px', borderRadius: '999px', backgroundColor: 'var(--accent-subtle)', color: 'var(--accent)', fontSize: '11px', fontWeight: 700 }}>
                Form {badge.formNo} ({quarter} • {financialYear})
              </span>
              <span style={{ padding: '2px 10px', borderRadius: '999px', backgroundColor: 'var(--card-subtle)', color: 'var(--text-secondary)', fontSize: '11px', fontWeight: 600 }}>
                {badge.actLabel}
              </span>
            </div>
            <p style={{ margin: '6px 0 0', fontSize: '13px', color: 'var(--muted)' }}>
              Generates ASCII CRLF caret-delimited `FH^BH^CD^DD` regular statement `{expectedFile}` (Protean FVU compliant filename:{' '}
              <strong>{isValidFvuFileName(expectedFile) ? 'Valid' : 'Invalid'}</strong>).
            </p>
          </div>

          <div style={{ display: 'flex', alignItems: 'flex-end', gap: '12px', flexWrap: 'wrap' }}>
            <div>
              <label style={labelStyle}>Client (Deductor)</label>
              <select
                value={activeClientId}
                onChange={(e) => onSelectClient(e.target.value)}
                style={{ padding: '7px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '13px' }}
              >
                {clients.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </div>
            <div>
              <label style={labelStyle}>Statement Form</label>
              <select
                value={formType}
                onChange={(e) => setFormType(e.target.value as '24Q' | '26Q' | '27Q')}
                style={{ padding: '7px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '13px' }}
              >
                <option value="24Q">24Q / 138 (Salary)</option>
                <option value="26Q">26Q / 140 (Resident Non-Salary)</option>
                <option value="27Q">27Q / 144 (Non-Resident)</option>
              </select>
            </div>
            <div>
              <label style={labelStyle}>Creation Date</label>
              <input
                type="date"
                value={fileDate}
                onChange={(e) => setFileDate(e.target.value)}
                style={{ padding: '6px 10px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '13px' }}
              />
            </div>
            <button
              type="button"
              disabled={generating || !activeClientId}
              onClick={handleGenerate}
              style={{
                padding: '9px 18px',
                backgroundColor: '#2563eb',
                color: '#fff',
                border: 'none',
                borderRadius: '6px',
                fontWeight: 600,
                fontSize: '13px',
                cursor: 'pointer',
              }}
            >
              {generating ? 'Validating & Building...' : 'Validate & Generate RPU .txt'}
            </button>
          </div>
        </div>
      </div>

      {/* Export Result, FVU Checklist & Preview */}
      {exportResult && (
        <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '20px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '12px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
              <span
                style={{
                  padding: '4px 12px',
                  borderRadius: '999px',
                  fontSize: '12px',
                  fontWeight: 700,
                  backgroundColor: exportResult.validForFvu ? 'var(--success-bg)' : 'var(--error-bg)',
                  color: exportResult.validForFvu ? 'var(--success)' : 'var(--error)',
                }}
              >
                {exportResult.validForFvu ? 'FVU Pre-Check Passed' : 'FVU Issues Found'}
              </span>
              <span style={{ fontSize: '13px', color: 'var(--text-secondary)', fontWeight: 600 }}>
                File: {exportResult.fileName} • Form {exportResult.officialFormNumber} • AY {exportResult.assessmentYear}
              </span>
            </div>
            <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
              <button
                type="button"
                onClick={() => download(exportResult.fileName, exportResult.content, 'text/plain;charset=us-ascii')}
                style={{ padding: '8px 14px', borderRadius: '6px', border: '1px solid var(--accent)', background: 'var(--accent-subtle)', color: 'var(--accent)', fontWeight: 600, fontSize: '13px', cursor: 'pointer' }}
              >
                Download {exportResult.fileName}
              </button>
              <button
                type="button"
                disabled={savingVault}
                onClick={handleSaveToVault}
                style={{ padding: '8px 14px', borderRadius: '6px', border: 'none', background: '#059669', color: '#fff', fontWeight: 600, fontSize: '13px', cursor: 'pointer' }}
              >
                {savingVault ? 'Saving...' : 'Save .txt to Client Vault'}
              </button>
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: '12px' }}>
            <div style={{ padding: '12px', backgroundColor: 'var(--card-subtle)', borderRadius: '6px', border: '1px solid var(--border)' }}>
              <div style={{ fontSize: '11px', color: 'var(--muted)', fontWeight: 600 }}>RECORDS</div>
              <div style={{ fontSize: '18px', fontWeight: 700, color: 'var(--text)' }}>{exportResult.lineCount} lines</div>
              <div style={{ fontSize: '11px', color: 'var(--muted)' }}>{exportResult.challanCount} CD • {exportResult.deducteeCount} DD</div>
            </div>
            <div style={{ padding: '12px', backgroundColor: 'var(--card-subtle)', borderRadius: '6px', border: '1px solid var(--border)' }}>
              <div style={{ fontSize: '11px', color: 'var(--muted)', fontWeight: 600 }}>AMOUNT PAID</div>
              <div style={{ fontSize: '18px', fontWeight: 700, color: 'var(--text)' }}>₹{exportResult.totalPaid.toLocaleString('en-IN')}</div>
            </div>
            <div style={{ padding: '12px', backgroundColor: 'var(--card-subtle)', borderRadius: '6px', border: '1px solid var(--border)' }}>
              <div style={{ fontSize: '11px', color: 'var(--muted)', fontWeight: 600 }}>TAX DEDUCTED</div>
              <div style={{ fontSize: '18px', fontWeight: 700, color: 'var(--error)' }}>₹{exportResult.totalDeducted.toLocaleString('en-IN')}</div>
            </div>
            <div style={{ padding: '12px', backgroundColor: 'var(--card-subtle)', borderRadius: '6px', border: '1px solid var(--border)' }}>
              <div style={{ fontSize: '11px', color: 'var(--muted)', fontWeight: 600 }}>CHALLAN DEPOSITED</div>
              <div style={{ fontSize: '18px', fontWeight: 700, color: 'var(--success)' }}>₹{exportResult.totalDeposited.toLocaleString('en-IN')}</div>
            </div>
          </div>

          {exportResult.issues.length > 0 && (
            <div style={{ border: '1px solid var(--border)', borderRadius: '6px', overflow: 'hidden' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px', textAlign: 'left' }}>
                <thead style={{ backgroundColor: 'var(--card-subtle)', borderBottom: '1px solid var(--border)' }}>
                  <tr>
                    <th style={{ padding: '8px 12px' }}>Severity</th>
                    <th style={{ padding: '8px 12px' }}>Record</th>
                    <th style={{ padding: '8px 12px' }}>Line</th>
                    <th style={{ padding: '8px 12px' }}>Rule</th>
                    <th style={{ padding: '8px 12px' }}>Message</th>
                  </tr>
                </thead>
                <tbody>
                  {exportResult.issues.map((issue, idx) => (
                    <tr key={`${issue.code}-${idx}`} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '8px 12px' }}>
                        <span
                          style={{
                            padding: '2px 8px',
                            borderRadius: '10px',
                            fontWeight: 700,
                            fontSize: '11px',
                            backgroundColor: issue.severity === 'error' ? 'var(--error-bg)' : 'var(--warning-bg)',
                            color: issue.severity === 'error' ? 'var(--error)' : 'var(--warning)',
                          }}
                        >
                          {issue.severity.toUpperCase()}
                        </span>
                      </td>
                      <td style={{ padding: '8px 12px', fontWeight: 600 }}>{issue.recordType}</td>
                      <td style={{ padding: '8px 12px' }}>{issue.lineNumber ?? '-'}</td>
                      <td style={{ padding: '8px 12px', fontFamily: 'monospace' }}>{issue.code || '-'}</td>
                      <td style={{ padding: '8px 12px', color: 'var(--text-secondary)' }}>{issue.message}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div>
            <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '6px' }}>
              Raw Protean Caret-Delimited Output (`^` • ASCII CRLF)
            </div>
            <pre
              style={{
                margin: 0,
                padding: '12px',
                backgroundColor: '#0f172a',
                color: '#e2e8f0',
                borderRadius: '6px',
                fontSize: '11px',
                lineHeight: 1.5,
                maxHeight: '260px',
                overflow: 'auto',
                whiteSpace: 'pre',
              }}
            >
              {exportResult.content}
            </pre>
          </div>
        </div>
      )}

      {/* Deductor & Responsible Person Profile (FH/BH) */}
      <form
        onSubmit={handleSaveProfile}
        style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '20px', display: 'flex', flexDirection: 'column', gap: '16px' }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '10px' }}>
          <div>
            <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 700, color: 'var(--text)' }}>
              Deductor & Responsible Person Profile (FH / BH Header Metadata)
            </h3>
            <p style={{ margin: '4px 0 0', fontSize: '12px', color: 'var(--muted)' }}>
              {loadingProfile ? 'Loading client metadata...' : 'Required once per client before generating FVU-ready .txt files.'}
            </p>
          </div>
          <div style={{ display: 'flex', gap: '8px' }}>
            <button
              type="button"
              onClick={copyDeductorAddrToResp}
              style={{ padding: '7px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)', background: 'var(--card)', color: 'var(--text)', fontSize: '12px', cursor: 'pointer' }}
            >
              Copy Deductor Address to Responsible Person
            </button>
            <button
              type="submit"
              disabled={savingProfile || !activeClientId}
              style={{ padding: '7px 16px', borderRadius: '6px', border: 'none', background: '#0f766e', color: '#fff', fontWeight: 600, fontSize: '13px', cursor: 'pointer' }}
            >
              {savingProfile ? 'Saving...' : 'Save Deductor Profile'}
            </button>
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(190px, 1fr))', gap: '12px' }}>
          <div>
            <label style={labelStyle}>
              TAN (AAAA99999A) {profile.tan && !isValidTan(profile.tan) && <span style={{ color: 'var(--error)' }}>• Invalid</span>}
            </label>
            <input
              type="text"
              required
              maxLength={10}
              value={profile.tan}
              onChange={(e) => setProfile((p) => ({ ...p, tan: e.target.value.toUpperCase() }))}
              placeholder="e.g. COCH01234A"
              style={inputStyle}
            />
          </div>
          <div>
            <label style={labelStyle}>
              Deductor PAN {profile.pan && profile.pan !== 'PANNOTREQD' && !isValidPan(profile.pan) && <span style={{ color: 'var(--error)' }}>• Invalid</span>}
            </label>
            <input
              type="text"
              required
              maxLength={10}
              value={profile.pan}
              onChange={(e) => setProfile((p) => ({ ...p, pan: e.target.value.toUpperCase() }))}
              placeholder="e.g. AABCC1234D"
              style={inputStyle}
            />
          </div>
          <div>
            <label style={labelStyle}>Deductor Category</label>
            <select
              value={profile.deductorType}
              onChange={(e) => setProfile((p) => ({ ...p, deductorType: e.target.value }))}
              style={inputStyle}
            >
              {!DEDUCTOR_TYPES.some((d) => d.code === profile.deductorType) && (
                <option value={profile.deductorType}>{profile.deductorType}</option>
              )}
              {DEDUCTOR_TYPES.map((d) => (
                <option key={d.code} value={d.code}>{d.name}</option>
              ))}
            </select>
          </div>
          <div>
            <label style={labelStyle}>Deductor Name (Max 75 chars)</label>
            <input
              type="text"
              required
              maxLength={75}
              value={profile.deductorName}
              onChange={(e) => setProfile((p) => ({ ...p, deductorName: e.target.value }))}
              style={inputStyle}
            />
          </div>
          <div>
            <label style={labelStyle}>Flat / Door / Block (Line 1)</label>
            <input
              type="text"
              required
              maxLength={25}
              value={profile.flatBlock}
              onChange={(e) => setProfile((p) => ({ ...p, flatBlock: e.target.value }))}
              style={inputStyle}
            />
          </div>
          <div>
            <label style={labelStyle}>City / District</label>
            <input
              type="text"
              maxLength={25}
              value={profile.cityDistrict || ''}
              onChange={(e) => setProfile((p) => ({ ...p, cityDistrict: e.target.value }))}
              style={inputStyle}
            />
          </div>
          <div>
            <label style={labelStyle}>State Code (Protean 01–37)</label>
            <select
              value={profile.stateCode}
              onChange={(e) => setProfile((p) => ({ ...p, stateCode: e.target.value }))}
              style={inputStyle}
            >
              {TDS_STATE_CODES.map((s) => (
                <option key={s.code} value={s.code}>{s.name}</option>
              ))}
            </select>
          </div>
          <div>
            <label style={labelStyle}>PIN Code (6 Digits)</label>
            <input
              type="text"
              required
              maxLength={6}
              value={profile.pincode}
              onChange={(e) => setProfile((p) => ({ ...p, pincode: e.target.value }))}
              style={inputStyle}
            />
          </div>
          <div>
            <label style={labelStyle}>Deductor Email</label>
            <input
              type="email"
              required
              value={profile.email}
              onChange={(e) => setProfile((p) => ({ ...p, email: e.target.value }))}
              style={inputStyle}
            />
          </div>
          <div>
            <label style={labelStyle}>Deductor Phone / Mobile</label>
            <input
              type="text"
              required
              maxLength={10}
              value={profile.phone || profile.mobile || ''}
              onChange={(e) => setProfile((p) => ({ ...p, phone: e.target.value, mobile: e.target.value }))}
              style={inputStyle}
            />
          </div>
        </div>

        <div style={{ borderTop: '1px solid var(--border)', paddingTop: '12px' }}>
          <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-secondary)', marginBottom: '10px' }}>
            Responsible Person Details (Mandatory for BH Record)
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(190px, 1fr))', gap: '12px' }}>
            <div>
              <label style={labelStyle}>Responsible Person Name</label>
              <input
                type="text"
                required
                maxLength={75}
                value={profile.respName}
                onChange={(e) => setProfile((p) => ({ ...p, respName: e.target.value }))}
                style={inputStyle}
              />
            </div>
            <div>
              <label style={labelStyle}>Designation</label>
              <input
                type="text"
                required
                maxLength={20}
                value={profile.respDesignation}
                onChange={(e) => setProfile((p) => ({ ...p, respDesignation: e.target.value }))}
                style={inputStyle}
              />
            </div>
            <div>
              <label style={labelStyle}>Responsible Person PAN</label>
              <input
                type="text"
                required
                maxLength={10}
                value={profile.respPan}
                onChange={(e) => setProfile((p) => ({ ...p, respPan: e.target.value.toUpperCase() }))}
                style={inputStyle}
              />
            </div>
            <div>
              <label style={labelStyle}>Responsible Flat / Block</label>
              <input
                type="text"
                required
                maxLength={25}
                value={profile.respFlatBlock}
                onChange={(e) => setProfile((p) => ({ ...p, respFlatBlock: e.target.value }))}
                style={inputStyle}
              />
            </div>
            <div>
              <label style={labelStyle}>Responsible State</label>
              <select
                value={profile.respStateCode}
                onChange={(e) => setProfile((p) => ({ ...p, respStateCode: e.target.value }))}
                style={inputStyle}
              >
                {TDS_STATE_CODES.map((s) => (
                  <option key={s.code} value={s.code}>{s.name}</option>
                ))}
              </select>
            </div>
            <div>
              <label style={labelStyle}>Responsible PIN</label>
              <input
                type="text"
                required
                maxLength={6}
                value={profile.respPincode}
                onChange={(e) => setProfile((p) => ({ ...p, respPincode: e.target.value }))}
                style={inputStyle}
              />
            </div>
            <div>
              <label style={labelStyle}>Responsible Email</label>
              <input
                type="email"
                required
                value={profile.respEmail}
                onChange={(e) => setProfile((p) => ({ ...p, respEmail: e.target.value }))}
                style={inputStyle}
              />
            </div>
            <div>
              <label style={labelStyle}>Responsible Mobile (10 digits)</label>
              <input
                type="text"
                required
                maxLength={10}
                value={profile.respMobile}
                onChange={(e) => setProfile((p) => ({ ...p, respMobile: e.target.value }))}
                style={inputStyle}
              />
            </div>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '16px', marginTop: '12px', flexWrap: 'wrap' }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', color: 'var(--text-secondary)', cursor: 'pointer' }}>
              <input
                type="checkbox"
                checked={profile.filedPreviousRegular || false}
                onChange={(e) => setProfile((p) => ({ ...p, filedPreviousRegular: e.target.checked }))}
              />
              <span>Filed Regular Statement for Earlier Period (Requires 15-digit RRR Token No.)</span>
            </label>
            {profile.filedPreviousRegular && (
              <input
                type="text"
                maxLength={15}
                value={profile.previousTokenNo || ''}
                onChange={(e) => setProfile((p) => ({ ...p, previousTokenNo: e.target.value }))}
                placeholder="15-digit Previous RRR Token"
                style={{ ...inputStyle, width: '220px' }}
              />
            )}
          </div>
        </div>
      </form>

      {/* Protean FVU .err Parser */}
      <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '20px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '10px', marginBottom: '10px' }}>
          <div>
            <h3 style={{ margin: 0, fontSize: '15px', fontWeight: 700, color: 'var(--text)' }}>Protean FVU `.err` Report Parser</h3>
            <p style={{ margin: '2px 0 0', fontSize: '12px', color: 'var(--muted)' }}>
              Upload or paste the caret-delimited `.err` file from Protean FVU 9.5 / 1.2 to inspect line-by-line errors.
            </p>
          </div>
          <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
            <input type="file" accept=".err,.txt" onChange={handleErrFile} style={{ fontSize: '12px' }} />
            <button
              type="button"
              disabled={parsingErr || !errContent.trim()}
              onClick={handleParseErr}
              style={{ padding: '7px 14px', borderRadius: '6px', border: '1px solid var(--border-subtle)', background: 'var(--card-subtle)', color: 'var(--text)', fontWeight: 600, fontSize: '12px', cursor: 'pointer' }}
            >
              {parsingErr ? 'Parsing...' : 'Parse .err Text'}
            </button>
          </div>
        </div>
        <textarea
          rows={3}
          value={errContent}
          onChange={(e) => setErrContent(e.target.value)}
          placeholder="Paste lines from Protean .err output (e.g. 4^DD^^1^2^^T-FV-3141^Invalid PAN of deductee)"
          style={{ ...inputStyle, fontFamily: 'monospace', fontSize: '12px' }}
        />
        {errEntries.length > 0 && (
          <div style={{ marginTop: '12px', border: '1px solid var(--border)', borderRadius: '6px', overflow: 'hidden' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px', textAlign: 'left' }}>
              <thead style={{ backgroundColor: 'var(--error-bg)', borderBottom: '1px solid var(--error-border)' }}>
                <tr>
                  <th style={{ padding: '8px 12px' }}>Line</th>
                  <th style={{ padding: '8px 12px' }}>Record</th>
                  <th style={{ padding: '8px 12px' }}>Challan #</th>
                  <th style={{ padding: '8px 12px' }}>Deductee #</th>
                  <th style={{ padding: '8px 12px' }}>Code</th>
                  <th style={{ padding: '8px 12px' }}>Description</th>
                </tr>
              </thead>
              <tbody>
                {errEntries.map((entry, idx) => (
                  <tr key={`${entry.errorCode}-${idx}`} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td style={{ padding: '8px 12px' }}>{entry.lineNumber ?? '-'}</td>
                    <td style={{ padding: '8px 12px', fontWeight: 600 }}>{entry.recordType || '-'}</td>
                    <td style={{ padding: '8px 12px' }}>{entry.challanNo ?? '-'}</td>
                    <td style={{ padding: '8px 12px' }}>{entry.deducteeNo ?? '-'}</td>
                    <td style={{ padding: '8px 12px', fontFamily: 'monospace', color: 'var(--error)' }}>{entry.errorCode}</td>
                    <td style={{ padding: '8px 12px' }}>{entry.description}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}
