using CaDesk.Application.Billing;
using CaDesk.Domain.Billing;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Firms;
using Xunit;

namespace CaDesk.UnitTests;

/// <summary>
/// Sprint 63: GST B2B Tax Invoicing conforming to Section 31 of CGST Act and Rule 46 of CGST Rules, 2017.
/// </summary>
public class GstB2bInvoiceUnitTests
{
    [Fact]
    public void ExtractStateCode_Pulls_Two_Digit_Prefix_Correctly()
    {
        Assert.Equal("32", GstInvoiceEngine.ExtractStateCode("32AAACB1234F1Z5"));
        Assert.Equal("27", GstInvoiceEngine.ExtractStateCode("27AAACB1234F1Z5"));
        Assert.Equal("29", GstInvoiceEngine.ExtractStateCode("29BBBCB5678G1Z2"));
        Assert.Equal("33", GstInvoiceEngine.ExtractStateCode("33CCCCB9999H1Z0"));
        Assert.Equal("32", GstInvoiceEngine.ExtractStateCode(null, "32"));
        Assert.Equal("32", GstInvoiceEngine.ExtractStateCode("", "32"));
    }

    [Fact]
    public void GetStateName_Resolves_Indian_States()
    {
        Assert.Equal("Kerala", GstInvoiceEngine.GetStateName("32"));
        Assert.Equal("Maharashtra", GstInvoiceEngine.GetStateName("27"));
        Assert.Equal("Karnataka", GstInvoiceEngine.GetStateName("29"));
        Assert.Equal("Tamil Nadu", GstInvoiceEngine.GetStateName("33"));
        Assert.Equal("Delhi", GstInvoiceEngine.GetStateName("07"));
    }

    [Fact]
    public void IsInterState_Correctly_Compares_Supplier_And_Recipient()
    {
        // Intra-State: Both in Kerala (32)
        Assert.False(GstInvoiceEngine.IsInterState("32", "32"));

        // Inter-State: Kerala (32) to Karnataka (29) or Maharashtra (27)
        Assert.True(GstInvoiceEngine.IsInterState("32", "29"));
        Assert.True(GstInvoiceEngine.IsInterState("32", "27"));
    }

    [Fact]
    public void CalculateItemTax_IntraState_Splits_Into_Cgst_And_Sgst()
    {
        // ₹1,00,000 at 18% GST intra-state (Kerala to Kerala)
        var (cgstRate, cgstAmt, sgstRate, sgstAmt, igstRate, igstAmt) =
            GstInvoiceEngine.CalculateItemTax(100000m, 18m, isInterState: false);

        Assert.Equal(9m, cgstRate);
        Assert.Equal(9000m, cgstAmt);
        Assert.Equal(9m, sgstRate);
        Assert.Equal(9000m, sgstAmt);
        Assert.Equal(0m, igstRate);
        Assert.Equal(0m, igstAmt);
    }

    [Fact]
    public void CalculateItemTax_InterState_Applies_Igst_Only()
    {
        // ₹1,00,000 at 18% GST inter-state (Kerala to Karnataka)
        var (cgstRate, cgstAmt, sgstRate, sgstAmt, igstRate, igstAmt) =
            GstInvoiceEngine.CalculateItemTax(100000m, 18m, isInterState: true);

        Assert.Equal(0m, cgstRate);
        Assert.Equal(0m, cgstAmt);
        Assert.Equal(0m, sgstRate);
        Assert.Equal(0m, sgstAmt);
        Assert.Equal(18m, igstRate);
        Assert.Equal(18000m, igstAmt);
    }

    [Fact]
    public void NumberToWords_Converts_Indian_Rupee_Denominations()
    {
        Assert.Equal("Rupees Fifty Thousand Only", GstInvoiceEngine.NumberToWordsInRupees(50000m));
        Assert.Equal("Rupees One Lakh Eighteen Thousand Only", GstInvoiceEngine.NumberToWordsInRupees(118000m));
        Assert.Equal("Rupees Two Lakh Thirty-Five Thousand and Fifty Paise Only", GstInvoiceEngine.NumberToWordsInRupees(235000.50m));
        Assert.Equal("Rupees One Crore Twenty-Five Lakh Only", GstInvoiceEngine.NumberToWordsInRupees(12500000m));
    }

    [Fact]
    public void GenerateRule46TaxInvoiceHtml_Contains_All_Statutory_Mandates()
    {
        var firm = new Firm
        {
            Id = Guid.NewGuid(),
            Name = "Venkitaraman & Co",
            LegalName = "Venkitaraman & Co Chartered Accountants",
            TaxIds = new Dictionary<string, string> { ["GSTIN"] = "32AAACB1234F1Z5" }
        };

        var client = new Client
        {
            Id = Guid.NewGuid(),
            Name = "Cochin Shipyard Ltd",
            LegalName = "Cochin Shipyard Limited",
            TaxIds = new Dictionary<string, string> { ["GSTIN"] = "32AABCC5678G1Z1" }
        };

        var invoice = new Invoice
        {
            FirmId = firm.Id,
            ClientId = client.Id,
            InvoiceNumber = "INV/2026-27/0042",
            IssueDate = new DateOnly(2026, 12, 2),
            DueDate = new DateOnly(2026, 12, 16),
            Status = InvoiceStatus.Sent,
            Currency = "INR",
            SupplierName = firm.LegalName,
            SupplierGstin = "32AAACB1234F1Z5",
            SupplierState = "Kerala",
            SupplierStateCode = "32",
            RecipientLegalName = client.LegalName,
            RecipientGstin = "32AABCC5678G1Z1",
            RecipientState = "Kerala",
            RecipientStateCode = "32",
            PlaceOfSupply = "32-Kerala",
            IsInterState = false,
            IsReverseCharge = false,
            Subtotal = 100000m,
            CgstAmount = 9000m,
            SgstAmount = 9000m,
            IgstAmount = 0m,
            TaxAmount = 18000m,
            TotalAmount = 118000m
        };

        invoice.Items.Add(new InvoiceItem
        {
            FirmId = firm.Id,
            InvoiceId = invoice.Id,
            Description = "Statutory Financial Audit for FY 2025-26 under Companies Act, 2013",
            SacCode = GstInvoiceEngine.DefaultSacFinancialAudit,
            Quantity = 1,
            UnitPrice = 100000m,
            GstRate = 18m,
            CgstRate = 9m,
            CgstAmount = 9000m,
            SgstRate = 9m,
            SgstAmount = 9000m,
            IgstRate = 0m,
            IgstAmount = 0m,
            Amount = 100000m,
            TaxAmount = 18000m,
            TotalAmount = 118000m
        });

        var html = GstInvoiceEngine.GenerateRule46TaxInvoiceHtml(invoice, firm, client);

        Assert.Contains("TAX INVOICE", html);
        Assert.Contains("Section 31", html);
        Assert.Contains("Rule 46", html);
        Assert.Contains("ORIGINAL FOR RECIPIENT", html);
        Assert.Contains("INV/2026-27/0042", html);
        Assert.Contains("32AAACB1234F1Z5", html);
        Assert.Contains("32AABCC5678G1Z1", html);
        Assert.Contains("998231", html);
        Assert.Contains("Intra-State Supply (CGST + SGST)", html);
        Assert.Contains("Rupees One Lakh Eighteen Thousand Only", html);
        Assert.Contains("Authorised Signatory", html);
    }
}
