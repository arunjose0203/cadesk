using System;
using System.Collections.Generic;

namespace CaDesk.Application.TrialBalance;

public record ParsedTallyLedger(
    string Name,
    string ParentGroup,
    decimal OpeningBalance,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal ClosingBalance
);

public record ParsedTrialBalanceResult(
    List<ParsedTallyLedger> Ledgers,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    bool SignsInverted = false // true when the file used Tally's own convention (debit negative) and was converted
);

/// <summary>
/// Backward-compatible adapter that forwards to UniversalTrialBalanceParser.
/// </summary>
public static class TallyTrialBalanceParser
{
    public static ParsedTrialBalanceResult Parse(string content, string sourceType)
    {
        return UniversalTrialBalanceParser.Parse(content, sourceType);
    }

    public static ParsedTrialBalanceResult NormaliseSignConvention(ParsedTrialBalanceResult parsed)
    {
        return UniversalTrialBalanceParser.NormaliseSignConvention(parsed);
    }

    public static ParsedTrialBalanceResult ParseTallyXml(string xmlContent)
    {
        return UniversalTrialBalanceParser.ParseXml(xmlContent);
    }

    public static ParsedTrialBalanceResult ParseTallyCsv(string csvContent)
    {
        return UniversalTrialBalanceParser.ParseDelimited(csvContent, "TallyCsv");
    }

    public static decimal ParseTallyAmount(string? val)
    {
        return UniversalTrialBalanceParser.ParseAmount(val);
    }
}
