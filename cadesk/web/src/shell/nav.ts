import { createElement, type ReactNode } from 'react'
import { NavIcon, TodayIcon } from './navIcons'

/* Every desk in the app, with who may see it. The sidebar, command palette,
   breadcrumbs and Today screen all read this one list. */

export type Page = 'today' | 'gstr1-export' | 'tasks' | 'deadlines' | 'clients' | 'billing' | 'trial-balance' | 'tax' | 'audit-workspace' | 'aqmm' | 'gst-reconcile' | 'tds' | 'mca' | 'engagement' | 'banking' | 'ledger-sync' | 'reports' | 'practice-health' | 'tax-notices' | 'tax-litigation' | 'payroll' | 'einvoicing' | 'fixed-assets' | 'advance-tax' | 'gstr9' | 'presumptive' | 'caro' | 'taxaudit' | 'schedule3' | 'branchaudit' | 'auditcommittee' | 'foreignremittance' | 'relatedparty' | 'internalaudit' | 'antiprofiteering' | 'secretarialaudit' | 'transferpricing' | 'uae-tax' | 'costaudit' | 'dsc' | 'portal' | 'users' | 'settings' | 'audit' | 'security' | 'subscriptions' | 'communications' | 'security-compliance' | 'partner-desk' | 'esg-csr' | 'ibc-cirp'

export type NavItem = {
  id: Page
  label: string
  icon: ReactNode
  visible: boolean
}

export type NavCategory = {
  id: string
  title: string
  items: NavItem[]
}

export const TODAY_ICON: ReactNode = createElement(TodayIcon)

export function buildNav(can: (perm: string) => boolean, isFeatureEnabled: (key: string) => boolean): NavCategory[] {
  return [
  {
    id: 'practice',
    title: 'Practice',
    items: [
      {
        id: 'tasks',
        label: 'Tasks',
        icon: createElement(NavIcon, { id: 'tasks' }),
        visible: can('tasks.read')
      },
      {
        id: 'deadlines',
        label: 'Deadlines',
        icon: createElement(NavIcon, { id: 'deadlines' }),
        visible: can('compliance.read')
      },
      {
        id: 'clients',
        label: 'Clients',
        icon: createElement(NavIcon, { id: 'clients' }),
        visible: true
      },
      {
        id: 'billing',
        label: 'Billing & Time',
        icon: createElement(NavIcon, { id: 'billing' }),
        visible: true
      },
      {
        id: 'partner-desk',
        label: 'Partner mobile desk',
        icon: createElement(NavIcon, { id: 'partner-desk' }),
        visible: true
      },
      {
        id: 'portal',
        label: 'Client Desk',
        icon: createElement(NavIcon, { id: 'portal' }),
        visible: can('portal.read')
      }
    ]
  },
  {
    id: 'accounting',
    title: 'Accounting & data',
    items: [
      {
        id: 'trial-balance',
        label: 'Trial Balance Hub',
        icon: createElement(NavIcon, { id: 'trial-balance' }),
        visible: can('trialbalance.read')
      },
      {
        id: 'banking',
        label: 'Bank Feeds & BRS',
        icon: createElement(NavIcon, { id: 'banking' }),
        visible: can('bank.read')
      },
      {
        id: 'ledger-sync',
        label: 'Tally/Busy Auto-Recon',
        icon: createElement(NavIcon, { id: 'ledger-sync' }),
        visible: (can('ledgersync.read') || can('bank.read')) && isFeatureEnabled('ledger-sync')
      },
      {
        id: 'fixed-assets',
        label: 'Fixed Assets & Depr',
        icon: createElement(NavIcon, { id: 'fixed-assets' }),
        visible: can('assets.read')
      }
    ]
  },
  {
    id: 'tax',
    title: 'Direct tax',
    items: [
      {
        id: 'tax',
        label: 'Tax Desk',
        icon: createElement(NavIcon, { id: 'tax' }),
        visible: can('computations.run')
      },
      {
        id: 'advance-tax',
        label: 'Advance Tax & 234',
        icon: createElement(NavIcon, { id: 'advance-tax' }),
        visible: can('advancetax.read')
      },
      {
        id: 'presumptive',
        label: 'Presumptive Tax (44AD)',
        icon: createElement(NavIcon, { id: 'presumptive' }),
        visible: can('presumptive.read')
      },
      {
        id: 'tds',
        label: 'TDS Desk',
        icon: createElement(NavIcon, { id: 'tds' }),
        visible: can('tds.read')
      },
      {
        id: 'tax-notices',
        label: 'Tax Notices & Scrutiny',
        icon: createElement(NavIcon, { id: 'tax-notices' }),
        visible: can('notices.read')
      },
      {
        id: 'tax-litigation',
        label: 'ITAT & HC Appeals',
        icon: createElement(NavIcon, { id: 'tax-litigation' }),
        visible: (can('taxlitigation.read') || can('notices.read')) && isFeatureEnabled('tax-litigation')
      },
      {
        id: 'transferpricing',
        label: 'Transfer Pricing (Form 3CEB)',
        icon: createElement(NavIcon, { id: 'transferpricing' }),
        visible: can('transferpricing.read')
      }
    ]
  },
  {
    id: 'gst',
    title: 'GST',
    items: [
      {
        id: 'gst-reconcile',
        label: 'GST 2B Match',
        icon: createElement(NavIcon, { id: 'gst-reconcile' }),
        visible: can('gst.read')
      },
      {
        id: 'gstr9',
        label: 'GSTR-9 / 9C Desk',
        icon: createElement(NavIcon, { id: 'gstr9' }),
        visible: can('gstr9.read')
      },
      {
        id: 'gstr1-export',
        label: 'GSTR-1 & 3B',
        icon: createElement(NavIcon, { id: 'gstr1-export' }),
        visible: can('gst.read')
      },
      {
        id: 'einvoicing',
        label: 'E-Invoice & EWB',
        icon: createElement(NavIcon, { id: 'einvoicing' }),
        visible: can('einvoice.read') && isFeatureEnabled('einvoicing')
      },
      {
        id: 'antiprofiteering',
        label: 'Anti-Profiteering (Sec 171)',
        icon: createElement(NavIcon, { id: 'antiprofiteering' }),
        visible: can('antiprofiteering.read')
      }
    ]
  },
  {
    id: 'audit',
    title: 'Audit & assurance',
    items: [
      {
        id: 'audit-workspace',
        label: 'Audit Workspace',
        icon: createElement(NavIcon, { id: 'audit-workspace' }),
        visible: can('audit.read')
      },
      {
        id: 'aqmm',
        label: 'AQMM Desk (ICAI)',
        icon: createElement(NavIcon, { id: 'aqmm' }),
        visible: can('audit.read')
      },
      {
        id: 'caro',
        label: 'CARO 2020 Desk',
        icon: createElement(NavIcon, { id: 'caro' }),
        visible: can('caro.read')
      },
      {
        id: 'taxaudit',
        label: 'Tax Audit (3CD)',
        icon: createElement(NavIcon, { id: 'taxaudit' }),
        visible: can('taxaudit.read')
      },
      {
        id: 'schedule3',
        label: 'Schedule III Desk',
        icon: createElement(NavIcon, { id: 'schedule3' }),
        visible: can('schedule3.read')
      },
      {
        id: 'branchaudit',
        label: 'Branch Audit (SA 600)',
        icon: createElement(NavIcon, { id: 'branchaudit' }),
        visible: can('branchaudit.read')
      },
      {
        id: 'auditcommittee',
        label: 'Audit Committee (SA 260)',
        icon: createElement(NavIcon, { id: 'auditcommittee' }),
        visible: can('auditcommittee.read')
      },
      {
        id: 'internalaudit',
        label: 'Internal Audit (RCM & IFC)',
        icon: createElement(NavIcon, { id: 'internalaudit' }),
        visible: can('internalaudit.read')
      },
      {
        id: 'secretarialaudit',
        label: 'Secretarial Audit (Sec 204)',
        icon: createElement(NavIcon, { id: 'secretarialaudit' }),
        visible: can('secretarialaudit.read')
      },
      {
        id: 'costaudit',
        label: 'Cost Audit (CRA 1-4)',
        icon: createElement(NavIcon, { id: 'costaudit' }),
        visible: can('costaudit.read')
      },
      {
        id: 'esg-csr',
        label: 'ESG & CSR Audit',
        icon: createElement(NavIcon, { id: 'esg-csr' }),
        visible: (can('esgcsr.read') || can('caro.read') || can('audit.read')) && isFeatureEnabled('esg-csr')
      }
    ]
  },
  {
    id: 'corporate',
    title: 'Corporate & global',
    items: [
      {
        id: 'mca',
        label: 'MCA / ROC',
        icon: createElement(NavIcon, { id: 'mca' }),
        visible: can('mca.read')
      },
      {
        id: 'engagement',
        label: 'Engagements & KYC',
        icon: createElement(NavIcon, { id: 'engagement' }),
        visible: can('engagement.read')
      },
      {
        id: 'dsc',
        label: 'DSC Custody',
        icon: createElement(NavIcon, { id: 'dsc' }),
        visible: can('dsc.read')
      },
      {
        id: 'foreignremittance',
        label: 'Foreign Remittance (15CA/CB)',
        icon: createElement(NavIcon, { id: 'foreignremittance' }),
        visible: can('foreignremittance.read')
      },
      {
        id: 'relatedparty',
        label: 'Related Party (Sec 188)',
        icon: createElement(NavIcon, { id: 'relatedparty' }),
        visible: can('relatedparty.read')
      },
      {
        id: 'uae-tax',
        label: 'UAE Corporate Tax',
        icon: createElement(NavIcon, { id: 'uae-tax' }),
        visible: can('uaecorporatetax.read') && isFeatureEnabled('uae-tax')
      },
      {
        id: 'ibc-cirp',
        label: 'IBC & CIRP Desk',
        icon: createElement(NavIcon, { id: 'ibc-cirp' }),
        visible: (can('ibccirp.read') || can('audit.read')) && isFeatureEnabled('ibc-cirp')
      }
    ]
  },
  {
    id: 'admin',
    title: 'Firm',
    items: [
      {
        id: 'users',
        label: 'Team',
        icon: createElement(NavIcon, { id: 'users' }),
        visible: can('users.read')
      },
      {
        id: 'practice-health',
        label: 'Valuation & Health',
        icon: createElement(NavIcon, { id: 'practice-health' }),
        visible: can('practicehealth.read') && isFeatureEnabled('practice-health')
      },
      {
        id: 'reports',
        label: 'MIS & Reports',
        icon: createElement(NavIcon, { id: 'reports' }),
        visible: can('reports.read')
      },
      {
        id: 'payroll',
        label: 'Payroll & PF/ESI',
        icon: createElement(NavIcon, { id: 'payroll' }),
        visible: can('payroll.read')
      },
      {
        id: 'subscriptions',
        label: 'Plan & Billing',
        icon: createElement(NavIcon, { id: 'subscriptions' }),
        visible: can('settings.manage')
      },
      {
        id: 'communications',
        label: 'Communications',
        icon: createElement(NavIcon, { id: 'communications' }),
        visible: can('settings.manage')
      },
      {
        id: 'security-compliance',
        label: 'Security & Trust Vault',
        icon: createElement(NavIcon, { id: 'security-compliance' }),
        visible: can('settings.manage')
      },
      {
        id: 'settings',
        label: 'Settings',
        icon: createElement(NavIcon, { id: 'settings' }),
        visible: can('settings.manage')
      },
      {
        id: 'audit',
        label: 'Audit Log',
        icon: createElement(NavIcon, { id: 'audit' }),
        visible: can('audit.read')
      },
      {
        id: 'security',
        label: 'My Account',
        icon: createElement(NavIcon, { id: 'security' }),
        visible: true
      }
    ]
  }
  ]
}

export function findDesk(nav: NavCategory[], page: Page): { item: NavItem; category: NavCategory } | null {
  for (const category of nav) {
    const item = category.items.find(i => i.id === page)
    if (item) return { item, category }
  }
  return null
}
