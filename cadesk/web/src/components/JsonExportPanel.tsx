import React, { useState } from 'react'
import type { IncomeTaxJsonExportResult } from '../api'
import { download, errorText, saveGeneratedDocument } from '../gst/vault'

type Props = {
  title: string
  subtitle?: string
  clientId: string
  fy: string
  vaultTypeCode: string
  generating: boolean
  result: IncomeTaxJsonExportResult | null
  onGenerate: () => void
  children?: React.ReactNode
}

export const JsonExportPanel: React.FC<Props> = ({
  title,
  subtitle,
  clientId,
  fy,
  vaultTypeCode,
  generating,
  result,
  onGenerate,
  children,
}) => {
  const [savingVault, setSavingVault] = useState(false)
  const [vaultMsg, setVaultMsg] = useState<string | null>(null)
  const [vaultErr, setVaultErr] = useState<string | null>(null)

  const handleSaveVault = async () => {
    if (!result || !clientId) return
    try {
      setSavingVault(true)
      setVaultErr(null)
      setVaultMsg(null)
      const doc = await saveGeneratedDocument(clientId, {
        fileName: result.fileName,
        content: result.jsonContent,
        typeCode: vaultTypeCode,
        fy,
        name: `${result.formType} AY ${result.assessmentYear} (${result.fileName})`,
        notes: `${result.schemaVersion} • ${result.schemaErrorCount} schema error(s)`,
      })
      setVaultMsg(`Saved "${doc.name}" to client document vault.`)
    } catch (err: unknown) {
      setVaultErr(errorText(err))
    } finally {
      setSavingVault(false)
    }
  }

  return (
    <div style={{ border: '1px solid var(--border-subtle)', borderRadius: '8px', backgroundColor: 'var(--card-subtle)', padding: '16px', marginTop: '16px', display: 'flex', flexDirection: 'column', gap: '12px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '10px' }}>
        <div>
          <div style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text)' }}>{title}</div>
          {subtitle && <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '2px' }}>{subtitle}</div>}
        </div>
        <button
          type="button"
          disabled={generating}
          onClick={onGenerate}
          style={{
            padding: '8px 14px',
            borderRadius: '6px',
            border: 'none',
            backgroundColor: '#0f766e',
            color: '#fff',
            fontWeight: 600,
            fontSize: '13px',
            cursor: 'pointer',
          }}
        >
          {generating ? 'Validating against CBDT Schema...' : 'Generate & Validate Official JSON'}
        </button>
      </div>

      {children}

      {result && (
        <div style={{ backgroundColor: 'var(--card)', border: '1px solid var(--border)', borderRadius: '6px', padding: '14px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '8px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
              <span
                style={{
                  padding: '3px 10px',
                  borderRadius: '999px',
                  fontSize: '11px',
                  fontWeight: 700,
                  backgroundColor: result.validAgainstSchema ? 'var(--success-bg)' : 'var(--error-bg)',
                  color: result.validAgainstSchema ? 'var(--success)' : 'var(--error)',
                }}
              >
                {result.validAgainstSchema ? 'CBDT Schema Validated (0 errors)' : `Blocked (${result.issues.filter(i => i.severity === 'error').length} error(s))`}
              </span>
              <span style={{ fontSize: '12px', color: 'var(--text-secondary)', fontWeight: 600 }}>
                {result.formType} • {result.schemaVersion} • {result.fileName}
              </span>
            </div>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button
                type="button"
                onClick={() => download(result.fileName, result.jsonContent, 'application/json')}
                style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid var(--accent)', background: 'var(--accent-subtle)', color: 'var(--accent)', fontWeight: 600, fontSize: '12px', cursor: 'pointer' }}
              >
                Download {result.fileName}
              </button>
              <button
                type="button"
                disabled={savingVault || !clientId}
                onClick={handleSaveVault}
                style={{ padding: '6px 12px', borderRadius: '6px', border: 'none', background: '#059669', color: '#fff', fontWeight: 600, fontSize: '12px', cursor: 'pointer' }}
              >
                {savingVault ? 'Saving...' : 'Save JSON to Vault'}
              </button>
            </div>
          </div>

          {vaultMsg && <div style={{ padding: '6px 10px', backgroundColor: 'var(--success-bg)', color: 'var(--success-text)', borderRadius: '6px', fontSize: '12px' }}>{vaultMsg}</div>}
          {vaultErr && <div style={{ padding: '6px 10px', backgroundColor: 'var(--error-bg)', color: 'var(--error)', borderRadius: '6px', fontSize: '12px' }}>{vaultErr}</div>}

          {result.issues.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
              {result.issues.map((issue, idx) => (
                <div
                  key={`${issue.code}-${idx}`}
                  style={{
                    padding: '6px 10px',
                    borderRadius: '4px',
                    fontSize: '12px',
                    backgroundColor: issue.severity === 'error' ? 'var(--error-bg)' : 'var(--warning-bg)',
                    color: issue.severity === 'error' ? 'var(--error)' : 'var(--warning)',
                  }}
                >
                  <strong>{issue.severity.toUpperCase()} [{issue.code}]</strong> {issue.message}{' '}
                  <span style={{ opacity: 0.75, fontFamily: 'monospace' }}>({issue.path})</span>
                </div>
              ))}
            </div>
          )}

          <pre
            style={{
              margin: 0,
              padding: '10px',
              backgroundColor: '#0f172a',
              color: '#e2e8f0',
              borderRadius: '6px',
              fontSize: '11px',
              maxHeight: '220px',
              overflow: 'auto',
            }}
          >
            {result.jsonContent}
          </pre>
        </div>
      )}
    </div>
  )
}
