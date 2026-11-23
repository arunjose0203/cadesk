using System;
using System.Collections.Generic;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.TrialBalance;

public static class TrialBalanceConstants
{
    // Source types
    public const string SourceTallyXml = "TallyXml";
    public const string SourceTallyCsv = "TallyCsv";
    public const string SourceBusyCsv = "BusyCsv";
    public const string SourceSapGl = "SapGl";
    public const string SourceExcel = "Excel";
    public const string SourceClipboardTsv = "ClipboardTsv";
    public const string SourceGenericCsv = "GenericCsv";
    public const string SourceAuto = "Auto";

    // Statuses
    public const string StatusDraft = "Draft";
    public const string StatusMapped = "Mapped";
    public const string StatusSynced = "Synced";

    // Schedule III Groupings (Balance Sheet & P&L Taxonomy)
    public const string S3ShareCapital = "ShareCapital";
    public const string S3ReservesAndSurplus = "ReservesAndSurplus";
    public const string S3LongTermBorrowings = "LongTermBorrowings";
    public const string S3DeferredTaxLiabilities = "DeferredTaxLiabilities";
    public const string S3OtherLongTermLiabilities = "OtherLongTermLiabilities";
    public const string S3LongTermProvisions = "LongTermProvisions";
    public const string S3ShortTermBorrowings = "ShortTermBorrowings";
    public const string S3TradePayablesMsme = "TradePayablesMsme";
    public const string S3TradePayablesOthers = "TradePayablesOthers";
    public const string S3OtherCurrentLiabilities = "OtherCurrentLiabilities";
    public const string S3ShortTermProvisions = "ShortTermProvisions";

    public const string S3PropertyPlantEquipment = "PropertyPlantEquipment";
    public const string S3CapitalWip = "CapitalWip";
    public const string S3NonCurrentInvestments = "NonCurrentInvestments";
    public const string S3DeferredTaxAssets = "DeferredTaxAssets";
    public const string S3LongTermLoansAndAdvances = "LongTermLoansAndAdvances";
    public const string S3OtherNonCurrentAssets = "OtherNonCurrentAssets";

    public const string S3CurrentInvestments = "CurrentInvestments";
    public const string S3Inventories = "Inventories";
    public const string S3TradeReceivables = "TradeReceivables";
    public const string S3CashAndBank = "CashAndBank";
    public const string S3ShortTermLoansAndAdvances = "ShortTermLoansAndAdvances";
    public const string S3OtherCurrentAssets = "OtherCurrentAssets";

    public const string S3RevenueFromOperations = "RevenueFromOperations";
    public const string S3OtherIncome = "OtherIncome";
    public const string S3CostOfMaterialsConsumed = "CostOfMaterialsConsumed";
    public const string S3PurchasesOfStockInTrade = "PurchasesOfStockInTrade";
    public const string S3ChangesInInventories = "ChangesInInventories";
    public const string S3EmployeeBenefitsExpense = "EmployeeBenefitsExpense";
    public const string S3FinanceCosts = "FinanceCosts";
    public const string S3DepreciationAndAmortization = "DepreciationAndAmortization";
    public const string S3OtherExpenses = "OtherExpenses";
    public const string S3TaxExpense = "TaxExpense"; // current and deferred tax charged to profit and loss

    /// <summary>Every Schedule III group a ledger may be mapped to.</summary>
    public static readonly IReadOnlySet<string> Schedule3Groups = new HashSet<string>
    {
        S3ShareCapital, S3ReservesAndSurplus, S3LongTermBorrowings, S3DeferredTaxLiabilities, S3OtherLongTermLiabilities,
        S3LongTermProvisions, S3ShortTermBorrowings, S3TradePayablesMsme, S3TradePayablesOthers, S3OtherCurrentLiabilities,
        S3ShortTermProvisions, S3PropertyPlantEquipment, S3CapitalWip, S3NonCurrentInvestments, S3DeferredTaxAssets,
        S3LongTermLoansAndAdvances, S3OtherNonCurrentAssets, S3CurrentInvestments, S3Inventories, S3TradeReceivables,
        S3CashAndBank, S3ShortTermLoansAndAdvances, S3OtherCurrentAssets, S3RevenueFromOperations, S3OtherIncome,
        S3CostOfMaterialsConsumed, S3PurchasesOfStockInTrade, S3ChangesInInventories, S3EmployeeBenefitsExpense,
        S3FinanceCosts, S3DepreciationAndAmortization, S3OtherExpenses, S3TaxExpense,
    };

    /// <summary>Groups whose natural balance is credit (equity, liabilities, income); every other group is debit-natured.</summary>
    public static readonly IReadOnlySet<string> CreditNatureGroups = new HashSet<string>
    {
        S3ShareCapital, S3ReservesAndSurplus, S3LongTermBorrowings, S3DeferredTaxLiabilities, S3OtherLongTermLiabilities,
        S3LongTermProvisions, S3ShortTermBorrowings, S3TradePayablesMsme, S3TradePayablesOthers, S3OtherCurrentLiabilities,
        S3ShortTermProvisions, S3RevenueFromOperations, S3OtherIncome,
    };

    /// <summary>
    /// A ledger's balance in its group's natural direction (balances are stored debit-positive): a creditor's credit
    /// balance is positive, a creditor with a debit balance is negative, so contra balances reduce their group.
    /// </summary>
    public static decimal NaturalBalance(string schedule3Group, decimal debitPositiveBalance) =>
        CreditNatureGroups.Contains(schedule3Group) ? -debitPositiveBalance : debitPositiveBalance;

    // Form 3CD Tax Categories
    public const string F3CdTurnover = "Turnover";
    public const string F3CdCashReceipt = "CashReceipt";
    public const string F3CdCashPayment = "CashPayment";
    public const string F3CdTdsDeductibleExpense = "TdsDeductibleExpense";
    public const string F3CdSec40A3Disallowance = "Sec40A3Disallowance";
    public const string F3CdSec43BStatutoryDue = "Sec43BStatutoryDue";
    public const string F3CdSec43BhMsme = "Sec43BhMsme";
    public const string F3CdSec361vaEmployeeContribution = "Sec361vaEmployeeContribution";

    // CARO Categories
    public const string CaroInventory = "Inventory";
    public const string CaroWorkingCapitalBorrowing = "WorkingCapitalBorrowing";
    public const string CaroTermLoan = "TermLoan";
    public const string CaroRelatedPartyAdvance = "RelatedPartyAdvance";
    public const string CaroStatutoryDue = "StatutoryDue";

    // Cost Audit (CRA-1 / CRA-3) Categories
    public const string CostAuditMaterial = "MaterialCost";
    public const string CostAuditLabor = "LaborCost";
    public const string CostAuditUtilities = "UtilitiesCost";
    public const string CostAuditOverheads = "OverheadsCost";

    // Fixed Asset Blocks (Income Tax Act & Schedule II)
    public const string FaPlantAndMachinery = "PlantAndMachinery";
    public const string FaBuildings = "Buildings";
    public const string FaFurnitureAndFixtures = "FurnitureAndFixtures";
    public const string FaComputers = "Computers";
    public const string FaVehicles = "Vehicles";
    public const string FaIntangibles = "Intangibles";
}

public class TrialBalanceImport : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string FinancialYear { get; set; } = "2024-25";
    public string SourceType { get; set; } = TrialBalanceConstants.SourceTallyXml;
    public string FileName { get; set; } = "tally_import.xml";

    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public bool IsBalanced { get; set; }

    public int TotalLedgersCount { get; set; }
    public int MappedLedgersCount { get; set; }
    public string Status { get; set; } = TrialBalanceConstants.StatusDraft;

    public DateTime? SyncedAt { get; set; }
    public string? SyncedDesks { get; set; } // e.g. "Audit,Form3Cd,Schedule3,Caro,FixedAssets"

    public List<TrialBalanceLedger> Ledgers { get; set; } = [];
}

public class TrialBalanceLedger : TenantEntity
{
    public Guid ImportId { get; set; }
    public TrialBalanceImport? Import { get; set; }

    public string LedgerName { get; set; } = "";
    public string TallyGroup { get; set; } = "";

    public decimal OpeningBalance { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal ClosingBalance { get; set; }

    public string Schedule3Group { get; set; } = TrialBalanceConstants.S3OtherExpenses;
    public string? Form3CdCategory { get; set; }
    public string? CaroCategory { get; set; }
    public string? FixedAssetBlock { get; set; }

    public bool IsMapped { get; set; }
}

public class ClientLedgerMapping : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string LedgerPattern { get; set; } = ""; // Exact name or prefix pattern
    public string TallyGroup { get; set; } = "";
    public string Schedule3Group { get; set; } = TrialBalanceConstants.S3OtherExpenses;
    public string? Form3CdCategory { get; set; }
    public string? CaroCategory { get; set; }
    public string? FixedAssetBlock { get; set; }
    public string? Notes { get; set; }
}
