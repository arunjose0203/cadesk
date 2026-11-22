namespace CaDesk.Domain.Common;

/// <summary>
/// Which income-tax law governs a year, and how to cite it.
///
/// The Income-tax Act, 2025 replaced the Income-tax Act, 1961 from 1 April 2026: tax year 2026-27 onward is under the
/// 2025 Act (sections and forms renumbered, "tax year" instead of previous year / assessment year); FY 2025-26 and
/// earlier stay under the 1961 Act. Documents must cite the law of the year they are about.
///
/// Source of every mapping below: CBDT "Utility to check provisions of Income-tax Act, 1961 vis-a-vis Income-tax Act,
/// 2025", "Utility to check Forms under Income-tax Rules, 1962 vis-a-vis Income-tax Rules, 2026" and the text of the
/// 2025 Act, all on incometaxindia.gov.in, checked 22 Nov 2026. Add entries only from those official sources.
/// </summary>
public static class IncomeTaxLaw
{
    /// <summary>First year governed by the Income-tax Act, 2025 (tax year 2026-27).</summary>
    public const int FirstTaxYearUnderAct2025 = 2026;

    public const string Act1961 = "Income-tax Act, 1961";
    public const string Act2025 = "Income-tax Act, 2025";

    /// <summary>"2026-27" → 2026; null when the text has no year.</summary>
    public static int? StartYear(string? financialYear) =>
        financialYear is { Length: >= 4 } && int.TryParse(financialYear.AsSpan(0, 4), out var y) ? y : null;

    /// <summary>True when the year is governed by the Income-tax Act, 2025. A missing year means today's law.</summary>
    public static bool IsAct2025(string? financialYear) =>
        (StartYear(financialYear) ?? CurrentFinancialYearStart()) >= FirstTaxYearUnderAct2025;

    public static string ActName(string? financialYear) => IsAct2025(financialYear) ? Act2025 : Act1961;

    /// <summary>"Tax Year 2026-27" under the 2025 Act; "FY 2025-26 (AY 2026-27)" under the 1961 Act.</summary>
    public static string YearLabel(string financialYear)
    {
        if (IsAct2025(financialYear)) return $"Tax Year {financialYear}";
        var start = StartYear(financialYear);
        return start is null ? $"FY {financialYear}" : $"FY {financialYear} (AY {start + 1}-{(start + 2) % 100:D2})";
    }

    /// <summary>
    /// The provision to cite for a year, given its 1961 reference: Sec("44AB", "2026-27") → "section 63",
    /// Sec("44AB", "2025-26") → "section 44AB". Under the 2025 Act a reference with no official mapping recorded
    /// here is cited explicitly as the 1961 provision, never replaced by a guessed number.
    /// </summary>
    public static string Sec(string ref1961, string? financialYear) =>
        !IsAct2025(financialYear) ? $"section {ref1961}"
        : Sections.TryGetValue(ref1961, out var p) ? $"section {p.Ref2025}"
        : $"section {ref1961} of the {Act1961}";

    /// <summary>Compact form for tables and labels: "Sec 63" / "Sec 44AB" / "Sec 36(1)(va) [1961 Act]".</summary>
    public static string SecShort(string ref1961, string? financialYear) =>
        !IsAct2025(financialYear) ? $"Sec {ref1961}"
        : Sections.TryGetValue(ref1961, out var p) ? $"Sec {p.Ref2025}"
        : $"Sec {ref1961} [1961 Act]";

    /// <summary>For transition-period documents: "section 63 (earlier section 44AB)"; plain under the 1961 Act.</summary>
    public static string SecWithEarlier(string ref1961, string? financialYear) =>
        IsAct2025(financialYear) && Sections.TryGetValue(ref1961, out var p)
            ? $"section {p.Ref2025} (earlier section {ref1961} of the {Act1961})"
            : Sec(ref1961, financialYear);

    /// <summary>Form("3CD", "2026-27") → "Form No. 26"; Form("3CD", "2025-26") → "Form No. 3CD".</summary>
    public static string Form(string form1962, string? financialYear) =>
        "Form No. " + FormNo(form1962, financialYear);

    public static string FormNo(string form1962, string? financialYear) =>
        IsAct2025(financialYear) && Forms.TryGetValue(form1962, out var f) ? f.Form2026 : form1962;

    /// <summary>True when an official 2025-Act counterpart is recorded for this 1961 reference.</summary>
    public static bool IsMapped(string ref1961) => Sections.ContainsKey(ref1961);

    public sealed record Provision(string Ref2025, string Title);
    public sealed record FormMapping(string Form2026, string Title);

    /// <summary>1961 reference → its main 2025 provision (the CBDT utility may list further related sections).</summary>
    public static readonly IReadOnlyDictionary<string, Provision> Sections = new Dictionary<string, Provision>
    {
        // business income
        ["32"] = new("33", "Deduction for depreciation"),
        ["37"] = new("34", "General conditions for allowable deductions"),
        ["40"] = new("35", "Amounts not deductible in certain circumstances"),
        ["40(a)(ia)"] = new("35(b)(i)", "30% of sum payable to a resident without deduction / payment of tax at source"),
        ["40A"] = new("36", "Expenses or payments not deductible in certain circumstances"),
        ["40A(2)(b)"] = new("36(2)", "Excessive or unreasonable payments to specified persons"),
        ["40A(3)"] = new("36(4)", "Payments exceeding Rs. 10,000 a day otherwise than by specified banking or online mode"),
        ["40A(3A)"] = new("36(5)", "Later payment of an earlier-deducted liability exceeding Rs. 10,000 otherwise than by specified mode"),
        ["43B"] = new("37", "Certain deductions allowed on actual payment basis only"),
        ["43B(h)"] = new("37(2)(g)", "Amount payable to a micro or small enterprise beyond the MSMED Act section 15 time limit"),
        ["43CA"] = new("53", "Full value of consideration for transfer of assets other than capital assets"),
        ["145A"] = new("277", "Method of accounting in certain cases"),

        // presumptive taxation, books and tax audit
        ["44AD"] = new("58(2) (Table: Sl. No. 1)", "Presumptive income of an eligible business"),
        ["44AD(4)"] = new("58(7)", "Five-year bar after opting out of presumptive taxation"),
        ["44ADA"] = new("58(2) (Table: Sl. No. 3)", "Presumptive income of a specified profession"),
        ["44AE"] = new("58(2) (Table: Sl. No. 2)", "Presumptive income from plying, hiring or leasing goods carriages"),
        ["44AA"] = new("62", "Maintenance of books of account"),
        ["44AB"] = new("63", "Tax audit"),
        ["44AB(a)"] = new("63(1) (Table: Sl. No. 1(a)/(b))", "Tax audit: business turnover above Rs. 1 crore (Rs. 10 crore where cash is within 5%)"),
        ["44AB(b)"] = new("63(1) (Table: Sl. No. 1(c))", "Tax audit: professional gross receipts above Rs. 50 lakh"),
        ["44AB(c)"] = new("63(1) (Table: Sl. No. 2)", "Tax audit: profit claimed lower than presumptive deemed profit"),
        ["44AB(d)"] = new("63(1) (Table: Sl. No. 2)", "Tax audit: profit claimed lower than presumptive deemed profit"),
        ["44AB(e)"] = new("58(8)", "Books and audit after opting out of presumptive taxation within the five-year bar"),

        // transfer pricing
        ["92A"] = new("162", "Meaning of associated enterprise"),
        ["92BA"] = new("164", "Meaning of specified domestic transaction"),
        ["92C"] = new("165", "Determination of arm's length price"),
        ["92CA"] = new("166", "Reference to Transfer Pricing Officer"),
        ["92D"] = new("171", "Maintenance, keeping and furnishing of information and documents"),
        ["92E"] = new("172", "Report from an accountant (international / specified domestic transactions)"),
        ["271BA"] = new("447", "Penalty for failure to furnish report under section 172"),

        // tax deduction at source and advance tax
        ["192"] = new("392", "Salary and accumulated balance due to an employee"),
        ["194A"] = new("393", "Tax to be deducted at source"),
        ["194C"] = new("393", "Tax to be deducted at source"),
        ["194H"] = new("393", "Tax to be deducted at source"),
        ["194-I"] = new("393", "Tax to be deducted at source"),
        ["194J"] = new("393", "Tax to be deducted at source"),
        ["194Q"] = new("393", "Tax to be deducted at source"),
        ["195"] = new("393", "Tax to be deducted at source (payments to non-residents)"),
        ["195A"] = new("393", "Tax to be deducted at source (grossing up)"),
        ["197"] = new("395", "Certificates (lower / nil deduction)"),
        ["201"] = new("398", "Consequences of failure to deduct or pay"),
        ["206AA"] = new("397", "Compliance and reporting (PAN)"),
        ["207"] = new("403", "Liability for payment of advance tax"),
        ["208"] = new("404", "Conditions of liability to pay advance tax"),
        ["211"] = new("408", "Instalments of advance tax and due dates"),
        ["234A"] = new("423", "Interest for defaults in furnishing return of income"),
        ["234B"] = new("424", "Interest for defaults in payment of advance tax"),
        ["234C"] = new("425", "Interest for deferment of advance tax"),

        // computation, deductions and rates
        ["10"] = new("11", "Incomes not included in total income"),
        ["16"] = new("19", "Deductions from salaries"),
        ["24"] = new("22", "Deductions from income from house property"),
        ["80C"] = new("123", "Deduction for life insurance premia, deferred annuity, contributions etc."),
        ["80CCD"] = new("124", "Deduction in respect of contribution to pension scheme"),
        ["80D"] = new("126", "Deduction in respect of health insurance premia"),
        ["80E"] = new("129", "Deduction in respect of interest on loan for higher education"),
        ["80G"] = new("133", "Deduction in respect of donations"),
        ["80TTA"] = new("153", "Deduction for interest on deposits"),
        ["87A"] = new("156", "Rebate of income-tax in case of certain individuals"),
        ["90"] = new("159", "Agreement with foreign countries or specified territories"),
        ["111A"] = new("196", "Tax on short-term capital gains in certain cases"),
        ["112"] = new("197", "Tax on long-term capital gains"),
        ["112A"] = new("198", "Tax on long-term capital gains in certain cases"),
        ["115BAA"] = new("200", "Tax on income of certain domestic companies"),
        ["115BAC"] = new("202", "New tax regime for individuals, HUF and others"),
        ["115BBE"] = new("195", "Tax on income referred to in sections 102 to 106"),

        // procedure, cash transactions
        ["139"] = new("263", "Return of income"),
        ["143"] = new("270", "Assessment"),
        ["148"] = new("280", "Issue of notice where income has escaped assessment"),
        ["156"] = new("289", "Notice of demand"),
        ["220"] = new("411", "When tax payable and when assessee deemed in default"),
        ["249"] = new("358", "Form of appeal and limitation"),
        ["269SS"] = new("185", "Mode of taking or accepting certain loans, deposits and specified sum"),
        ["269ST"] = new("186", "Mode of undertaking transactions"),
        ["269T"] = new("188", "Mode of repayment of certain loans or deposits or specified sum"),
        // 206AB has no counterpart: it was omitted from 1 April 2025 (CBDT utility shows none)
    };

    /// <summary>Income-tax Rules 1962 form → Income-tax Rules 2026 form.</summary>
    public static readonly IReadOnlyDictionary<string, FormMapping> Forms = new Dictionary<string, FormMapping>
    {
        ["3CA"] = new("26", "Audit report and statement of particulars under section 63"),
        ["3CB"] = new("26", "Audit report and statement of particulars under section 63"),
        ["3CD"] = new("26", "Audit report and statement of particulars under section 63"),
        ["3CEB"] = new("48", "Report from an accountant under section 172"),
        ["3CEAA"] = new("56", "Information by a constituent entity under section 171(4)"),
        ["15CA"] = new("145", "Information for payments to a non-resident"),
        ["15CB"] = new("146", "Certificate of an accountant for payments to a non-resident"),
        ["10F"] = new("41", "Information under section 159(8)"),
        ["13"] = new("128", "Application for lower or nil deduction / collection certificate"),
        ["16"] = new("130", "Certificate of tax deducted at source on salary"),
        ["16A"] = new("131", "Certificate of tax deducted at source other than salary"),
        ["24Q"] = new("138", "Quarterly TDS statement: salary"),
        ["26Q"] = new("140", "Quarterly TDS statement: payments other than salary"),
        ["27Q"] = new("144", "Quarterly TDS statement: payments to non-residents"),
        ["26AS"] = new("168", "Annual Information Statement"),
    };

    private static int CurrentFinancialYearStart()
    {
        var today = DateTime.UtcNow;
        return today.Month >= 4 ? today.Year : today.Year - 1;
    }
}
