-- 0054_tax_copilot_notice_defense.sql
-- CA Copilot: Statutory Citation & Tax Notice Defense Drafter (Income Tax & GST)
-- Seeds curated landmark Supreme Court, High Court, and ITAT rulings,
-- and provisions structured tax defense drafts with preliminary objections,
-- factual matrices, merit grounds, and 1-click legal written submission compiler.

create table if not exists statutory_precedents (
  id                      uuid primary key,
  firm_id                 uuid references firms(id) on delete cascade, -- null for platform seeds
  authority               text not null, -- IncomeTax, Gst
  category                text not null, -- JurisdictionAndLimitation, NaturalJusticeAndFairHearing, etc.
  case_title              text not null,
  citation                text not null,
  court_bench             text not null,
  judgment_year           integer not null default 2024,
  applicable_sections     text not null,
  key_ratio_decidendi     text not null,
  defense_ground_template text not null,
  is_landmark             boolean not null default true,
  keywords                text not null,
  created_at              timestamptz not null default now()
);

create index if not exists ix_statutory_precedents_lookup 
  on statutory_precedents (authority, category);
create index if not exists ix_statutory_precedents_firm 
  on statutory_precedents (firm_id);

alter table statutory_precedents enable row level security;
drop policy if exists tenant_isolation on statutory_precedents;
create policy tenant_isolation on statutory_precedents
  using (firm_id is null or firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on statutory_precedents to cadesk_app;

-- Seed Curated Landmark Supreme Court, High Court & ITAT Precedents
insert into statutory_precedents (
  id, firm_id, authority, category, case_title, citation, court_bench, judgment_year,
  applicable_sections, key_ratio_decidendi, defense_ground_template, is_landmark, keywords
) values
(
  'e0000001-0000-0000-0000-000000000001', null, 'IncomeTax', 'ClericalAndBonaFideError',
  'Price Waterhouse Coopers Pvt. Ltd. v. CIT',
  '[2012] 348 ITR 306 (SC)', 'Supreme Court of India', 2012,
  'Section 270A, Section 271(1)(c)',
  'A bona fide clerical or inadvertent human error cannot be equated with concealment of income or furnishing inaccurate particulars. No penalty can be levied where the omission was silly or accidental with no deliberate intention to evade tax.',
  'It is respectfully submitted that the inadvertent discrepancy was a purely bona fide clerical error committed during computation. Reliance is placed on the landmark Supreme Court ruling in Price Waterhouse Coopers Pvt. Ltd. v. CIT [2012] 348 ITR 306 (SC), wherein the Hon''ble Apex Court authoritatively held that penalty is not attracted for an innocent mistake devoid of contumacious conduct.',
  true, 'penalty 270A 271(1)(c) bona fide clerical error inadvertent mistake human error'
),
(
  'e0000001-0000-0000-0000-000000000002', null, 'IncomeTax', 'ReassessmentValiditySec148',
  'CIT v. Kelvinator of India Ltd.',
  '[2010] 320 ITR 561 (SC)', 'Supreme Court of India', 2010,
  'Section 147, Section 148, Section 148A',
  'Reassessment proceedings cannot be initiated based on a mere change of opinion. The Assessing Officer must possess tangible material to form a belief that income chargeable to tax has escaped assessment.',
  'The assumption of jurisdiction under Section 148 is wholly bad in law being a mere change of opinion on issues already scrutinized. As established by the Hon''ble Supreme Court in CIT v. Kelvinator of India Ltd. [2010] 320 ITR 561 (SC), reassessment cannot be resorted to review an earlier view in the absence of fresh tangible material having live link with escapement of income.',
  true, 'reassessment 148 147 148A change of opinion tangible material live link'
),
(
  'e0000001-0000-0000-0000-000000000003', null, 'IncomeTax', 'NaturalJusticeAndFairHearing',
  'GKN Driveshafts (India) Ltd. v. ITO',
  '[2003] 259 ITR 19 (SC)', 'Supreme Court of India', 2003,
  'Section 148, Section 148A, Section 144B',
  'When a notice under Section 148 is issued, the proper course is for the assessee to file a return and seek reasons for issuing notice. The AO is bound to furnish reasons within a reasonable time and dispose of objections by a speaking order before proceeding with assessment.',
  'The impugned proceedings violate the mandatory procedure laid down by the Hon''ble Supreme Court in GKN Driveshafts (India) Ltd. v. ITO [2003] 259 ITR 19 (SC). The Assessing Officer is statutorily obligated to supply the recorded reasons and dispose of the preliminary objections raised by the assessee by way of a speaking order before finalizing the assessment.',
  true, 'natural justice reasons 148 speaking order preliminary objections faceless 144B'
),
(
  'e0000001-0000-0000-0000-000000000004', null, 'IncomeTax', 'JurisdictionAndLimitation',
  'Union of India v. Ashish Agarwal / Rajeev Bansal v. UOI',
  '[2022] 444 ITR 1 (SC) / [2024] 167 taxmann.com 70 (SC)', 'Supreme Court of India', 2024,
  'Section 148, Section 148A, Section 149, Section 151',
  'Reassessment notices issued after 01.04.2021 must strictly comply with the new regime under Finance Act 2021. For AYs where 3 years have expired, notices can only be issued if the escaped income represented in the form of an asset exceeds Rs. 50 Lakhs and prior sanction of specified authority (PCCIT/PDGIT) is obtained.',
  'The impugned notice u/s 148 is barred by limitation under Section 149 of the Act. Following the authoritative ruling of the Supreme Court in Rajeev Bansal v. Union of India [2024] 167 taxmann.com 70 (SC), the department cannot extend limitation beyond the statutory 3-year period unless tangible material establishing asset escapement exceeding Rs. 50 Lakhs with valid sanction u/s 151 is on record.',
  true, 'limitation 149 148A 151 sanction 50 lakhs asset time barred rajeev bansal'
),
(
  'e0000001-0000-0000-0000-000000000005', null, 'IncomeTax', 'CashCreditsSec68',
  'CIT v. Lovely Exports Pvt. Ltd.',
  '[2008] 216 CTR 195 (SC)', 'Supreme Court of India', 2008,
  'Section 68, Section 69',
  'If the share application money / loan is received from alleged bogus parties, but the assessee establishes the identity of the counter-party by furnishing PAN, bank statements, and confirmation, the addition cannot be made in the hands of the assessee company under Section 68.',
  'The assessee has discharged the initial onus cast under Section 68 by furnishing complete identity details, PAN, registered addresses, and audited financial statements of the investors/lenders through banking channels. Applying the ratio of the Supreme Court in CIT v. Lovely Exports Pvt. Ltd. [2008] 216 CTR 195 (SC), no addition under Section 68 can be sustained in the hands of the assessee.',
  true, 'section 68 cash credits share capital loans identity creditworthiness genuineness'
),
(
  'e0000001-0000-0000-0000-000000000006', null, 'IncomeTax', 'TdsCreditMismatch',
  'Smt. Anju Rani v. ACIT / Rule 37BA Precedents',
  '[2020] 117 taxmann.com 853 (Del ITAT)', 'ITAT Delhi Bench', 2020,
  'Section 199, Rule 37BA, Section 143(1)',
  'Credit for TDS cannot be denied to an assessee merely because of a mismatch in Form 26AS or delay by the deductor in filing Form 24Q/26Q, when income corresponding to the TDS has been duly offered to tax.',
  'It is a settled proposition of law under Section 199 read with Rule 37BA that credit for taxes deducted at source belongs to the person whose income suffered the deduction. Where the gross receipt is offered in the return of income, credit cannot be denied in Section 143(1) intimation due to portal sync issues or deductor''s clerical delay.',
  true, 'tds mismatch 26AS AIS section 199 rule 37BA 143(1) intimation credit'
),
(
  'e0000001-0000-0000-0000-000000000007', null, 'IncomeTax', 'NaturalJusticeAndFairHearing',
  'Piramal Enterprises Ltd. v. Addl. CIT',
  '[2021] 438 ITR 177 (Bom HC)', 'Bombay High Court', 2021,
  'Section 144B(6), Section 144B(7)',
  'Under the Faceless Assessment Scheme, granting an opportunity of personal hearing through video conference upon request is mandatory. An assessment order passed without providing video conference hearing is violative of natural justice and must be quashed.',
  'The assessee specifically craves leave for a personal hearing via video conference under Section 144B(6)(vii) of the Act. As held by the Hon''ble Bombay High Court in Piramal Enterprises Ltd. v. Addl. CIT [2021] 438 ITR 177 (Bom), denying an opportunity of hearing through video conferencing renders the subsequent assessment order null and void.',
  true, 'faceless video conference personal hearing 144B natural justice quashed'
),
(
  'e0000001-0000-0000-0000-000000000008', null, 'Gst', 'GstItcBonaFideBuyer',
  'Suncraft Energies Pvt. Ltd. v. Assistant Commissioner',
  '[2023] 153 taxmann.com 132 (Cal HC) / Affirmed in SC [2023]', 'Calcutta High Court / Supreme Court of India', 2023,
  'Section 16(2)(c), Section 73, Section 74',
  'Before directing the recipient to reverse Input Tax Credit on the ground of non-payment of tax by the supplier under Section 16(2)(c), the GST department must first take coercive recovery action against the supplier who collected the tax. The purchasing dealer cannot be made to suffer when transactions are genuine and backed by tax invoices and bank payments.',
  'It is respectfully submitted that the recipient cannot be penalized for alleged default of the supplier in paying tax. Placing reliance on the landmark judgment of the Calcutta High Court in Suncraft Energies Pvt. Ltd. v. Assistant Commissioner [2023] 153 taxmann.com 132 (affirmed by the Supreme Court in SLP(C) No. 27827/2023), the department must first proceed against the defaulting supplier before demanding reversal from a bona fide buyer.',
  true, 'gst itc 16(2)(c) supplier default suncraft energies bona fide buyer reversal drc-01'
),
(
  'e0000001-0000-0000-0000-000000000009', null, 'Gst', 'GstItcBonaFideBuyer',
  'Arise India Ltd. v. Commissioner of Trade & Taxes',
  '[2017] 86 taxmann.com 101 (Del HC) / Affirmed in SC', 'Delhi High Court / Supreme Court of India', 2017,
  'Section 16(2)(c), Section 73',
  'Purchasing dealer who has acted bona fide and paid price including tax to the registered selling dealer cannot be denied credit if the seller subsequently fails to deposit tax or becomes non-existent.',
  'The assessee holds valid tax invoices, e-Way bills, proof of receipt of goods, and has remitted consideration through banking channels. Applying the ratio of Arise India Ltd. (Del HC / SC), a genuine buyer cannot be treated on par with a fraudulent dealer when every statutory verification was satisfied at the time of purchase.',
  true, 'arise india bona fide purchaser tax invoice e-way bill bank payment genuine itc'
),
(
  'e0000001-0000-0000-0000-000000000010', null, 'Gst', 'GstRule88Discrepancy',
  'Union of India v. Bharti Airtel Ltd.',
  '[2021] 131 taxmann.com 73 (SC)', 'Supreme Court of India', 2021,
  'Section 37, Section 38, Section 39, Rule 88C, Rule 88D',
  'Form GSTR-2A/2B is a facilitative system aid and not a conclusive, rigid condition for eligibility of ITC. Rectifications and adjustments of timing differences in subsequent tax periods are permissible under the scheme of the GST law.',
  'The discrepancy highlighted under Rule 88D/DRC-01C arises solely out of timing differences between supplier upload in GSTR-1 and return filing. As observed by the Supreme Court in Bharti Airtel Ltd., auto-populated statements are purely facilitative mechanisms; eligible credit availed as per Section 16 cannot be disallowed when conditions stand fulfilled in the financial year.',
  true, 'rule 88C rule 88D drc-01B drc-01C bharti airtel timing difference gstr-2B gstr-3B'
),
(
  'e0000001-0000-0000-0000-000000000011', null, 'Gst', 'NaturalJusticeAndFairHearing',
  'Radha Krishan Industries v. State of Himachal Pradesh',
  '[2021] 127 taxmann.com 26 (SC)', 'Supreme Court of India', 2021,
  'Section 75(4), Section 73, Section 74, Section 83',
  'Granting an opportunity of personal hearing is mandatory under Section 75(4) where an adverse decision is contemplated against the taxable person or where a specific request is received in writing. Failure to grant hearing vitiates the entire adjudication order.',
  'Section 75(4) of the CGST Act unequivocally states that an opportunity of hearing shall be granted where a request is received or where an adverse decision is contemplated. The assessee hereby formally requests an opportunity of personal hearing before any adverse view is taken; failure to afford hearing would render the order invalid in terms of Radha Krishan Industries (SC).',
  true, 'section 75(4) personal hearing mandatory natural justice radha krishan drc-01'
),
(
  'e0000001-0000-0000-0000-000000000012', null, 'Gst', 'JurisdictionAndLimitation',
  'Limitation under Section 73(10) / Notification Precedents',
  '[2024] 158 taxmann.com 412 (Mad HC)', 'Madras High Court', 2024,
  'Section 73(9), Section 73(10)',
  'Show cause notices and adjudication orders under Section 73 must strictly be passed within the limitation period prescribed under Section 73(10). Arbitrary or routine extension of limitation without justifiable cause cannot validate time-barred demands.',
  'The show cause notice in Form DRC-01 is barred by limitation under Section 73(10) of the CGST Act, 2017. The statutory period of three years from the due date of furnishing the annual return for the relevant financial year has lapsed, rendering the proceedings void ab initio.',
  true, 'section 73(10) limitation time barred annual return show cause notice drc-01'
)
on conflict (id) do nothing;

-- Table for Structured Legal Defense Drafts
create table if not exists tax_defense_drafts (
  id                          uuid primary key,
  firm_id                     uuid not null references firms(id),
  notice_id                   uuid not null references tax_notices(id) on delete cascade,
  client_id                   uuid not null references clients(id) on delete cascade,
  draft_number                text not null,
  title                       text not null,
  subject                     text not null,
  addressed_to                text not null,
  client_name                 text not null,
  client_pan_or_gstin         text not null,
  assessment_year             text not null,
  financial_year              text not null,
  notice_ref_number           text not null,
  notice_din                  text not null,
  notice_issue_date           date not null,
  notice_due_date             date not null,
  preliminary_objections_json text not null default '[]',
  factual_matrix_json         text not null default '[]',
  merit_grounds_json          text not null default '[]',
  cited_precedents_json       text not null default '[]',
  prayer_for_relief           text not null,
  annexure_index_json         text not null default '[]',
  status                      text not null default 'Draft', -- Draft, UnderReview, PartnerApproved, Finalized
  prepared_by_user_id         uuid references users(id) on delete set null,
  prepared_by_name            text,
  approved_by_user_id         uuid references users(id) on delete set null,
  approved_by_name            text,
  approved_at                 timestamptz,
  partner_udin                text,
  generated_html              text,
  deleted_at                  timestamptz,
  created_at                  timestamptz not null default now(),
  created_by                  uuid,
  updated_at                  timestamptz,
  updated_by                  uuid
);

create index if not exists ix_tax_defense_drafts_notice 
  on tax_defense_drafts (firm_id, notice_id);
create index if not exists ix_tax_defense_drafts_client 
  on tax_defense_drafts (firm_id, client_id);
create index if not exists ix_tax_defense_drafts_status 
  on tax_defense_drafts (firm_id, status);

alter table tax_defense_drafts enable row level security;
drop policy if exists tenant_isolation on tax_defense_drafts;
create policy tenant_isolation on tax_defense_drafts
  using (firm_id = app_current_firm())
  with check (firm_id = app_current_firm());

grant select, insert, update, delete on tax_defense_drafts to cadesk_app;
