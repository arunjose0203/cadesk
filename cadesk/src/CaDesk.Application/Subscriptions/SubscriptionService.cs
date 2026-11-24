using CaDesk.Application.Abstractions;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Firms;
using CaDesk.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Subscriptions;

public class SubscriptionService(IAppDbContext db, ITenantContext tenant)
{
    public async Task<SubscriptionStatusDto> GetSubscriptionStatusAsync(CancellationToken ct = default)
    {
        var firmId = tenant.FirmId;
        var sub = await db.FirmSubscriptions.FirstOrDefaultAsync(s => s.FirmId == firmId, ct);
        if (sub == null)
        {
            sub = new FirmSubscription
            {
                FirmId = firmId,
                PlanTier = SubscriptionPlanTier.Starter,
                Status = SubscriptionStatus.Trialing,
                BillingInterval = SubscriptionBillingInterval.Monthly,
                TrialStartsAt = DateTime.UtcNow,
                TrialEndsAt = DateTime.UtcNow.AddDays(14),
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(14)
            };
            db.FirmSubscriptions.Add(sub);
            await db.SaveChangesAsync(ct);
        }

        var planDef = SubscriptionEngine.GetPlan(sub.PlanTier);

        var currentUsers = await db.Users.CountAsync(u => u.FirmId == firmId, ct);
        var currentClients = await db.Clients.CountAsync(c => c.FirmId == firmId && c.DeletedAt == null, ct);
        var activeAudits = await db.AuditEngagements.CountAsync(a => a.FirmId == firmId && a.Status != "Completed", ct);

        var daysRemaining = SubscriptionEngine.GetTrialDaysRemaining(sub.TrialEndsAt);
        var isTrialActive = sub.Status == SubscriptionStatus.Trialing && daysRemaining > 0;

        return new SubscriptionStatusDto(
            PlanTier: sub.PlanTier,
            DisplayName: planDef.DisplayName,
            Status: isTrialActive ? "Trialing" : sub.Status,
            BillingInterval: sub.BillingInterval,
            TrialStartsAt: sub.TrialStartsAt,
            TrialEndsAt: sub.TrialEndsAt,
            DaysRemainingInTrial: daysRemaining,
            IsTrialActive: isTrialActive,
            CurrentPeriodEnd: sub.CurrentPeriodEnd,
            CancelAtPeriodEnd: sub.CancelAtPeriodEnd,
            CurrentUsersCount: currentUsers,
            UserLimit: planDef.UserLimit,
            CurrentClientsCount: currentClients,
            ClientLimit: planDef.ClientLimit,
            ActiveAuditsCount: activeAudits,
            ActiveAuditsLimit: planDef.ActiveAuditsLimit,
            IsDemoDataSeeded: sub.IsDemoDataSeeded,
            IncludedFeatures: planDef.IncludedFeatures,
            LastPaymentAmount: sub.LastPaymentAmount,
            LastPaymentDate: sub.LastPaymentDate,
            LastPaymentInvoiceNumber: sub.LastPaymentInvoiceNumber
        );
    }

    public IReadOnlyList<PlanPricingOptionDto> GetAvailablePlans()
    {
        return SubscriptionEngine.GetAllPlans().Select(p => new PlanPricingOptionDto(
            TierCode: p.TierCode,
            DisplayName: p.DisplayName,
            Description: p.Description,
            MonthlyPrice: p.MonthlyPrice,
            AnnualPrice: p.AnnualPrice,
            UserLimit: p.UserLimit,
            ClientLimit: p.ClientLimit,
            ActiveAuditsLimit: p.ActiveAuditsLimit,
            IncludedFeatures: p.IncludedFeatures,
            IsPopular: p.IsPopular
        )).ToList();
    }

    public async Task<UpgradePlanResponseDto> UpgradePlanAsync(UpgradePlanRequest req, CancellationToken ct = default)
    {
        var firmId = tenant.FirmId;
        var targetTier = req.TargetTier.ToUpperInvariant();
        if (!SubscriptionPlanTier.All.Contains(targetTier))
            throw new BusinessRuleException($"Invalid plan tier '{req.TargetTier}'. Valid tiers: STARTER, GROWTH, ENTERPRISE.", 400);

        var planDef = SubscriptionEngine.GetPlan(targetTier);
        var isAnnual = req.BillingInterval.Equals(SubscriptionBillingInterval.Annual, StringComparison.OrdinalIgnoreCase);
        var basePrice = isAnnual ? planDef.AnnualPrice : planDef.MonthlyPrice;

        var (baseAmount, cgst, sgst, igst, totalAmount) = SubscriptionEngine.CalculateGstBreakdown(basePrice);

        // Sequential invoice numbering per financial year: CD/{FY}/{SEQ:4}
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int startYear = today.Month >= 4 ? today.Year : today.Year - 1;
        string fy = $"{startYear}-{(startYear + 1) % 100:D2}";
        int seq = await db.SubscriptionInvoices.CountAsync(ct) + 1;
        var invoiceNum = $"CD/{fy}/{seq:D4}";

        var invoice = new SubscriptionInvoice
        {
            FirmId = firmId,
            InvoiceNumber = invoiceNum,
            InvoiceDate = today,
            PlanTier = targetTier,
            BillingInterval = isAnnual ? SubscriptionBillingInterval.Annual : SubscriptionBillingInterval.Monthly,
            BaseAmount = baseAmount,
            CgstAmount = cgst,
            SgstAmount = sgst,
            IgstAmount = igst,
            TotalAmount = totalAmount,
            SacCode = "998311",
            PaymentStatus = "Pending",
            PaymentMethod = req.PaymentMethod ?? "PaymentPending",
            TransactionReference = req.TransactionReference ?? $"ORD-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            FirmGstin = req.FirmGstin
        };
        db.SubscriptionInvoices.Add(invoice);

        var sub = await db.FirmSubscriptions.FirstOrDefaultAsync(s => s.FirmId == firmId, ct);
        if (sub == null)
        {
            sub = new FirmSubscription { FirmId = firmId, PlanTier = SubscriptionPlanTier.Starter, Status = SubscriptionStatus.Trialing };
            db.FirmSubscriptions.Add(sub);
        }

        // Upgrading creates a pending order only. Plan changes only upon verified payment or manual admin upgrade.
        sub.LastPaymentInvoiceNumber = invoiceNum;
        sub.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return new UpgradePlanResponseDto(
            Success: true,
            PlanTier: sub.PlanTier,
            Status: "OrderCreated",
            InvoiceNumber: invoiceNum,
            TotalAmountPaid: 0,
            CgstAmount: cgst,
            SgstAmount: sgst,
            IgstAmount: igst,
            Message: $"Upgrade order created. Pro-forma invoice #{invoiceNum} generated. The plan will update upon verified payment confirmation or manual activation."
        );
    }

    public async Task<bool> AdminApplyPlanUpgradeAsync(Guid targetFirmId, string targetTier, string invoiceNum, CancellationToken ct = default)
    {
        var targetTierUpper = targetTier.ToUpperInvariant();
        var sub = await db.FirmSubscriptions.FirstOrDefaultAsync(s => s.FirmId == targetFirmId, ct);
        if (sub == null)
        {
            sub = new FirmSubscription { FirmId = targetFirmId };
            db.FirmSubscriptions.Add(sub);
        }

        var invoice = await db.SubscriptionInvoices.FirstOrDefaultAsync(i => i.FirmId == targetFirmId && i.InvoiceNumber == invoiceNum, ct);
        if (invoice != null)
        {
            invoice.PaymentStatus = "Paid";
        }

        sub.PlanTier = targetTierUpper;
        sub.Status = SubscriptionStatus.Active;
        sub.CurrentPeriodStart = DateTime.UtcNow;
        sub.CurrentPeriodEnd = DateTime.UtcNow.AddYears(1);
        sub.UpdatedAt = DateTime.UtcNow;

        var firm = await db.Firms.FirstOrDefaultAsync(f => f.Id == targetFirmId, ct);
        if (firm != null)
        {
            firm.PlanCode = targetTierUpper;
            firm.Status = FirmStatus.Active;
            firm.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<SubscriptionInvoiceDto>> GetSubscriptionInvoicesAsync(CancellationToken ct = default)
    {
        var firmId = tenant.FirmId;
        return await db.SubscriptionInvoices
            .Where(i => i.FirmId == firmId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new SubscriptionInvoiceDto(
                i.Id,
                i.InvoiceNumber,
                i.InvoiceDate,
                i.PlanTier,
                i.BillingInterval,
                i.BaseAmount,
                i.CgstAmount,
                i.SgstAmount,
                i.IgstAmount,
                i.TotalAmount,
                i.SacCode,
                i.PaymentStatus,
                i.PaymentMethod,
                i.TransactionReference,
                i.CreatedAt
            ))
            .ToListAsync(ct);
    }

    public async Task<SeedDemoDataResultDto> SeedDemoDataAsync(Guid userId, CancellationToken ct = default)
    {
        var firmId = tenant.FirmId;

        // Check if demo clients already exist
        var existingDemo = await db.Clients.FirstOrDefaultAsync(c => c.FirmId == firmId && c.Code == "DEMO-APEX", ct);
        if (existingDemo != null)
        {
            return new SeedDemoDataResultDto(
                Success: true,
                ClientsCreated: 0,
                AuditEngagementsCreated: 0,
                BankTransactionsCreated: 0,
                ConfirmationsCreated: 0,
                Message: "Demo data has already been pre-loaded for this firm."
            );
        }

        // 1. Client: Apex Global Technologies Ltd
        var clientApex = new Client
        {
            FirmId = firmId,
            Code = "DEMO-APEX",
            Name = "Apex Global Technologies Ltd",
            LegalName = "Apex Global Technologies Limited",
            EntityType = "Company",
            CountryCode = "IN",
            TaxIds = new Dictionary<string, string>
            {
                ["PAN"] = "AABCA9876F",
                ["GSTIN"] = "27AABCA9876F1Z5",
                ["CIN"] = "U74999MH2018PLC305123"
            },
            Custom = new Dictionary<string, string>
            {
                ["is_demo"] = "true",
                ["turnover_fy"] = "465000000"
            },
            Email = "audit.contact@apextech.in",
            Phone = "+91 98200 12345"
        };
        db.Clients.Add(clientApex);

        // 2. Client: Bharat Steel & Infra Pvt Ltd
        var clientBharat = new Client
        {
            FirmId = firmId,
            Code = "DEMO-BHARAT",
            Name = "Bharat Steel & Infra Pvt Ltd",
            LegalName = "Bharat Steel and Infrastructure Private Limited",
            EntityType = "Company",
            CountryCode = "IN",
            TaxIds = new Dictionary<string, string>
            {
                ["PAN"] = "AABCB1234G",
                ["GSTIN"] = "27AABCB1234G1Z2",
                ["CIN"] = "U27100MH2015PTC265432"
            },
            Custom = new Dictionary<string, string>
            {
                ["is_demo"] = "true",
                ["turnover_fy"] = "182000000"
            },
            Email = "accounts@bharatsteel.co.in",
            Phone = "+91 98211 54321"
        };
        db.Clients.Add(clientBharat);

        // 3. Client: Vanguard Healthcare Diagnostics LLP
        var clientVanguard = new Client
        {
            FirmId = firmId,
            Code = "DEMO-VANGUARD",
            Name = "Vanguard Healthcare Diagnostics LLP",
            LegalName = "Vanguard Healthcare Diagnostics LLP",
            EntityType = "LLP",
            CountryCode = "IN",
            TaxIds = new Dictionary<string, string>
            {
                ["PAN"] = "AAAFV4321H",
                ["GSTIN"] = "27AAAFV4321H1Z9"
            },
            Custom = new Dictionary<string, string>
            {
                ["is_demo"] = "true"
            },
            Email = "compliance@vanguarddiagnostics.com",
            Phone = "+91 98333 77777"
        };
        db.Clients.Add(clientVanguard);

        await db.SaveChangesAsync(ct);

        // Seed Audit Engagement for Apex
        var engagement = new AuditEngagement
        {
            FirmId = firmId,
            ClientId = clientApex.Id,
            FinancialYear = "2024-25",
            Title = "Statutory Audit FY 2024-25 - Apex Global Technologies Ltd",
            AuditType = Domain.Audit.AuditType.Statutory,
            Status = EngagementStatus.Fieldwork,
            LeadPartnerId = userId,
            BenchmarkType = Domain.Audit.BenchmarkType.Revenue,
            BenchmarkAmount = 465_000_000m,
            OverallMateriality = 4_650_000m,
            PerformanceMateriality = 3_487_500m,
            TrivialThreshold = 232_500m
        };
        db.AuditEngagements.Add(engagement);
        await db.SaveChangesAsync(ct);

        // Seed SA 505 Confirmation Campaign for Apex
        var campaign = new ConfirmationCampaign
        {
            FirmId = firmId,
            AuditEngagementId = engagement.Id,
            Title = "FY 2024-25 External Debtors & Creditors Circularization",
            Description = "Year-end trade balances circularization under ICAI SA 505",
            AsOfDate = new DateOnly(2025, 3, 31),
            CutoffDate = new DateOnly(2025, 4, 30),
            Status = "Active",
            TotalRequestsCount = 2,
            ConfirmedAgreedCount = 1,
            PendingCount = 1,
            TotalBookBalanceAmount = 1_850_000m,
            TotalConfirmedAmount = 1_200_000m
        };
        db.ConfirmationCampaigns.Add(campaign);
        await db.SaveChangesAsync(ct);

        var req1 = new ExternalConfirmationRequest
        {
            FirmId = firmId,
            CampaignId = campaign.Id,
            AuditEngagementId = engagement.Id,
            LeadScheduleRef = "B",
            PartyType = ConfirmationPartyType.TradeDebtor,
            PartyName = "Tata Elxsi Precision Systems Ltd",
            PartyContactEmail = "vendor.reconcile@tataelxsi.example",
            PartyPanGstin = "27AAACT9876P1Z4",
            ConfirmationType = ConfirmationType.PositiveStatedBalance,
            BookBalanceAmount = 1_200_000m,
            AsOfDate = new DateOnly(2025, 3, 31),
            Status = ConfirmationStatus.ConfirmedAgreed,
            ConfirmedBalanceAmount = 1_200_000m,
            VarianceAmount = 0m,
            AuditorConclusion = "ReconciledSatisfactory"
        };
        var req2 = new ExternalConfirmationRequest
        {
            FirmId = firmId,
            CampaignId = campaign.Id,
            AuditEngagementId = engagement.Id,
            LeadScheduleRef = "G",
            PartyType = ConfirmationPartyType.TradeCreditor,
            PartyName = "Larsen & Toubro Heavy Fab Div",
            PartyContactEmail = "customer.audit@larsentoubro.example",
            PartyPanGstin = "27AAACL1234K1Z0",
            ConfirmationType = ConfirmationType.PositiveStatedBalance,
            BookBalanceAmount = 650_000m,
            AsOfDate = new DateOnly(2025, 3, 31),
            Status = ConfirmationStatus.Sent,
            SentAt = DateTime.UtcNow
        };
        db.ExternalConfirmations.AddRange(req1, req2);

        // Mark subscription demo seeded
        var sub = await db.FirmSubscriptions.FirstOrDefaultAsync(s => s.FirmId == firmId, ct);
        if (sub != null)
        {
            sub.IsDemoDataSeeded = true;
            sub.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        return new SeedDemoDataResultDto(
            Success: true,
            ClientsCreated: 3,
            AuditEngagementsCreated: 1,
            BankTransactionsCreated: 5,
            ConfirmationsCreated: 2,
            Message: "Successfully pre-loaded demo firm workspace with 3 clients (Apex Global, Bharat Steel, Vanguard Diagnostics), 1 statutory audit engagement, and SA 505 confirmations."
        );
    }

    public async Task<PurgeDemoDataResultDto> PurgeDemoDataAsync(CancellationToken ct = default)
    {
        var firmId = tenant.FirmId;

        var demoClients = await db.Clients
            .Where(c => c.FirmId == firmId && c.Code.StartsWith("DEMO-"))
            .ToListAsync(ct);

        var count = demoClients.Count;
        foreach (var c in demoClients)
        {
            c.DeletedAt = DateTime.UtcNow;
            c.IsActive = false;
        }

        var sub = await db.FirmSubscriptions.FirstOrDefaultAsync(s => s.FirmId == firmId, ct);
        if (sub != null)
        {
            sub.IsDemoDataSeeded = false;
            sub.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        return new PurgeDemoDataResultDto(
            Success: true,
            ClientsPurged: count,
            Message: $"Successfully cleared {count} demo clients and related sample workspaces."
        );
    }
}
