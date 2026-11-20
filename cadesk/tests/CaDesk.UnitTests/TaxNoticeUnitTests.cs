using CaDesk.Domain.TaxNotices;

namespace CaDesk.UnitTests;

public class TaxNoticeUnitTests
{
    [Fact]
    public void DinValidator_PostMandate_IncomeTax_ValidDin_ReturnsValid()
    {
        // CBDT mandate: 01/10/2019
        var issueDate = new DateOnly(2024, 6, 15);
        var din = "ITBA/AST/S/143(1)/2024-25/1068834928(1)";

        var status = DinValidator.EvaluateDin(din, TaxAuthority.IncomeTax, issueDate);

        Assert.Equal(DinStatus.Valid, status);
    }

    [Fact]
    public void DinValidator_PostMandate_IncomeTax_MissingDin_ReturnsInvalid()
    {
        var issueDate = new DateOnly(2024, 6, 15);

        var status = DinValidator.EvaluateDin(null, TaxAuthority.IncomeTax, issueDate);

        Assert.Equal(DinStatus.Invalid, status);
    }

    [Fact]
    public void DinValidator_PreMandate_IncomeTax_MissingDin_ReturnsNotApplicable()
    {
        // Before 01/10/2019, DIN was not mandatory
        var issueDate = new DateOnly(2018, 5, 20);

        var status = DinValidator.EvaluateDin(null, TaxAuthority.IncomeTax, issueDate);

        Assert.Equal(DinStatus.NotApplicable, status);
    }

    [Fact]
    public void DinValidator_PostMandate_Gst_ValidDin_ReturnsValid()
    {
        // CBIC mandate: 08/11/2019
        var issueDate = new DateOnly(2024, 8, 10);
        var din = "20240827101234567890";

        var status = DinValidator.EvaluateDin(din, TaxAuthority.Gst, issueDate);

        Assert.Equal(DinStatus.Valid, status);
    }

    [Fact]
    public void DinValidator_PostMandate_Gst_EmptyDin_ReturnsInvalid()
    {
        var issueDate = new DateOnly(2024, 8, 10);

        var status = DinValidator.EvaluateDin("   ", TaxAuthority.Gst, issueDate);

        Assert.Equal(DinStatus.Invalid, status);
    }

    [Fact]
    public void DinValidator_TooShortOrInvalidCharacters_ReturnsInvalid()
    {
        var issueDate = new DateOnly(2024, 1, 10);

        var status = DinValidator.EvaluateDin("123", TaxAuthority.IncomeTax, issueDate);

        Assert.Equal(DinStatus.Invalid, status);
    }

    [Fact]
    public void AppealDeadlineCalculator_IncomeTax_Adds30DaysUnderSec249()
    {
        var demandDate = new DateOnly(2024, 10, 1);
        var expectedDueDate = new DateOnly(2024, 10, 31);

        var appealDueDate = AppealDeadlineCalculator.CalculateAppealDueDate(TaxAuthority.IncomeTax, demandDate);

        Assert.Equal(expectedDueDate, appealDueDate);
    }

    [Fact]
    public void AppealDeadlineCalculator_Gst_Adds3MonthsUnderSec107()
    {
        var orderDate = new DateOnly(2024, 5, 15);
        var expectedDueDate = new DateOnly(2024, 8, 15);

        var appealDueDate = AppealDeadlineCalculator.CalculateAppealDueDate(TaxAuthority.Gst, orderDate);

        Assert.Equal(expectedDueDate, appealDueDate);
    }

    [Fact]
    public void TaxDemand_CalculatesOutstandingBalance_Correctly()
    {
        var demand = new TaxDemand
        {
            OriginalDemand = 250000m,
            StayedAmount = 50000m,
            PaidAmount = 100000m,
            AdjustedRefund = 25000m
        };

        // 250,000 - 50,000 (stayed) - 100,000 (paid) - 25,000 (refund adjusted) = 75,000
        Assert.Equal(75000m, demand.OutstandingAmount);
    }

    [Fact]
    public void TaxDemand_DaysUntilAppealDue_CalculatesCountdownCorrectly()
    {
        var demand = new TaxDemand
        {
            DemandDate = new DateOnly(2024, 11, 1),
            AppealDueDate = new DateOnly(2024, 12, 1)
        };

        var asOf = new DateOnly(2024, 11, 20);
        var remainingDays = demand.DaysUntilAppealDue(asOf);

        Assert.Equal(11, remainingDays);
    }

    [Fact]
    public void TaxDemand_OverdueAppeal_CalculatesNegativeDays()
    {
        var demand = new TaxDemand
        {
            DemandDate = new DateOnly(2024, 9, 1),
            AppealDueDate = new DateOnly(2024, 10, 1)
        };

        var asOf = new DateOnly(2024, 10, 15);
        var remainingDays = demand.DaysUntilAppealDue(asOf);

        Assert.Equal(-14, remainingDays);
    }

    [Fact]
    public void TaxNotice_ResponseWorkflow_DraftToPartnerApproval()
    {
        var response = new NoticeResponse
        {
            ResponseVersion = 1,
            SubmissionTitle = "Written Submission u/s 142(1)",
            SummaryOfSubmissions = "Detailed reconciliation of Schedule BP with GSTR-9",
            Status = ResponseStatus.Draft
        };

        Assert.Equal(ResponseStatus.Draft, response.Status);

        // Advance to Partner Approved
        response.Status = ResponseStatus.PartnerApproved;
        response.ApprovedByName = "CA Partner A";
        response.ApprovedAt = DateTimeOffset.UtcNow;

        Assert.Equal(ResponseStatus.PartnerApproved, response.Status);
        Assert.NotNull(response.ApprovedAt);

        // Upload to ITBA / E-filing portal
        response.Status = ResponseStatus.UploadedToPortal;
        response.PortalAckNumber = "ACK-ITBA-9876543210";
        response.SubmittedAt = DateTimeOffset.UtcNow;

        Assert.Equal(ResponseStatus.UploadedToPortal, response.Status);
        Assert.Equal("ACK-ITBA-9876543210", response.PortalAckNumber);
    }
}
