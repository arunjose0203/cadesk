import React, { useState, useEffect } from 'react'
import {
  payrollApi,
  api,
  type PayrollEmployeeDto,
  type PayrollRunDto,
  type PayrollPayslipDto,
  type PayrollSummaryDto,
  type EpfoEcrExportDto,
  type SavePayrollEmployeeRequest,
  type ProcessPayrollRunRequest,
  type Client,
  type ClientPage
} from './api'

interface PayrollPageProps {
  can?: (p: string) => boolean
}

export const PayrollPage: React.FC<PayrollPageProps> = ({ can }) => {
  const [activeTab, setActiveTab] = useState<'employees' | 'runs' | 'payslips' | 'ecr'>('employees')
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Filters & State
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [summary, setSummary] = useState<PayrollSummaryDto | null>(null)
  const [employees, setEmployees] = useState<PayrollEmployeeDto[]>([])
  const [runs, setRuns] = useState<PayrollRunDto[]>([])
  const [selectedRun, setSelectedRun] = useState<PayrollRunDto | null>(null)
  const [payslips, setPayslips] = useState<PayrollPayslipDto[]>([])
  const [ecrData, setEcrData] = useState<EpfoEcrExportDto | null>(null)

  // Modals
  const [showEmployeeModal, setShowEmployeeModal] = useState<boolean>(false)
  const [showProcessModal, setShowProcessModal] = useState<boolean>(false)
  const [editingEmployeeId, setEditingEmployeeId] = useState<string | null>(null)

  // Employee Form
  const [employeeForm, setEmployeeForm] = useState<SavePayrollEmployeeRequest>({
    clientId: '',
    employeeCode: '',
    name: '',
    pan: '',
    uan: '',
    esicNumber: '',
    dateOfJoining: new Date().toISOString().split('T')[0],
    designation: '',
    department: '',
    state: 'Maharashtra',
    taxRegime: 'NewRegime',
    monthlyCtc: 30000,
    basicSalary: 15000,
    hra: 7500,
    specialAllowance: 5000,
    otherAllowances: 2500,
    isPfEligible: true,
    isEsiEligible: true,
    isPtEligible: true,
    isActive: true
  })

  // Run Form
  const [runForm, setRunForm] = useState<ProcessPayrollRunRequest>({
    clientId: '',
    year: new Date().getFullYear(),
    month: new Date().getMonth() + 1,
    notes: ''
  })

  useEffect(() => {
    loadClients()
  }, [])

  useEffect(() => {
    if (selectedClientId) {
      loadClientData(selectedClientId)
    }
  }, [selectedClientId])

  const loadClients = async () => {
    try {
      const res = await api<ClientPage>('/api/clients?take=200')
      const items = res.items || []
      setClients(items)
      if (items.length > 0) {
        setSelectedClientId(items[0].id)
        setEmployeeForm(prev => ({ ...prev, clientId: items[0].id }))
        setRunForm(prev => ({ ...prev, clientId: items[0].id }))
      }
    } catch (e: any) {
      setError(e.message || 'Failed to load clients')
    }
  }

  const loadClientData = async (cId: string) => {
    setLoading(true)
    setError(null)
    try {
      const [sum, empList, rList] = await Promise.all([
        payrollApi.getSummary(cId),
        payrollApi.getEmployees(cId),
        payrollApi.getRuns(cId)
      ])
      setSummary(sum)
      setEmployees(empList)
      setRuns(rList)

      if (rList.length > 0 && !selectedRun) {
        setSelectedRun(rList[0])
        loadPayslips(rList[0].id)
      }
    } catch (e: any) {
      setError(e.message || 'Failed to load payroll data')
    } finally {
      setLoading(false)
    }
  }

  const loadPayslips = async (runId: string) => {
    try {
      const list = await payrollApi.getPayslips(runId)
      setPayslips(list)
    } catch (e: any) {
      setError(e.message || 'Failed to load payslips')
    }
  }

  const handleSelectRun = async (r: PayrollRunDto) => {
    setSelectedRun(r)
    await loadPayslips(r.id)
    setActiveTab('payslips')
  }

  const handleExportEcr = async (runId: string) => {
    try {
      const ecr = await payrollApi.exportEcr(runId)
      setEcrData(ecr)
      setActiveTab('ecr')
    } catch (e: any) {
      setError(e.message || 'Failed to export EPFO ECR')
    }
  }

  const handleSaveEmployee = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setSuccess(null)
    try {
      if (editingEmployeeId) {
        await payrollApi.updateEmployee(editingEmployeeId, employeeForm)
        setSuccess('Employee details updated successfully!')
      } else {
        await payrollApi.createEmployee(employeeForm)
        setSuccess('New employee registered successfully!')
      }
      setShowEmployeeModal(false)
      setEditingEmployeeId(null)
      loadClientData(selectedClientId)
    } catch (e: any) {
      setError(e.message || 'Failed to save employee')
    }
  }

  const handleOpenEditEmployee = (emp: PayrollEmployeeDto) => {
    setEditingEmployeeId(emp.id)
    setEmployeeForm({
      clientId: emp.clientId,
      employeeCode: emp.employeeCode,
      name: emp.name,
      pan: emp.pan || '',
      uan: emp.uan || '',
      esicNumber: emp.esicNumber || '',
      dateOfJoining: emp.dateOfJoining,
      designation: emp.designation,
      department: emp.department || '',
      state: emp.state,
      taxRegime: emp.taxRegime,
      monthlyCtc: emp.monthlyCtc,
      basicSalary: emp.basicSalary,
      hra: emp.hra,
      specialAllowance: emp.specialAllowance,
      otherAllowances: emp.otherAllowances,
      isPfEligible: emp.isPfEligible,
      isEsiEligible: emp.isEsiEligible,
      isPtEligible: emp.isPtEligible,
      isActive: emp.isActive
    })
    setShowEmployeeModal(true)
  }

  const handleProcessRun = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setSuccess(null)
    try {
      const run = await payrollApi.processRun({
        ...runForm,
        clientId: selectedClientId
      })
      setSuccess(`Payroll for ${run.payrollPeriod} calculated successfully for ${run.totalEmployees} employees!`)
      setShowProcessModal(false)
      loadClientData(selectedClientId)
      setSelectedRun(run)
      loadPayslips(run.id)
      setActiveTab('payslips')
    } catch (e: any) {
      setError(e.message || 'Failed to process monthly payroll')
    }
  }

  const handleUpdateRunStatus = async (runId: string, status: any) => {
    setError(null)
    setSuccess(null)
    try {
      const updated = await payrollApi.updateRunStatus(runId, { status })
      setSuccess(`Payroll run marked as ${status}!`)
      setRuns(prev => prev.map(r => r.id === runId ? updated : r))
      if (selectedRun?.id === runId) setSelectedRun(updated)
    } catch (e: any) {
      setError(e.message || 'Failed to update payroll status')
    }
  }

  const formatCurrency = (val?: number | null) => {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      maximumFractionDigits: 0
    }).format(val || 0)
  }

  return (
    <div style={{ padding: '20px', maxWidth: '1350px', margin: '0 auto' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
        <div>
          <h1 style={{ margin: '0 0 6px 0', fontSize: '24px', fontWeight: 700 }}>
            Payroll, Salary TDS & Statutory PF / ESIC Desk
          </h1>
          <p style={{ margin: 0, color: 'var(--muted, #666)', fontSize: '14px' }}>
            Client Employee Master, EPF/ESIC Auto-Splits, State PT Slabs, Monthly Salary Runs & EPFO ECR Export (Sprint 23)
          </p>
        </div>

        {/* Client Selector & Actions */}
        <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span style={{ fontSize: '13px', fontWeight: 600 }}>Client:</span>
            <select
              value={selectedClientId}
              onChange={e => {
                setSelectedClientId(e.target.value)
                setEmployeeForm(prev => ({ ...prev, clientId: e.target.value }))
                setRunForm(prev => ({ ...prev, clientId: e.target.value }))
              }}
              style={{ padding: '6px 12px', borderRadius: '4px', border: '1px solid var(--border, #ccc)', fontWeight: 600 }}
            >
              {clients.map(c => (
                <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
              ))}
            </select>
          </div>

          {(!can || can('payroll.manage')) && (
            <button
              className="secondary"
              onClick={() => {
                setEditingEmployeeId(null)
                setEmployeeForm({
                  clientId: selectedClientId,
                  employeeCode: `EMP${(employees.length + 1).toString().padStart(3, '0')}`,
                  name: '',
                  pan: '',
                  uan: '',
                  esicNumber: '',
                  dateOfJoining: new Date().toISOString().split('T')[0],
                  designation: '',
                  department: '',
                  state: 'Maharashtra',
                  taxRegime: 'NewRegime',
                  monthlyCtc: 30000,
                  basicSalary: 15000,
                  hra: 7500,
                  specialAllowance: 5000,
                  otherAllowances: 2500,
                  isPfEligible: true,
                  isEsiEligible: true,
                  isPtEligible: true,
                  isActive: true
                })
                setShowEmployeeModal(true)
              }}
            >
              + Add Employee
            </button>
          )}

          {(!can || can('payroll.process')) && (
            <button className="primary" onClick={() => setShowProcessModal(true)}>
              ⚡ Process Salary Run
            </button>
          )}
        </div>
      </div>

      {/* Notifications */}
      {error && (
        <div style={{ padding: '12px', background: '#ffebee', color: '#c62828', borderRadius: '6px', marginBottom: '16px' }}>
          {error}
        </div>
      )}
      {success && (
        <div style={{ padding: '12px', background: '#e8f5e9', color: '#2e7d32', borderRadius: '6px', marginBottom: '16px' }}>
          {success}
        </div>
      )}

      {/* KPI Summary Cards */}
      {summary && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          <div style={{ padding: '16px', background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted, #666)', textTransform: 'uppercase', fontWeight: 600 }}>Employees</div>
            <div style={{ fontSize: '24px', fontWeight: 700, marginTop: '4px' }}>{summary.activeEmployees}</div>
            <div style={{ fontSize: '12px', color: '#555', marginTop: '4px' }}>Active ({summary.totalEmployees} total)</div>
          </div>

          <div style={{ padding: '16px', background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted, #666)', textTransform: 'uppercase', fontWeight: 600 }}>Monthly Gross Pay</div>
            <div style={{ fontSize: '22px', fontWeight: 700, marginTop: '4px', color: '#1565c0' }}>{formatCurrency(summary.totalMonthlyGross)}</div>
            <div style={{ fontSize: '12px', color: '#555', marginTop: '4px' }}>Combined payroll</div>
          </div>

          <div style={{ padding: '16px', background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted, #666)', textTransform: 'uppercase', fontWeight: 600 }}>EPF Liability</div>
            <div style={{ fontSize: '22px', fontWeight: 700, marginTop: '4px', color: '#2e7d32' }}>{formatCurrency(summary.totalMonthlyPfLiability)}</div>
            <div style={{ fontSize: '12px', color: '#555', marginTop: '4px' }}>EE 12% + ER 12% (EPS/EPF)</div>
          </div>

          <div style={{ padding: '16px', background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted, #666)', textTransform: 'uppercase', fontWeight: 600 }}>ESIC Liability</div>
            <div style={{ fontSize: '22px', fontWeight: 700, marginTop: '4px', color: '#6a1b9a' }}>{formatCurrency(summary.totalMonthlyEsiLiability)}</div>
            <div style={{ fontSize: '12px', color: '#555', marginTop: '4px' }}>EE 0.75% + ER 3.25%</div>
          </div>

          <div style={{ padding: '16px', background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted, #666)', textTransform: 'uppercase', fontWeight: 600 }}>Professional Tax</div>
            <div style={{ fontSize: '22px', fontWeight: 700, marginTop: '4px', color: '#e65100' }}>{formatCurrency(summary.totalMonthlyPt)}</div>
            <div style={{ fontSize: '12px', color: '#555', marginTop: '4px' }}>State PT collections</div>
          </div>

          <div style={{ padding: '16px', background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted, #666)', textTransform: 'uppercase', fontWeight: 600 }}>Sec 192 Salary TDS</div>
            <div style={{ fontSize: '22px', fontWeight: 700, marginTop: '4px', color: '#d32f2f' }}>{formatCurrency(summary.totalMonthlyTds)}</div>
            <div style={{ fontSize: '12px', color: '#555', marginTop: '4px' }}>Form 24Q withholding</div>
          </div>
        </div>
      )}

      {/* Tabs */}
      <div style={{ display: 'flex', borderBottom: '2px solid var(--border, #e0e0e0)', marginBottom: '20px' }}>
        <button
          onClick={() => setActiveTab('employees')}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'employees' ? 700 : 400,
            borderBottom: activeTab === 'employees' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'employees' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Employees & Salaries ({employees.length})
        </button>
        <button
          onClick={() => setActiveTab('runs')}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'runs' ? 700 : 400,
            borderBottom: activeTab === 'runs' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'runs' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Monthly Payroll Runs ({runs.length})
        </button>
        <button
          onClick={() => setActiveTab('payslips')}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'payslips' ? 700 : 400,
            borderBottom: activeTab === 'payslips' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'payslips' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Payslips & Register {selectedRun ? `(${selectedRun.payrollPeriod})` : ''}
        </button>
        <button
          onClick={() => setActiveTab('ecr')}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'ecr' ? 700 : 400,
            borderBottom: activeTab === 'ecr' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'ecr' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          EPFO ECR Text Export
        </button>
      </div>

      {/* Tab 1: Employees & Salaries */}
      {activeTab === 'employees' && (
        <div style={{ background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px', overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
            <thead>
              <tr style={{ background: 'var(--table-header, #f9f9f9)', borderBottom: '1px solid var(--border, #e0e0e0)' }}>
                <th style={{ padding: '12px' }}>Emp Code & Name</th>
                <th style={{ padding: '12px' }}>Designation / Dept</th>
                <th style={{ padding: '12px' }}>PAN / UAN</th>
                <th style={{ padding: '12px', textAlign: 'right' }}>Basic Salary</th>
                <th style={{ padding: '12px', textAlign: 'right' }}>Gross Pay</th>
                <th style={{ padding: '12px' }}>Statutory Flags</th>
                <th style={{ padding: '12px', textAlign: 'right' }}>Est. Deductions</th>
                <th style={{ padding: '12px', textAlign: 'right' }}>Est. Net Pay</th>
                <th style={{ padding: '12px', textAlign: 'center' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {employees.map(emp => (
                <tr key={emp.id} style={{ borderBottom: '1px solid var(--border, #f0f0f0)' }}>
                  <td style={{ padding: '12px' }}>
                    <div style={{ fontWeight: 600 }}>{emp.name}</div>
                    <div style={{ fontSize: '11px', color: 'var(--muted, #666)' }}>{emp.employeeCode} | Joined {emp.dateOfJoining}</div>
                  </td>
                  <td style={{ padding: '12px' }}>
                    <div>{emp.designation}</div>
                    <div style={{ fontSize: '11px', color: '#666' }}>{emp.department || '-'} | {emp.state}</div>
                  </td>
                  <td style={{ padding: '12px' }}>
                    <div style={{ fontFamily: 'monospace', fontSize: '12px' }}>{emp.pan || '-'}</div>
                    <div style={{ fontSize: '11px', color: '#666', fontFamily: 'monospace' }}>UAN: {emp.uan || '-'}</div>
                  </td>
                  <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600 }}>
                    {formatCurrency(emp.basicSalary)}
                  </td>
                  <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: '#1565c0' }}>
                    {formatCurrency(emp.grossSalary)}
                  </td>
                  <td style={{ padding: '12px' }}>
                    <div style={{ display: 'flex', gap: '4px', flexWrap: 'wrap' }}>
                      <span style={{
                        padding: '2px 5px',
                        borderRadius: '3px',
                        fontSize: '10px',
                        fontWeight: 700,
                        background: emp.isPfEligible ? '#e8f5e9' : '#f5f5f5',
                        color: emp.isPfEligible ? '#2e7d32' : '#999'
                      }}>
                        PF: {emp.isPfEligible ? 'YES' : 'NO'}
                      </span>
                      <span style={{
                        padding: '2px 5px',
                        borderRadius: '3px',
                        fontSize: '10px',
                        fontWeight: 700,
                        background: emp.isEsiEligible && emp.grossSalary <= 21000 ? '#ede7f6' : '#f5f5f5',
                        color: emp.isEsiEligible && emp.grossSalary <= 21000 ? '#4527a0' : '#999'
                      }}>
                        ESI: {emp.isEsiEligible && emp.grossSalary <= 21000 ? 'YES' : 'EXEMPT'}
                      </span>
                      <span style={{
                        padding: '2px 5px',
                        borderRadius: '3px',
                        fontSize: '10px',
                        fontWeight: 700,
                        background: '#e3f2fd',
                        color: '#1565c0'
                      }}>
                        {emp.taxRegime === 'NewRegime' ? 'New 115BAC' : 'Old'}
                      </span>
                    </div>
                  </td>
                  <td style={{ padding: '12px', textAlign: 'right', color: '#c62828' }}>
                    {formatCurrency(emp.estimatedPf + emp.estimatedEsi + emp.estimatedPt + emp.estimatedTds)}
                    <div style={{ fontSize: '10px', color: '#666' }}>
                      PF:{emp.estimatedPf} | ESI:{emp.estimatedEsi} | PT:{emp.estimatedPt}
                    </div>
                  </td>
                  <td style={{ padding: '12px', textAlign: 'right', fontWeight: 700, color: '#2e7d32' }}>
                    {formatCurrency(emp.estimatedNet)}
                  </td>
                  <td style={{ padding: '12px', textAlign: 'center' }}>
                    <button
                      className="ghost small"
                      onClick={() => handleOpenEditEmployee(emp)}
                      title="Edit Employee Salary Structure"
                    >
                      ✏️ Edit
                    </button>
                  </td>
                </tr>
              ))}
              {employees.length === 0 && (
                <tr>
                  <td colSpan={9} style={{ padding: '24px', textAlign: 'center', color: '#777' }}>
                    No employees registered for this client. Click "+ Add Employee" above.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Tab 2: Monthly Payroll Runs */}
      {activeTab === 'runs' && (
        <div style={{ background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px', overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
            <thead>
              <tr style={{ background: 'var(--table-header, #f9f9f9)', borderBottom: '1px solid var(--border, #e0e0e0)' }}>
                <th style={{ padding: '12px' }}>Period</th>
                <th style={{ padding: '12px' }}>Employees</th>
                <th style={{ padding: '12px', textAlign: 'right' }}>Total Gross Pay</th>
                <th style={{ padding: '12px', textAlign: 'right' }}>Net Disbursed</th>
                <th style={{ padding: '12px', textAlign: 'right' }}>EPF (EE+ER)</th>
                <th style={{ padding: '12px', textAlign: 'right' }}>ESIC</th>
                <th style={{ padding: '12px', textAlign: 'right' }}>PT</th>
                <th style={{ padding: '12px' }}>Status</th>
                <th style={{ padding: '12px', textAlign: 'center' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {runs.map(r => (
                <tr key={r.id} style={{ borderBottom: '1px solid var(--border, #f0f0f0)' }}>
                  <td style={{ padding: '12px', fontWeight: 700 }}>
                    {r.payrollPeriod}
                    <div style={{ fontSize: '11px', color: '#666' }}>Processed {new Date(r.processedAt).toLocaleDateString()}</div>
                  </td>
                  <td style={{ padding: '12px' }}>{r.totalEmployees} staff</td>
                  <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600 }}>
                    {formatCurrency(r.totalGrossPay)}
                  </td>
                  <td style={{ padding: '12px', textAlign: 'right', fontWeight: 700, color: '#2e7d32' }}>
                    {formatCurrency(r.totalNetPay)}
                  </td>
                  <td style={{ padding: '12px', textAlign: 'right', color: '#1565c0' }}>
                    {formatCurrency(r.totalEmployeePf + r.totalEmployerPf)}
                  </td>
                  <td style={{ padding: '12px', textAlign: 'right', color: '#6a1b9a' }}>
                    {formatCurrency(r.totalEmployeeEsi + r.totalEmployerEsi)}
                  </td>
                  <td style={{ padding: '12px', textAlign: 'right' }}>
                    {formatCurrency(r.totalPt)}
                  </td>
                  <td style={{ padding: '12px' }}>
                    <span style={{
                      padding: '3px 8px',
                      borderRadius: '12px',
                      fontSize: '11px',
                      fontWeight: 600,
                      background: r.status === 'Disbursed' ? '#e8f5e9' : (r.status === 'Approved' ? '#e3f2fd' : '#fff3e0'),
                      color: r.status === 'Disbursed' ? '#2e7d32' : (r.status === 'Approved' ? '#1565c0' : '#e65100')
                    }}>
                      {r.status}
                    </span>
                  </td>
                  <td style={{ padding: '12px', textAlign: 'center' }}>
                    <div style={{ display: 'flex', gap: '6px', justifyContent: 'center' }}>
                      <button
                        className="secondary small"
                        onClick={() => handleSelectRun(r)}
                        title="View detailed payslips"
                      >
                        📄 Payslips
                      </button>
                      <button
                        className="ghost small"
                        onClick={() => handleExportEcr(r.id)}
                        title="Export EPFO ECR text file"
                      >
                        📥 ECR
                      </button>
                      {r.status === 'Draft' && (!can || can('payroll.process')) && (
                        <button
                          className="primary small"
                          onClick={() => handleUpdateRunStatus(r.id, 'Approved')}
                          title="Approve Run"
                        >
                          ✓ Approve
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
              {runs.length === 0 && (
                <tr>
                  <td colSpan={9} style={{ padding: '24px', textAlign: 'center', color: '#777' }}>
                    No payroll runs recorded yet. Click "⚡ Process Salary Run" to calculate one.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Tab 3: Payslips & Salary Register */}
      {activeTab === 'payslips' && (
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <div>
              <h3 style={{ margin: 0 }}>
                {selectedRun ? `Salary Register for Period ${selectedRun.payrollPeriod}` : 'Select a payroll run from the Runs tab'}
              </h3>
            </div>
            {selectedRun && (
              <div style={{ display: 'flex', gap: '10px' }}>
                <button className="secondary small" onClick={() => handleExportEcr(selectedRun.id)}>
                  📥 EPFO ECR File
                </button>
                <button className="ghost small" onClick={() => window.print()}>
                  🖨️ Print Register
                </button>
              </div>
            )}
          </div>

          <div style={{ background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px', overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
              <thead>
                <tr style={{ background: 'var(--table-header, #f9f9f9)', borderBottom: '1px solid var(--border, #e0e0e0)' }}>
                  <th style={{ padding: '12px' }}>Employee</th>
                  <th style={{ padding: '12px' }}>Days</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Basic Salary</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>HRA / Allow</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Gross Pay</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>EE PF (12%)</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>ESIC (0.75%)</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>PT</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>TDS (192)</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Total Deductions</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Net Salary</th>
                </tr>
              </thead>
              <tbody>
                {payslips.map(p => (
                  <tr key={p.id} style={{ borderBottom: '1px solid var(--border, #f0f0f0)' }}>
                    <td style={{ padding: '12px' }}>
                      <div style={{ fontWeight: 600 }}>{p.employeeName}</div>
                      <div style={{ fontSize: '11px', color: '#666' }}>{p.employeeCode} | UAN: {p.uan || '-'}</div>
                    </td>
                    <td style={{ padding: '12px' }}>
                      {p.daysWorked}/{p.daysInMonth}
                    </td>
                    <td style={{ padding: '12px', textAlign: 'right' }}>
                      {formatCurrency(p.basicSalary)}
                    </td>
                    <td style={{ padding: '12px', textAlign: 'right' }}>
                      {formatCurrency(p.hra + p.specialAllowance + p.bonusIncentive)}
                    </td>
                    <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600 }}>
                      {formatCurrency(p.grossSalary)}
                    </td>
                    <td style={{ padding: '12px', textAlign: 'right', color: '#1565c0' }}>
                      {formatCurrency(p.employeePf)}
                    </td>
                    <td style={{ padding: '12px', textAlign: 'right', color: '#6a1b9a' }}>
                      {formatCurrency(p.employeeEsi)}
                    </td>
                    <td style={{ padding: '12px', textAlign: 'right' }}>
                      {formatCurrency(p.professionalTax)}
                    </td>
                    <td style={{ padding: '12px', textAlign: 'right', color: '#d32f2f' }}>
                      {formatCurrency(p.salaryTds)}
                    </td>
                    <td style={{ padding: '12px', textAlign: 'right', color: '#c62828', fontWeight: 600 }}>
                      {formatCurrency(p.totalDeductions)}
                    </td>
                    <td style={{ padding: '12px', textAlign: 'right', fontWeight: 700, color: '#2e7d32' }}>
                      {formatCurrency(p.netSalary)}
                    </td>
                  </tr>
                ))}
                {payslips.length === 0 && (
                  <tr>
                    <td colSpan={11} style={{ padding: '24px', textAlign: 'center', color: '#777' }}>
                      No payslips found for this run.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Tab 4: EPFO ECR Export */}
      {activeTab === 'ecr' && (
        <div style={{ background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px', padding: '20px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <div>
              <h3 style={{ margin: '0 0 4px 0' }}>EPFO Electronic Challan cum Return (ECR)</h3>
              <p style={{ margin: 0, color: '#666', fontSize: '13px' }}>
                Standard #~# delimited return file ready for direct upload on the Unified EPFO Employer Portal.
              </p>
            </div>
            {ecrData && (
              <div style={{ display: 'flex', gap: '10px' }}>
                <button
                  className="secondary"
                  onClick={() => {
                    navigator.clipboard.writeText(ecrData.ecrText)
                    setSuccess('ECR file text copied to clipboard!')
                  }}
                >
                  📋 Copy Text
                </button>
                <button
                  className="primary"
                  onClick={() => {
                    const blob = new Blob([ecrData.ecrText], { type: 'text/plain;charset=utf-8' })
                    const url = URL.createObjectURL(blob)
                    const a = document.createElement('a')
                    a.href = url
                    a.download = `ECR_${ecrData.clientName.replace(/\s+/g, '_')}_${ecrData.payrollPeriod}.txt`
                    a.click()
                    URL.revokeObjectURL(url)
                  }}
                >
                  💾 Download ECR.txt
                </button>
              </div>
            )}
          </div>

          {ecrData ? (
            <div>
              <div style={{ display: 'flex', gap: '20px', background: '#f5f5f5', padding: '12px 16px', borderRadius: '6px', marginBottom: '16px', fontSize: '13px' }}>
                <div><strong>Period:</strong> {ecrData.payrollPeriod}</div>
                <div><strong>Member Count:</strong> {ecrData.employeeCount}</div>
                <div><strong>Total Wages:</strong> {formatCurrency(ecrData.totalWages)}</div>
                <div><strong>Total EE PF (12%):</strong> {formatCurrency(ecrData.totalEmployeePf)}</div>
                <div><strong>Total ER EPS (8.33%):</strong> {formatCurrency(ecrData.totalEmployerEps)}</div>
                <div><strong>Total ER EPF (3.67%):</strong> {formatCurrency(ecrData.totalEmployerEpf)}</div>
              </div>

              <textarea
                value={ecrData.ecrText}
                readOnly
                rows={12}
                style={{
                  width: '100%',
                  fontFamily: 'monospace',
                  fontSize: '12px',
                  padding: '12px',
                  borderRadius: '6px',
                  border: '1px solid #ccc',
                  background: '#fafafa'
                }}
              />
            </div>
          ) : (
            <div style={{ textAlign: 'center', padding: '30px', color: '#777' }}>
              Select a payroll run from the "Monthly Payroll Runs" tab and click "📥 ECR" to generate the return file.
            </div>
          )}
        </div>
      )}

      {/* Modal 1: Add / Edit Employee */}
      {showEmployeeModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', padding: '24px', borderRadius: '8px', maxWidth: '650px', width: '90%', maxHeight: '90vh', overflowY: 'auto' }}>
            <h2 style={{ marginTop: 0 }}>{editingEmployeeId ? 'Edit Employee Details' : 'Register New Employee'}</h2>
            <form onSubmit={handleSaveEmployee}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '14px' }}>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Employee Code *</label>
                  <input
                    type="text"
                    value={employeeForm.employeeCode}
                    onChange={e => setEmployeeForm({ ...employeeForm, employeeCode: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Full Name *</label>
                  <input
                    type="text"
                    value={employeeForm.name}
                    onChange={e => setEmployeeForm({ ...employeeForm, name: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '14px' }}>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>PAN (10 digits)</label>
                  <input
                    type="text"
                    value={employeeForm.pan || ''}
                    onChange={e => setEmployeeForm({ ...employeeForm, pan: e.target.value.toUpperCase() })}
                    placeholder="ABCDE1234F"
                    maxLength={10}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>EPF UAN (12 digits)</label>
                  <input
                    type="text"
                    value={employeeForm.uan || ''}
                    onChange={e => setEmployeeForm({ ...employeeForm, uan: e.target.value })}
                    placeholder="100XXXXXXXXX"
                    maxLength={12}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '14px' }}>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Designation *</label>
                  <input
                    type="text"
                    value={employeeForm.designation}
                    onChange={e => setEmployeeForm({ ...employeeForm, designation: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>State (For PT Slabs)</label>
                  <select
                    value={employeeForm.state}
                    onChange={e => setEmployeeForm({ ...employeeForm, state: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    <option value="Maharashtra">Maharashtra (₹200/₹300 Feb)</option>
                    <option value="Karnataka">Karnataka (₹200 &gt; ₹15k)</option>
                    <option value="Telangana">Telangana (₹150/₹200)</option>
                    <option value="West Bengal">West Bengal</option>
                    <option value="Delhi">Delhi (No PT)</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '14px' }}>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Date of Joining *</label>
                  <input
                    type="date"
                    value={employeeForm.dateOfJoining}
                    onChange={e => setEmployeeForm({ ...employeeForm, dateOfJoining: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Tax Regime (Sec 192)</label>
                  <select
                    value={employeeForm.taxRegime}
                    onChange={e => setEmployeeForm({ ...employeeForm, taxRegime: e.target.value as any })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    <option value="NewRegime">New Regime (Sec 115BAC - Default)</option>
                    <option value="OldRegime">Old Regime (With Deductions)</option>
                  </select>
                </div>
              </div>

              {/* Salary Structure */}
              <div style={{ background: '#f9f9f9', padding: '12px', borderRadius: '6px', marginBottom: '14px' }}>
                <div style={{ fontWeight: 700, fontSize: '13px', marginBottom: '10px' }}>Monthly Salary Components (₹)</div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '10px' }}>
                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '2px' }}>Basic Salary *</label>
                    <input
                      type="number"
                      value={employeeForm.basicSalary}
                      onChange={e => setEmployeeForm({ ...employeeForm, basicSalary: parseFloat(e.target.value) || 0 })}
                      required
                      style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid #ccc' }}
                    />
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '2px' }}>HRA</label>
                    <input
                      type="number"
                      value={employeeForm.hra}
                      onChange={e => setEmployeeForm({ ...employeeForm, hra: parseFloat(e.target.value) || 0 })}
                      style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid #ccc' }}
                    />
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '2px' }}>Special Allowance</label>
                    <input
                      type="number"
                      value={employeeForm.specialAllowance}
                      onChange={e => setEmployeeForm({ ...employeeForm, specialAllowance: parseFloat(e.target.value) || 0 })}
                      style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid #ccc' }}
                    />
                  </div>
                </div>
              </div>

              {/* Statutory Toggles */}
              <div style={{ display: 'flex', gap: '20px', marginBottom: '16px' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={employeeForm.isPfEligible}
                    onChange={e => setEmployeeForm({ ...employeeForm, isPfEligible: e.target.checked })}
                  />
                  EPF Eligible (12%)
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={employeeForm.isEsiEligible}
                    onChange={e => setEmployeeForm({ ...employeeForm, isEsiEligible: e.target.checked })}
                  />
                  ESIC Eligible (&le; ₹21k)
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={employeeForm.isPtEligible}
                    onChange={e => setEmployeeForm({ ...employeeForm, isPtEligible: e.target.checked })}
                  />
                  Professional Tax Eligible
                </label>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <button type="button" className="ghost" onClick={() => setShowEmployeeModal(false)}>Cancel</button>
                <button type="submit" className="primary" disabled={loading}>
                  {loading ? 'Saving…' : 'Save Employee'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal 2: Process Monthly Payroll */}
      {showProcessModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', padding: '24px', borderRadius: '8px', maxWidth: '450px', width: '90%' }}>
            <h2 style={{ marginTop: 0 }}>Process Monthly Payroll Run</h2>
            <p style={{ color: '#666', fontSize: '13px' }}>
              Calculates monthly wages, pro-rata attendance, EPF, ESIC, PT, and Section 192 Salary TDS for active employees.
            </p>
            <form onSubmit={handleProcessRun}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '14px' }}>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Year *</label>
                  <input
                    type="number"
                    value={runForm.year}
                    onChange={e => setRunForm({ ...runForm, year: parseInt(e.target.value) || 2024 })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Month *</label>
                  <select
                    value={runForm.month}
                    onChange={e => setRunForm({ ...runForm, month: parseInt(e.target.value) || 1 })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    {[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12].map(m => (
                      <option key={m} value={m}>
                        {new Date(2024, m - 1, 1).toLocaleString('default', { month: 'long' })}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Notes</label>
                <input
                  type="text"
                  value={runForm.notes || ''}
                  onChange={e => setRunForm({ ...runForm, notes: e.target.value })}
                  placeholder="e.g. Regular monthly payroll run"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <button type="button" className="ghost" onClick={() => setShowProcessModal(false)}>Cancel</button>
                <button type="submit" className="primary" disabled={loading}>
                  {loading ? 'Executing…' : '⚡ Execute Run'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
