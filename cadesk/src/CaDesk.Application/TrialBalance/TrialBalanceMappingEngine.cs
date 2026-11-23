using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CaDesk.Domain.TrialBalance;

namespace CaDesk.Application.TrialBalance;

/// <summary>
/// Maps imported ledgers to Schedule III groups (and Form 3CD / CARO / fixed-asset categories).
/// Order of trust: the client's saved rule for that exact ledger, then group (the parent group in Tally, Busy, or SAP),
/// then the ledger name. A ledger nothing recognises is left unmapped for the user to review.
/// </summary>
public static class TrialBalanceMappingEngine
{
    public static void ApplyMappings(
        IEnumerable<TrialBalanceLedger> ledgers,
        IEnumerable<ClientLedgerMapping> clientRules)
    {
        // exact ledger name only: a rule saved for "Rent" must not also capture "Rent Received"
        var rules = clientRules
            .GroupBy(r => r.LedgerPattern.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var l in ledgers)
        {
            if (rules.TryGetValue(l.LedgerName.Trim(), out var rule))
            {
                l.Schedule3Group = rule.Schedule3Group;
                l.Form3CdCategory = rule.Form3CdCategory;
                l.CaroCategory = rule.CaroCategory;
                l.FixedAssetBlock = rule.FixedAssetBlock;
                l.IsMapped = true;
                continue;
            }

            l.IsMapped = InferStandardMapping(l);
        }
    }

    /// <summary>Fills the ledger's groups from parent group (Tally/Busy/SAP), then its name. Returns false when neither is recognised.</summary>
    public static bool InferStandardMapping(TrialBalanceLedger l)
    {
        var name = l.LedgerName.Trim().ToLowerInvariant();
        var group = l.TallyGroup.Trim().ToLowerInvariant();

        if (MapByTallyGroup(l, group, name)) return true;
        if (MapByName(l, name)) return true;

        l.Schedule3Group = TrialBalanceConstants.S3OtherExpenses; // placeholder until the user maps it
        return false;
    }

    // ---- Predefined groups across Tally, Busy, and SAP GL classifications ----

    private static bool MapByTallyGroup(TrialBalanceLedger l, string group, string name)
    {
        switch (group)
        {
            case "capital account" or "capital a/c" or "share capital" or "equity" or "equity capital":
                l.Schedule3Group = Has(name, "reserve", "surplus", "profit & loss", "profit and loss", "p&l", "retained")
                    ? TrialBalanceConstants.S3ReservesAndSurplus : TrialBalanceConstants.S3ShareCapital;
                return true;
            case "reserves & surplus" or "reserves and surplus" or "retained earnings" or "reserves":
                l.Schedule3Group = TrialBalanceConstants.S3ReservesAndSurplus;
                return true;
            case "bank od a/c" or "bank od" or "bank occ a/c" or "bank occ" or "bank o/d a/c" or "bank o/d":
                SetShortTermBorrowing(l);
                return true;
            case "secured loans" or "unsecured loans" or "loans (liability)" or "long-term borrowings" or "term loan" or "term loans":
                if (Has(name, "cash credit", "overdraft", "working capital") || HasWord(name, "cc", "od", "occ")) SetShortTermBorrowing(l);
                else { l.Schedule3Group = TrialBalanceConstants.S3LongTermBorrowings; l.CaroCategory = TrialBalanceConstants.CaroTermLoan; }
                return true;
            case "duties & taxes" or "duties and taxes" or "statutory dues" or "tax liabilities":
                SetStatutoryDue(l);
                return true;
            case "provisions" or "short-term provisions":
                l.Schedule3Group = TrialBalanceConstants.S3ShortTermProvisions;
                return true;
            case "sundry creditors" or "trade payables" or "accounts payable" or "vendor payables":
                SetTradePayable(l, name);
                return true;
            case "current liabilities":
                if (IsStatutoryDueName(name)) SetStatutoryDue(l);
                else l.Schedule3Group = TrialBalanceConstants.S3OtherCurrentLiabilities;
                return true;
            case "fixed assets" or "tangible assets" or "property plant and equipment" or "property, plant and equipment":
                SetFixedAsset(l, name);
                return true;
            case "investments" or "non-current investments":
                l.Schedule3Group = TrialBalanceConstants.S3NonCurrentInvestments;
                return true;
            case "bank accounts" or "cash-in-hand" or "cash in hand" or "cash and bank" or "cash and cash equivalents":
                SetCashOrBank(l, name);
                return true;
            case "deposits (asset)" or "security deposits":
                l.Schedule3Group = TrialBalanceConstants.S3LongTermLoansAndAdvances;
                return true;
            case "loans & advances (asset)" or "loans and advances (asset)" or "short-term loans and advances":
                l.Schedule3Group = TrialBalanceConstants.S3ShortTermLoansAndAdvances;
                return true;
            case "stock-in-hand" or "stock in hand" or "inventories" or "inventory":
                l.Schedule3Group = TrialBalanceConstants.S3Inventories;
                l.CaroCategory = TrialBalanceConstants.CaroInventory;
                return true;
            case "sundry debtors" or "trade receivables" or "accounts receivable":
                l.Schedule3Group = TrialBalanceConstants.S3TradeReceivables;
                return true;
            case "current assets" or "other current assets":
                l.Schedule3Group = TrialBalanceConstants.S3OtherCurrentAssets;
                return true;
            case "misc. expenses (asset)" or "misc expenses (asset)" or "other non-current assets":
                l.Schedule3Group = TrialBalanceConstants.S3OtherNonCurrentAssets;
                return true;
            case "sales accounts" or "sales a/c" or "sales" or "direct incomes" or "direct income" or "revenue" or "revenue from operations":
                l.Schedule3Group = TrialBalanceConstants.S3RevenueFromOperations;
                l.Form3CdCategory = TrialBalanceConstants.F3CdTurnover;
                return true;
            case "indirect incomes" or "indirect income" or "other income":
                l.Schedule3Group = TrialBalanceConstants.S3OtherIncome;
                return true;
            case "purchase accounts" or "purchase a/c" or "cost of goods sold" or "cogs" or "materials consumed":
                l.Schedule3Group = TrialBalanceConstants.S3CostOfMaterialsConsumed;
                return true;
            case "direct expenses" or "indirect expenses" or "personnel expenses" or "operating expenses" or "administrative expenses":
                if (!MapExpenseByName(l, name))
                    l.Schedule3Group = (group == "direct expenses" || group.Contains("materials"))
                        ? TrialBalanceConstants.S3CostOfMaterialsConsumed
                        : TrialBalanceConstants.S3OtherExpenses;
                return true;
            default:
                return false;
        }
    }

    // ---- Ledger names, used when the parent group is empty or custom ----

    private static bool MapByName(TrialBalanceLedger l, string name)
    {
        // Expense and income names first, so "Bank Charges" or "Interest on Loan" never land in a balance-sheet group
        if (MapExpenseByName(l, name)) return true;

        if (Has(name, "advance tax", "tds receivable", "tcs receivable", "income tax refund", "gst input", "input cgst", "input sgst", "input igst", "itc receivable") || HasWord(name, "itc"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3OtherCurrentAssets;
            return true;
        }
        if (Has(name, "prepaid", "advance to", "advances to", "advance paid"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3ShortTermLoansAndAdvances;
            return true;
        }
        if (Has(name, "provision for"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3ShortTermProvisions;
            return true;
        }
        if (Has(name, "outstanding", "accrued", "expenses payable", "salary payable", "audit fee payable", "director remuneration payable", "gratuity payable", "bonus payable"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3OtherCurrentLiabilities;
            return true;
        }
        if (Has(name, "security deposit", "rent deposit", "electricity deposit", "telephone deposit"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3LongTermLoansAndAdvances;
            return true;
        }

        if (Has(name, "sales", "revenue from operations", "fees received", "professional fees received", "wholesale sales", "retail sales", "product sales", "export sales", "service revenue", "managed it", "cloud architecture"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3RevenueFromOperations;
            l.Form3CdCategory = TrialBalanceConstants.F3CdTurnover;
            return true;
        }
        if (Has(name, "interest received", "dividend received", "discount received", "rent received", "interest income", "dividend income", "scrap sales", "scrap recovery", "incentives received"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3OtherIncome;
            return true;
        }
        if (Has(name, "sundry debtor", "trade receivable", "customer receivable", "accounts receivable") || HasWord(name, "debtors", "debtor"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3TradeReceivables;
            return true;
        }
        if (Has(name, "sundry creditor", "trade payable", "vendor payable", "accounts payable") || HasWord(name, "creditors", "creditor"))
        {
            SetTradePayable(l, name);
            return true;
        }
        if (Has(name, "cash credit", "overdraft", "working capital facility", "working capital od") || HasWord(name, "od", "cc", "occ"))
        {
            SetShortTermBorrowing(l);
            return true;
        }
        if (Has(name, "term loan", "secured loan", "unsecured loan", "loan from", "bank borrowings"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3LongTermBorrowings;
            l.CaroCategory = TrialBalanceConstants.CaroTermLoan;
            return true;
        }
        if (IsStatutoryDueName(name))
        {
            SetStatutoryDue(l);
            return true;
        }
        if (HasWord(name, "cash", "petty") || Has(name, "bank a/c", "bank account", "current account", "savings account", "fixed deposit", "escrow account"))
        {
            SetCashOrBank(l, name);
            return true;
        }
        if (Has(name, "stock", "inventory", "raw material", "finished goods", "work in progress", "work-in-progress", "trading stock"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3Inventories;
            l.CaroCategory = TrialBalanceConstants.CaroInventory;
            return true;
        }
        if (Has(name, "plant", "machinery", "building", "furniture", "fixture", "computer", "laptop", "server", "vehicle", "motor car", "truck", "godown", "leasehold", "office equipment", "hardware"))
        {
            SetFixedAsset(l, name);
            return true;
        }
        if (Has(name, "share capital", "equity capital", "capital account", "capital a/c"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3ShareCapital;
            return true;
        }
        if (Has(name, "reserve", "surplus", "profit & loss a/c", "profit and loss a/c", "retained earnings", "general reserve"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3ReservesAndSurplus;
            return true;
        }
        return false;
    }

    /// <summary>Classifies profit-and-loss charges by name. Returns false when the name is not a recognised expense.</summary>
    private static bool MapExpenseByName(TrialBalanceLedger l, string name)
    {
        // Balance-sheet items that merely mention an expense: "Prepaid Expenses", "Outstanding Expenses", "Advance Tax"
        if (Has(name, "prepaid", "advance", "payable", "outstanding", "accrued", "receivable", "deposit", "provision for", "refund"))
            return false;

        if (Has(name, "income tax", "current tax", "deferred tax", "tax expense", "mat credit", "corporate tax") && !Has(name, "payable", "refund", "tds", "tcs"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3TaxExpense;
            return true;
        }
        if (Has(name, "depreciation", "amortisation", "amortization"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3DepreciationAndAmortization;
            return true;
        }
        if (Has(name, "interest on loan", "interest paid", "interest expense", "bank charges", "finance charge", "processing fee", "loan processing", "overdraft interest", "term loan interest"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3FinanceCosts;
            return true;
        }
        if (Has(name, "salary", "salaries", "wages", "bonus", "gratuity", "staff welfare", "leave encashment", "director remuneration", "labor", "labour", "payroll", "personnel") ||
            Has(name, "contribution to provident fund", "contribution to pf", "employer contribution", "esic contribution", "esi contribution", "pf expense"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3EmployeeBenefitsExpense;
            if (HasWord(name, "pf", "esi", "esic") || Has(name, "provident"))
                l.Form3CdCategory = TrialBalanceConstants.F3CdSec361vaEmployeeContribution;
            return true;
        }
        if (Has(name, "purchase", "purchases", "raw material consumed", "materials consumed", "freight inward", "carriage inward", "power and fuel", "electricity and power", "factory power", "packaging", "steel", "alloys"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3CostOfMaterialsConsumed;
            return true;
        }
        if (Has(name, "audit fee", "legal fee", "consultancy", "contractor", "contract charges", "technical fee", "commission", "professional fee", "rent paid", "rent expense", "cloud hosting", "software subscription", "advisory"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3OtherExpenses;
            l.Form3CdCategory = TrialBalanceConstants.F3CdTdsDeductibleExpense;
            return true;
        }
        if (Has(name, "expense", "expenses", "charges", "electricity", "telephone", "printing", "stationery", "travelling", "conveyance", "repairs", "insurance", "advertisement", "postage", "maintenance", "godown lease", "utilities"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3OtherExpenses;
            return true;
        }
        return false;
    }

    // ---- Shared setters ----

    private static void SetShortTermBorrowing(TrialBalanceLedger l)
    {
        l.Schedule3Group = TrialBalanceConstants.S3ShortTermBorrowings;
        l.CaroCategory = TrialBalanceConstants.CaroWorkingCapitalBorrowing;
    }

    private static void SetStatutoryDue(TrialBalanceLedger l)
    {
        l.Schedule3Group = TrialBalanceConstants.S3OtherCurrentLiabilities;
        l.Form3CdCategory = TrialBalanceConstants.F3CdSec43BStatutoryDue;
        l.CaroCategory = TrialBalanceConstants.CaroStatutoryDue;
    }

    private static void SetTradePayable(TrialBalanceLedger l, string name)
    {
        if (HasWord(name, "msme", "micro") || Has(name, "small enterprise", "udyam"))
        {
            l.Schedule3Group = TrialBalanceConstants.S3TradePayablesMsme;
            l.Form3CdCategory = TrialBalanceConstants.F3CdSec43BhMsme;
        }
        else l.Schedule3Group = TrialBalanceConstants.S3TradePayablesOthers;
    }

    private static void SetCashOrBank(TrialBalanceLedger l, string name)
    {
        l.Schedule3Group = TrialBalanceConstants.S3CashAndBank;
        // the cash-in-hand ledger carries the cash receipts / payments used for the 44AB 5% cash test
        if (HasWord(name, "cash", "petty")) l.Form3CdCategory = TrialBalanceConstants.F3CdCashReceipt;
    }

    private static void SetFixedAsset(TrialBalanceLedger l, string name)
    {
        l.Schedule3Group = TrialBalanceConstants.S3PropertyPlantEquipment;
        l.FixedAssetBlock =
            Has(name, "computer", "laptop", "server", "hardware", "scanner") ? TrialBalanceConstants.FaComputers
            : Has(name, "building", "office premises", "factory shed", "godown", "leasehold") ? TrialBalanceConstants.FaBuildings
            : Has(name, "vehicle", "motor car", "truck", "van") || HasWord(name, "car", "cars", "vans") ? TrialBalanceConstants.FaVehicles
            : Has(name, "furniture", "fixture") ? TrialBalanceConstants.FaFurnitureAndFixtures
            : Has(name, "software", "goodwill", "patent", "trademark", "licence", "license") ? TrialBalanceConstants.FaIntangibles
            : TrialBalanceConstants.FaPlantAndMachinery;
    }

    private static bool IsStatutoryDueName(string name) =>
        Has(name, "gst payable", "tds payable", "tcs payable", "pf payable", "esi payable", "esic payable", "professional tax payable", "pt payable",
            "output cgst", "output sgst", "output igst", "cgst payable", "sgst payable", "igst payable", "gst output payable");

    /// <summary>Phrase anywhere in the name (use for words long enough not to occur inside other words).</summary>
    private static bool Has(string name, params string[] phrases) => phrases.Any(p => name.Contains(p, StringComparison.Ordinal));

    /// <summary>Whole word only: "od" must not match "goods", "car" must not match "carriage".</summary>
    private static bool HasWord(string name, params string[] words) =>
        words.Any(w => Regex.IsMatch(name, $@"(^|[^a-z0-9]){Regex.Escape(w)}($|[^a-z0-9])"));
}
