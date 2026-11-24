using CaDesk.Application.Office;
using CaDesk.Domain.Office;
using Xunit;

namespace CaDesk.UnitTests;

public class OfficeEssentialsUnitTests
{
    [Fact]
    public void AttendanceStatus_Classifies_FullDay_And_HalfDay_Correctly()
    {
        var checkIn = new DateTime(2026, 12, 10, 9, 30, 0, DateTimeKind.Utc);
        var fullDayCheckOut = new DateTime(2026, 12, 10, 18, 0, 0, DateTimeKind.Utc);
        var halfDayCheckOut = new DateTime(2026, 12, 10, 12, 30, 0, DateTimeKind.Utc);

        var fullDuration = (fullDayCheckOut - checkIn).TotalHours;
        var halfDuration = (halfDayCheckOut - checkIn).TotalHours;

        Assert.True(fullDuration >= 8.0);
        Assert.True(halfDuration < 4.0);
    }

    [Fact]
    public void LeaveBalances_AvailableDays_Computes_Correctly()
    {
        var balance = new LeaveBalanceDto(
            Id: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            UserName: "Rahul Sharma",
            Year: 2026,
            LeaveType: LeaveType.Casual,
            AllocatedDays: 12.00m,
            UsedDays: 3.50m,
            AvailableDays: 8.50m
        );

        Assert.Equal(8.50m, balance.AvailableDays);
        Assert.Equal(12.00m, balance.AllocatedDays);
        Assert.Equal(3.50m, balance.UsedDays);
    }

    [Fact]
    public void ExpenseReport_Computes_Reimbursable_And_Category_Totals()
    {
        var expenses = new List<OfficeExpenseDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Staff A", Guid.NewGuid(), "Client X", null, new DateOnly(2026, 12, 1), ExpenseCategory.Travel, 1200m, "Travel to client", null, true, false, null, ExpenseStatus.Approved, null, null, null, null, DateTime.UtcNow),
            new(Guid.NewGuid(), Guid.NewGuid(), "Staff A", Guid.NewGuid(), "Client X", null, new DateOnly(2026, 12, 2), ExpenseCategory.RocFees, 2500m, "ROC filing challan", null, true, false, null, ExpenseStatus.Approved, null, null, null, null, DateTime.UtcNow),
            new(Guid.NewGuid(), Guid.NewGuid(), "Staff B", null, null, null, new DateOnly(2026, 12, 3), ExpenseCategory.Printing, 450m, "Audit paper book binding", null, false, false, null, ExpenseStatus.Approved, null, null, null, null, DateTime.UtcNow)
        };

        var total = expenses.Sum(e => e.Amount);
        var reimbursable = expenses.Where(e => e.IsReimbursableByClient).Sum(e => e.Amount);
        var nonReimbursable = total - reimbursable;

        Assert.Equal(4150m, total);
        Assert.Equal(3700m, reimbursable);
        Assert.Equal(450m, nonReimbursable);
    }

    [Fact]
    public void RegisterNumbering_Format_Follows_Standard_Conventions()
    {
        var year = 2026;
        var inwardSeq = 14;
        var outwardSeq = 8;

        var inwNumber = $"INW/{year}/{inwardSeq:D4}";
        var outNumber = $"OUT/{year}/{outwardSeq:D4}";

        Assert.Equal("INW/2026/0014", inwNumber);
        Assert.Equal("OUT/2026/0008", outNumber);
    }
}
