import React, { useEffect, useState } from 'react'
import {
  aqmmApi,
  type AqmmAssessmentSummaryDto,
  type AqmmQuestionResponseDto,
  type AqmmGapAnalysisDto,
  type AqmmScoringCriterion,
  type AqmmRecommendationDto,
  type CertifyAqmmAssessmentRequest
} from './api'

export function AqmmDeskPage() {
  const [financialYear, setFinancialYear] = useState<string>('2024-25')
  const [summary, setSummary] = useState<AqmmAssessmentSummaryDto | null>(null)
  const [questions, setQuestions] = useState<AqmmQuestionResponseDto[]>([])
  const [gapAnalysis, setGapAnalysis] = useState<AqmmGapAnalysisDto | null>(null)
  const [activeTab, setActiveTab] = useState<'questionnaire' | 'gap' | 'certify'>('questionnaire')
  const [sectionFilter, setSectionFilter] = useState<'all' | 'PracticeManagement' | 'HumanResources' | 'AuditExecution'>('all')

  const [loading, setLoading] = useState<boolean>(true)
  const [savingCode, setSavingCode] = useState<string | null>(null)
  const [autoScoring, setAutoScoring] = useState<boolean>(false)
  const [toastMessage, setToastMessage] = useState<string | null>(null)

  // Edit buffer for answers
  const [selectedScores, setSelectedScores] = useState<Record<string, number>>({})
  const [remarksBuffer, setRemarksBuffer] = useState<Record<string, string>>({})
  const [evidenceBuffer, setEvidenceBuffer] = useState<Record<string, string>>({})

  // Certification modal / form
  const [partnerName, setPartnerName] = useState<string>('')
  const [membershipNo, setMembershipNo] = useState<string>('')
  const [frn, setFrn] = useState<string>('')
  const [udin, setUdin] = useState<string>('')
  const [certifying, setCertifying] = useState<boolean>(false)

  useEffect(() => {
    loadData()
  }, [financialYear])

  async function loadData() {
    try {
      setLoading(true)
      const [sum, qList, gap] = await Promise.all([
        aqmmApi.getCurrent(financialYear),
        aqmmApi.getQuestions(financialYear),
        aqmmApi.getGapAnalysis(financialYear)
      ])
      setSummary(sum)
      setQuestions(qList)
      setGapAnalysis(gap)

      // Initialize buffers
      const scores: Record<string, number> = {}
      const remarks: Record<string, string> = {}
      const evidences: Record<string, string> = {}

      qList.forEach(q => {
        scores[q.questionCode] = q.awardedPoints
        remarks[q.questionCode] = q.partnerRemarks || ''
        evidences[q.questionCode] = q.evidenceReference || ''
      })

      setSelectedScores(scores)
      setRemarksBuffer(remarks)
      setEvidenceBuffer(evidences)

      if (sum.leadPartnerName) setPartnerName(sum.leadPartnerName)
      if (sum.partnerMembershipNo) setMembershipNo(sum.partnerMembershipNo)
      if (sum.firmRegistrationNo) setFrn(sum.firmRegistrationNo)
      if (sum.udin) setUdin(sum.udin)
    } catch (err: unknown) {
      alert(`Failed to load AQMM data: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }

  async function handleAutoScore() {
    try {
      setAutoScoring(true)
      const res = await aqmmApi.autoScore(financialYear)
      setToastMessage(res.message)
      await loadData()
    } catch (err: unknown) {
      alert(`Auto-evaluation failed: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setAutoScoring(false)
    }
  }

  async function handleSaveQuestion(qCode: string) {
    try {
      setSavingCode(qCode)
      const points = selectedScores[qCode] ?? 0
      const remarks = remarksBuffer[qCode] || null
      const evidence = evidenceBuffer[qCode] || null

      const updated = await aqmmApi.updateResponse({
        questionCode: qCode,
        awardedPoints: points,
        partnerRemarks: remarks,
        evidenceReference: evidence
      }, financialYear)

      setSummary(updated)
      const gap = await aqmmApi.getGapAnalysis(financialYear)
      setGapAnalysis(gap)
      setToastMessage(`Saved response for ${qCode}. New Total: ${updated.totalScore}/600 (${updated.maturityLevel})`)
    } catch (err: unknown) {
      alert(`Failed to save question ${qCode}: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setSavingCode(null)
    }
  }

  async function handleCertify(e: React.FormEvent) {
    e.preventDefault()
    if (!udin || udin.length !== 18) {
      alert('Please enter a valid 18-digit ICAI UDIN.')
      return
    }

    try {
      setCertifying(true)
      const req: CertifyAqmmAssessmentRequest = {
        leadPartnerName: partnerName,
        partnerMembershipNo: membershipNo,
        firmRegistrationNo: frn,
        udin: udin.toUpperCase()
      }
      const updated = await aqmmApi.certify(req, financialYear)
      setSummary(updated)
      setToastMessage(`🎉 AQMM Assessment certified under UDIN ${updated.udin}!`)
      setActiveTab('questionnaire')
    } catch (err: unknown) {
      alert(`Certification failed: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setCertifying(false)
    }
  }

  async function handleDownloadDossier() {
    try {
      const html = await aqmmApi.getDossierHtml(financialYear)
      const win = window.open('', '_blank')
      if (win) {
        win.document.write(html)
        win.document.close()
      } else {
        alert('Popup blocked. Please allow popups to view the official dossier.')
      }
    } catch (err: unknown) {
      alert(`Failed to generate dossier: ${err instanceof Error ? err.message : 'Unknown error'}`)
    }
  }

  const filteredQuestions = questions.filter(q => {
    if (sectionFilter === 'all') return true
    return q.section === sectionFilter
  })

  const levelColor = summary?.maturityLevel === 'Level 3'
    ? { bg: '#ecfdf5', text: '#065f46', border: '#a7f3d0' }
    : summary?.maturityLevel === 'Level 2'
    ? { bg: '#fffbeb', text: '#92400e', border: '#fde68a' }
    : { bg: '#fef2f2', text: '#991b1b', border: '#fecaca' }

  return (
    <div style={{ padding: '28px', maxWidth: '1360px', margin: '0 auto', color: '#1e293b' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px', flexWrap: 'wrap', gap: '14px' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <h1 style={{ fontSize: '24px', fontWeight: 700, margin: 0, color: '#0f172a' }}>
              🛡️ ICAI Audit Quality Maturity Model (AQMM v1.0) Desk
            </h1>
            <span style={{ fontSize: '11px', background: '#e0e7ff', color: '#3730a3', padding: '3px 8px', borderRadius: '4px', fontWeight: 700 }}>
              MANDATORY CAQ FRAMEWORK
            </span>
          </div>
          <p style={{ fontSize: '13px', color: '#64748b', margin: '4px 0 0 0' }}>
            Comprehensive 600-point self-assessment and peer-review scoring matrix for Indian Chartered Accountant firms.
          </p>
        </div>

        <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
          <select
            value={financialYear}
            onChange={e => setFinancialYear(e.target.value)}
            style={{ padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px', fontWeight: 600, background: '#fff' }}
          >
            <option value="2024-25">FY 2024-25 (Current)</option>
            <option value="2023-24">FY 2023-24</option>
          </select>

          <button
            onClick={handleAutoScore}
            disabled={autoScoring || loading}
            style={{ padding: '8px 16px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, fontSize: '13px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '6px' }}
          >
            <span>{autoScoring ? 'Evaluating Telemetry...' : '⚡ Auto-Evaluate from CA Desk'}</span>
          </button>

          <button
            onClick={handleDownloadDossier}
            style={{ padding: '8px 16px', background: '#0f172a', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, fontSize: '13px', cursor: 'pointer' }}
          >
            📄 Print ICAI Dossier
          </button>
        </div>
      </div>

      {/* Toast Notification */}
      {toastMessage && (
        <div style={{ padding: '12px 16px', background: '#f0fdf4', border: '1px solid #86efac', borderRadius: '8px', marginBottom: '20px', color: '#166534', fontSize: '13px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>{toastMessage}</div>
          <button onClick={() => setToastMessage(null)} style={{ background: 'none', border: 'none', color: '#166534', cursor: 'pointer', fontWeight: 700 }}>✕</button>
        </div>
      )}

      {/* Overview Cards & Level Meter */}
      {summary && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          {/* Level Card */}
          <div style={{ padding: '20px', borderRadius: '8px', background: levelColor.bg, border: `1px solid ${levelColor.border}` }}>
            <div style={{ fontSize: '11px', textTransform: 'uppercase', fontWeight: 700, color: levelColor.text, letterSpacing: '0.5px' }}>
              ICAI Maturity Classification
            </div>
            <div style={{ fontSize: '28px', fontWeight: 800, color: levelColor.text, margin: '4px 0 2px 0' }}>
              {summary.maturityLevel}
            </div>
            <div style={{ fontSize: '12px', color: levelColor.text }}>
              {summary.maturityLevel === 'Level 3' && '🏆 Audit Quality Excellence (Eligible for Tier-1 Public Audits)'}
              {summary.maturityLevel === 'Level 2' && '✅ Good Quality Systems (Standard Peer Review Clearance)'}
              {summary.maturityLevel === 'Level 1' && '⚠️ Needs Substantial Quality Improvement (< 450 Pts)'}
            </div>
            {summary.udin && (
              <div style={{ marginTop: '10px', fontSize: '11px', background: '#fff', padding: '4px 8px', borderRadius: '4px', border: `1px solid ${levelColor.border}`, display: 'inline-block' }}>
                UDIN: <strong>{summary.udin}</strong>
              </div>
            )}
          </div>

          {/* Section A Card */}
          <div style={{ padding: '20px', borderRadius: '8px', background: '#fff', border: '1px solid #e2e8f0' }}>
            <div style={{ fontSize: '11px', textTransform: 'uppercase', fontWeight: 700, color: '#64748b' }}>
              Section A: Practice Operations
            </div>
            <div style={{ fontSize: '24px', fontWeight: 800, color: '#0f172a', margin: '4px 0' }}>
              {summary.sectionAScore} <span style={{ fontSize: '14px', color: '#94a3b8' }}>/ 190 pts</span>
            </div>
            <div style={{ width: '100%', background: '#f1f5f9', height: '6px', borderRadius: '3px', overflow: 'hidden', margin: '8px 0' }}>
              <div style={{ width: `${Math.min(100, summary.sectionAPercentage)}%`, background: '#2563eb', height: '100%' }} />
            </div>
            <div style={{ fontSize: '12px', color: '#64748b' }}>Score: {summary.sectionAPercentage}% (7 criteria)</div>
          </div>

          {/* Section B Card */}
          <div style={{ padding: '20px', borderRadius: '8px', background: '#fff', border: '1px solid #e2e8f0' }}>
            <div style={{ fontSize: '11px', textTransform: 'uppercase', fontWeight: 700, color: '#64748b' }}>
              Section B: Human Resources
            </div>
            <div style={{ fontSize: '24px', fontWeight: 800, color: '#0f172a', margin: '4px 0' }}>
              {summary.sectionBScore} <span style={{ fontSize: '14px', color: '#94a3b8' }}>/ 110 pts</span>
            </div>
            <div style={{ width: '100%', background: '#f1f5f9', height: '6px', borderRadius: '3px', overflow: 'hidden', margin: '8px 0' }}>
              <div style={{ width: `${Math.min(100, summary.sectionBPercentage)}%`, background: '#8b5cf6', height: '100%' }} />
            </div>
            <div style={{ fontSize: '12px', color: '#64748b' }}>Score: {summary.sectionBPercentage}% (5 criteria)</div>
          </div>

          {/* Section C Card */}
          <div style={{ padding: '20px', borderRadius: '8px', background: '#fff', border: '1px solid #e2e8f0' }}>
            <div style={{ fontSize: '11px', textTransform: 'uppercase', fontWeight: 700, color: '#64748b' }}>
              Section C: Audit Execution & SQC 1
            </div>
            <div style={{ fontSize: '24px', fontWeight: 800, color: '#0f172a', margin: '4px 0' }}>
              {summary.sectionCScore} <span style={{ fontSize: '14px', color: '#94a3b8' }}>/ 300 pts</span>
            </div>
            <div style={{ width: '100%', background: '#f1f5f9', height: '6px', borderRadius: '3px', overflow: 'hidden', margin: '8px 0' }}>
              <div style={{ width: `${Math.min(100, summary.sectionCPercentage)}%`, background: '#059669', height: '100%' }} />
            </div>
            <div style={{ fontSize: '12px', color: '#64748b' }}>Score: {summary.sectionCPercentage}% (9 criteria)</div>
          </div>
        </div>
      )}

      {/* Tabs */}
      <div style={{ display: 'flex', borderBottom: '1px solid #e2e8f0', marginBottom: '24px' }}>
        <button
          onClick={() => setActiveTab('questionnaire')}
          style={{ padding: '10px 20px', background: 'none', border: 'none', borderBottom: activeTab === 'questionnaire' ? '2px solid #0284c7' : 'none', color: activeTab === 'questionnaire' ? '#0284c7' : '#64748b', fontWeight: 700, fontSize: '14px', cursor: 'pointer' }}
        >
          📋 Assessment Questionnaire ({questions.length} Criteria)
        </button>
        <button
          onClick={() => setActiveTab('gap')}
          style={{ padding: '10px 20px', background: 'none', border: 'none', borderBottom: activeTab === 'gap' ? '2px solid #0284c7' : 'none', color: activeTab === 'gap' ? '#0284c7' : '#64748b', fontWeight: 700, fontSize: '14px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '6px' }}
        >
          <span>🎯 Gap Analysis &amp; Path to Level 3</span>
          {summary && summary.level3Gap > 0 && (
            <span style={{ fontSize: '11px', background: '#fee2e2', color: '#b91c1c', padding: '2px 6px', borderRadius: '10px', fontWeight: 700 }}>
              +{summary.level3Gap} pts needed
            </span>
          )}
        </button>
        <button
          onClick={() => setActiveTab('certify')}
          style={{ padding: '10px 20px', background: 'none', border: 'none', borderBottom: activeTab === 'certify' ? '2px solid #0284c7' : 'none', color: activeTab === 'certify' ? '#0284c7' : '#64748b', fontWeight: 700, fontSize: '14px', cursor: 'pointer' }}
        >
          ✍️ Partner Certification &amp; UDIN
        </button>
      </div>

      {/* TAB 1: Questionnaire */}
      {activeTab === 'questionnaire' && (
        <div>
          {/* Section Filter Pills */}
          <div style={{ display: 'flex', gap: '8px', marginBottom: '16px' }}>
            {(
              [
                { id: 'all', label: 'All Criteria (21)' },
                { id: 'PracticeManagement', label: 'Section A: Operations (190 pts)' },
                { id: 'HumanResources', label: 'Section B: HR Management (110 pts)' },
                { id: 'AuditExecution', label: 'Section C: Audit Execution (300 pts)' }
              ] as const
            ).map(pill => (
              <button
                key={pill.id}
                onClick={() => setSectionFilter(pill.id)}
                style={{ padding: '6px 14px', borderRadius: '20px', border: '1px solid #cbd5e1', background: sectionFilter === pill.id ? '#0f172a' : '#fff', color: sectionFilter === pill.id ? '#fff' : '#475569', fontSize: '12px', fontWeight: 600, cursor: 'pointer' }}
              >
                {pill.label}
              </button>
            ))}
          </div>

          {/* Question Cards List */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
            {filteredQuestions.map(q => {
              const currentScore = selectedScores[q.questionCode] ?? q.awardedPoints
              const remarks = remarksBuffer[q.questionCode] ?? ''
              const evidence = evidenceBuffer[q.questionCode] ?? ''
              const isSaving = savingCode === q.questionCode

              return (
                <div key={q.questionCode} style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '12px' }}>
                    <div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px' }}>
                        <span style={{ fontSize: '12px', fontWeight: 700, color: '#0369a1', background: '#f0f9ff', padding: '2px 8px', borderRadius: '4px' }}>
                          {q.questionCode}
                        </span>
                        <span style={{ fontSize: '11px', color: '#64748b' }}>
                          {q.section === 'PracticeManagement' ? 'Section A: Operations' : (q.section === 'HumanResources' ? 'Section B: HR' : 'Section C: Audit Execution')}
                        </span>
                        {q.isAutoEvaluated && (
                          <span style={{ fontSize: '10px', background: '#dcfce7', color: '#166534', padding: '2px 6px', borderRadius: '4px', fontWeight: 700 }}>
                            ⚡ Auto-Evaluated by CA Desk
                          </span>
                        )}
                      </div>
                      <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 4px 0', color: '#0f172a' }}>
                        {q.title}
                      </h3>
                      <p style={{ fontSize: '13px', color: '#475569', margin: 0 }}>
                        {q.description}
                      </p>
                    </div>

                    <div style={{ textAlign: 'right' }}>
                      <div style={{ fontSize: '20px', fontWeight: 800, color: currentScore === q.maxPoints ? '#166534' : '#b45309' }}>
                        {currentScore} <span style={{ fontSize: '12px', color: '#94a3b8' }}>/ {q.maxPoints} pts</span>
                      </div>
                    </div>
                  </div>

                  {/* Scoring Criteria Radios */}
                  <div style={{ background: '#f8fafc', padding: '14px', borderRadius: '6px', marginBottom: '14px' }}>
                    <div style={{ fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '8px' }}>
                      Select Scoring Level:
                    </div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                      {q.criteria.map((c: AqmmScoringCriterion) => (
                        <label
                          key={c.points}
                          style={{ display: 'flex', alignItems: 'flex-start', gap: '10px', fontSize: '13px', cursor: 'pointer', padding: '6px 10px', borderRadius: '4px', background: currentScore === c.points ? '#eff6ff' : 'transparent', border: currentScore === c.points ? '1px solid #bfdbfe' : '1px solid transparent' }}
                        >
                          <input
                            type="radio"
                            name={`radio_${q.questionCode}`}
                            checked={currentScore === c.points}
                            onChange={() => setSelectedScores(prev => ({ ...prev, [q.questionCode]: c.points }))}
                            style={{ marginTop: '3px' }}
                          />
                          <div>
                            <span style={{ fontWeight: 700, color: '#0f172a' }}>{c.label} ({c.points} pts): </span>
                            <span style={{ color: '#475569' }}>{c.description}</span>
                          </div>
                        </label>
                      ))}
                    </div>
                  </div>

                  {/* Remarks & Evidence inputs */}
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
                    <div>
                      <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#64748b', marginBottom: '4px' }}>
                        Partner Evaluation Remarks:
                      </label>
                      <input
                        type="text"
                        value={remarks}
                        onChange={e => setRemarksBuffer(prev => ({ ...prev, [q.questionCode]: e.target.value }))}
                        placeholder="e.g. Documented in firm Quality Control Manual Section 4.2"
                        style={{ width: '100%', padding: '6px 10px', borderRadius: '4px', border: '1px solid #cbd5e1', fontSize: '12px' }}
                      />
                    </div>
                    <div>
                      <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#64748b', marginBottom: '4px' }}>
                        Evidence Reference / Attachment Citation:
                      </label>
                      <input
                        type="text"
                        value={evidence}
                        onChange={e => setEvidenceBuffer(prev => ({ ...prev, [q.questionCode]: e.target.value }))}
                        placeholder="e.g. Workpaper Folder WP-2024-KYC or ClamAV scan log"
                        style={{ width: '100%', padding: '6px 10px', borderRadius: '4px', border: '1px solid #cbd5e1', fontSize: '12px' }}
                      />
                    </div>
                  </div>

                  <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                    <button
                      onClick={() => handleSaveQuestion(q.questionCode)}
                      disabled={isSaving}
                      style={{ padding: '6px 14px', background: '#0f172a', color: '#fff', border: 'none', borderRadius: '4px', fontSize: '12px', fontWeight: 600, cursor: 'pointer' }}
                    >
                      {isSaving ? 'Saving...' : 'Save Evaluation'}
                    </button>
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      )}

      {/* TAB 2: Gap Analysis & Path to Level 3 */}
      {activeTab === 'gap' && gapAnalysis && (
        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '24px' }}>
          <div style={{ marginBottom: '20px' }}>
            <h2 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 6px 0', color: '#0f172a' }}>
              🎯 Quality Maturity Gap Analysis &amp; Action Plan
            </h2>
            <p style={{ fontSize: '13px', color: '#64748b', margin: 0 }}>
              Actionable roadmap detailing the highest point-yield remediation tasks required to elevate your firm from <strong>{gapAnalysis.currentLevel}</strong> to <strong>Level 3 Excellence</strong>.
            </p>
          </div>

          <div style={{ display: 'flex', gap: '20px', marginBottom: '24px', flexWrap: 'wrap' }}>
            <div style={{ padding: '16px 20px', borderRadius: '6px', background: '#f8fafc', border: '1px solid #e2e8f0', flex: '1 1 200px' }}>
              <div style={{ fontSize: '11px', color: '#64748b', textTransform: 'uppercase', fontWeight: 700 }}>Current Score</div>
              <div style={{ fontSize: '26px', fontWeight: 800, color: '#0f172a' }}>{gapAnalysis.currentScore} / 600</div>
            </div>
            <div style={{ padding: '16px 20px', borderRadius: '6px', background: '#fef3c7', border: '1px solid #fde68a', flex: '1 1 200px' }}>
              <div style={{ fontSize: '11px', color: '#b45309', textTransform: 'uppercase', fontWeight: 700 }}>Gap to Level 2 (450 Pts)</div>
              <div style={{ fontSize: '26px', fontWeight: 800, color: '#92400e' }}>
                {gapAnalysis.pointsNeededForLevel2 > 0 ? `+${gapAnalysis.pointsNeededForLevel2} pts needed` : '✅ Achieved'}
              </div>
            </div>
            <div style={{ padding: '16px 20px', borderRadius: '6px', background: '#ecfdf5', border: '1px solid #a7f3d0', flex: '1 1 200px' }}>
              <div style={{ fontSize: '11px', color: '#065f46', textTransform: 'uppercase', fontWeight: 700 }}>Gap to Level 3 (510 Pts)</div>
              <div style={{ fontSize: '26px', fontWeight: 800, color: '#047857' }}>
                {gapAnalysis.pointsNeededForLevel3 > 0 ? `+${gapAnalysis.pointsNeededForLevel3} pts needed` : '🏆 Level 3 Achieved!'}
              </div>
            </div>
          </div>

          <h3 style={{ fontSize: '15px', fontWeight: 700, margin: '0 0 12px 0', color: '#0f172a' }}>
            Prioritized Remediation Roadmap (Sorted by Highest Potential Point Gain)
          </h3>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
              <thead>
                <tr style={{ background: '#f1f5f9', borderBottom: '2px solid #cbd5e1' }}>
                  <th style={{ padding: '10px 12px', textAlign: 'left' }}>Code</th>
                  <th style={{ padding: '10px 12px', textAlign: 'left' }}>Audit Quality Area</th>
                  <th style={{ padding: '10px 12px', textAlign: 'center' }}>Current</th>
                  <th style={{ padding: '10px 12px', textAlign: 'center' }}>Max</th>
                  <th style={{ padding: '10px 12px', textAlign: 'center' }}>Potential Gain</th>
                  <th style={{ padding: '10px 12px', textAlign: 'center' }}>Priority</th>
                  <th style={{ padding: '10px 12px', textAlign: 'left' }}>Remedial Action Required</th>
                </tr>
              </thead>
              <tbody>
                {gapAnalysis.topRecommendations.map((r: AqmmRecommendationDto) => (
                  <tr key={r.questionCode} style={{ borderBottom: '1px solid #e2e8f0' }}>
                    <td style={{ padding: '10px 12px', fontWeight: 700, color: '#0369a1' }}>{r.questionCode}</td>
                    <td style={{ padding: '10px 12px', fontWeight: 600 }}>{r.title}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'center' }}>{r.currentPoints}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'center' }}>{r.maxPoints}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'center', fontWeight: 800, color: '#166534' }}>+{r.potentialGain}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                      <span style={{ fontSize: '11px', padding: '2px 8px', borderRadius: '4px', fontWeight: 700, background: r.priority === 'High' ? '#fee2e2' : (r.priority === 'Medium' ? '#fef3c7' : '#f1f5f9'), color: r.priority === 'High' ? '#b91c1c' : (r.priority === 'Medium' ? '#b45309' : '#475569') }}>
                        {r.priority}
                      </span>
                    </td>
                    <td style={{ padding: '10px 12px', color: '#475569' }}>{r.remedialAction}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* TAB 3: Certification & UDIN */}
      {activeTab === 'certify' && (
        <div style={{ maxWidth: '640px', background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '28px', margin: '0 auto' }}>
          <h2 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 6px 0', color: '#0f172a' }}>
            ✍️ Certify AQMM Self-Assessment with ICAI UDIN
          </h2>
          <p style={{ fontSize: '13px', color: '#64748b', margin: '0 0 20px 0' }}>
            Once the Managing / Lead Quality Partner completes the self-evaluation, bind the scorecard with your 18-digit Unique Document Identification Number (UDIN).
          </p>

          <form onSubmit={handleCertify}>
            <div style={{ marginBottom: '14px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                Lead Partner Name:
              </label>
              <input
                type="text"
                required
                value={partnerName}
                onChange={e => setPartnerName(e.target.value)}
                placeholder="e.g. CA Rajesh Kumar"
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '14px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                  ICAI Membership Number:
                </label>
                <input
                  type="text"
                  required
                  value={membershipNo}
                  onChange={e => setMembershipNo(e.target.value)}
                  placeholder="e.g. 054321"
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                  Firm Registration Number (FRN):
                </label>
                <input
                  type="text"
                  required
                  value={frn}
                  onChange={e => setFrn(e.target.value)}
                  placeholder="e.g. 104523W"
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
                />
              </div>
            </div>

            <div style={{ marginBottom: '20px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: '#334155', marginBottom: '4px' }}>
                18-Digit ICAI UDIN:
              </label>
              <input
                type="text"
                required
                maxLength={18}
                value={udin}
                onChange={e => setUdin(e.target.value.toUpperCase())}
                placeholder="e.g. 25054321AAAA123456"
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '14px', fontFamily: 'monospace', letterSpacing: '1px', fontWeight: 700 }}
              />
              <div style={{ fontSize: '11px', color: '#64748b', marginTop: '4px' }}>
                Generated from the ICAI UDIN Portal under Certificate/AQMM category. Must be exactly 18 characters.
              </div>
            </div>

            <button
              type="submit"
              disabled={certifying}
              style={{ width: '100%', padding: '10px', background: '#0f172a', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 700, fontSize: '14px', cursor: 'pointer' }}
            >
              {certifying ? 'Signing & Sealing Dossier...' : 'Lock & Certify AQMM Dossier'}
            </button>
          </form>
        </div>
      )}
    </div>
  )
}
export default AqmmDeskPage
