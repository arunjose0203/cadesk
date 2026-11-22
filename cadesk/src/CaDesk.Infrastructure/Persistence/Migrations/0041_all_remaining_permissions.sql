-- 0041_all_remaining_permissions: Register permissions for Foreign Remittance (15CA/15CB), Related Party (Sec 188),
-- Internal Audit (IFC/RCM), Anti-Profiteering (Sec 171), Secretarial Audit (MR-3), Transfer Pricing (3CEB), Cost Audit (CRA-1/3), and GSTR-9.

insert into permissions (code, area, description) values
  ('foreignremittance.read',    'compliance', 'View Form 15CA/15CB foreign remittance assessments, TRC, and checklists'),
  ('foreignremittance.manage',  'compliance', 'Prepare Form 15CA/15CB remittances, verify withholding, and track UDIN'),
  ('foreignremittance.certify', 'compliance', 'Sign and issue Form 15CB Chartered Accountant certificates'),
  ('relatedparty.read',         'compliance', 'View related party register, Section 188 limits, and omnibus approvals'),
  ('relatedparty.manage',       'compliance', 'Record related parties, benchmark transactions, and manage board limits'),
  ('relatedparty.approve',      'compliance', 'Grant audit committee omnibus approvals for related party transactions'),
  ('internalaudit.read',        'audit',      'View internal audit engagements, RCM matrices, and IFC testing'),
  ('internalaudit.manage',      'audit',      'Manage risk control matrices, test sample transactions, and log findings'),
  ('internalaudit.report',      'audit',      'Issue internal audit reports and executive dashboards for management'),
  ('antiprofiteering.read',     'compliance', 'View GST anti-profiteering assessments, rate change impacts, and simulations'),
  ('antiprofiteering.manage',   'compliance', 'Calculate post-tax benefit pass-through and track penalty exposure'),
  ('antiprofiteering.certify',  'compliance', 'Issue anti-profiteering compliance clearance and methodology sign-off'),
  ('secretarialaudit.read',     'compliance', 'View Form MR-3 secretarial audit engagements and governance checklists'),
  ('secretarialaudit.manage',   'compliance', 'Execute Companies Act secretarial verifications, event logs, and minutes'),
  ('secretarialaudit.certify',  'compliance', 'Issue Form MR-3 Secretarial Audit Report with partner qualifications'),
  ('transferpricing.read',      'compliance', 'View transfer pricing documentation, Form 3CEB studies, and benchmark data'),
  ('transferpricing.manage',    'compliance', 'Manage associated enterprise profiles, 3CEB clauses, and calculate arm length price'),
  ('transferpricing.certify',   'compliance', 'Issue Section 92E Form 3CEB Accountant Report and TP study sign-off'),
  ('costaudit.read',            'compliance', 'View Form CRA-1 cost records, CRA-3 audit reports, and reconciliation items'),
  ('costaudit.manage',          'compliance', 'Build cost heads, product cost statements, and reconcile with financial accounts'),
  ('costaudit.certify',         'compliance', 'Issue Form CRA-3 Cost Audit Report with Annexure disclosures'),
  ('gstr9.read',                'gst',        'View annual GSTR-9 and GSTR-9C reconciliation returns'),
  ('gstr9.manage',              'gst',        'Prepare and reconcile GSTR-9 annual return tables against GSTR-1 and GSTR-3B'),
  ('gstr9.certify',             'gst',        'Sign and certify annual GSTR-9 / GSTR-9C reconciliation statements')
on conflict (code) do nothing;
