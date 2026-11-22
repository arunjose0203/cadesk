using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace CaDesk.Application.Tax;

public record AisTdsEntry(
    string Section,
    string InfoCode,
    string DeductorName,
    string DeductorTan,
    decimal AmountCredited,
    decimal TaxDeducted
);

public record AisSftEntry(
    string InfoCode, // SFT-005 (Savings), SFT-006 (FD), SFT-018 (Dividend), SFT-017 (Securities)
    string Description,
    string ReportingEntity,
    decimal Amount,
    decimal? CostOfAcquisition
);

public record AisTaxPaymentEntry(
    string ChallanType, // "AdvanceTax" (100), "SelfAssessmentTax" (300)
    string BsrCode,
    string ChallanNumber,
    DateOnly TenderDate,
    decimal Amount
);

public record ParsedAisResult(
    string Pan,
    string? TaxpayerName,
    string FinancialYear,
    string AssessmentYear,
    decimal GrossSalary,
    decimal SavingsInterest,
    decimal FixedDepositInterest,
    decimal DividendIncome,
    decimal CapitalGainsStcg,
    decimal CapitalGainsLtcg,
    decimal TotalTds,
    decimal TotalTcs,
    decimal TotalAdvanceTaxPaid,
    decimal TotalSelfAssessmentTaxPaid,
    List<AisTdsEntry> TdsEntries,
    List<AisSftEntry> SftEntries,
    List<AisTaxPaymentEntry> TaxPayments
);

public static class AnnualInformationStatementParser
{
    /// <summary>
    /// Refuses an AIS that belongs to someone else or to another year: an AIS is the taxpayer's own statement, and
    /// importing the wrong one would put another person's income and tax credits into this client's figures.
    /// </summary>
    public static void EnsureBelongsTo(ParsedAisResult ais, IReadOnlyDictionary<string, string> clientTaxIds, string? expectedFinancialYear)
    {
        if (string.IsNullOrWhiteSpace(ais.Pan))
            throw new CaDesk.Application.Abstractions.BusinessRuleException("This AIS file has no PAN, so it can't be matched to the client.", 400);
        if (!clientTaxIds.TryGetValue("PAN", out var clientPan) || string.IsNullOrWhiteSpace(clientPan))
            throw new CaDesk.Application.Abstractions.BusinessRuleException("Add the client's PAN before importing an AIS, so the file can be checked against it.", 400);
        if (!string.Equals(ais.Pan.Trim(), clientPan.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new CaDesk.Application.Abstractions.BusinessRuleException(
                $"This AIS is for PAN {ais.Pan}, but the client's PAN is {clientPan}. Import the client's own AIS.", 400);
        if (!string.IsNullOrWhiteSpace(expectedFinancialYear) && !string.IsNullOrWhiteSpace(ais.FinancialYear)
            && !string.Equals(ais.FinancialYear.Trim(), expectedFinancialYear.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new CaDesk.Application.Abstractions.BusinessRuleException(
                $"This AIS is for FY {ais.FinancialYear}, but this plan is for FY {expectedFinancialYear}.", 400);
    }

    public static ParsedAisResult Parse(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            throw new ArgumentException("AIS JSON content cannot be empty.");

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            string pan = GetStringProp(root, "pan", "PAN", "panNumber") ?? "AABCT1234F";
            string? name = GetStringProp(root, "taxpayerName", "name", "assesseeName");
            string fy = GetStringProp(root, "financialYear", "fy", "financial_year") ?? "2024-25";
            string ay = GetStringProp(root, "assessmentYear", "ay", "assessment_year") ?? "2025-26";

            var tdsList = new List<AisTdsEntry>();
            var sftList = new List<AisSftEntry>();
            var paymentsList = new List<AisTaxPaymentEntry>();

            decimal grossSalary = 0m;
            decimal savingsInterest = 0m;
            decimal fdInterest = 0m;
            decimal dividendIncome = 0m;
            decimal stcg = 0m;
            decimal ltcg = 0m;
            decimal totalTds = 0m;
            decimal totalTcs = 0m;
            decimal advanceTaxPaid = 0m;
            decimal satPaid = 0m;

            decimal salaryFromTds = 0m;

            // 1. Parse TDS/TCS (Part B1 / "tdsTcsDetails" / "tdsDetails" / "taxCreditStatements")
            if (TryGetArray(root, out var tdsArray, "tdsTcsDetails", "tdsDetails", "tds_details", "taxCreditStatements"))
            {
                foreach (var el in tdsArray.EnumerateArray())
                {
                    var sec = GetStringProp(el, "section", "sec", "secCode") ?? "194C";
                    var code = GetStringProp(el, "infoCode", "informationCode", "code") ?? $"TDS-{sec}";
                    var deductor = GetStringProp(el, "deductorName", "tanName", "employerName") ?? "Deductor";
                    var tan = GetStringProp(el, "deductorTan", "tan", "deductorTAN") ?? "";
                    var credited = GetDecimalProp(el, "amountCredited", "grossAmount", "totalAmountPaidOrCredited", "amountPaid");
                    var deducted = GetDecimalProp(el, "taxDeducted", "totalTaxDeducted", "tdsAmount", "tdsClaimed", "tdsDeducted");

                    if (sec.Equals("192", StringComparison.OrdinalIgnoreCase) || code.Contains("192"))
                    {
                        salaryFromTds += credited;
                    }

                    if (sec.StartsWith("206C", StringComparison.OrdinalIgnoreCase))
                    {
                        totalTcs += deducted;
                    }
                    else
                    {
                        totalTds += deducted;
                    }

                    tdsList.Add(new AisTdsEntry(sec, code, deductor, tan, credited, deducted));
                }
            }

            // Direct salary array
            if (TryGetArray(root, out var salArray, "salary", "salaries"))
            {
                foreach (var el in salArray.EnumerateArray())
                {
                    var sal = GetDecimalProp(el, "grossSalary", "salary", "amount");
                    grossSalary += sal;
                    var employer = GetStringProp(el, "employerName", "name") ?? "Employer";
                    var tan = GetStringProp(el, "tan") ?? "";
                    var tds = GetDecimalProp(el, "tdsDeducted", "tds", "taxDeducted");
                    if (tds > 0 && !tdsList.Any(t => t.TaxDeducted == tds && (t.DeductorTan == tan || t.DeductorName.Equals(employer, StringComparison.OrdinalIgnoreCase) || t.Section == "192")))
                    {
                        totalTds += tds;
                        tdsList.Add(new AisTdsEntry("192", "TDS-192", employer, tan, sal, tds));
                    }
                }
            }

            if (grossSalary == 0m && salaryFromTds > 0m)
            {
                grossSalary = salaryFromTds;
            }

            // Direct savings interest array
            if (TryGetArray(root, out var savArray, "savingsInterest", "savings"))
            {
                foreach (var el in savArray.EnumerateArray())
                {
                    savingsInterest += GetDecimalProp(el, "amount", "interestAmount");
                }
            }

            // Direct fixed deposit interest array
            if (TryGetArray(root, out var fdArray, "fixedDepositInterest", "timeDeposits"))
            {
                foreach (var el in fdArray.EnumerateArray())
                {
                    fdInterest += GetDecimalProp(el, "amount", "interestAmount");
                }
            }

            // Direct dividend array
            if (TryGetArray(root, out var divArray, "dividend", "dividends"))
            {
                foreach (var el in divArray.EnumerateArray())
                {
                    dividendIncome += GetDecimalProp(el, "amount", "dividendAmount");
                }
            }

            // Direct securities array
            if (TryGetArray(root, out var secArray, "securities", "shares"))
            {
                foreach (var el in secArray.EnumerateArray())
                {
                    var sale = GetDecimalProp(el, "saleValue", "amount", "consideration");
                    var buy = GetDecimalProp(el, "purchaseValue", "cost", "costOfAcquisition");
                    var isLt = GetStringProp(el, "isLongTerm", "type")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
                               || (el.TryGetProperty("isLongTerm", out var ltProp) && ltProp.ValueKind == JsonValueKind.True);
                    decimal gain = sale - buy;
                    if (gain > 0)
                    {
                        if (isLt) ltcg += gain;
                        else stcg += gain;
                    }
                }
            }

            // 2. Parse SFT (Part B2 / "sftDetails" / "sft_details" / "specifiedFinancialTransactions")
            if (TryGetArray(root, out var sftArray, "sftDetails", "sft_details", "sftTransactions"))
            {
                foreach (var el in sftArray.EnumerateArray())
                {
                    var code = GetStringProp(el, "infoCode", "sftCode", "code") ?? "";
                    var desc = GetStringProp(el, "description", "narrative", "sftDescription") ?? "";
                    var entity = GetStringProp(el, "reportingEntity", "reportingParty", "bankName") ?? "";
                    var amt = GetDecimalProp(el, "amount", "transactionValue", "aggregateAmount");
                    var cost = GetOptionalDecimalProp(el, "costOfAcquisition", "cost");

                    // Heuristics based on SFT codes:
                    // SFT-005 / interest savings
                    if (code.Contains("005") || desc.Contains("savings", StringComparison.OrdinalIgnoreCase))
                    {
                        savingsInterest += amt;
                    }
                    // SFT-006 / time deposit / fixed deposit
                    else if (code.Contains("006") || desc.Contains("time deposit", StringComparison.OrdinalIgnoreCase) || desc.Contains("fixed deposit", StringComparison.OrdinalIgnoreCase) || desc.Contains("term deposit", StringComparison.OrdinalIgnoreCase))
                    {
                        fdInterest += amt;
                    }
                    // SFT-018 / dividend
                    else if (code.Contains("018") || desc.Contains("dividend", StringComparison.OrdinalIgnoreCase))
                    {
                        dividendIncome += amt;
                    }
                    // SFT-017 / sale of securities / mutual funds
                    else if (code.Contains("017") || desc.Contains("share", StringComparison.OrdinalIgnoreCase) || desc.Contains("mutual fund", StringComparison.OrdinalIgnoreCase))
                    {
                        decimal costVal = cost ?? Math.Round(amt * 0.70m, 2);
                        decimal gain = amt - costVal;
                        if (gain > 0)
                        {
                            stcg += gain; // conservative default to STCG unless holding period specified
                        }
                    }

                    sftList.Add(new AisSftEntry(code, desc, entity, amt, cost));
                }
            }

            // 3. Parse Tax Payments (Part B3 / "taxPayments" / "challanDetails" / "advanceTaxChallans")
            if (TryGetArray(root, out var payArray, "taxPayments", "challanDetails", "advanceTaxChallans", "challans"))
            {
                foreach (var el in payArray.EnumerateArray())
                {
                    var typ = GetStringProp(el, "challanType", "minorHead", "type") ?? "AdvanceTax";
                    var bsr = GetStringProp(el, "bsrCode", "bsr", "bankBSRCode") ?? "0210045";
                    var challanNo = GetStringProp(el, "challanNumber", "challanNo", "serialNo") ?? "00001";
                    var dateStr = GetStringProp(el, "tenderDate", "challanDate", "date") ?? "2024-06-15";
                    var amt = GetDecimalProp(el, "amount", "taxPaid", "totalAmount");

                    DateOnly.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var tenderDate);
                    if (tenderDate == default) tenderDate = new DateOnly(2024, 6, 15);

                    if (typ.Contains("Self", StringComparison.OrdinalIgnoreCase) || typ.Contains("300"))
                    {
                        satPaid += amt;
                    }
                    else
                    {
                        advanceTaxPaid += amt;
                    }

                    paymentsList.Add(new AisTaxPaymentEntry(typ, bsr, challanNo, tenderDate, amt));
                }
            }

            return new ParsedAisResult(
                pan,
                name,
                fy,
                ay,
                grossSalary,
                savingsInterest,
                fdInterest,
                dividendIncome,
                stcg,
                ltcg,
                totalTds,
                totalTcs,
                advanceTaxPaid,
                satPaid,
                tdsList,
                sftList,
                paymentsList
            );
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to parse AIS JSON: {ex.Message}", ex);
        }
    }

    private static string? GetStringProp(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var val))
            {
                if (val.ValueKind == JsonValueKind.String) return val.GetString();
                if (val.ValueKind == JsonValueKind.True) return "true";
                if (val.ValueKind == JsonValueKind.False) return "false";
                return val.ToString();
            }
        }
        return null;
    }

    private static decimal GetDecimalProp(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var val))
            {
                if (val.ValueKind == JsonValueKind.Number) return val.GetDecimal();
                if (val.ValueKind == JsonValueKind.String && decimal.TryParse(val.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    return d;
            }
        }
        return 0m;
    }

    private static decimal? GetOptionalDecimalProp(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var val))
            {
                if (val.ValueKind == JsonValueKind.Number) return val.GetDecimal();
                if (val.ValueKind == JsonValueKind.String && decimal.TryParse(val.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    return d;
            }
        }
        return null;
    }

    private static bool TryGetArray(JsonElement el, out JsonElement arrayEl, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out arrayEl) && arrayEl.ValueKind == JsonValueKind.Array)
            {
                return true;
            }
        }
        arrayEl = default;
        return false;
    }
}
