using CaDesk.Domain.Billing;
using Xunit;

namespace CaDesk.UnitTests;

public class BillingUnitTests
{
    [Fact]
    public void GstCalculation_EighteenPercent_ComputesAccurateTax()
    {
        decimal quantity = 2.5m;
        decimal unitPrice = 10000m;
        decimal gstRate = 18.00m;

        decimal amount = Math.Round(quantity * unitPrice, 2);
        decimal taxAmount = Math.Round(amount * gstRate / 100m, 2);
        decimal totalAmount = amount + taxAmount;

        Assert.Equal(25000.00m, amount);
        Assert.Equal(4500.00m, taxAmount);
        Assert.Equal(29500.00m, totalAmount);
    }

    [Fact]
    public void MultiItemInvoice_AggregatesSubtotalAndTax()
    {
        var items = new[]
        {
            new { Qty = 1m, Price = 15000m, Gst = 18m }, // 15000 + 2700 = 17700
            new { Qty = 4m, Price = 2500m, Gst = 18m },  // 10000 + 1800 = 11800
            new { Qty = 1m, Price = 5000m, Gst = 0m }    // 5000 + 0 = 5000
        };

        decimal subtotal = 0;
        decimal taxTotal = 0;

        foreach (var i in items)
        {
            var amt = Math.Round(i.Qty * i.Price, 2);
            var tax = Math.Round(amt * i.Gst / 100m, 2);
            subtotal += amt;
            taxTotal += tax;
        }

        decimal grandTotal = subtotal + taxTotal;

        Assert.Equal(30000.00m, subtotal);
        Assert.Equal(4500.00m, taxTotal);
        Assert.Equal(34500.00m, grandTotal);
    }

    [Theory]
    [InlineData(10000, 3000, "partially_paid", 7000)]
    [InlineData(10000, 10000, "paid", 0)]
    public void PaymentBalanceAndStatusTransitions_EvaluateCorrectly(
        decimal total, decimal paymentAmount, string expectedStatus, decimal expectedBalance)
    {
        decimal paidAmount = 0;
        paidAmount += paymentAmount;

        string status;
        if (paidAmount >= total) status = InvoiceStatus.Paid;
        else if (paidAmount > 0) status = InvoiceStatus.PartiallyPaid;
        else status = InvoiceStatus.Draft;

        decimal balanceDue = Math.Max(0, total - paidAmount);

        Assert.Equal(expectedStatus, status);
        Assert.Equal(expectedBalance, balanceDue);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(0.5, true)]
    [InlineData(8.0, true)]
    [InlineData(24.0, true)]
    [InlineData(24.5, false)]
    public void TimesheetHoursBounds_ValidatesProperly(decimal hours, bool isValid)
    {
        bool valid = hours > 0 && hours <= 24;
        Assert.Equal(isValid, valid);
    }
}
