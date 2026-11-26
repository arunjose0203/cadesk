# CaDesk — സമ്പൂർണ്ണ എൻഡ്-ടു-എൻഡ് ഗൈഡ് (End-to-End Application Guide in Malayalam)

**ഇന്ത്യൻ ചാർട്ടേഡ് അക്കൗണ്ടന്റ് (CA) സ്ഥാപനങ്ങൾക്കായുള്ള സമ്പൂർണ്ണ പ്രാക്ടീസ്, ടാക്സ്, ജി.എസ്.ടി (GST), ഓഡിറ്റ് ക്ലൗഡ് പ്ലാറ്റ്‌ഫോം**

---

## 1. എന്താണ് CaDesk? (ആമുഖം)

ഒരു CA ഓഫീസിൽ ദൈനംദിന പ്രവൃത്തികൾക്കായി Tally/Excel, നിരവധി ഡെസ്ക്ടോപ്പ് സോഫ്റ്റ്‌വെയറുകൾ, WhatsApp, ഇമെയിൽ, ഫയൽ ഫോൾഡറുകൾ എന്നിവയെല്ലാം വെവ്വേറെ ഉപയോഗിക്കേണ്ടിവരുന്ന ബുദ്ധിമുട്ട് ഒഴിവാക്കി, **ക്ലയന്റ് മാനേജ്‌മെന്റ് മുതൽ GST, Income Tax, TDS, Statutory Audit, ഫീസ് ബില്ലിംഗ്** വരെ ഒരൊറ്റ സുരക്ഷിത വെബ് പ്ലാറ്റ്‌ഫോമിലേക്ക് കൊണ്ടുവരുന്ന സംവിധാനമാണ് **CaDesk**.

### പ്രധാന സവിശേഷതകൾ ചുരുക്കത്തിൽ
- **Practice & Compliance**: ക്ലയന്റ് രേഖകൾ, ഓട്ടോമാറ്റിക് ഡെഡ്‌ലൈൻ കലണ്ടർ, ടാസ്ക് ട്രാക്കിംഗ്, സ്റ്റാഫ് വർക്ക്‌ലോഡ്, ടൈംഷീറ്റ് & GST ബില്ലിംഗ്.
- **GST ഫയലിംഗ് എക്സ്പോർട്ടുകൾ**: Sales Register (Excel/CSV)-ൽ നിന്ന് നേരിട്ട് **GSTR-1 JSON**, **GSTR-3B ചെക്ക്**, **GSTR-9 വർക്കിംഗ്** എന്നിവ തയ്യാറാക്കൽ.
- **Direct Tax & TDS**: Income Tax കണക്കുകൂട്ടൽ (New/Old Regime), **ITR-1, ITR-4, Form 3CA/3CB-3CD JSON** എക്സ്പോർട്ട്, **TDS RPU `.txt` (`24Q/26Q/27Q/27EQ`)** ജനറേഷൻ.
- **Statutory Audit (ICAI / SA Standards)**: Lead Schedules (`A–Q`), Working Papers, CARO 2020, Schedule III, SA 505 കൺഫർമേഷനുകൾ, SA 230 ഡിജിറ്റൽ ഡോസിയർ PDF (**SHA-256 സീൽ** സഹിതം), SA 700 ഓഡിറ്റർ റിപ്പോർട്ട് + **UDIN** ബൈൻഡിംഗ്.
- **Client Portal & Online Payment**: ക്ലയന്റുകൾക്ക് അപ്‌ലോഡ് ലിങ്ക് വഴി ഡോക്യുമെന്റുകൾ അയക്കാനും, **Razorpay** വഴി ബില്ലുകൾ ഓൺലൈനായി അടയ്ക്കാനുമുള്ള സൗകര്യം.

---

## 2. സാങ്കേതിക ഘടനയും സുരക്ഷയും (Architecture & Security)

ഇന്ത്യയിലെ **DPDP Act 2023 / DPDP Rules 2025** മാനദണ്ഡങ്ങൾക്കും CA സ്ഥാപനങ്ങളുടെ രഹസ്യസ്വഭാവത്തിനും (Chartered Accountants Act, 1949) അനുസരിച്ചാണ് CaDesk രൂപകൽപ്പന ചെയ്തിരിക്കുന്നത്.

| ഘടകം | സാങ്കേതികവിദ്യ / വിവരണം |
| --- | --- |
| **വെബ് ആപ്പ് (Frontend)** | React 19 + TypeScript + Vite (`Clear` തീം, Light/Dark മോഡ്, `Ctrl + K` കമാൻഡ് പാലറ്റ്, ഓരോ ഡെസ്കിനും വെവ്വേറെ URL) |
| **സെർവർ API (Backend)** | .NET 10 Web API (Minimal APIs, Entity Framework Core, Role & Plan Authorization) |
| **ഡാറ്റാബേസ്** | PostgreSQL 17 / 18 (ഡാറ്റ പൂർണ്ണമായും ഇന്ത്യയിൽ സൂക്ഷിക്കുന്നു) |
| **Two-Lock Tenant Isolation** | 1) ആപ്ലിക്കേഷൻ തലത്തിൽ EF Core Global Filter, 2) ഡാറ്റാബേസ് തലത്തിൽ PostgreSQL Row-Level Security (`app_current_firm()`). ഒരു ഫേമിന്റെ ഡാറ്റ മറ്റൊരു ഫേമിന് ഒരിക്കലും കാണാനോ ആക്സസ് ചെയ്യാനോ കഴിയില്ല. |
| **ലോഗിൻ സുരക്ഷ** | Authenticator App വഴിയുള്ള **2FA (Two-Step Sign-in / TOTP)** (പാർട്ണർമാർക്ക് എല്ലാ സ്റ്റാഫിനും നിർബന്ധമാക്കാം), നിശ്ചിത സമയം ഉപയോഗിക്കാതിരുന്നാൽ ഓട്ടോമാറ്റിക് ലോഗ്ഔട്ട്, Brute-force Rate Limiting. |
| **ഫയൽ സുരക്ഷ & എൻക്രിപ്ഷൻ** | അപ്‌ലോഡ് ചെയ്യുന്ന എല്ലാ ഫയലുകളും **ClamAV** ഉപയോഗിച്ച് വൈറസ് സ്കാൻ ചെയ്യുന്നു; API കീകൾ `SecretStore` വഴി എൻക്രിപ്റ്റ് ചെയ്ത് സൂക്ഷിക്കുന്നു. |
| **ബാക്കപ്പ് സംവിധാനം** | ദിവസേനയുള്ള ഓട്ടോമാറ്റിക് `pg_dump` ബാക്കപ്പ്, **OpenSSL AES-256-CBC** ഉപയോഗിച്ച് എൻക്രിപ്റ്റ് ചെയ്ത് സൂക്ഷിക്കുന്നു (Local / S3-Compatible Vault). |
| **Audit Log** | ആര്, എപ്പോൾ, എന്ത് മാറ്റം വരുത്തി (പ്രത്യേകിച്ച് Partner Sign-off, Payout, Settings, Document delete) എന്നതിന്റെ സമ്പൂർണ്ണ ലോഗ്. |

---

## 3. സ്ഥാപനത്തിന്റെ തുടക്കം മുതൽ ദൈനംദിന ഉപയോഗം വരെ (End-to-End Workflow)

### ഘട്ടം 1: അക്കൗണ്ട് തുടങ്ങലും ടീം സജ്ജീകരണവും (Setup — ഏകദേശം 30 മിനിറ്റ്)
1. **Create a Firm**: സൈൻ-ഇൻ പേജിൽ `Create a firm` തിരഞ്ഞെടുത്ത് ഫേമിന്റെ പേര്, പാർട്ണറുടെ പേര്, ഇമെയിൽ, പാസ്‌വേഡ് എന്നിവ നൽകി Terms of Service, Privacy Notice, Data Processing Terms (DPA) എന്നിവ അംഗീകരിക്കുക. ആദ്യം അക്കൗണ്ട് എടുക്കുന്നയാൾ ഫേമിന്റെ **Partner** ആകും. *(വേണമെങ്കിൽ ട്രയലിനായി 3 സാമ്പിൾ ക്ലയന്റുകളുള്ള ഡെമോ ഡാറ്റ വൺ-ക്ലിക്കിൽ ലോഡ് ചെയ്യാം).*
2. **2FA (Two-Step Sign-in) ഓൺ ചെയ്യുക**: സൈഡ്‌ബാറിന്റെ താഴെയുള്ള *My Account* തുറന്ന് Authenticator App (Google Authenticator / Microsoft Authenticator മുതലായവ) സ്കാൻ ചെയ്ത് 2FA സെറ്റ് ചെയ്യുക.
3. **ടീം അംഗങ്ങളെ ചേർക്കുക (`Team`)**: Manager, Staff / Article Assistant തുടങ്ങി വിവിധ റോളുകളിൽ ടീമിനെ ഇമെയിൽ വഴി ക്ഷണിക്കുക. ഓരോ റോളിനും കാണാവുന്ന ഡെസ്കുകളും ചെയ്യാവുന്ന പ്രവൃത്തികളും പെർമിഷനുകൾ വഴി നിയന്ത്രിക്കപ്പെടുന്നു.

### ഘട്ടം 2: ക്ലയന്റുകളെ ചേർക്കലും എക്സൽ ഇമ്പോർട്ടും (`Clients`)
- **ഒന്നൊന്നായി ചേർക്കൽ**: വ്യക്തികൾ, കമ്പനികൾ, പാർട്ണർഷിപ്പ് ഫേമുകൾ, LLP, ട്രസ്റ്റുകൾ എന്നിവയെല്ലാം PAN, GSTIN, TAN, Custom Fields, Client Groups എന്നിവ സഹിതം ചേർക്കാം.
- **Excel / CSV Bulk Import**: നൂറുകണക്കിന് ക്ലയന്റുകളെ ഒറ്റയടിക്ക് കൊണ്ടുവരാൻ *Import from Excel* ഉപയോഗിക്കാം:
  1. ടെംപ്ലേറ്റ് ഡൗൺലോഡ് ചെയ്ത് വിവരങ്ങൾ നിറയ്ക്കുക (Client Name മാത്രമാണ് നിർബന്ധം; Code ഒഴിച്ചിട്ടാൽ സിസ്റ്റം സ്വയം നമ്പർ നൽകും).
  2. ഫയൽ അപ്‌ലോഡ് ചെയ്യുമ്പോൾ **Dry-run Preview** ലഭിക്കും — സേവ് ചെയ്യുന്നതിന് മുൻപ് തന്നെ ഏതൊക്കെ വരികൾ ശരിയാണെന്നും ഏതിലൊക്കെ PAN/GSTIN തെറ്റുണ്ടെന്നും കാണിച്ചുതരും.
  3. തെറ്റുകൾ തിരുത്തി കൺഫേം ചെയ്താൽ എല്ലാ ക്ലയന്റുകളും സേവ് ആകും (പ്ലാൻ പരിധി സിസ്റ്റം സ്വയം പരിശോധിക്കും).

### ഘട്ടം 3: Today സ്ക്രീനും ഡെഡ്‌ലൈൻ/ടാസ്ക് മാനേജ്‌മെന്റും (`Today`, `Deadlines`, `Tasks`)
- **Today Home Screen**: ആപ്പ് തുറക്കുമ്പോൾ തന്നെ കാലാവധി കഴിഞ്ഞ ഫയലിംഗുകൾ (Overdue), ഈ ആഴ്ചയിലെ ഡെഡ്‌ലൈനുകൾ, സ്വന്തം ടാസ്‌ക്കുകൾ എന്നിവ ഒറ്റനോട്ടത്തിൽ കാണാം.
- **Deadlines**: ക്ലയന്റിന്റെ രജിസ്ട്രേഷൻ (GSTIN, TAN, PAN, Company CIN) അടിസ്ഥാനമാക്കി **GSTR-1, GSTR-3B, TDS, ITR, Advance Tax, MCA/ROC** ഡ്യൂ ഡേറ്റുകൾ ഓട്ടോമാറ്റിക്കായി കലണ്ടറിൽ വരും. ഓരോന്നും കൃത്യമായ സ്റ്റാഫിന് അസൈൻ ചെയ്യാം.
- **Tasks & Review**: ടാസ്‌ക്കുകൾക്ക് Checklist-ഉം ഡോക്യുമെന്റ് അറ്റാച്ച്‌മെന്റും ഉണ്ട്. ജോലി പൂർത്തിയായാൽ Partner/Manager റിവ്യൂ ചെയ്ത് അംഗീകരിക്കുന്ന രീതിയാണുള്ളത് (`task.review_required`).
- **Quick Navigation**: `Ctrl + K` (Mac-ൽ `⌘ + K`) അമർത്തി ഏത് ഡെസ്കിലേക്കും നേരിട്ട് പോകാം; സ്ഥിരം ഉപയോഗിക്കുന്ന ഡെസ്കുകൾ സൈഡ്‌ബാറിൽ Pin ചെയ്തുവയ്ക്കാം.

---

## 4. പ്രധാന മോഡ്യൂളുകളുടെ വിശദമായ പ്രവർത്തനം

### A. Accounting & Data Hub
1. **Trial Balance Hub**: Tally Prime (XML/CSV), Busy, SAP അല്ലെങ്കിൽ Excel-ൽ നിന്ന് നേരിട്ട് Trial Balance കോപ്പി-പേസ്റ്റ് ചെയ്തോ അപ്‌ലോഡ് ചെയ്തോ എടുക്കാം. അക്കൗണ്ട് ഹെഡുകൾ Schedule III, Form 3CD, CARO 2020 എന്നിവയിലേക്ക് ഓട്ടോമാറ്റിക്കായി മാപ്പ് ചെയ്യപ്പെടും.
2. **Bank Feeds & BRS** *(Growth പ്ലാൻ / Feature toggle)*: PDF/Excel/CSV ബാങ്ക് സ്റ്റേറ്റ്‌മെന്റുകൾ അപ്‌ലോഡ് ചെയ്ത് ബുക്കുകളുമായി താരതമ്യം ചെയ്യാം. Income Tax സെക്ഷനുകളായ **Sec 40A(3)** (₹10,000 മുകളിലുള്ള പണമിടപാട്), **Sec 269ST**, round-tripping തുടങ്ങിയ ഫോറൻസിക് അപാകതകൾ സിസ്റ്റം കണ്ടെത്തും.
3. **Fixed Assets & Depreciation**: കമ്പനീസ് ആക്ട് Schedule II / Income Tax ആക്ട് സെക്ഷൻ 32 പ്രകാരമുള്ള ഫിക്സഡ് അസറ്റ് രജിസ്റ്ററും ഡിപ്രീസിയേഷൻ ഷെഡ്യൂളും.

---

### B. GST ഫയലിംഗ് സ്യൂട്ട് (`GSTR-1 & 3B`, `GST 2B Match`, `GSTR-9 / 9C`)

പൈലറ്റ് ഫേമുകൾക്കും ദൈനംദിന പ്രാക്ടീസിനും ഏറ്റവും ഉപകാരപ്രദമായ ഡെസ്ക് ആണിത്. കണക്കുകൂട്ടലുകൾ വേഗത്തിൽ ബ്രൗസറിൽ തന്നെ നടക്കുന്നു.

1. **GSTR-1 (Sales Register-ൽ നിന്ന് JSON നിർമ്മാണം)**:
   - ക്ലയന്റിന്റെ Tally/Billing Excel (`.xlsx`) അല്ലെങ്കിൽ `.csv` അപ്‌ലോഡ് ചെയ്യുക (*Voucher No, GSTIN/UIN, Taxable Amount, Integrated Tax* തുടങ്ങിയ സാധാരണ ഹെഡറുകൾ സിസ്റ്റം തനിയെ തിരിച്ചറിയും).
   - **ഓട്ടോമാറ്റിക് പരിശോധനകൾ**: തെറ്റായ GSTIN, നിരക്കും നികുതിയും തമ്മിലുള്ള വ്യത്യാസം, Place of Supply vs IGST/CGST+SGST തെറ്റുകൾ, ഇൻവോയ്സ് നമ്പറിംഗിലെ വിടവുകൾ (gaps), ഭാവി തീയതികൾ എന്നിവ Errors/Warnings ആയി കാണിക്കും.
   - **തയ്യാറാക്കുന്ന ടേബിളുകൾ**: `B2B`, `B2CL` (അന്തർസംസ്ഥാന B2C ₹1 ലക്ഷത്തിന് മുകളിൽ), `B2CS`, `CDNR` / `CDNUR`, `Exports` (WPAY/WOPAY), `Nil/Exempt`, `HSN Summary` (B2B/B2C വേർതിരിച്ച്), `Table 13` (ഇഷ്യൂ ചെയ്തതും ക്യാൻസൽ ചെയ്തതുമായ ഡോക്യുമെന്റുകൾ), `Amendments` (`B2BA`, `B2CLA`, `CDNRA`, `EXPA`).
   - തയ്യാറായ **GSTN Offline-Tool JSON** ഡൗൺലോഡ് ചെയ്യുകയോ നേരിട്ട് ക്ലയന്റിന്റെ **Document Vault**-ലേക്ക് സേവ് ചെയ്യുകയോ ചെയ്യാം.
   - *ശ്രദ്ധിക്കുക: പോർട്ടലിൽ അപ്‌ലോഡ് ചെയ്യുന്നതിന് മുൻപ് JSON ഫയൽ ഔദ്യോഗിക GSTN Offline Tool-ലോ പോർട്ടലിലോ ഇമ്പോർട്ട് ചെയ്ത് പരിശോധിച്ച് ഉറപ്പുവരുത്തേണ്ട ഉത്തരവാദിത്തം സ്ഥാപനത്തിനാണ്.*

2. **GSTR-3B Check (പ്രതിമാസ / QRMP പരിശോധന)**:
   - 2025 ജൂലൈ മുതൽ GSTR-3B Table 3 GSTR-1-ൽ നിന്ന് പോർട്ടൽ ഓട്ടോ-ഫിൽ ചെയ്യുന്നതിനാൽ, ഫയൽ ചെയ്യുന്നതിന് മുൻപ് കൃത്യത ഉറപ്പിക്കാൻ GSTR-3B Check സഹായിക്കുന്നു.
   - അതേ Sales Register-ൽ നിന്നുള്ള `Table 3.1 / 3.2`, *GST 2B Match*-ൽ നിന്നുള്ള `Table 4 ITC`, **Rule 88A / Sec 49(5)** പ്രകാരമുള്ള Credit vs Cash സെറ്റ്-ഓഫ് (RCM എപ്പോഴും ക്യാഷ് വഴി), **Sec 50 (18% പലിശ)**, **Sec 47 / Notification 19/2021** പ്രകാരമുള്ള Late Fee കണക്കാക്കുന്നു.
   - കേരളം ഉൾപ്പെടെയുള്ള സംസ്ഥാനങ്ങളിലെ **QRMP (22nd / 24th)** ഡ്യൂ ഡേറ്റുകളും Monthly (20th) ഡ്യൂ ഡേറ്റുകളും പിന്തുണയ്ക്കുന്നു.

3. **GSTR-9 / 9C Desk** *(Growth പ്ലാൻ)*:
   - ഒരു സാമ്പത്തിക വർഷത്തെ മുഴുവൻ Sales Register-ൽ നിന്നും **Tables 4, 5, 17** കണക്കാക്കുന്നു (`4F/4G` അഡ്വാൻസുകൾ നൽകിയാൽ `4H/4N/5N` എന്നിവയിലേക്ക് തനിയെ അപ്‌ഡേറ്റ് ആകും). വർക്കിംഗുകൾ CSV / JSON ആയി ഡൗൺലോഡ് ചെയ്യാം.

---

### C. Direct Tax & Income Tax (`Tax Desk`, `Presumptive Tax`, `Tax Audit 3CD`, `TDS Desk`)

1. **Income Tax കണക്കുകൂട്ടലും JSON എക്സ്പോർട്ടും**:
   - **Tax Desk**: സാലറി, ബിസിനസ്, ക്യാപിറ്റൽ ഗെയിൻസ് (`111A/112A` 15% സർചാർജ് ക്യാപ്പ് ഉൾപ്പെടെ), Chapter VI-A / `80G` കിഴിവുകൾ, `₹50L / ₹1Cr / ₹2Cr / ₹5Cr` പരിധികളിലെ Marginal Relief എന്നിവ സഹിതം **New Regime (Sec 115BAC) vs Old Regime** താരതമ്യം ചെയ്യുന്നു.
   - **Presumptive Tax (`Sec 44AD / 44ADA / 44AE`)**: ഡിജിറ്റൽ vs ക്യാഷ് ടേൺഓവർ, Sec 44AA Minimal Balance Sheet വിവരങ്ങൾ.
   - **Tax Audit (`Form 3CA/3CB-3CD`)**: ക്ലോസുകൾ 1 മുതൽ 44 വരെയുള്ള വിവരങ്ങളും ഡിസ്‌ക്ലോഷറുകളും.
   - **ഔദ്യോഗിക CBDT JSON Export (AY 2026-27)**: ഉൾച്ചേർത്ത ഔദ്യോഗിക സ്കീമകൾ (`ITR-1_2026_Main_V1.1`, `ITR-4_2026_Main_V1.1`, `schema_3CA` / `schema_3CB V2.5`) ഉപയോഗിച്ച് സ്കീമ വാലിഡേഷൻ നടത്തി **ITR-1, ITR-4, Form 3CA/3CB-3CD JSON** ഡൗൺലോഡ് ചെയ്യാം / Document Vault-ൽ സൂക്ഷിക്കാം.

2. **TDS Desk (`24Q`, `26Q`, `27Q`, `27EQ`)**:
   - ചലാൻ മാനേജ്‌മെന്റ്, ഡിഡക്ഷൻ എൻട്രികൾ, Lower Deduction Certificate ട്രാക്കിംഗ്, Deductor Profile.
   - **Dual Act സപ്പോർട്ട്**: Income Tax Act 1961 (FY ≤ 2025-26: `24Q/26Q/27Q/27EQ`), Income Tax Act 2025 (FY ≥ 2026-27: Forms `138/140/144/143`).
   - Form 16/16A CSV ഇമ്പോർട്ട്, TAN/PAN ഫോർമാറ്റ് പരിശോധന, **NSDL/Protean Fixed-Caret RPU `.txt`** ഫയൽ ജനറേഷൻ (Protean Java FVU യൂട്ടിലിറ്റി വഴി `.fvu` ആക്കാനുള്ളത്), കൂടാതെ FVU `.err` എറർ ഫയലുകൾ വായിക്കാനുള്ള സൗകര്യം.

3. **Advance Tax & Tax Notices**: 234A/B/C പലിശ കണക്കുകൂട്ടലും ഇൻകം ടാക്സ് നോട്ടീസ്/സ്ക്രൂട്ടിനി ഹിയറിംഗ് ട്രാക്കിംഗും.

---

### D. Statutory Audit & Assurance (`Audit Workspace` മുതൽ `AQMM` വരെ)

*Audit Workspace, CARO, Schedule III, AQMM എന്നിവ Growth പ്ലാനിലും; Transfer Pricing, Cost Audit, Secretarial Audit എന്നിവ Enterprise പ്ലാനിലും ലഭ്യമാണ്.*

1. **Audit Engagement & Working Papers**: എൻഗേജ്‌മെന്റ് തുടക്കം മുതൽ **17 Lead Schedules (A–Q)**, സാംപ്ലിംഗ്, റിവ്യൂ നോട്ടുകൾ, EQR (Engagement Quality Review).
2. **Partner Quick-Sign & UDIN Guardrail**: EQR ക്ലിയർ ചെയ്തതും Partner Sign-off സ്റ്റേജിൽ എത്തിയതുമായ ഓഡിറ്റുകളിൽ മാത്രമേ **Partner** പദവിയുള്ളവർക്ക് സൈൻ-ഓഫ് ചെയ്യാൻ അനുവാദമുള്ളൂ. 18-അക്ക **UDIN** ഫോർമാറ്റ് പരിശോധിച്ച് പ്രത്യേകം സേവ് ചെയ്യുകയും Audit Log എഴുതുകയും ചെയ്യുന്നു.
3. **SA 230 Digital Dossier & SA 700 Report (PDF)**:
   - SQC 1 പ്രകാരം 60-ദിവസത്തെ ഇമ്മ്യൂട്ടബിൾ ആർക്കൈവ് ലോക്ക് + **SHA-256 Tamper-Evident സീൽ**.
   - **ഒറ്റ ക്ലിക്കിൽ PDF**: ബ്രൗസറിൽ തന്നെ Bookmarks, പേജ് നമ്പറുകൾ, repeating table headers, SHA-256 സീൽ (ഫൂട്ടറിൽ) എന്നിവയുള്ള A4 PDF ഡൗൺലോഡ് ചെയ്യാനോ ക്ലയന്റ് ഡോക്യുമെന്റ് വോൾട്ടിൽ സേവ് ചെയ്യാനോ കഴിയും.
4. **SA 505 Automated External Confirmations**: Debtors/Creditors/Banks-ലേക്ക് സുരക്ഷിത ടോക്കൺ ലിങ്ക് അയച്ച് തേർഡ്-പാർട്ടി ബാലൻസ് കൺഫർമേഷൻ നേരിട്ട് ശേഖരിക്കാം.
5. **CARO 2020 & Schedule III**: 21-ക്ലോസ് CARO ചെക്ക്‌ലിസ്റ്റ്, Schedule III Notes to Accounts & 11 Ratios, Branch Audit Consolidation (SA 600), Audit Committee (SA 260), Internal Audit (RCM/IFC), **ICAI AQMM v1.0** (600-point maturity scoring).

---

### E. Corporate, Global & Office Features

- **സ്ഥിരം കോർപ്പറേറ്റ് ഡെസ്കുകൾ**:
  - **MCA / ROC**: കമ്പനി മാസ്റ്റർ, DIN, വാർഷിക ഫയലിംഗുകൾ, ബോർഡ് റെസലൂഷനുകൾ.
  - **DSC Custody**: ക്ലയന്റുകളുടെ USB Digital Signature Token ഓഫീസിൽ എപ്പോൾ വാങ്ങി, ആർക്കൊക്കെ കൈമാറി, എപ്പോൾ തിരിച്ചുനൽകി എന്ന കസ്റ്റഡി ലോഗ്.
  - **Foreign Remittance (`15CA/15CB`)** *(Growth)*: Section 195 / DTAA / Form 15CA-15CB & RBI FIRMS/FLA കണക്കുകൂട്ടലുകൾ.
  - **Engagements & KYC**, **Related Party (`Sec 188`)**, **Payroll & PF/ESI**.
- **Feature-Flagged സ്പെഷ്യലിസ്റ്റ് മോഡ്യൂളുകൾ** *(ആവശ്യമുള്ള ഫേമുകൾക്ക് Settings-ൽ ഓൺ ചെയ്യാവുന്നവ — സ്ഥിരമായി ഓഫ്)*: Connected Banking Payouts (Maker-Checker നിയന്ത്രണമുള്ളവ), E-Invoicing JSON Builder, Tally/Busy Live Sync, Tax Litigation (ITAT/HC Paper Book), SEBI BRSR/CSR, IBC/CIRP, UAE Corporate Tax, Practice Valuation & Health.
- **ഓഫീസ് മാനേജ്‌മെന്റ് API (Sprint 64)**: ജീവനക്കാരുടെ Attendance Check-in/out, Leave Balances (Casual/Sick/Earned/Exam) & Leave Approvals (ലീവിലുള്ള സ്റ്റാഫിന് ഓട്ടോ ടാസ്ക് അസൈൻ ചെയ്യില്ല), Office Expenses & Client Reimbursables, Inward/Outward Document Register (`INW`/`OUT`) എന്നിവയ്ക്കുള്ള ബാക്കെൻഡ് സർവീസുകളും ഡാറ്റാബേസ് സുരക്ഷയും സജ്ജമാണ്.

---

## 5. ഡോക്യുമെന്റ് വോൾട്ട്, ക്ലയന്റ് പോർട്ടൽ, ബില്ലിംഗ് & Razorpay

### 1. Document Vault & Client Upload Links
- ഓരോ ക്ലയന്റിനും സാമ്പത്തിക വർഷവും ഫയലിംഗും (`GSTR-1`, `ITR`, `TAX_AUDIT` മുതലായവ) തിരിച്ച് ഫയലുകൾ സൂക്ഷിക്കാം.
- **Upload Link**: ക്ലയന്റിന് അക്കൗണ്ടോ പാസ്‌വേഡോ ഇല്ലാതെ തന്നെ സുരക്ഷിത ലിങ്ക് (Expiry & File limit ഉള്ളത്) വഴി രേഖകൾ അപ്‌ലോഡ് ചെയ്യാൻ നൽകാം.
- **Storage**: സെർവർ ലോക്കൽ ഡിസ്കിലും (`/documents`), അല്ലെങ്കിൽ `AWS S3 / Cloudflare R2 / MinIO / Wasabi` തുടങ്ങിയ S3-Compatible ക്ലൗഡ് ബക്കറ്റുകളിലും (`Documents:Storage=S3`) സൂക്ഷിക്കാം.

### 2. GST Tax Invoices & Razorpay ഓൺലൈൻ ഫീസ് കളക്ഷൻ
- **Rule 46 GST Invoices**: സ്ഥാപനത്തിന്റെയും ക്ലയന്റിന്റെയും GSTIN, Place of Supply, SAC കോഡ് (`998221/998222`) എന്നിവ പ്രകാരം **കേരളത്തിനകത്തുള്ള ക്ലയന്റുകൾക്ക് CGST + SGST** ആയും, **പുറത്തുള്ളവർക്ക് IGST** ആയും ബിൽ തയ്യാറാകും. Timesheets-ഉം ക്ലയന്റ് റീഇംബേഴ്‌സബിൾ ചെലവുകളും ഇൻവോയ്‌സിലേക്ക് എടുക്കാം.
- **Razorpay Fee Collection**:
  - *Settings → Payments*-ൽ ഫേമിന്റെ സ്വന്തം Razorpay `Key ID`, `Key Secret`, `Webhook Secret` (`SecretStore`-ൽ എൻക്രിപ്റ്റ് ചെയ്തത്) ചേർക്കുക.
  - അയച്ച ഇൻവോയ്‌സിൽ **Payment Link** ക്രിയേറ്റ് ചെയ്ത് ക്ലയന്റിന് അയക്കാം.
  - ക്ലയന്റ് UPI/Card/NetBanking വഴി പണം അയക്കുമ്പോൾ **നേരിട്ട് ഫേമിന്റെ Razorpay അക്കൗണ്ടിലേക്കെത്തും** (CaDesk പണം കൈവശം വയ്ക്കില്ല). Webhook (`/api/webhooks/razorpay/{firmId}`) സിഗ്നേച്ചർ പരിശോധിച്ച് ഇൻവോയ്‌സ് ഓട്ടോമാറ്റിക്കായി `Paid` അല്ലെങ്കിൽ `Part-Paid` ആക്കും.

### 3. Notifications (Email & WhatsApp)
- ഇമെയിൽ (SMTP, SendGrid, Amazon SES) & WhatsApp Business Cloud API ഔട്ട്ബോക്സ് വഴി ഡെഡ്‌ലൈൻ, ഇൻവോയ്‌സ് ഓർമ്മപ്പെടുത്തലുകൾ അയക്കാം. *(നിലവിൽ പ്രൊഡക്ഷനിൽ Email വഴി സജ്ജമാണ്; WhatsApp Cloud API ഓൺ ചെയ്യണമെങ്കിൽ Meta Business നമ്പറും Template approval-ഉം `WHATSAPP_MODE=Cloud` ഉം ആവശ്യമാണ്).*

---

## 6. പ്ലാനുകളും നിരക്കുകളും (`Plans & Limits`)

എല്ലാ പ്ലാനുകൾക്കും **14 ദിവസത്തെ Free Trial** ലഭ്യമാണ് (SAC `998311` പ്രകാരം +18% GST).

| പ്ലാൻ | നിരക്ക് (GST പുറമെ) | Users | Clients | ഉൾപ്പെടുന്ന പ്രധാന ഡെസ്കുകൾ |
| --- | --- | --- | --- | --- |
| **Starter (Solo CA)** | `₹1,499/മാസം` അല്ലെങ്കിൽ `₹14,999/വർഷം` | 3 | 100 | Practice (Tasks, Deadlines, Clients, Billing & Timesheets, Client Portal), Trial Balance, Fixed Assets, Income Tax (`ITR-1/4`), Advance Tax, TDS (`RPU`), Notices, Form 3CD, **GSTR-1 & 3B Check**, MCA, KYC, DSC, Payroll |
| **Growth (Mid Firm)** | `₹4,999/മാസം` അല്ലെങ്കിൽ `₹49,999/വർഷം` | 15 | 500 | Starter-ലെ എല്ലാം + Unlimited Audits, Audit Workspace, CARO 2020, Schedule III, Branch Audit, Audit Committee, Internal Audit, AQMM, **GST 2B Match**, **GSTR-9/9C**, Bank Feeds, 15CA/15CB, Related Party, Tax Litigation, E-Invoicing, Practice Health |
| **Enterprise** | `₹14,999/മാസം` അല്ലെങ്കിൽ `₹1,49,999/വർഷം` | Unlimited | Unlimited | Growth-ലെ എല്ലാം + Transfer Pricing (`3CEB`), Cost Audit (`CRA 1-4`), Secretarial Audit (`MR-3`), Anti-Profiteering, ESG/BRSR & CSR, IBC/CIRP, UAE Corporate Tax |

> **കേരള പൈലറ്റ് ഓഫർ**: പൈലറ്റിൽ പങ്കെടുക്കുന്ന ആദ്യ 3 കേരള CA സ്ഥാപനങ്ങൾക്ക് **2027 മാർച്ച് 31 വരെ Growth പ്ലാൻ പൂർണ്ണമായും സൗജന്യമായി** നൽകുന്നു.

---

## 7. പ്രൊഡക്ഷൻ ഡിപ്ലോയ്‌മെന്റും ഇൻ-ആപ്പ് സപ്പോർട്ടും (Deployment & Support)

- **Deployment Stack**: `docker-compose.prod.yml` വഴി Caddy (Automatic HTTPS), PostgreSQL 17, .NET 10 API, Web Frontend, ClamAV, Automated AES-256 Backup (`backup-db.sh` / `restore-db.sh`) എന്നിവ ഒറ്റയടിക്ക് പ്രവർത്തിപ്പിക്കാം. Kubernetes (K8s) ഫയലുകളും തയ്യാറാണ്.
- **Pre-Launch Check & Load Test**: ലോഞ്ചിന് മുൻപ് `deploy/precheck-prod.ps1` (അല്ലെങ്കിൽ `.sh`) വഴി ലീഗൽ പ്ലേസ്‌ഹോൾഡറുകൾ, `.env` കീകൾ, `/health/live`, `/health/ready`, `/metrics` എന്നിവ പരിശോധിക്കാം. ഒരേ സമയം 50 ഫേമുകൾ × 500 ക്ലയന്റുകൾ (150 concurrent users) ടെസ്റ്റ് ചെയ്യാനുള്ള `k6` ലോഡ് ടെസ്റ്റ് സ്ക്രിപ്റ്റുകളും ലഭ്യമാണ്.
- **In-App Bug Reporting (`Report a problem`)**: സൈഡ്‌ബാറിന്റെ താഴെയുള്ള ഫ്ലാഗ് ബട്ടൺ അമർത്തി ഉപയോക്താക്കൾക്ക് പ്രശ്നങ്ങൾ റിപ്പോർട്ട് ചെയ്യാം (`PR-XXXXXXXX` റഫറൻസ് നമ്പർ ലഭിക്കും). എന്തെങ്കിലും സെർവർ തകരാറുണ്ടായാൽ `E-XXXXXXXXXX` കോഡോടെ ഓട്ടോമാറ്റിക്കായി രേഖപ്പെടുത്തപ്പെടും.
- **കേരള പൈലറ്റ് ഷെഡ്യൂൾ (`PILOT.md`)**:
  - **2026 നവംബർ 30 – ഡിസംബർ 5**: 3 പൈലറ്റ് സ്ഥാപനങ്ങളുടെ Onboarding (1-hour call, Client Excel import, 2FA, Deadlines).
  - **ഡിസംബർ 11**: നവംബർ മാസത്തെ **GSTR-1** ഫയലിംഗ് (QRMP IFF ഡിസംബർ 13).
  - **ഡിസംബർ 20**: നവംബർ മാസത്തെ **GSTR-3B** (CaDesk 3B ചെക്ക് ഉപയോഗിച്ച്).
  - **ഡിസംബർ 28**: പൈലറ്റ് അവലോകനം (Target: 0 ബ്ലോക്കറുകൾ, ക്ലയന്റിന് GSTR-1 തയ്യാറാക്കാൻ 10 മിനിറ്റിൽ താഴെ).
