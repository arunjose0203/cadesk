using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.RelatedParty;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.RelatedParty;

public class RelatedPartyService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public RelatedPartyService(IAppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<RelatedPartySummaryDto> GetSummaryAsync(Guid? clientId, string? financialYear, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;

        var entityQuery = _db.RelatedPartyEntities.Where(x => x.FirmId == firmId);
        if (clientId.HasValue) entityQuery = entityQuery.Where(x => x.ClientId == clientId.Value);
        var totalParties = await entityQuery.CountAsync(ct);

        var txQuery = _db.RelatedPartyTransactions.Where(x => x.FirmId == firmId);
        if (clientId.HasValue) txQuery = txQuery.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear)) txQuery = txQuery.Where(x => x.FinancialYear == financialYear);

        var txList = await txQuery.ToListAsync(ct);

        int totalTx = txList.Count;
        decimal totalVolume = txList.Sum(x => x.TransactionAmount);
        int armLengthCount = txList.Count(x => x.IsArmLength);
        decimal complianceRate = totalTx > 0 ? Math.Round(((decimal)armLengthCount / totalTx) * 100m, 2) : 100m;
        int specialResolutionCount = txList.Count(x => x.RequiresShareholderApproval);
        int approvedCount = txList.Count(x => x.AuditCommitteeApprovalStatus == "Approved" || x.AuditCommitteeApprovalStatus == "OmnibusApproved");
        int risk40a2bCount = txList.Count(x => x.Section40A2bRisk);
        decimal totalClosingBalances = txList.Sum(x => x.OutstandingBalanceAtYearEnd);

        return new RelatedPartySummaryDto(
            TotalParties: totalParties,
            TotalTransactions: totalTx,
            TotalVolumeInr: totalVolume,
            ArmLengthComplianceRate: complianceRate,
            SpecialResolutionRequiredCount: specialResolutionCount,
            AuditCommitteeApprovedCount: approvedCount,
            Sec40A2bRiskCount: risk40a2bCount,
            TotalYearEndBalancesInr: totalClosingBalances
        );
    }

    // -------------------------------------------------------------
    // Related Party Entities
    // -------------------------------------------------------------

    public async Task<List<RelatedPartyEntityDto>> ListEntitiesAsync(Guid? clientId, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var query = _db.RelatedPartyEntities
            .Include(x => x.Client)
            .Include(x => x.Transactions)
            .Where(x => x.FirmId == firmId);

        if (clientId.HasValue)
        {
            query = query.Where(x => x.ClientId == clientId.Value);
        }

        var list = await query.OrderBy(x => x.Name).ToListAsync(ct);

        return list.Select(e => new RelatedPartyEntityDto(
            Id: e.Id,
            ClientId: e.ClientId,
            ClientName: e.Client?.Name,
            Name: e.Name,
            RelationshipType: e.RelationshipType,
            Pan: e.Pan,
            Cin: e.Cin,
            DirectorOrKmpName: e.DirectorOrKmpName,
            NatureOfInterest: e.NatureOfInterest,
            IsActive: e.IsActive,
            TransactionCount: e.Transactions.Count,
            CreatedAt: e.CreatedAt,
            UpdatedAt: e.UpdatedAt
        )).ToList();
    }

    public async Task<RelatedPartyEntityDto?> GetEntityByIdAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var e = await _db.RelatedPartyEntities
            .Include(x => x.Client)
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (e is null) return null;

        return new RelatedPartyEntityDto(
            Id: e.Id,
            ClientId: e.ClientId,
            ClientName: e.Client?.Name,
            Name: e.Name,
            RelationshipType: e.RelationshipType,
            Pan: e.Pan,
            Cin: e.Cin,
            DirectorOrKmpName: e.DirectorOrKmpName,
            NatureOfInterest: e.NatureOfInterest,
            IsActive: e.IsActive,
            TransactionCount: e.Transactions.Count,
            CreatedAt: e.CreatedAt,
            UpdatedAt: e.UpdatedAt
        );
    }

    public async Task<RelatedPartyEntityDto> CreateOrUpdateEntityAsync(Guid? id, SaveRelatedPartyEntityRequest req, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        RelatedPartyEntity? entity = null;

        if (id.HasValue && id.Value != Guid.Empty)
        {
            entity = await _db.RelatedPartyEntities.FirstOrDefaultAsync(x => x.Id == id.Value && x.FirmId == firmId, ct);
            if (entity is null)
                throw new KeyNotFoundException("Related party entity not found.");
        }
        else
        {
            entity = new RelatedPartyEntity
            {
                FirmId = firmId,
                ClientId = req.ClientId,
                CreatedAt = DateTime.UtcNow
            };
            _db.RelatedPartyEntities.Add(entity);
        }

        entity.Name = req.Name.Trim();
        entity.RelationshipType = req.RelationshipType;
        entity.Pan = req.Pan?.ToUpperInvariant().Trim() ?? "";
        entity.Cin = req.Cin?.ToUpperInvariant().Trim() ?? "";
        entity.DirectorOrKmpName = req.DirectorOrKmpName?.Trim() ?? "";
        entity.NatureOfInterest = req.NatureOfInterest?.Trim() ?? "";
        entity.IsActive = req.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return (await GetEntityByIdAsync(entity.Id, ct))!;
    }

    public async Task<bool> DeleteEntityAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var entity = await _db.RelatedPartyEntities.FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);
        if (entity is null) return false;

        _db.RelatedPartyEntities.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------------------------------------------------------------
    // Related Party Transactions
    // -------------------------------------------------------------

    public async Task<List<RelatedPartyTransactionDto>> ListTransactionsAsync(
        Guid? clientId,
        string? financialYear,
        Guid? relatedPartyId,
        string? status,
        CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var query = _db.RelatedPartyTransactions
            .Include(x => x.Client)
            .Include(x => x.RelatedParty)
            .Where(x => x.FirmId == firmId);

        if (clientId.HasValue) query = query.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear)) query = query.Where(x => x.FinancialYear == financialYear);
        if (relatedPartyId.HasValue) query = query.Where(x => x.RelatedPartyId == relatedPartyId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        var list = await query.OrderByDescending(x => x.TransactionDate).ToListAsync(ct);
        return list.Select(ToTxDto).ToList();
    }

    public async Task<RelatedPartyTransactionDto?> GetTransactionByIdAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var t = await _db.RelatedPartyTransactions
            .Include(x => x.Client)
            .Include(x => x.RelatedParty)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        return t is null ? null : ToTxDto(t);
    }

    public async Task<RelatedPartyTransactionDto> CreateOrUpdateTransactionAsync(Guid? id, SaveRelatedPartyTransactionRequest req, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        RelatedPartyTransaction? tx = null;

        if (id.HasValue && id.Value != Guid.Empty)
        {
            tx = await _db.RelatedPartyTransactions
                .Include(x => x.RelatedParty)
                .FirstOrDefaultAsync(x => x.Id == id.Value && x.FirmId == firmId, ct);

            if (tx is null)
                throw new KeyNotFoundException("Related party transaction not found.");
        }
        else
        {
            var count = await _db.RelatedPartyTransactions.CountAsync(x => x.FirmId == firmId && x.FinancialYear == req.FinancialYear, ct);
            var contractRef = string.IsNullOrWhiteSpace(req.ContractReference)
                ? $"RPT-{req.FinancialYear.Replace("-", "")}-{(count + 1):D3}"
                : req.ContractReference;

            tx = new RelatedPartyTransaction
            {
                FirmId = firmId,
                ClientId = req.ClientId,
                ContractReference = contractRef,
                CreatedAt = DateTime.UtcNow,
                Status = "Draft"
            };
            _db.RelatedPartyTransactions.Add(tx);
        }

        tx.RelatedPartyId = req.RelatedPartyId;
        tx.FinancialYear = req.FinancialYear;
        tx.TransactionDate = req.TransactionDate;
        if (!string.IsNullOrWhiteSpace(req.ContractReference)) tx.ContractReference = req.ContractReference;
        tx.TransactionType = req.TransactionType;
        tx.Description = req.Description ?? "";
        tx.TransactionAmount = req.TransactionAmount;
        tx.Currency = req.Currency ?? "INR";
        tx.IsOrdinaryCourseOfBusiness = req.IsOrdinaryCourseOfBusiness;
        tx.ArmLengthMethod = req.ArmLengthMethod ?? "CUP";
        tx.BenchmarkArmLengthPrice = req.BenchmarkArmLengthPrice;
        tx.AuditCommitteeApprovalStatus = req.AuditCommitteeApprovalStatus ?? "Approved";
        tx.AuditCommitteeMeetingDate = req.AuditCommitteeMeetingDate;
        tx.OmnibusApprovalId = req.OmnibusApprovalId;
        tx.BoardResolutionDate = req.BoardResolutionDate;
        tx.ShareholdersResolutionDate = req.ShareholdersResolutionDate;
        tx.Mbp4EnteredDate = req.Mbp4EnteredDate ?? DateTime.UtcNow;
        tx.OutstandingBalanceAtYearEnd = req.OutstandingBalanceAtYearEnd;
        tx.BalanceType = req.BalanceType ?? "Debit";
        tx.UpdatedAt = DateTime.UtcNow;

        // Perform statutory evaluation
        RelatedPartyEngine.EvaluateTransaction(tx, req.AnnualTurnoverBenchmark, req.NetWorthBenchmark);

        // Track omnibus utilization if applicable
        if (tx.OmnibusApprovalId.HasValue)
        {
            var omni = await _db.RelatedPartyOmnibusApprovals.FirstOrDefaultAsync(x => x.Id == tx.OmnibusApprovalId.Value && x.FirmId == firmId, ct);
            if (omni != null)
            {
                omni.UtilizedAmount += tx.TransactionAmount;
            }
        }

        await _db.SaveChangesAsync(ct);
        return (await GetTransactionByIdAsync(tx.Id, ct))!;
    }

    public async Task<bool> DeleteTransactionAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var tx = await _db.RelatedPartyTransactions.FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);
        if (tx is null) return false;

        _db.RelatedPartyTransactions.Remove(tx);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<RelatedPartyTransactionDto> ApproveTransactionAsync(Guid id, ApproveTransactionRequest req, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var tx = await _db.RelatedPartyTransactions
            .Include(x => x.RelatedParty)
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);

        if (tx is null)
            throw new KeyNotFoundException("Related party transaction not found.");

        tx.AuditCommitteeApprovalStatus = req.ApprovalStatus;
        if (req.MeetingDate.HasValue) tx.AuditCommitteeMeetingDate = req.MeetingDate.Value;
        if (req.OmnibusApprovalId.HasValue) tx.OmnibusApprovalId = req.OmnibusApprovalId.Value;
        if (req.BoardDate.HasValue) tx.BoardResolutionDate = req.BoardDate.Value;
        if (req.ShareholdersDate.HasValue) tx.ShareholdersResolutionDate = req.ShareholdersDate.Value;
        tx.Status = "Approved";
        tx.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return (await GetTransactionByIdAsync(tx.Id, ct))!;
    }

    // -------------------------------------------------------------
    // Omnibus Approvals (Section 177)
    // -------------------------------------------------------------

    public async Task<List<RelatedPartyOmnibusApprovalDto>> ListOmnibusApprovalsAsync(Guid? clientId, string? financialYear, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var query = _db.RelatedPartyOmnibusApprovals
            .Include(x => x.Client)
            .Where(x => x.FirmId == firmId);

        if (clientId.HasValue) query = query.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(financialYear)) query = query.Where(x => x.FinancialYear == financialYear);

        var list = await query.OrderByDescending(x => x.AuditCommitteeMeetingDate).ToListAsync(ct);

        return list.Select(o => new RelatedPartyOmnibusApprovalDto(
            Id: o.Id,
            ClientId: o.ClientId,
            ClientName: o.Client?.Name,
            FinancialYear: o.FinancialYear,
            AuditCommitteeMeetingDate: o.AuditCommitteeMeetingDate,
            Title: o.Title,
            MaximumApprovedLimit: o.MaximumApprovedLimit,
            UtilizedAmount: o.UtilizedAmount,
            ApprovalCriteria: o.ApprovalCriteria,
            ValidityPeriodTo: o.ValidityPeriodTo,
            IsActive: o.IsActive,
            CreatedAt: o.CreatedAt,
            UpdatedAt: o.UpdatedAt
        )).ToList();
    }

    public async Task<RelatedPartyOmnibusApprovalDto> CreateOrUpdateOmnibusApprovalAsync(Guid? id, SaveOmnibusApprovalRequest req, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        RelatedPartyOmnibusApproval? omni = null;

        if (id.HasValue && id.Value != Guid.Empty)
        {
            omni = await _db.RelatedPartyOmnibusApprovals.FirstOrDefaultAsync(x => x.Id == id.Value && x.FirmId == firmId, ct);
            if (omni is null) throw new KeyNotFoundException("Omnibus approval not found.");
        }
        else
        {
            omni = new RelatedPartyOmnibusApproval
            {
                FirmId = firmId,
                ClientId = req.ClientId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _db.RelatedPartyOmnibusApprovals.Add(omni);
        }

        omni.FinancialYear = req.FinancialYear;
        omni.AuditCommitteeMeetingDate = req.AuditCommitteeMeetingDate;
        omni.Title = req.Title.Trim();
        omni.MaximumApprovedLimit = req.MaximumApprovedLimit;
        omni.ApprovalCriteria = req.ApprovalCriteria ?? "";
        omni.ValidityPeriodTo = req.ValidityPeriodTo;
        omni.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new RelatedPartyOmnibusApprovalDto(
            Id: omni.Id,
            ClientId: omni.ClientId,
            ClientName: omni.Client?.Name,
            FinancialYear: omni.FinancialYear,
            AuditCommitteeMeetingDate: omni.AuditCommitteeMeetingDate,
            Title: omni.Title,
            MaximumApprovedLimit: omni.MaximumApprovedLimit,
            UtilizedAmount: omni.UtilizedAmount,
            ApprovalCriteria: omni.ApprovalCriteria,
            ValidityPeriodTo: omni.ValidityPeriodTo,
            IsActive: omni.IsActive,
            CreatedAt: omni.CreatedAt,
            UpdatedAt: omni.UpdatedAt
        );
    }

    public async Task<bool> DeleteOmnibusApprovalAsync(Guid id, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var omni = await _db.RelatedPartyOmnibusApprovals.FirstOrDefaultAsync(x => x.Id == id && x.FirmId == firmId, ct);
        if (omni is null) return false;

        _db.RelatedPartyOmnibusApprovals.Remove(omni);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // -------------------------------------------------------------
    // Reports: Form MBP-4 Register & AS 18 / Ind AS 24 Disclosures
    // -------------------------------------------------------------

    public async Task<RelatedPartyReportsDto> GenerateReportsAsync(Guid clientId, string financialYear, CancellationToken ct = default)
    {
        var firmId = _tenantContext.FirmId;
        var client = await _db.Clients.FirstOrDefaultAsync(x => x.Id == clientId && x.FirmId == firmId, ct);
        if (client is null) throw new KeyNotFoundException("Client not found.");

        var txList = await _db.RelatedPartyTransactions
            .Include(x => x.RelatedParty)
            .Where(x => x.FirmId == firmId && x.ClientId == clientId && x.FinancialYear == financialYear)
            .OrderBy(x => x.TransactionDate)
            .ToListAsync(ct);

        var mbp4 = RelatedPartyEngine.GenerateMbp4Register(txList, client.Name, financialYear);
        var as18 = RelatedPartyEngine.GenerateAs18IndAs24DisclosureNote(txList, client.Name, financialYear);

        return new RelatedPartyReportsDto(mbp4, as18);
    }

    private static RelatedPartyTransactionDto ToTxDto(RelatedPartyTransaction t)
    {
        return new RelatedPartyTransactionDto(
            Id: t.Id,
            ClientId: t.ClientId,
            ClientName: t.Client?.Name,
            RelatedPartyId: t.RelatedPartyId,
            RelatedPartyName: t.RelatedParty?.Name,
            RelationshipType: t.RelatedParty?.RelationshipType,
            FinancialYear: t.FinancialYear,
            TransactionDate: t.TransactionDate,
            ContractReference: t.ContractReference,
            TransactionType: t.TransactionType,
            Description: t.Description,
            TransactionAmount: t.TransactionAmount,
            Currency: t.Currency,
            AnnualTurnoverBenchmark: t.AnnualTurnoverBenchmark,
            NetWorthBenchmark: t.NetWorthBenchmark,
            ThresholdLimitAmount: t.ThresholdLimitAmount,
            ThresholdPercentage: t.ThresholdPercentage,
            ExceedsStatutoryThreshold: t.ExceedsStatutoryThreshold,
            IsOrdinaryCourseOfBusiness: t.IsOrdinaryCourseOfBusiness,
            IsArmLength: t.IsArmLength,
            ArmLengthMethod: t.ArmLengthMethod,
            BenchmarkArmLengthPrice: t.BenchmarkArmLengthPrice,
            PriceVariancePercentage: t.PriceVariancePercentage,
            AuditCommitteeApprovalStatus: t.AuditCommitteeApprovalStatus,
            AuditCommitteeMeetingDate: t.AuditCommitteeMeetingDate,
            OmnibusApprovalId: t.OmnibusApprovalId,
            BoardResolutionDate: t.BoardResolutionDate,
            ShareholdersResolutionDate: t.ShareholdersResolutionDate,
            RequiresShareholderApproval: t.RequiresShareholderApproval,
            Section40A2bRisk: t.Section40A2bRisk,
            Mbp4EnteredDate: t.Mbp4EnteredDate,
            OutstandingBalanceAtYearEnd: t.OutstandingBalanceAtYearEnd,
            BalanceType: t.BalanceType,
            Status: t.Status,
            CreatedAt: t.CreatedAt,
            UpdatedAt: t.UpdatedAt
        );
    }
}
