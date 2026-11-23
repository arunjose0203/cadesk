using CaDesk.Application.PartnerCompanion;
using CaDesk.Domain.PartnerCompanion;
using CaDesk.Domain.Users;
using Xunit;

namespace CaDesk.UnitTests;

public class PartnerCompanionUnitTests
{
    [Theory]
    [InlineData("1234")]
    [InlineData("0000")]
    [InlineData("987654")]
    [InlineData("4826")]
    public void QuickPin_HashAndVerify_SucceedsForValidPin(string pin)
    {
        var (hash, salt) = PartnerMobilePreference.HashPin(pin);

        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.False(string.IsNullOrWhiteSpace(salt));

        bool verified = PartnerMobilePreference.VerifyPin(pin, hash, salt);
        Assert.True(verified);

        bool wrongPinFailed = PartnerMobilePreference.VerifyPin(pin + "9", hash, salt);
        Assert.False(wrongPinFailed);
    }

    [Fact]
    public void QuickPin_DifferentSalts_ProduceDifferentHashesForSamePin()
    {
        string pin = "8821";
        var (hash1, salt1) = PartnerMobilePreference.HashPin(pin);
        var (hash2, salt2) = PartnerMobilePreference.HashPin(pin);

        Assert.NotEqual(salt1, salt2);
        Assert.NotEqual(hash1, hash2);

        Assert.True(PartnerMobilePreference.VerifyPin(pin, hash1, salt1));
        Assert.True(PartnerMobilePreference.VerifyPin(pin, hash2, salt2));
    }

    [Fact]
    public void PartnerPulseSummary_CalculatesValuesCorrectly()
    {
        var pulse = new PartnerPulseSummaryDto(
            TodayCollections: 150000m,
            MonthCollections: 1250000m,
            UnbilledWipAmount: 480000m,
            BillableHoursThisWeek: 165.5m,
            ActiveStaffCount: 14,
            CriticalDeadlinesCount: 3,
            PendingAuditSignoffsCount: 2,
            PendingTimesheetApprovalsCount: 8,
            PendingInvoiceApprovalsCount: 4,
            UnresolvedForensicAlertsCount: 5
        );

        Assert.Equal(150000m, pulse.TodayCollections);
        Assert.Equal(1250000m, pulse.MonthCollections);
        Assert.Equal(480000m, pulse.UnbilledWipAmount);
        Assert.Equal(165.5m, pulse.BillableHoursThisWeek);
        Assert.Equal(14, pulse.ActiveStaffCount);
        Assert.Equal(3, pulse.CriticalDeadlinesCount);
        Assert.Equal(2, pulse.PendingAuditSignoffsCount);
        Assert.Equal(8, pulse.PendingTimesheetApprovalsCount);
        Assert.Equal(4, pulse.PendingInvoiceApprovalsCount);
        Assert.Equal(5, pulse.UnresolvedForensicAlertsCount);
    }

    [Fact]
    public void PartnerCompanion_PermissionConstant_IsCorrect()
    {
        Assert.Equal("partner.companion", Perms.PartnerCompanion);
    }

    [Fact]
    public void QuickSignAuditRequest_BindsParametersCorrectly()
    {
        var req = new QuickSignAuditRequest(
            PartnerMembershipNo: "058291",
            Udin: "25058291BGXYZ1234",
            Remarks: "Reviewed final lead schedules A through Q; issued unmodified opinion."
        );

        Assert.Equal("058291", req.PartnerMembershipNo);
        Assert.Equal("25058291BGXYZ1234", req.Udin);
        Assert.Contains("lead schedules", req.Remarks);
    }

    [Fact]
    public void TriageForensicAlertRequest_AcceptsValidStatuses()
    {
        var req = new TriageForensicAlertRequest("Reviewed", "Cash transaction below Section 40A(3) exception limit.");
        Assert.Equal("Reviewed", req.Status);
        Assert.NotNull(req.AuditorNotes);
    }
}
