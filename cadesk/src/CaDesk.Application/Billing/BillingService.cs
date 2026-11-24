using CaDesk.Application.Abstractions;
using CaDesk.Application.Clients;
using CaDesk.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Billing;

public class BillingService(IAppDbContext db, ITenantContext tenant)
{
    private const string InvoiceSequenceEntity = "client_invoice";
    private const string DefaultPrefixPattern = "INV/{FY}/{SEQ:4}";

    public async Task<List<InvoiceDto>> GetInvoicesAsync(
        string? status = null,
        Guid? clientId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default)
    {
        var q = db.Invoices
            .Include(x => x.Client)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            if (status == "overdue")
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                q = q.Where(x => x.Status != InvoiceStatus.Paid && x.Status != InvoiceStatus.Cancelled && x.DueDate < today);
            }
            else
            {
                q = q.Where(x => x.Status == status);
            }
        }

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (from.HasValue) q = q.Where(x => x.IssueDate >= from.Value);
        if (to.HasValue) q = q.Where(x => x.IssueDate <= to.Value);

        var list = await q
            .OrderByDescending(x => x.IssueDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(Math.Min(take, 200))
            .ToListAsync(ct);

        return list.Select(ToDto).ToList();
    }

    public async Task<InvoiceDto> GetInvoiceByIdAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .Include(x => x.Client)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (invoice is null) throw new BusinessRuleException("Invoice not found.", 404);
        return ToDto(invoice);
    }

    public async Task<BillingSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startOfMonth = new DateOnly(today.Year, today.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

        var invoices = await db.Invoices.AsNoTracking().ToListAsync(ct);
        var payments = await db.Payments.AsNoTracking().ToListAsync(ct);

        var activeInvoices = invoices.Where(x => x.Status != InvoiceStatus.Cancelled).ToList();

        var totalOutstanding = activeInvoices
            .Where(x => x.Status != InvoiceStatus.Draft && x.PaidAmount < x.TotalAmount)
            .Sum(x => x.TotalAmount - x.PaidAmount);

        var totalOverdue = activeInvoices
            .Where(x => x.Status != InvoiceStatus.Paid && x.Status != InvoiceStatus.Draft && x.DueDate < today)
            .Sum(x => x.TotalAmount - x.PaidAmount);

        var billedThisMonth = activeInvoices
            .Where(x => x.IssueDate >= startOfMonth && x.IssueDate <= endOfMonth)
            .Sum(x => x.TotalAmount);

        var collectedThisMonth = payments
            .Where(x => x.PaymentDate >= startOfMonth && x.PaymentDate <= endOfMonth)
            .Sum(x => x.Amount);

        var openInvoicesCount = activeInvoices
            .Count(x => x.Status is InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid || (x.Status == InvoiceStatus.Draft));

        return new BillingSummaryDto(
            totalOutstanding,
            totalOverdue,
            billedThisMonth,
            collectedThisMonth,
            openInvoicesCount
        );
    }

    public async Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceRequest req, Guid currentUserId, CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == req.ClientId, ct);
        if (client is null) throw new BusinessRuleException("Client not found.", 404);

        if (req.DueDate < req.IssueDate)
            throw new RequestValidationException(nameof(req.DueDate), "Due date cannot be earlier than issue date.");

        var allItems = new List<InvoiceItemInput>();
        if (req.Items != null) allItems.AddRange(req.Items);

        // Include unbilled time entries if specified
        List<TimeEntry>? timeEntriesToBill = null;
        if (req.TimeEntryIds != null && req.TimeEntryIds.Count > 0)
        {
            timeEntriesToBill = await db.TimeEntries
                .Where(x => req.TimeEntryIds.Contains(x.Id) && x.ClientId == req.ClientId && !x.IsBilled)
                .ToListAsync(ct);

            foreach (var te in timeEntriesToBill)
            {
                allItems.Add(new InvoiceItemInput(
                    $"{te.Description} ({te.Date:dd/MM/yyyy} - {te.Hours}h)",
                    "9982",
                    te.Hours,
                    te.HourlyRate,
                    18.00m,
                    te.Id
                ));
            }
        }

        // Include unbilled reimbursable client expenses
        List<Domain.Office.OfficeExpense>? expensesToBill = null;
        if (req.IncludeUnbilledExpenses == true || (req.ExpenseIds != null && req.ExpenseIds.Count > 0))
        {
            var expQuery = db.OfficeExpenses
                .Where(x => x.ClientId == req.ClientId && x.IsReimbursableByClient && !x.IsClientBilled && x.Status == Domain.Office.ExpenseStatus.Approved);

            if (req.ExpenseIds != null && req.ExpenseIds.Count > 0)
                expQuery = expQuery.Where(x => req.ExpenseIds.Contains(x.Id));

            expensesToBill = await expQuery.ToListAsync(ct);
            foreach (var exp in expensesToBill)
            {
                allItems.Add(new InvoiceItemInput(
                    $"Reimbursement: {exp.Description} ({exp.Date:dd/MM/yyyy} - {exp.Category})",
                    "998239",
                    1m,
                    exp.Amount,
                    18.00m
                ));
            }
        }

        if (allItems.Count == 0)
            throw new RequestValidationException("Items", "An invoice must contain at least one line item.");

        var invoiceNumber = await GenerateNextInvoiceNumberAsync(req.IssueDate, ct);

        var firm = await db.Firms.FirstOrDefaultAsync(x => x.Id == tenant.FirmId, ct);
        var supplierGstin = !string.IsNullOrWhiteSpace(req.SupplierGstin) ? req.SupplierGstin.Trim().ToUpperInvariant()
            : (firm?.TaxIds.GetValueOrDefault("GSTIN") ?? "32AAACB1234F1Z5");
        var supplierStateCode = GstInvoiceEngine.ExtractStateCode(supplierGstin, "32");
        var supplierState = GstInvoiceEngine.GetStateName(supplierStateCode);
        var supplierName = firm?.LegalName ?? firm?.Name ?? "Chartered Accountants Firm";

        var recipientGstin = !string.IsNullOrWhiteSpace(req.RecipientGstin) ? req.RecipientGstin.Trim().ToUpperInvariant()
            : (client.TaxIds.GetValueOrDefault("GSTIN") ?? "");
        var recipientStateCode = GstInvoiceEngine.ExtractStateCode(recipientGstin, supplierStateCode);
        var recipientState = GstInvoiceEngine.GetStateName(recipientStateCode);
        var recipientLegalName = client.LegalName ?? client.Name;

        var placeOfSupply = !string.IsNullOrWhiteSpace(req.PlaceOfSupply) ? req.PlaceOfSupply.Trim() : $"{recipientStateCode}-{recipientState}";
        var isInterState = GstInvoiceEngine.IsInterState(supplierStateCode, recipientStateCode);
        var isReverseCharge = req.IsReverseCharge ?? false;

        var invoice = new Invoice
        {
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            InvoiceNumber = invoiceNumber,
            IssueDate = req.IssueDate,
            DueDate = req.DueDate,
            Status = InvoiceStatus.Draft,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "INR" : req.Currency.Trim().ToUpperInvariant(),
            Notes = req.Notes?.Trim(),
            Terms = req.Terms?.Trim(),
            CreatedBy = currentUserId,
            SupplierName = supplierName,
            SupplierAddress = "Chartered Accountants Bhavan, MG Road, Ernakulam, Kerala - 682016",
            SupplierGstin = supplierGstin,
            SupplierState = supplierState,
            SupplierStateCode = supplierStateCode,
            RecipientLegalName = recipientLegalName,
            RecipientTradeName = client.Name,
            RecipientAddress = "Client Registered Office Address",
            RecipientGstin = recipientGstin,
            RecipientState = recipientState,
            RecipientStateCode = recipientStateCode,
            PlaceOfSupply = placeOfSupply,
            PlaceOfSupplyStateCode = recipientStateCode,
            IsReverseCharge = isReverseCharge,
            IsInterState = isInterState
        };

        decimal subtotal = 0;
        decimal cgstTotal = 0;
        decimal sgstTotal = 0;
        decimal igstTotal = 0;

        foreach (var itemInput in allItems)
        {
            if (string.IsNullOrWhiteSpace(itemInput.Description))
                throw new RequestValidationException("Description", "Item description is required.");

            var qty = Math.Max(0.01m, itemInput.Quantity);
            var rate = Math.Max(0, itemInput.UnitPrice);
            var gst = Math.Max(0, itemInput.GstRate ?? 18.00m);
            var unit = !string.IsNullOrWhiteSpace(itemInput.Unit) ? itemInput.Unit.Trim().ToUpperInvariant() : "NOS";

            var lineAmount = Math.Round(qty * rate, 2);
            var (cgstRate, cgstAmt, sgstRate, sgstAmt, igstRate, igstAmt) =
                GstInvoiceEngine.CalculateItemTax(lineAmount, gst, isInterState);

            var lineTax = cgstAmt + sgstAmt + igstAmt;
            var lineTotal = lineAmount + lineTax;

            subtotal += lineAmount;
            cgstTotal += cgstAmt;
            sgstTotal += sgstAmt;
            igstTotal += igstAmt;

            var item = new InvoiceItem
            {
                FirmId = tenant.FirmId,
                InvoiceId = invoice.Id,
                Description = itemInput.Description.Trim(),
                SacCode = itemInput.SacCode?.Trim() ?? GstInvoiceEngine.DefaultSacFinancialAudit,
                Unit = unit,
                Quantity = qty,
                UnitPrice = rate,
                GstRate = gst,
                CgstRate = cgstRate,
                CgstAmount = cgstAmt,
                SgstRate = sgstRate,
                SgstAmount = sgstAmt,
                IgstRate = igstRate,
                IgstAmount = igstAmt,
                Amount = lineAmount,
                TaxAmount = lineTax,
                TotalAmount = lineTotal,
                TimeEntryId = itemInput.TimeEntryId
            };

            invoice.Items.Add(item);
        }

        invoice.Subtotal = subtotal;
        invoice.CgstAmount = cgstTotal;
        invoice.SgstAmount = sgstTotal;
        invoice.IgstAmount = igstTotal;
        invoice.TaxAmount = cgstTotal + sgstTotal + igstTotal;
        invoice.TotalAmount = subtotal + invoice.TaxAmount;
        invoice.PaidAmount = 0;

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);

        if (timeEntriesToBill != null && timeEntriesToBill.Count > 0)
        {
            foreach (var te in timeEntriesToBill)
            {
                te.IsBilled = true;
                var matchingItem = invoice.Items.FirstOrDefault(x => x.TimeEntryId == te.Id);
                if (matchingItem != null) te.InvoiceItemId = matchingItem.Id;
            }
            await db.SaveChangesAsync(ct);
        }

        if (expensesToBill != null && expensesToBill.Count > 0)
        {
            foreach (var exp in expensesToBill)
            {
                exp.IsClientBilled = true;
                exp.BilledInvoiceId = invoice.Id;
                exp.UpdatedAt = DateTime.UtcNow;
                exp.UpdatedBy = currentUserId;
            }
            await db.SaveChangesAsync(ct);
        }

        return await GetInvoiceByIdAsync(invoice.Id, ct);
    }

    public async Task<InvoiceDto> UpdateInvoiceAsync(Guid id, UpdateInvoiceRequest req, Guid currentUserId, CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (invoice is null) throw new BusinessRuleException("Invoice not found.", 404);
        if (invoice.Status != InvoiceStatus.Draft)
            throw new BusinessRuleException("Only draft invoices can be edited.", 409);

        if (req.DueDate < req.IssueDate)
            throw new RequestValidationException(nameof(req.DueDate), "Due date cannot be earlier than issue date.");

        if (req.Items == null || req.Items.Count == 0)
            throw new RequestValidationException("Items", "An invoice must contain at least one line item.");

        invoice.IssueDate = req.IssueDate;
        invoice.DueDate = req.DueDate;
        invoice.Notes = req.Notes?.Trim();
        invoice.Terms = req.Terms?.Trim();
        invoice.UpdatedBy = currentUserId;

        // Replace line items
        db.InvoiceItems.RemoveRange(invoice.Items);
        invoice.Items.Clear();

        decimal subtotal = 0;
        decimal taxAmount = 0;

        foreach (var itemInput in req.Items)
        {
            var qty = Math.Max(0.01m, itemInput.Quantity);
            var rate = Math.Max(0, itemInput.UnitPrice);
            var gst = Math.Max(0, itemInput.GstRate ?? 18.00m);

            var lineAmount = Math.Round(qty * rate, 2);
            var lineTax = Math.Round(lineAmount * gst / 100m, 2);
            var lineTotal = lineAmount + lineTax;

            subtotal += lineAmount;
            taxAmount += lineTax;

            invoice.Items.Add(new InvoiceItem
            {
                FirmId = tenant.FirmId,
                InvoiceId = invoice.Id,
                Description = itemInput.Description.Trim(),
                SacCode = itemInput.SacCode?.Trim() ?? "9982",
                Quantity = qty,
                UnitPrice = rate,
                GstRate = gst,
                Amount = lineAmount,
                TaxAmount = lineTax,
                TotalAmount = lineTotal,
                TimeEntryId = itemInput.TimeEntryId
            });
        }

        invoice.Subtotal = subtotal;
        invoice.TaxAmount = taxAmount;
        invoice.TotalAmount = subtotal + taxAmount;

        await db.SaveChangesAsync(ct);
        return await GetInvoiceByIdAsync(invoice.Id, ct);
    }

    public async Task<InvoiceDto> UpdateStatusAsync(Guid id, string newStatus, CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (invoice is null) throw new BusinessRuleException("Invoice not found.", 404);

        if (!InvoiceStatus.All.Contains(newStatus))
            throw new RequestValidationException("Status", $"Invalid invoice status '{newStatus}'.");

        if (newStatus == InvoiceStatus.Cancelled)
        {
            // Unlink any billed time entries
            var timeEntryIds = invoice.Items.Where(x => x.TimeEntryId.HasValue).Select(x => x.TimeEntryId!.Value).ToList();
            if (timeEntryIds.Count > 0)
            {
                var timeEntries = await db.TimeEntries.Where(x => timeEntryIds.Contains(x.Id)).ToListAsync(ct);
                foreach (var te in timeEntries)
                {
                    te.IsBilled = false;
                    te.InvoiceItemId = null;
                }
            }
        }

        invoice.Status = newStatus;
        await db.SaveChangesAsync(ct);
        return await GetInvoiceByIdAsync(invoice.Id, ct);
    }

    public async Task<PaymentDto> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest req, Guid currentUserId, CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == invoiceId, ct);

        if (invoice is null) throw new BusinessRuleException("Invoice not found.", 404);
        if (invoice.Status == InvoiceStatus.Cancelled)
            throw new BusinessRuleException("Cannot record payment on a cancelled invoice.", 409);

        if (req.Amount <= 0)
            throw new RequestValidationException(nameof(req.Amount), "Payment amount must be greater than zero.");

        var balanceDue = invoice.TotalAmount - invoice.PaidAmount;
        if (req.Amount > balanceDue)
            throw new RequestValidationException(nameof(req.Amount), $"Payment amount cannot exceed balance due of {balanceDue:F2}.");

        if (!PaymentMode.All.Contains(req.PaymentMode))
            throw new RequestValidationException(nameof(req.PaymentMode), $"Invalid payment mode '{req.PaymentMode}'.");

        var payment = new Payment
        {
            FirmId = tenant.FirmId,
            InvoiceId = invoice.Id,
            ClientId = invoice.ClientId,
            PaymentDate = req.PaymentDate,
            Amount = Math.Round(req.Amount, 2),
            PaymentMode = req.PaymentMode,
            ReferenceNumber = req.ReferenceNumber?.Trim(),
            Notes = req.Notes?.Trim(),
            CreatedBy = currentUserId
        };

        db.Payments.Add(payment);

        invoice.PaidAmount += payment.Amount;
        if (invoice.PaidAmount >= invoice.TotalAmount)
            invoice.Status = InvoiceStatus.Paid;
        else if (invoice.PaidAmount > 0)
            invoice.Status = InvoiceStatus.PartiallyPaid;

        await db.SaveChangesAsync(ct);

        return new PaymentDto(
            payment.Id,
            payment.InvoiceId,
            invoice.InvoiceNumber,
            payment.ClientId,
            invoice.Client?.Name,
            payment.PaymentDate,
            payment.Amount,
            payment.PaymentMode,
            payment.ReferenceNumber,
            payment.Notes,
            payment.CreatedAt
        );
    }

    public async Task DeleteInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (invoice is null) return;
        if (invoice.Status != InvoiceStatus.Draft)
            throw new BusinessRuleException("Only draft invoices can be deleted.", 409);

        // Unlink time entries
        var timeEntryIds = invoice.Items.Where(x => x.TimeEntryId.HasValue).Select(x => x.TimeEntryId!.Value).ToList();
        if (timeEntryIds.Count > 0)
        {
            var timeEntries = await db.TimeEntries.Where(x => timeEntryIds.Contains(x.Id)).ToListAsync(ct);
            foreach (var te in timeEntries)
            {
                te.IsBilled = false;
                te.InvoiceItemId = null;
            }
        }

        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync(ct);
    }

    private async Task<string> GenerateNextInvoiceNumberAsync(DateOnly issueDate, CancellationToken ct)
    {
        var fyStartMonth = 4;
        var period = NumberFormat.Period(DefaultPrefixPattern, issueDate, fyStartMonth);

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var seq = (await db.Database
                .SqlQuery<long>($"select next_number({InvoiceSequenceEntity}, {period}) as \"Value\"")
                .ToListAsync(ct))[0];

            var number = NumberFormat.Format(DefaultPrefixPattern, seq, issueDate, fyStartMonth);
            var exists = await db.Invoices.AnyAsync(x => x.InvoiceNumber == number, ct);
            if (!exists) return number;
        }

        throw new BusinessRuleException("Could not generate a unique invoice number.");
    }

    public async Task<string> GetInvoiceHtmlAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .Include(x => x.Client)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new BusinessRuleException("Invoice not found.", 404);

        var firm = await db.Firms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenant.FirmId, ct);
        return GstInvoiceEngine.GenerateRule46TaxInvoiceHtml(invoice, firm, invoice.Client);
    }

    public async Task<InvoiceDto> SendInvoiceEmailAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .Include(x => x.Client)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new BusinessRuleException("Invoice not found.", 404);

        if (invoice.Status == InvoiceStatus.Draft)
        {
            invoice.Status = InvoiceStatus.Sent;
            invoice.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return await GetInvoiceByIdAsync(invoice.Id, ct);
    }

    private static InvoiceDto ToDto(Invoice x)
    {
        var balance = Math.Max(0, x.TotalAmount - x.PaidAmount);
        return new InvoiceDto(
            x.Id,
            x.ClientId,
            x.Client?.Name,
            x.InvoiceNumber,
            x.IssueDate,
            x.DueDate,
            x.Status,
            x.Currency,
            x.Subtotal,
            x.TaxAmount,
            x.TotalAmount,
            x.PaidAmount,
            balance,
            x.Notes,
            x.Terms,
            x.Items.Select(i => new InvoiceItemDto(
                i.Id,
                i.Description,
                i.SacCode,
                i.Quantity,
                i.UnitPrice,
                i.GstRate,
                i.Amount,
                i.TaxAmount,
                i.TotalAmount,
                i.TimeEntryId,
                string.IsNullOrEmpty(i.Unit) ? "NOS" : i.Unit,
                i.CgstRate,
                i.CgstAmount,
                i.SgstRate,
                i.SgstAmount,
                i.IgstRate,
                i.IgstAmount
            )).ToList(),
            x.Payments.OrderByDescending(p => p.PaymentDate).Select(p => new PaymentDto(
                p.Id,
                p.InvoiceId,
                x.InvoiceNumber,
                p.ClientId,
                x.Client?.Name,
                p.PaymentDate,
                p.Amount,
                p.PaymentMode,
                p.ReferenceNumber,
                p.Notes,
                p.CreatedAt
            )).ToList(),
            x.CreatedAt,
            x.SupplierName,
            x.SupplierAddress,
            x.SupplierGstin,
            x.SupplierState,
            x.SupplierStateCode,
            x.RecipientLegalName,
            x.RecipientTradeName,
            x.RecipientAddress,
            x.RecipientGstin,
            x.RecipientState,
            x.RecipientStateCode,
            x.PlaceOfSupply,
            x.PlaceOfSupplyStateCode,
            x.IsReverseCharge,
            x.IsInterState,
            x.CgstAmount,
            x.SgstAmount,
            x.IgstAmount,
            x.CessAmount
        );
    }
}
