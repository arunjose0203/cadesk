using System.Globalization;
using System.Text;
using CaDesk.Domain.Billing;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Firms;

namespace CaDesk.Application.Billing;

/// <summary>
/// Implements statutory GST Tax Invoicing under Section 31 of CGST Act, 2017 and Rule 46 of CGST Rules, 2017.
/// Handles SAC classification for Chartered Accountants services, Place of Supply determination,
/// Intra-State (CGST + SGST) vs Inter-State (IGST) split, and printable HTML invoice synthesis.
/// </summary>
public static class GstInvoiceEngine
{
    public const string DefaultSacFinancialAudit = "998231";     // Financial Auditing Services
    public const string DefaultSacAccounting = "998232";         // Accounting & Bookkeeping Services
    public const string DefaultSacTaxation = "998233";           // Taxation Advisory & Return Filing Services
    public const string DefaultSacOtherLegal = "998239";         // Other Legal & Secretarial Services
    public const string DefaultSacManagementConsulting = "998311";// Management Consulting Services

    public static readonly Dictionary<string, string> IndianStateCodes = new()
    {
        ["01"] = "Jammu and Kashmir",
        ["02"] = "Himachal Pradesh",
        ["03"] = "Punjab",
        ["04"] = "Chandigarh",
        ["05"] = "Uttarakhand",
        ["06"] = "Haryana",
        ["07"] = "Delhi",
        ["08"] = "Rajasthan",
        ["09"] = "Uttar Pradesh",
        ["10"] = "Bihar",
        ["11"] = "Sikkim",
        ["12"] = "Arunachal Pradesh",
        ["13"] = "Nagaland",
        ["14"] = "Manipur",
        ["15"] = "Mizoram",
        ["16"] = "Tripura",
        ["17"] = "Meghalaya",
        ["18"] = "Assam",
        ["19"] = "West Bengal",
        ["20"] = "Jharkhand",
        ["21"] = "Odisha",
        ["22"] = "Chhattisgarh",
        ["23"] = "Madhya Pradesh",
        ["24"] = "Gujarat",
        ["26"] = "Dadra and Nagar Haveli and Daman and Diu",
        ["27"] = "Maharashtra",
        ["29"] = "Karnataka",
        ["30"] = "Goa",
        ["31"] = "Lakshadweep",
        ["32"] = "Kerala",
        ["33"] = "Tamil Nadu",
        ["34"] = "Puducherry",
        ["35"] = "Andaman and Nicobar Islands",
        ["36"] = "Telangana",
        ["37"] = "Andhra Pradesh",
        ["38"] = "Ladakh"
    };

    public static string ExtractStateCode(string? gstin, string fallback = "32")
    {
        if (!string.IsNullOrWhiteSpace(gstin))
        {
            var clean = gstin.Trim().ToUpperInvariant();
            if (clean.Length >= 2 && char.IsDigit(clean[0]) && char.IsDigit(clean[1]))
            {
                var code = clean[..2];
                if (IndianStateCodes.ContainsKey(code)) return code;
            }
        }
        return fallback;
    }

    public static string GetStateName(string stateCode) =>
        IndianStateCodes.TryGetValue(stateCode.Trim(), out var name) ? name : "Kerala";

    public static bool IsInterState(string supplierStateCode, string recipientStateCode) =>
        !string.Equals(supplierStateCode.Trim(), recipientStateCode.Trim(), StringComparison.OrdinalIgnoreCase);

    public static (decimal CgstRate, decimal CgstAmount, decimal SgstRate, decimal SgstAmount, decimal IgstRate, decimal IgstAmount)
        CalculateItemTax(decimal taxableAmount, decimal gstRate, bool isInterState)
    {
        if (isInterState)
        {
            var igstAmt = Math.Round(taxableAmount * gstRate / 100m, 2);
            return (0m, 0m, 0m, 0m, gstRate, igstAmt);
        }
        else
        {
            var halfRate = Math.Round(gstRate / 2m, 2);
            var halfAmt = Math.Round(taxableAmount * halfRate / 100m, 2);
            return (halfRate, halfAmt, halfRate, halfAmt, 0m, 0m);
        }
    }

    public static string NumberToWordsInRupees(decimal amount)
    {
        if (amount == 0) return "Zero Rupees Only";

        var wholeNumber = (long)Math.Floor(amount);
        var fraction = (int)Math.Round((amount - wholeNumber) * 100m);

        var words = ConvertWholeNumberToWords(wholeNumber);
        var sb = new StringBuilder("Rupees ");
        sb.Append(words);

        if (fraction > 0)
        {
            sb.Append(" and ");
            sb.Append(ConvertWholeNumberToWords(fraction));
            sb.Append(" Paise");
        }

        sb.Append(" Only");
        return sb.ToString();
    }

    private static string ConvertWholeNumberToWords(long n)
    {
        if (n == 0) return "Zero";

        var units = new[] { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
                            "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen",
                            "Seventeen", "Eighteen", "Nineteen" };
        var tens = new[] { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

        var parts = new List<string>();

        if (n >= 10000000) // Crores
        {
            parts.Add(ConvertWholeNumberToWords(n / 10000000) + " Crore");
            n %= 10000000;
        }
        if (n >= 100000) // Lakhs
        {
            parts.Add(ConvertWholeNumberToWords(n / 100000) + " Lakh");
            n %= 100000;
        }
        if (n >= 1000) // Thousands
        {
            parts.Add(ConvertWholeNumberToWords(n / 1000) + " Thousand");
            n %= 1000;
        }
        if (n >= 100) // Hundreds
        {
            parts.Add(ConvertWholeNumberToWords(n / 100) + " Hundred");
            n %= 100;
        }
        if (n > 0)
        {
            if (n < 20) parts.Add(units[n]);
            else
            {
                var t = tens[n / 10];
                var u = units[n % 10];
                parts.Add(string.IsNullOrEmpty(u) ? t : $"{t}-{u}");
            }
        }

        return string.Join(" ", parts);
    }

    public static string GenerateRule46TaxInvoiceHtml(Invoice invoice, Firm? firm, Client? client)
    {
        var supplierName = !string.IsNullOrEmpty(invoice.SupplierName) ? invoice.SupplierName : (firm?.LegalName ?? firm?.Name ?? "Chartered Accountants Firm");
        var supplierAddress = !string.IsNullOrEmpty(invoice.SupplierAddress) ? invoice.SupplierAddress : "Chartered Accountants Bhavan, MG Road, Ernakulam, Kerala - 682016";
        var supplierGstin = !string.IsNullOrEmpty(invoice.SupplierGstin) ? invoice.SupplierGstin : (firm?.TaxIds.GetValueOrDefault("GSTIN") ?? "32AAACB1234F1Z5");
        var supplierState = !string.IsNullOrEmpty(invoice.SupplierState) ? invoice.SupplierState : "Kerala";
        var supplierStateCode = !string.IsNullOrEmpty(invoice.SupplierStateCode) ? invoice.SupplierStateCode : "32";

        var recipientName = !string.IsNullOrEmpty(invoice.RecipientLegalName) ? invoice.RecipientLegalName : (client?.LegalName ?? client?.Name ?? "Client Entity");
        var recipientAddress = !string.IsNullOrEmpty(invoice.RecipientAddress) ? invoice.RecipientAddress : "Client Registered Office Address";
        var recipientGstin = !string.IsNullOrEmpty(invoice.RecipientGstin) ? invoice.RecipientGstin : (client?.TaxIds.GetValueOrDefault("GSTIN") ?? "URP (Unregistered Person)");
        var recipientState = !string.IsNullOrEmpty(invoice.RecipientState) ? invoice.RecipientState : "Kerala";
        var recipientStateCode = !string.IsNullOrEmpty(invoice.RecipientStateCode) ? invoice.RecipientStateCode : "32";

        var placeOfSupply = !string.IsNullOrEmpty(invoice.PlaceOfSupply) ? invoice.PlaceOfSupply : $"{recipientStateCode}-{recipientState}";
        var reverseCharge = invoice.IsReverseCharge ? "YES" : "NO";
        var isInterState = invoice.IsInterState;

        var itemsRows = new StringBuilder();
        var idx = 1;
        foreach (var item in invoice.Items)
        {
            var sac = !string.IsNullOrEmpty(item.SacCode) ? item.SacCode : DefaultSacFinancialAudit;
            var unit = !string.IsNullOrEmpty(item.Unit) ? item.Unit : "NOS";

            itemsRows.Append($@"
            <tr>
                <td style='text-align: center; border: 1px solid #cbd5e1; padding: 8px;'>{idx++}</td>
                <td style='border: 1px solid #cbd5e1; padding: 8px;'>
                    <strong>{item.Description}</strong>
                </td>
                <td style='text-align: center; border: 1px solid #cbd5e1; padding: 8px;'>{sac}</td>
                <td style='text-align: right; border: 1px solid #cbd5e1; padding: 8px;'>{item.Quantity:N2} {unit}</td>
                <td style='text-align: right; border: 1px solid #cbd5e1; padding: 8px;'>₹{item.UnitPrice:N2}</td>
                <td style='text-align: right; border: 1px solid #cbd5e1; padding: 8px;'>₹{item.Amount:N2}</td>");

            if (!isInterState)
            {
                itemsRows.Append($@"
                <td style='text-align: right; border: 1px solid #cbd5e1; padding: 8px;'>{item.CgstRate:N1}%<br/>₹{item.CgstAmount:N2}</td>
                <td style='text-align: right; border: 1px solid #cbd5e1; padding: 8px;'>{item.SgstRate:N1}%<br/>₹{item.SgstAmount:N2}</td>");
            }
            else
            {
                itemsRows.Append($@"
                <td colspan='2' style='text-align: right; border: 1px solid #cbd5e1; padding: 8px;'>{item.IgstRate:N1}%<br/>₹{item.IgstAmount:N2}</td>");
            }

            itemsRows.Append($@"
                <td style='text-align: right; border: 1px solid #cbd5e1; padding: 8px; font-weight: bold;'>₹{item.TotalAmount:N2}</td>
            </tr>");
        }

        var amountInWords = NumberToWordsInRupees(invoice.TotalAmount);

        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<title>TAX INVOICE - {invoice.InvoiceNumber}</title>
<style>
  body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 30px; color: #1e293b; line-height: 1.4; font-size: 13px; }}
  .invoice-box {{ max-width: 900px; margin: auto; border: 2px solid #1e3a8a; padding: 25px; border-radius: 8px; background: #fff; }}
  .header {{ display: flex; justify-content: space-between; border-bottom: 2px solid #1e3a8a; padding-bottom: 15px; margin-bottom: 15px; }}
  .title-badge {{ text-align: center; font-size: 20px; font-weight: 800; color: #1e3a8a; text-transform: uppercase; letter-spacing: 1px; }}
  .sub-badge {{ text-align: center; font-size: 11px; color: #64748b; margin-top: 2px; }}
  .grid-2 {{ display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 15px; }}
  .section-card {{ border: 1px solid #e2e8f0; background: #f8fafc; border-radius: 6px; padding: 12px; }}
  .card-title {{ font-size: 11px; text-transform: uppercase; font-weight: 800; color: #1e3a8a; margin-bottom: 6px; border-bottom: 1px solid #cbd5e1; padding-bottom: 4px; }}
  table {{ width: 100%; border-collapse: collapse; margin-top: 15px; margin-bottom: 15px; font-size: 12px; }}
  th {{ background: #1e3a8a; color: #fff; padding: 8px; border: 1px solid #1e3a8a; text-align: left; }}
  .summary-grid {{ display: grid; grid-template-columns: 1.5fr 1fr; gap: 20px; margin-top: 15px; }}
  .footer {{ margin-top: 25px; border-top: 1px solid #e2e8f0; padding-top: 15px; display: grid; grid-template-columns: 1fr 1fr; }}
  .signatory {{ text-align: right; padding-top: 40px; }}
</style>
</head>
<body>
<div class='invoice-box'>
  <div class='title-badge'>TAX INVOICE</div>
  <div class='sub-badge'>[Issued under Section 31 of the Central Goods and Services Tax Act, 2017 &amp; Rule 46 of CGST Rules, 2017]</div>
  <div class='sub-badge' style='margin-bottom: 15px; font-weight: bold; color: #047857;'>ORIGINAL FOR RECIPIENT</div>

  <div class='header'>
    <div>
      <h2 style='margin: 0; color: #1e3a8a; font-size: 18px;'>{supplierName}</h2>
      <div style='font-size: 12px; color: #475569; margin-top: 4px;'>{supplierAddress}</div>
      <div style='margin-top: 6px;'><strong>GSTIN:</strong> <code>{supplierGstin}</code></div>
      <div><strong>State / Code:</strong> {supplierState} ({supplierStateCode})</div>
    </div>
    <div style='text-align: right;'>
      <div style='font-size: 16px; font-weight: 800; color: #1e3a8a;'>Invoice No: {invoice.InvoiceNumber}</div>
      <div style='margin-top: 4px;'><strong>Date of Issue:</strong> {invoice.IssueDate:dd-MMM-yyyy}</div>
      <div><strong>Due Date:</strong> {invoice.DueDate:dd-MMM-yyyy}</div>
      <div><strong>Status:</strong> <span style='text-transform: uppercase; font-weight: bold;'>{invoice.Status}</span></div>
    </div>
  </div>

  <div class='grid-2'>
    <div class='section-card'>
      <div class='card-title'>Details of Recipient / Billed To</div>
      <div style='font-weight: bold; font-size: 14px;'>{recipientName}</div>
      <div style='color: #475569; margin-top: 3px;'>{recipientAddress}</div>
      <div style='margin-top: 6px;'><strong>GSTIN / UIN:</strong> <code>{recipientGstin}</code></div>
      <div><strong>State / Code:</strong> {recipientState} ({recipientStateCode})</div>
    </div>
    <div class='section-card'>
      <div class='card-title'>Place of Supply &amp; Statutory Particulars</div>
      <div><strong>Place of Supply (State &amp; Code):</strong> {placeOfSupply}</div>
      <div style='margin-top: 4px;'><strong>Nature of Supply:</strong> {(isInterState ? "Inter-State Supply (IGST)" : "Intra-State Supply (CGST + SGST)")}</div>
      <div style='margin-top: 4px;'><strong>Tax Payable on Reverse Charge:</strong> {reverseCharge}</div>
      <div style='margin-top: 4px;'><strong>Currency:</strong> {invoice.Currency}</div>
    </div>
  </div>

  <table>
    <thead>
      <tr>
        <th style='width: 30px; text-align: center;'>#</th>
        <th>Description of Services</th>
        <th style='width: 65px; text-align: center;'>SAC Code</th>
        <th style='width: 80px; text-align: right;'>Qty / Unit</th>
        <th style='width: 85px; text-align: right;'>Rate</th>
        <th style='width: 95px; text-align: right;'>Taxable Value</th>
        {(isInterState
            ? "<th colspan='2' style='width: 140px; text-align: right;'>IGST (Rate / Amt)</th>"
            : "<th style='width: 85px; text-align: right;'>CGST</th><th style='width: 85px; text-align: right;'>SGST</th>")}
        <th style='width: 105px; text-align: right;'>Total</th>
      </tr>
    </thead>
    <tbody>
      {itemsRows}
    </tbody>
  </table>

  <div class='summary-grid'>
    <div>
      <div style='background: #f1f5f9; border-radius: 6px; padding: 12px; font-size: 12px;'>
        <strong>Amount Chargeable in Words:</strong><br/>
        <span style='font-weight: bold; color: #1e3a8a;'>{amountInWords}</span>
      </div>
      <div style='margin-top: 15px; font-size: 12px;'>
        <strong>Bank &amp; Payment Details:</strong><br/>
        Bank Name: State Bank of India | Current A/c No: 41238910293<br/>
        IFSC Code: SBIN0001234 | Branch: Ernakulam Commercial Branch<br/>
        UPI VPA: <code>cadesk.firm@sbi</code>
      </div>
    </div>
    <div>
      <table style='margin: 0; font-size: 12px;'>
        <tr>
          <td style='padding: 6px; border: 1px solid #cbd5e1;'><strong>Total Taxable Value</strong></td>
          <td style='padding: 6px; border: 1px solid #cbd5e1; text-align: right;'>₹{invoice.Subtotal:N2}</td>
        </tr>
        {(!isInterState ? $@"
        <tr>
          <td style='padding: 6px; border: 1px solid #cbd5e1;'><strong>Central Tax (CGST)</strong></td>
          <td style='padding: 6px; border: 1px solid #cbd5e1; text-align: right;'>₹{invoice.CgstAmount:N2}</td>
        </tr>
        <tr>
          <td style='padding: 6px; border: 1px solid #cbd5e1;'><strong>State Tax (SGST)</strong></td>
          <td style='padding: 6px; border: 1px solid #cbd5e1; text-align: right;'>₹{invoice.SgstAmount:N2}</td>
        </tr>" : $@"
        <tr>
          <td style='padding: 6px; border: 1px solid #cbd5e1;'><strong>Integrated Tax (IGST)</strong></td>
          <td style='padding: 6px; border: 1px solid #cbd5e1; text-align: right;'>₹{invoice.IgstAmount:N2}</td>
        </tr>")}
        {(!string.IsNullOrEmpty(invoice.Terms) ? $@"
        <tr>
          <td colspan='2' style='padding: 6px; border: 1px solid #cbd5e1; font-size: 11px; color: #64748b;'><strong>Terms:</strong> {invoice.Terms}</td>
        </tr>" : "")}
        <tr style='background: #f8fafc;'>
          <td style='padding: 8px; border: 2px solid #1e3a8a; font-size: 14px; font-weight: 800; color: #1e3a8a;'>Total Invoice Value</td>
          <td style='padding: 8px; border: 2px solid #1e3a8a; text-align: right; font-size: 14px; font-weight: 800; color: #1e3a8a;'>₹{invoice.TotalAmount:N2}</td>
        </tr>
      </table>
    </div>
  </div>

  <div class='footer'>
    <div style='font-size: 11px; color: #64748b;'>
      <strong>Declaration:</strong><br/>
      We declare that this invoice shows the actual price of the services described and that all particulars are true and correct.
    </div>
    <div class='signatory'>
      <div style='font-weight: bold;'>For {supplierName}</div>
      <div style='margin-top: 35px; color: #64748b; font-size: 11px;'>Authorised Signatory / Partner</div>
    </div>
  </div>
</div>
</body>
</html>";
    }
}
