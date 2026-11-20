using CaDesk.Application.Gst;
using CaDesk.Domain.Gst;
using Xunit;

namespace CaDesk.UnitTests;

public class GstReconciliationUnitTests
{
    [Theory]
    [InlineData("INV/2024/0045", "INV202445")]
    [InlineData("inv-2024-45", "INV202445")]
    [InlineData("00042", "42")]
    [InlineData("42", "42")]
    [InlineData("INV 001", "INV1")]
    [InlineData("TAX/24-25/00099", "TAX242599")]
    [InlineData("A-0000", "A0")]
    public void NormalizeInvoiceNumber_HandlesComplexIndianNumberingFormats(string input, string expected)
    {
        var result = GstReconciliationEngine.NormalizeInvoiceNumber(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Reconcile_AccuratelyClassifiesAllMatchCategories()
    {
        var firmId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        var prInvoices = new List<PurchaseRegisterInvoice>
        {
            // 1. Exact Match
            new("27ABCDE1234F1Z5", "Alpha Tech", "INV/2024/001", new DateOnly(2024, 4, 10), "regular", 100000, 9000, 9000, 0, 0),
            // 2. Match with Tolerance (0.50 round-off diff)
            new("27ABCDE1234F1Z5", "Alpha Tech", "INV/2024/002", new DateOnly(2024, 4, 12), "regular", 50000, 4500, 4500, 0, 0),
            // 3. Tax Mismatch (PR says 18000, 2B says 9000)
            new("29XYZAB5678C1Z2", "Beta Corp", "INV-101", new DateOnly(2024, 4, 15), "regular", 100000, 0, 0, 18000, 0),
            // 4. Missing in 2B (in PR, but supplier did not file)
            new("06DEFGH9012J1Z8", "Gamma Ltd", "INV/G/555", new DateOnly(2024, 4, 20), "regular", 75000, 6750, 6750, 0, 0)
        };

        var twoBInvoices = new List<Gstr2BInvoice>
        {
            // Exact match counterpart
            new("27ABCDE1234F1Z5", "Alpha Tech Ltd", "INV-2024-1", new DateOnly(2024, 4, 10), "regular", 100000, 9000, 9000, 0, 0, true, null),
            // Tolerance counterpart (tax is 4500.50)
            new("27ABCDE1234F1Z5", "Alpha Tech Ltd", "INV-2024-02", new DateOnly(2024, 4, 12), "regular", 50000, 4500.25m, 4500.25m, 0, 0, true, null),
            // Tax mismatch counterpart
            new("29XYZAB5678C1Z2", "Beta Corp", "INV101", new DateOnly(2024, 4, 15), "regular", 100000, 0, 0, 9000, 0, true, null),
            // 5. Missing in Books (reflected in 2B, unbooked in PR)
            new("33LMNOP3456K1Z9", "Delta Supplies", "DS/888", new DateOnly(2024, 4, 25), "regular", 30000, 0, 0, 5400, 0, true, null)
        };

        var result = GstReconciliationEngine.Reconcile(
            firmId, clientId, "042024", "2024-25", 1.00m, prInvoices, twoBInvoices);

        Assert.Equal(4, result.Batch.PurchaseRegisterCount);
        Assert.Equal(4, result.Batch.Gstr2BCount);
        Assert.Equal(1, result.Batch.MatchedCount);
        Assert.Equal(1, result.Batch.MatchedWithToleranceCount);
        Assert.Equal(1, result.Batch.MismatchedTaxCount);
        Assert.Equal(1, result.Batch.MissingIn2BCount);
        Assert.Equal(1, result.Batch.MissingInBooksCount);

        // Total items created: 4 PR items + 1 Unbooked 2B item = 5 items
        Assert.Equal(5, result.Items.Count);

        var exact = result.Items.First(x => x.InvoiceNumber == "INV/2024/001");
        Assert.Equal(GstMatchStatus.ExactMatch, exact.MatchStatus);
        Assert.Equal(0, exact.TaxDifference);

        var tol = result.Items.First(x => x.InvoiceNumber == "INV/2024/002");
        Assert.Equal(GstMatchStatus.MatchWithTolerance, tol.MatchStatus);
        Assert.Equal(0.50m, tol.TaxDifference);

        var mismatch = result.Items.First(x => x.InvoiceNumber == "INV-101");
        Assert.Equal(GstMatchStatus.TaxMismatch, mismatch.MatchStatus);
        Assert.Equal(9000m, mismatch.TaxDifference);

        var missing2B = result.Items.First(x => x.InvoiceNumber == "INV/G/555");
        Assert.Equal(GstMatchStatus.MissingIn2B, missing2B.MatchStatus);

        var unbooked = result.Items.First(x => x.InvoiceNumber == "DS/888");
        Assert.Equal(GstMatchStatus.MissingInBooks, unbooked.MatchStatus);
    }
}
