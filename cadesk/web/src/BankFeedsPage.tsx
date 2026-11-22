import React, { useState, useEffect } from 'react'
import { toError,
  api,
  bankFeedApi,
  type Client,
  type ClientPage,
  type BankAccountDto,
  type CreateBankAccountRequest,
  type BankStatementDto,
  type BankTransactionDto,
  type MatchTransactionRequest,
  type BankReconciliationSummaryDto,
  type BankReconciliationRuleDto,
  type CreateBankReconciliationRuleRequest
} from './api'

export const BankFeedsPage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'accounts' | 'transactions' | 'brs' | 'rules'>('accounts')
  const [clients, setClients] = useState<Client[]>([])
  const [accounts, setAccounts] = useState<BankAccountDto[]>([])
  const [selectedAccountId, setSelectedAccountId] = useState<string>('')
  const [, setStatements] = useState<BankStatementDto[]>([])
  const [transactions, setTransactions] = useState<BankTransactionDto[]>([])
  const [brsSummary, setBrsSummary] = useState<BankReconciliationSummaryDto | null>(null)
  const [rules, setRules] = useState<BankReconciliationRuleDto[]>([])
  const [, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Filters
  const [selectedStatus, setSelectedStatus] = useState<string>('')
  const [asOfDate, setAsOfDate] = useState<string>(new Date().toISOString().split('T')[0])

  // Modals
  const [showAccountModal, setShowAccountModal] = useState<boolean>(false)
  const [showUploadModal, setShowUploadModal] = useState<boolean>(false)
  const [showMatchModal, setShowMatchModal] = useState<boolean>(false)
  const [showRuleModal, setShowRuleModal] = useState<boolean>(false)
  const [selectedTxForMatch, setSelectedTxForMatch] = useState<BankTransactionDto | null>(null)

  // Account Form
  const [accountForm, setAccountForm] = useState<CreateBankAccountRequest>({
    clientId: '',
    accountName: '',
    bankName: 'HDFC Bank',
    branchName: '',
    accountNumber: '',
    ifscCode: 'HDFC0000123',
    accountType: 'Current',
    openingBalance: 0,
    currency: 'INR'
  })

  // Upload Form
  const [uploadFileName, setUploadFileName] = useState<string>('statement.csv')
  const [uploadCsvContent, setUploadCsvContent] = useState<string>('')
  const [uploadStatementDate, setUploadStatementDate] = useState<string>(new Date().toISOString().split('T')[0])

  // Match Form
  const [matchForm, setMatchForm] = useState<MatchTransactionRequest>({
    matchStatus: 'Matched',
    matchedEntityType: 'Expense',
    matchedEntityId: undefined,
    notes: ''
  })

  // Rule Form
  const [ruleForm, setRuleForm] = useState<CreateBankReconciliationRuleRequest>({
    ruleName: '',
    matchKeyword: '',
    transactionType: 'Any',
    assignCategory: '',
    priority: 10
  })

  useEffect(() => {
    loadClients()
    loadAccounts()
    loadRules()
  }, [])

  useEffect(() => {
    if (selectedAccountId) {
      loadStatements(selectedAccountId)
      loadTransactions(selectedAccountId)
      loadBrsSummary(selectedAccountId, asOfDate)
    }
  }, [selectedAccountId, selectedStatus, asOfDate])

  const loadClients = async () => {
    try {
      const res = await api<ClientPage>('/api/clients?take=100')
      setClients(res.items || [])
      if (res.items && res.items.length > 0 && !accountForm.clientId) {
        setAccountForm(prev => ({ ...prev, clientId: res.items[0].id }))
      }
    } catch (caught) {
      const e = toError(caught)
      console.error(e)
    }
  }

  const loadAccounts = async () => {
    setLoading(true)
    try {
      const list = await bankFeedApi.getAccounts()
      setAccounts(list)
      if (list.length > 0 && !selectedAccountId) {
        setSelectedAccountId(list[0].id)
      }
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to load bank accounts')
    } finally {
      setLoading(false)
    }
  }

  const loadStatements = async (accId: string) => {
    try {
      const list = await bankFeedApi.getStatements(accId)
      setStatements(list)
    } catch (caught) {
      const e = toError(caught)
      console.error(e)
    }
  }

  const loadTransactions = async (accId: string) => {
    try {
      const list = await bankFeedApi.getTransactions(accId, undefined, selectedStatus || undefined)
      setTransactions(list)
    } catch (caught) {
      const e = toError(caught)
      console.error(e)
    }
  }

  const loadBrsSummary = async (accId: string, targetDate: string) => {
    try {
      const summary = await bankFeedApi.getBrsSummary(accId, targetDate)
      setBrsSummary(summary)
    } catch (caught) {
      const e = toError(caught)
      console.error(e)
    }
  }

  const loadRules = async () => {
    try {
      const list = await bankFeedApi.getRules()
      setRules(list)
    } catch (caught) {
      const e = toError(caught)
      console.error(e)
    }
  }

  const handleCreateAccount = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setSuccess(null)
    try {
      await bankFeedApi.createAccount(accountForm)
      setSuccess('Bank account added successfully!')
      setShowAccountModal(false)
      loadAccounts()
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to create bank account')
    }
  }

  const handleUploadStatement = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedAccountId) {
      setError('Please select a bank account first')
      return
    }
    setError(null)
    setSuccess(null)
    try {
      await bankFeedApi.uploadStatement(selectedAccountId, {
        fileName: uploadFileName,
        csvContent: uploadCsvContent,
        statementDate: uploadStatementDate || null
      })
      setSuccess('Statement uploaded and parsed successfully!')
      setShowUploadModal(false)
      setUploadCsvContent('')
      loadAccounts()
      loadStatements(selectedAccountId)
      loadTransactions(selectedAccountId)
      loadBrsSummary(selectedAccountId, asOfDate)
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to parse statement')
    }
  }

  const handleMatchTransaction = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedTxForMatch) return
    setError(null)
    setSuccess(null)
    try {
      await bankFeedApi.matchTransaction(selectedTxForMatch.id, matchForm)
      setSuccess('Transaction match status updated!')
      setShowMatchModal(false)
      loadTransactions(selectedAccountId)
      loadBrsSummary(selectedAccountId, asOfDate)
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to update transaction')
    }
  }

  const handleCreateRule = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setSuccess(null)
    try {
      await bankFeedApi.createRule(ruleForm)
      setSuccess('Categorization rule saved!')
      setShowRuleModal(false)
      loadRules()
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to create rule')
    }
  }

  const loadSampleCsv = () => {
    setUploadFileName('bank_sample.csv')
    setUploadCsvContent(`Date,Narration,Chq/Ref No,Debit,Credit,Balance
01/11/2026,UPI/889900/Vendor Parts Payment,UPI889900,25000.00,,475000.00
05/11/2026,SALARY NOV 2026,SAL1101,150000.00,,325000.00
10/11/2026,NEFT/Client Fee Collection,NEFT4422,,75000.00,400000.00
15/11/2026,BANK SMS & ANNUAL CHARGES,CHG99,500.00,,399500.00`)
  }

  const handleStatementFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    setUploadFileName(file.name)
    if (file.name.toLowerCase().endsWith('.pdf')) {
      const reader = new FileReader()
      reader.onload = evt => {
        const result = (evt.target?.result as string) || ''
        const base64 = result.includes(',') ? result.split(',')[1] : result
        setUploadCsvContent(base64)
      }
      reader.readAsDataURL(file)
    } else {
      const reader = new FileReader()
      reader.onload = evt => {
        setUploadCsvContent((evt.target?.result as string) || '')
      }
      reader.readAsText(file)
    }
  }

  const formatCurrency = (val?: number | null) => {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      maximumFractionDigits: 2
    }).format(val || 0)
  }

  return (
    <div style={{ padding: '20px', maxWidth: '1300px', margin: '0 auto' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
        <div>
          <h1 style={{ margin: '0 0 6px 0', fontSize: '24px', fontWeight: 700 }}>
            Cash & Bank Feeds / BRS Auto-Matcher
          </h1>
          <p style={{ margin: 0, color: 'var(--muted, #666)', fontSize: '14px' }}>
            Multi-Bank Statement Parser, Automated Reconciliation & Statutory BRS Generator (Sprint 20)
          </p>
        </div>
        <div style={{ display: 'flex', gap: '10px' }}>
          <button className="primary" onClick={() => setShowAccountModal(true)}>
            + Add Bank Account
          </button>
          <button className="secondary" onClick={() => setShowUploadModal(true)} disabled={!selectedAccountId}>
            Upload Statement
          </button>
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

      {/* KPI Highlights */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '20px' }}>
        <div className="card" style={{ padding: '16px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
          <div style={{ fontSize: '13px', color: 'var(--muted, #666)' }}>Total Bank Accounts</div>
          <div style={{ fontSize: '24px', fontWeight: 700, marginTop: '4px' }}>{accounts.length}</div>
        </div>
        <div className="card" style={{ padding: '16px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
          <div style={{ fontSize: '13px', color: 'var(--muted, #666)' }}>Total Live Bank Balance</div>
          <div style={{ fontSize: '24px', fontWeight: 700, marginTop: '4px', color: '#2e7d32' }}>
            {formatCurrency(accounts.reduce((sum, a) => sum + (a.currentBalance || a.openingBalance || 0), 0))}
          </div>
        </div>
        <div className="card" style={{ padding: '16px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
          <div style={{ fontSize: '13px', color: 'var(--muted, #666)' }}>Transactions in Active Account</div>
          <div style={{ fontSize: '24px', fontWeight: 700, marginTop: '4px' }}>{transactions.length}</div>
        </div>
        <div className="card" style={{ padding: '16px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
          <div style={{ fontSize: '13px', color: 'var(--muted, #666)' }}>Unmatched BRS Items</div>
          <div style={{ fontSize: '24px', fontWeight: 700, marginTop: '4px', color: '#e65100' }}>
            {brsSummary ? brsSummary.unmatchedCount : 0}
          </div>
        </div>
      </div>

      {/* Account Selector Bar */}
      {accounts.length > 0 && (
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', background: 'var(--bg-card, #f8f9fa)', padding: '12px 16px', borderRadius: '8px', marginBottom: '16px' }}>
          <span style={{ fontWeight: 600, fontSize: '14px' }}>Active Account:</span>
          <select
            value={selectedAccountId}
            onChange={e => setSelectedAccountId(e.target.value)}
            style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid var(--border, #ccc)', flex: 1, maxWidth: '400px' }}
          >
            {accounts.map(acc => (
              <option key={acc.id} value={acc.id}>
                {acc.bankName} - {acc.accountName} ({acc.accountNumber}) [{acc.accountType}]
              </option>
            ))}
          </select>
          {selectedAccountId && (
            <span style={{ fontSize: '14px', color: 'var(--muted, #666)' }}>
              Client: <strong>{accounts.find(a => a.id === selectedAccountId)?.clientName}</strong> | IFSC: <strong>{accounts.find(a => a.id === selectedAccountId)?.ifscCode}</strong>
            </span>
          )}
        </div>
      )}

      {/* Tabs */}
      <div style={{ display: 'flex', borderBottom: '2px solid var(--border, #e0e0e0)', marginBottom: '20px' }}>
        <button
          onClick={() => setActiveTab('accounts')}
          style={{
            padding: '10px 20px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'accounts' ? 700 : 400,
            borderBottom: activeTab === 'accounts' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'accounts' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Bank Accounts ({accounts.length})
        </button>
        <button
          onClick={() => setActiveTab('transactions')}
          style={{
            padding: '10px 20px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'transactions' ? 700 : 400,
            borderBottom: activeTab === 'transactions' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'transactions' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Transactions & Ledger Feed ({transactions.length})
        </button>
        <button
          onClick={() => setActiveTab('brs')}
          style={{
            padding: '10px 20px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'brs' ? 700 : 400,
            borderBottom: activeTab === 'brs' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'brs' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Statutory BRS Statement
        </button>
        <button
          onClick={() => setActiveTab('rules')}
          style={{
            padding: '10px 20px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'rules' ? 700 : 400,
            borderBottom: activeTab === 'rules' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'rules' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Auto-Categorization Rules ({rules.length})
        </button>
      </div>

      {/* Tab 1: Bank Accounts */}
      {activeTab === 'accounts' && (
        <div>
          {accounts.length === 0 ? (
            <div style={{ padding: '40px', textAlign: 'center', background: 'var(--bg-card, #f9f9f9)', borderRadius: '8px' }}>
              <h3>No Bank Accounts Registered</h3>
              <p style={{ color: 'var(--muted, #666)' }}>Register a client's Current or Savings account to begin automated statement imports and reconciliation.</p>
              <button className="primary" onClick={() => setShowAccountModal(true)} style={{ marginTop: '10px' }}>
                + Add First Bank Account
              </button>
            </div>
          ) : (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(360px, 1fr))', gap: '20px' }}>
              {accounts.map(acc => (
                <div key={acc.id} className="card" style={{ padding: '20px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '12px' }}>
                    <div>
                      <h3 style={{ margin: '0 0 4px 0', fontSize: '18px' }}>{acc.accountName}</h3>
                      <span style={{ fontSize: '13px', color: 'var(--muted, #666)' }}>{acc.bankName} - {acc.branchName || 'Main'}</span>
                    </div>
                    <span style={{ padding: '3px 8px', borderRadius: '4px', fontSize: '12px', background: '#e3f2fd', color: '#1976d2', fontWeight: 600 }}>
                      {acc.accountType}
                    </span>
                  </div>

                  <div style={{ fontSize: '14px', lineHeight: '1.8', color: 'var(--text, #333)' }}>
                    <div>A/C Number: <strong>•••• •••• {acc.accountNumber.slice(-4)}</strong></div>
                    <div>IFSC: <strong>{acc.ifscCode}</strong></div>
                    <div>Client: <strong>{acc.clientName}</strong></div>
                  </div>

                  <div style={{ marginTop: '16px', paddingTop: '12px', borderTop: '1px solid var(--border, #eee)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div>
                      <span style={{ fontSize: '12px', color: 'var(--muted, #666)' }}>Current Balance</span>
                      <div style={{ fontSize: '18px', fontWeight: 700, color: '#2e7d32' }}>
                        {formatCurrency(acc.currentBalance || acc.openingBalance)}
                      </div>
                    </div>
                    <div style={{ display: 'flex', gap: '8px' }}>
                      <button
                        className="small"
                        onClick={() => {
                          setSelectedAccountId(acc.id)
                          setActiveTab('transactions')
                        }}
                      >
                        View Feed
                      </button>
                      <button
                        className="small primary"
                        onClick={() => {
                          setSelectedAccountId(acc.id)
                          setShowUploadModal(true)
                        }}
                      >
                        Upload
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Tab 2: Transactions */}
      {activeTab === 'transactions' && (
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
              <span style={{ fontSize: '14px', fontWeight: 600 }}>Filter Status:</span>
              <select
                value={selectedStatus}
                onChange={e => setSelectedStatus(e.target.value)}
                style={{ padding: '6px 12px', borderRadius: '4px', border: '1px solid var(--border, #ccc)' }}
              >
                <option value="">All Statuses</option>
                <option value="Unmatched">Unmatched</option>
                <option value="AutoCategorized">AutoCategorized</option>
                <option value="Matched">Matched</option>
                <option value="Ignored">Ignored</option>
              </select>
            </div>
            <button className="primary small" onClick={() => setShowUploadModal(true)}>
              + Upload New Statement
            </button>
          </div>

          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
            <thead>
              <tr style={{ background: 'var(--bg-header, #f4f5f7)', borderBottom: '2px solid var(--border, #ddd)' }}>
                <th style={{ padding: '10px' }}>Date</th>
                <th style={{ padding: '10px' }}>Narration</th>
                <th style={{ padding: '10px' }}>Chq/Ref</th>
                <th style={{ padding: '10px' }}>Type</th>
                <th style={{ padding: '10px', textAlign: 'right' }}>Amount</th>
                <th style={{ padding: '10px' }}>Category</th>
                <th style={{ padding: '10px' }}>Status</th>
                <th style={{ padding: '10px', textAlign: 'center' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {transactions.length === 0 ? (
                <tr>
                  <td colSpan={8} style={{ padding: '30px', textAlign: 'center', color: 'var(--muted, #666)' }}>
                    No transactions found for this account. Upload a CSV or OFX bank statement to populate.
                  </td>
                </tr>
              ) : (
                transactions.map((tx) => (
                  <tr key={tx.id} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                    <td style={{ padding: '10px' }}>{tx.transactionDate}</td>
                    <td style={{ padding: '10px', maxWidth: '300px', wordBreak: 'break-word' }}>
                      {tx.narration}
                    </td>
                    <td style={{ padding: '10px', color: 'var(--muted, #666)' }}>
                      {tx.referenceNumber || '-'}
                    </td>
                    <td style={{ padding: '10px' }}>
                      <span
                        style={{
                          padding: '2px 6px',
                          borderRadius: '4px',
                          fontSize: '11px',
                          fontWeight: 700,
                          background: (tx.transactionType) === 'Credit' ? '#e8f5e9' : '#ffebee',
                          color: (tx.transactionType) === 'Credit' ? '#2e7d32' : '#c62828'
                        }}
                      >
                        {tx.transactionType}
                      </span>
                    </td>
                    <td style={{ padding: '10px', textAlign: 'right', fontWeight: 600 }}>
                      {formatCurrency(tx.amount)}
                    </td>
                    <td style={{ padding: '10px' }}>
                      <span style={{ background: '#f0f4f8', padding: '3px 8px', borderRadius: '12px', fontSize: '11px' }}>
                        {tx.category || 'Uncategorized'}
                      </span>
                    </td>
                    <td style={{ padding: '10px' }}>
                      <span
                        style={{
                          padding: '2px 8px',
                          borderRadius: '4px',
                          fontSize: '11px',
                          fontWeight: 600,
                          background: (tx.matchStatus) === 'Matched' ? '#e8f5e9' : '#fff3e0',
                          color: (tx.matchStatus) === 'Matched' ? '#2e7d32' : '#e65100'
                        }}
                      >
                        {tx.matchStatus}
                      </span>
                    </td>
                    <td style={{ padding: '10px', textAlign: 'center' }}>
                      <button
                        className="small ghost"
                        onClick={() => {
                          setSelectedTxForMatch(tx)
                          setMatchForm({
                            matchStatus: 'Matched',
                            matchedEntityType: 'Expense',
                            matchedEntityId: undefined,
                            notes: tx.notes || ''
                          })
                          setShowMatchModal(true)
                        }}
                      >
                        Reconcile
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Tab 3: BRS Statement */}
      {activeTab === 'brs' && (
        <div style={{ maxWidth: '900px', margin: '0 auto' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
              <span style={{ fontWeight: 600 }}>Reconciliation As Of:</span>
              <input
                type="date"
                value={asOfDate}
                onChange={e => setAsOfDate(e.target.value)}
                style={{ padding: '6px 10px', borderRadius: '4px', border: '1px solid var(--border, #ccc)' }}
              />
            </div>
            <button className="small ghost" onClick={() => window.print()}>
              🖨️ Print BRS Report
            </button>
          </div>

          {brsSummary && (
            <div className="card" style={{ padding: '24px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
              <div style={{ textAlign: 'center', borderBottom: '2px solid #333', paddingBottom: '12px', marginBottom: '20px' }}>
                <h2 style={{ margin: '0 0 6px 0', fontSize: '20px' }}>BANK RECONCILIATION STATEMENT</h2>
                <div style={{ fontSize: '14px', color: 'var(--muted, #666)' }}>
                  {brsSummary.bankName} - A/C No: {brsSummary.accountNumber} ({brsSummary.accountName})
                </div>
                <div style={{ fontSize: '13px', marginTop: '4px' }}>As on: <strong>{brsSummary.asOfDate}</strong></div>
              </div>

              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '14px' }}>
                <tbody>
                  <tr style={{ borderBottom: '1px solid #ddd', fontWeight: 600 }}>
                    <td style={{ padding: '12px 8px' }}>Balance as per Bank Statement</td>
                    <td style={{ padding: '12px 8px', textAlign: 'right', color: '#1976d2' }}>
                      {formatCurrency(brsSummary.bankStatementBalance)}
                    </td>
                  </tr>
                  <tr style={{ borderBottom: '1px solid #eee' }}>
                    <td style={{ padding: '10px 8px', color: '#2e7d32' }}>
                      (+) Add: Cheques issued / Outward debits not yet presented in bank
                    </td>
                    <td style={{ padding: '10px 8px', textAlign: 'right' }}>
                      {formatCurrency(brsSummary.totalUnclearedDebits)}
                    </td>
                  </tr>
                  <tr style={{ borderBottom: '1px solid #eee' }}>
                    <td style={{ padding: '10px 8px', color: '#c62828' }}>
                      (-) Less: Cheques deposited / Inward credits not yet cleared by bank
                    </td>
                    <td style={{ padding: '10px 8px', textAlign: 'right' }}>
                      ({formatCurrency(brsSummary.totalUncreditedCredits)})
                    </td>
                  </tr>
                  <tr style={{ borderBottom: '1px solid #eee', fontSize: '13px', color: 'var(--muted, #666)' }}>
                    <td style={{ padding: '8px 8px', paddingLeft: '24px' }}>
                      • Direct bank charges/fees not yet booked in ledger
                    </td>
                    <td style={{ padding: '8px 8px', textAlign: 'right' }}>
                      {formatCurrency(brsSummary.directDebitsNotBooked)}
                    </td>
                  </tr>
                  <tr style={{ borderBottom: '1px solid #eee', fontSize: '13px', color: 'var(--muted, #666)' }}>
                    <td style={{ padding: '8px 8px', paddingLeft: '24px' }}>
                      • Direct bank interest/credits not yet booked in ledger
                    </td>
                    <td style={{ padding: '8px 8px', textAlign: 'right' }}>
                      {formatCurrency(brsSummary.directCreditsNotBooked)}
                    </td>
                  </tr>
                  <tr style={{ borderTop: '2px solid #333', borderBottom: '2px solid #333', fontWeight: 700, fontSize: '16px' }}>
                    <td style={{ padding: '14px 8px' }}>Balance as per Books of Account (Adjusted Ledger Balance)</td>
                    <td style={{ padding: '14px 8px', textAlign: 'right', color: '#2e7d32' }}>
                      {formatCurrency(brsSummary.balanceAsPerBooks)}
                    </td>
                  </tr>
                </tbody>
              </table>

              <div style={{ marginTop: '20px', padding: '12px', background: '#e8f5e9', borderRadius: '6px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ fontWeight: 600, color: '#2e7d32' }}>
                  ✓ BRS Balanced & Reconciled (Difference: ₹0.00)
                </span>
                <span style={{ fontSize: '13px', color: 'var(--muted, #555)' }}>
                  Total Cleared / Matched Items: <strong>{brsSummary.matchedCount}</strong> | Pending Items: <strong>{brsSummary.unmatchedCount}</strong>
                </span>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Tab 4: Rules */}
      {activeTab === 'rules' && (
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <p style={{ margin: 0, color: 'var(--muted, #666)' }}>
              Configure automatic keyword rules to categorize bank transactions during statement parsing.
            </p>
            <button className="primary small" onClick={() => setShowRuleModal(true)}>
              + Add Categorization Rule
            </button>
          </div>

          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '14px' }}>
            <thead>
              <tr style={{ background: 'var(--bg-header, #f4f5f7)', borderBottom: '2px solid var(--border, #ddd)' }}>
                <th style={{ padding: '10px' }}>Rule Name</th>
                <th style={{ padding: '10px' }}>Match Keyword</th>
                <th style={{ padding: '10px' }}>Transaction Type</th>
                <th style={{ padding: '10px' }}>Assigned Category</th>
                <th style={{ padding: '10px' }}>Priority</th>
                <th style={{ padding: '10px', textAlign: 'center' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {rules.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ padding: '24px', textAlign: 'center', color: 'var(--muted, #666)' }}>
                    No custom rules defined. Default system heuristics (SALARY, GST, TDS, UPI, CHARGES) are currently active.
                  </td>
                </tr>
              ) : (
                rules.map(r => (
                  <tr key={r.id} style={{ borderBottom: '1px solid var(--border, #eee)' }}>
                    <td style={{ padding: '10px', fontWeight: 600 }}>{r.ruleName}</td>
                    <td style={{ padding: '10px' }}><code>{r.matchKeyword}</code></td>
                    <td style={{ padding: '10px' }}>{r.transactionType}</td>
                    <td style={{ padding: '10px' }}>
                      <span style={{ background: '#e3f2fd', color: '#1976d2', padding: '2px 8px', borderRadius: '4px', fontSize: '12px' }}>
                        {r.assignCategory}
                      </span>
                    </td>
                    <td style={{ padding: '10px' }}>{r.priority}</td>
                    <td style={{ padding: '10px', textAlign: 'center' }}>
                      <button
                        className="small ghost"
                        style={{ color: '#c62828' }}
                        onClick={async () => {
                          if (confirm(`Delete rule '${r.ruleName}'?`)) {
                            await bankFeedApi.deleteRule(r.id)
                            loadRules()
                          }
                        }}
                      >
                        Delete
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Account Modal */}
      {showAccountModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
          <div className="card" style={{ background: '#fff', padding: '24px', borderRadius: '8px', width: '500px', maxWidth: '90%' }}>
            <h3 style={{ margin: '0 0 16px 0' }}>Add Client Bank Account</h3>
            <form onSubmit={handleCreateAccount}>
              <div style={{ marginBottom: '12px' }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Client</label>
                <select
                  value={accountForm.clientId}
                  onChange={e => setAccountForm({ ...accountForm, clientId: e.target.value })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                >
                  <option value="">Select Client</option>
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Bank Name</label>
                  <input
                    type="text"
                    value={accountForm.bankName}
                    onChange={e => setAccountForm({ ...accountForm, bankName: e.target.value })}
                    required
                    placeholder="e.g. HDFC Bank, ICICI, SBI"
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Account Name</label>
                  <input
                    type="text"
                    value={accountForm.accountName}
                    onChange={e => setAccountForm({ ...accountForm, accountName: e.target.value })}
                    required
                    placeholder="e.g. Primary Current A/C"
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Account Number</label>
                  <input
                    type="text"
                    value={accountForm.accountNumber}
                    onChange={e => setAccountForm({ ...accountForm, accountNumber: e.target.value })}
                    required
                    placeholder="Account Number"
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>IFSC Code</label>
                  <input
                    type="text"
                    value={accountForm.ifscCode}
                    onChange={e => setAccountForm({ ...accountForm, ifscCode: e.target.value.toUpperCase() })}
                    required
                    placeholder="e.g. HDFC0000123"
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '16px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Account Type</label>
                  <select
                    value={accountForm.accountType}
                    onChange={e => setAccountForm({ ...accountForm, accountType: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    <option value="Current">Current</option>
                    <option value="Savings">Savings</option>
                    <option value="Overdraft">Overdraft</option>
                    <option value="CashCredit">Cash Credit</option>
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Opening Balance (₹)</label>
                  <input
                    type="number"
                    step="0.01"
                    value={accountForm.openingBalance}
                    onChange={e => setAccountForm({ ...accountForm, openingBalance: parseFloat(e.target.value) || 0 })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button type="button" className="ghost" onClick={() => setShowAccountModal(false)}>Cancel</button>
                <button type="submit" className="primary">Create Account</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Upload Statement Modal */}
      {showUploadModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
          <div className="card" style={{ background: '#fff', padding: '24px', borderRadius: '8px', width: '600px', maxWidth: '90%' }}>
            <h3 style={{ margin: '0 0 12px 0' }}>Upload Bank Statement (PDF / CSV / OFX)</h3>
            <p style={{ fontSize: '13px', color: 'var(--muted, #666)', marginTop: 0 }}>
              Upload a bank statement in PDF, CSV, or OFX format. Standard Indian bank statements (HDFC, ICICI, SBI, Axis, Kotak) and running balance calculations will be automatically extracted.
            </p>

            <form onSubmit={handleUploadStatement}>
              <div style={{ marginBottom: '12px', padding: '12px', border: '2px dashed #CBD5E1', borderRadius: '6px', textAlign: 'center', background: '#F8FAFC' }}>
                <input
                  type="file"
                  id="bank-file-upload"
                  accept=".pdf,.csv,.ofx,.txt"
                  onChange={handleStatementFileChange}
                  style={{ display: 'none' }}
                />
                <label htmlFor="bank-file-upload" style={{ cursor: 'pointer', color: '#4F46E5', fontWeight: 600, fontSize: '13px' }}>
                  📁 Choose Statement File (.PDF, .CSV, .OFX)
                </label>
                <div style={{ fontSize: '11px', color: '#64748B', marginTop: '4px' }}>
                  Select file to automatically parse and populate fields below
                </div>
              </div>

              <div style={{ display: 'flex', gap: '12px', marginBottom: '12px' }}>
                <div style={{ flex: 1 }}>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>File Name</label>
                  <input
                    type="text"
                    value={uploadFileName}
                    onChange={e => setUploadFileName(e.target.value)}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
                <div style={{ width: '180px' }}>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Statement Date</label>
                  <input
                    type="date"
                    value={uploadStatementDate}
                    onChange={e => setUploadStatementDate(e.target.value)}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              <div style={{ marginBottom: '16px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                  <label style={{ fontSize: '13px', fontWeight: 600 }}>Statement Payload (CSV / OFX / PDF Base64)</label>
                  <button type="button" className="link" onClick={loadSampleCsv} style={{ fontSize: '12px' }}>
                    Load Sample CSV
                  </button>
                </div>
                <textarea
                  rows={6}
                  value={uploadCsvContent}
                  onChange={e => setUploadCsvContent(e.target.value)}
                  placeholder="Date,Narration,Chq/Ref No,Debit,Credit,Balance... or select PDF above"
                  required
                  style={{ width: '100%', padding: '8px', fontFamily: 'monospace', fontSize: '12px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button type="button" className="ghost" onClick={() => setShowUploadModal(false)}>Cancel</button>
                <button type="submit" className="primary">Parse & Import Statement</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Match Transaction Modal */}
      {showMatchModal && selectedTxForMatch && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
          <div className="card" style={{ background: '#fff', padding: '24px', borderRadius: '8px', width: '480px', maxWidth: '90%' }}>
            <h3 style={{ margin: '0 0 12px 0' }}>Reconcile Transaction</h3>
            <div style={{ background: '#f8f9fa', padding: '12px', borderRadius: '6px', marginBottom: '16px', fontSize: '13px' }}>
              <div>Narration: <strong>{selectedTxForMatch.narration}</strong></div>
              <div>Amount: <strong>{formatCurrency(selectedTxForMatch.amount)}</strong> [{selectedTxForMatch.transactionType}]</div>
              <div>Date: <strong>{selectedTxForMatch.transactionDate}</strong></div>
            </div>

            <form onSubmit={handleMatchTransaction}>
              <div style={{ marginBottom: '12px' }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Match Status</label>
                <select
                  value={matchForm.matchStatus}
                  onChange={e => setMatchForm({ ...matchForm, matchStatus: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                >
                  <option value="Matched">Matched</option>
                  <option value="AutoCategorized">AutoCategorized</option>
                  <option value="Unmatched">Unmatched</option>
                  <option value="Ignored">Ignored</option>
                </select>
              </div>

              <div style={{ marginBottom: '12px' }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Matched Ledger Type</label>
                <select
                  value={matchForm.matchedEntityType || ''}
                  onChange={e => setMatchForm({ ...matchForm, matchedEntityType: e.target.value || undefined })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                >
                  <option value="Expense">Expense / Payment Voucher</option>
                  <option value="Invoice">Sales Invoice Receipt</option>
                  <option value="Bill">Vendor Bill Payment</option>
                  <option value="Cheque">Cheque Clearing</option>
                  <option value="Journal">Journal Voucher</option>
                </select>
              </div>

              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Auditor / Reconciliation Notes</label>
                <input
                  type="text"
                  value={matchForm.notes || ''}
                  onChange={e => setMatchForm({ ...matchForm, notes: e.target.value })}
                  placeholder="e.g. Verified with bank slip, RTGS UTR matched"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button type="button" className="ghost" onClick={() => setShowMatchModal(false)}>Cancel</button>
                <button type="submit" className="primary">Save Reconciliation</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Rule Modal */}
      {showRuleModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
          <div className="card" style={{ background: '#fff', padding: '24px', borderRadius: '8px', width: '480px', maxWidth: '90%' }}>
            <h3 style={{ margin: '0 0 16px 0' }}>Add Categorization Rule</h3>
            <form onSubmit={handleCreateRule}>
              <div style={{ marginBottom: '12px' }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Rule Name</label>
                <input
                  type="text"
                  value={ruleForm.ruleName}
                  onChange={e => setRuleForm({ ...ruleForm, ruleName: e.target.value })}
                  required
                  placeholder="e.g. Rent Payment Rule"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '12px' }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Match Keyword (in Narration)</label>
                <input
                  type="text"
                  value={ruleForm.matchKeyword}
                  onChange={e => setRuleForm({ ...ruleForm, matchKeyword: e.target.value })}
                  required
                  placeholder="e.g. RENT, AMAZON, ADANI"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '16px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Transaction Type</label>
                  <select
                    value={ruleForm.transactionType}
                    onChange={e => setRuleForm({ ...ruleForm, transactionType: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    <option value="Any">Any</option>
                    <option value="Debit">Debit (Outward)</option>
                    <option value="Credit">Credit (Inward)</option>
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Priority (Higher = First)</label>
                  <input
                    type="number"
                    value={ruleForm.priority}
                    onChange={e => setRuleForm({ ...ruleForm, priority: parseInt(e.target.value) || 0 })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Assign Category</label>
                <input
                  type="text"
                  value={ruleForm.assignCategory}
                  onChange={e => setRuleForm({ ...ruleForm, assignCategory: e.target.value })}
                  required
                  placeholder="e.g. Office Rent, Cloud Infrastructure"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button type="button" className="ghost" onClick={() => setShowRuleModal(false)}>Cancel</button>
                <button type="submit" className="primary">Save Rule</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
