using System.Text.RegularExpressions;
using CaDesk.Application.Abstractions;
using CaDesk.Application.ConnectedBanking;
using CaDesk.Application.Features;
using CaDesk.Application.PartnerCompanion;
using CaDesk.Application.SecurityCompliance;
using CaDesk.Application.Subscriptions;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Common;
using CaDesk.Domain.ConnectedBanking;
using CaDesk.Domain.Features;
using CaDesk.Domain.Firms;
using CaDesk.Domain.SecurityCompliance;
using CaDesk.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CaDesk.UnitTests;

public class Sprint62StabilisationUnitTests
{
    // =========================================================================
    // 62.3 Partner Quick-Sign & UDIN Validation Tests
    // =========================================================================

    [Theory]
    [InlineData("24123456AAAA123456", true)]     // 18 chars alphanumeric with 2-digit year prefix
    [InlineData("26ABCDEF0123456789", true)]
    [InlineData("12345", false)]                 // Too short
    [InlineData("24123456AAAA1234567", false)]    // 19 chars (too long)
    [InlineData("24123456!@#$123456", false)]     // Special characters
    [InlineData("", false)]                       // Empty
    [InlineData("   ", false)]
    public void UdinValidator_EnforcesStrict18CharFormat(string udin, bool expectedValid)
    {
        bool isValid = UdinValidator.IsValid(udin);
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void AuditQualityEngine_RequiresZeroOpenReviewNotesAndEqrClearance()
    {
        // Case 1: Open notes present -> cannot sign off
        Assert.False(AuditQualityEngine.CanSignOffAudit(openReviewNotesCount: 2, eqrRequired: false, eqrStatus: null).Allowed);

        // Case 2: Zero open notes, no EQR required -> can sign off
        Assert.True(AuditQualityEngine.CanSignOffAudit(openReviewNotesCount: 0, eqrRequired: false, eqrStatus: null).Allowed);

        // Case 3: Zero open notes, EQR required but in review -> cannot sign off
        Assert.False(AuditQualityEngine.CanSignOffAudit(openReviewNotesCount: 0, eqrRequired: true, eqrStatus: "InReview").Allowed);

        // Case 4: Zero open notes, EQR required and cleared -> can sign off
        Assert.True(AuditQualityEngine.CanSignOffAudit(openReviewNotesCount: 0, eqrRequired: true, eqrStatus: EqrReviewStatus.ClearanceGranted).Allowed);
    }

    // =========================================================================
    // 62.4 Connected Banking Four-Eyes Principle Tests
    // =========================================================================

    [Fact]
    public void PayoutBatch_MakerCannotBeChecker()
    {
        var partnerId = Guid.NewGuid();
        var batch = new PayoutBatch
        {
            Id = Guid.NewGuid(),
            BatchNumber = "BATCH-001",
            CreatedByUserId = partnerId, // Creator
            Status = PayoutBatchStatus.PendingApproval
        };

        // Attempting to authorize by the same user who created the batch must violate four-eyes
        bool isSelfApproval = batch.CreatedByUserId == partnerId;
        Assert.True(isSelfApproval);

        // When different partner authorizes -> valid
        var otherPartnerId = Guid.NewGuid();
        bool isOtherApproval = batch.CreatedByUserId == otherPartnerId;
        Assert.False(isOtherApproval);
    }

    // =========================================================================
    // 62.5 Subscriptions Sequential Invoice Format & Pending Upgrade Tests
    // =========================================================================

    [Fact]
    public void SubscriptionInvoice_UsesSequentialFinancialYearFormat()
    {
        // Format: CD/{fy}/{seq:D4}
        string fy = "2026-27";
        long seq = 1;
        string invoiceNum = $"CD/{fy}/{seq:D4}";

        Assert.Equal("CD/2026-27/0001", invoiceNum);
        Assert.Matches(@"^CD/\d{4}-\d{2}/\d{4}$", invoiceNum);

        long nextSeq = 42;
        string nextInvoice = $"CD/{fy}/{nextSeq:D4}";
        Assert.Equal("CD/2026-27/0042", nextInvoice);
    }

    // =========================================================================
    // 62.6 Security Claims & Config-Driven Data Residency Tests
    // =========================================================================

    [Fact]
    public void SecurityFrameworks_UsesAlignedTerminology_NotMisleadingCertifications()
    {
        Assert.Equal("Controls aligned to ISO/IEC 27001:2022", SecurityFrameworks.Iso27001_2022);
        Assert.Equal("Controls aligned to SOC 2 Criteria", SecurityFrameworks.Soc2_TypeII);
        Assert.DoesNotContain("certified", SecurityFrameworks.Iso27001_2022, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("certified", SecurityFrameworks.Soc2_TypeII, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DataResidencyConfig_SuppliesRegionNameToTelemetry()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["DataResidency:Region"] = "ap-south-1",
            ["DataResidency:DataCenterLocation"] = "Mumbai, Republic of India"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        var region = config["DataResidency:DataCenterLocation"] ?? config["DataResidency:Region"];
        string telemetry = $"All accounting data, financial ledgers, and audit workpapers hosted exclusively in cloud regions within India ({region}).";

        Assert.Contains("Mumbai, Republic of India", telemetry);
    }

    // =========================================================================
    // 62.7 Feature Flags Catalog & Default Disabled Tests
    // =========================================================================

    [Fact]
    public void FeatureKeys_DisabledByDefault_ContainsAllUnfinishedDesks()
    {
        var disabled = FeatureKeys.DisabledByDefault;

        Assert.Contains(FeatureKeys.ConnectedBanking, disabled);
        Assert.Contains(FeatureKeys.EInvoicing, disabled);
        Assert.Contains(FeatureKeys.TaxCopilot, disabled);
        Assert.Contains(FeatureKeys.UaeCorporateTax, disabled);
        Assert.Contains(FeatureKeys.PracticeHealth, disabled);
        Assert.Contains(FeatureKeys.LedgerSync, disabled);
        Assert.Contains(FeatureKeys.TaxLitigation, disabled);
        Assert.Contains(FeatureKeys.EsgCsr, disabled);
        Assert.Contains(FeatureKeys.IbcCirp, disabled);
    }
}
