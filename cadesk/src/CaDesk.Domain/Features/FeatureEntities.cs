using CaDesk.Domain.Common;

namespace CaDesk.Domain.Features;

public class FirmFeature : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public string FeatureKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class FeatureKeys
{
    public const string ConnectedBanking = "connected-banking";
    public const string Banking = "banking";
    public const string EInvoicing = "einvoicing";
    public const string TaxCopilot = "tax-copilot";
    public const string UaeCorporateTax = "uae-tax";
    public const string PracticeHealth = "practice-health";
    public const string LedgerSync = "ledger-sync";
    public const string TaxLitigation = "tax-litigation";
    public const string EsgCsr = "esg-csr";
    public const string IbcCirp = "ibc-cirp";

    public static readonly string[] DisabledByDefault =
    [
        ConnectedBanking,
        Banking,
        EInvoicing,
        TaxCopilot,
        UaeCorporateTax,
        PracticeHealth,
        LedgerSync,
        TaxLitigation,
        EsgCsr,
        IbcCirp
    ];

    public static readonly string[] AllConfigurable =
    [
        ConnectedBanking,
        EInvoicing,
        TaxCopilot,
        UaeCorporateTax,
        PracticeHealth,
        LedgerSync,
        TaxLitigation,
        EsgCsr,
        IbcCirp
    ];
}
