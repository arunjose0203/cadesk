using System.Text.RegularExpressions;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Dsc;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Dsc;

public partial class DscService(IAppDbContext db, ITenantContext tenant)
{
    private static readonly Regex PanRegex = MyPanRegex();
    [GeneratedRegex("^[A-Z]{5}[0-9]{4}[A-Z]$")]
    private static partial Regex MyPanRegex();

    public async Task<List<DscDto>> GetListAsync(
        Guid? clientId = null,
        string? status = null,
        string? location = null,
        int? expiringWithinDays = null,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var q = db.DscRegisters
            .Include(x => x.Client)
            .Include(x => x.CustodianUser)
            .Include(x => x.MovementLogs).ThenInclude(m => m.HandlerUser)
            .AsNoTracking();

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(location) && location != "all") q = q.Where(x => x.Location == location);

        if (expiringWithinDays.HasValue)
        {
            var limit = today.AddDays(expiringWithinDays.Value);
            q = q.Where(x => x.ValidTo >= today && x.ValidTo <= limit);
        }

        var list = await q
            .OrderBy(x => x.ValidTo)
            .Skip(skip)
            .Take(Math.Min(take, 200))
            .ToListAsync(ct);

        var dtos = list.Select(x => ToDto(x, today)).ToList();

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            dtos = dtos.Where(x => x.Status == status).ToList();
        }

        return dtos;
    }

    public async Task<DscDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dsc = await db.DscRegisters
            .Include(x => x.Client)
            .Include(x => x.CustodianUser)
            .Include(x => x.MovementLogs).ThenInclude(m => m.HandlerUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (dsc is null) throw new BusinessRuleException("DSC not found.", 404);
        return ToDto(dsc, today);
    }

    public async Task<DscSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var list = await db.DscRegisters.AsNoTracking().ToListAsync(ct);

        var activeCount = 0;
        var expiringSoonCount = 0;
        var expiredCount = 0;
        var inVaultCount = 0;
        var checkedOutCount = 0;

        foreach (var d in list)
        {
            var status = ComputeStatus(d.ValidTo, today);
            if (status == DscStatus.Active) activeCount++;
            else if (status == DscStatus.ExpiringSoon) expiringSoonCount++;
            else if (status == DscStatus.Expired) expiredCount++;

            if (d.Location == DscLocation.Vault) inVaultCount++;
            else checkedOutCount++;
        }

        return new DscSummaryDto(activeCount, expiringSoonCount, expiredCount, inVaultCount, checkedOutCount);
    }

    public async Task<DscDto> CreateAsync(CreateDscRequest req, Guid currentUserId, CancellationToken ct = default)
    {
        var pan = req.Pan.Trim().ToUpperInvariant();
        if (!PanRegex.IsMatch(pan))
            throw new RequestValidationException(nameof(req.Pan), "Invalid PAN format (e.g. ABCDE1234F).");

        if (string.IsNullOrWhiteSpace(req.HolderName))
            throw new RequestValidationException(nameof(req.HolderName), "Holder name is required.");

        if (req.ValidTo < req.ValidFrom)
            throw new RequestValidationException(nameof(req.ValidTo), "Validity end date cannot be earlier than start date.");

        if (req.ClientId.HasValue)
        {
            var clientExists = await db.Clients.AnyAsync(x => x.Id == req.ClientId.Value, ct);
            if (!clientExists) throw new BusinessRuleException("Client not found.", 404);
        }

        if (req.CustodianUserId.HasValue)
        {
            var userExists = await db.Users.AnyAsync(x => x.Id == req.CustodianUserId.Value, ct);
            if (!userExists) throw new BusinessRuleException("Custodian user not found.", 404);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var location = string.IsNullOrWhiteSpace(req.Location) ? DscLocation.Vault : req.Location;
        var status = ComputeStatus(req.ValidTo, today);

        var dsc = new DscRegister
        {
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            HolderName = req.HolderName.Trim(),
            Pan = pan,
            DscType = req.DscType ?? DscType.Class3,
            IssuingAuthority = string.IsNullOrWhiteSpace(req.IssuingAuthority) ? "eMudhra" : req.IssuingAuthority.Trim(),
            ValidFrom = req.ValidFrom,
            ValidTo = req.ValidTo,
            SerialNumber = req.SerialNumber?.Trim(),
            UsbTokenId = req.UsbTokenId?.Trim(),
            Location = location,
            CustodianUserId = req.CustodianUserId,
            Status = status,
            Notes = req.Notes?.Trim(),
            CreatedBy = currentUserId
        };

        db.DscRegisters.Add(dsc);

        // Initial movement log
        dsc.MovementLogs.Add(new DscMovementLog
        {
            FirmId = tenant.FirmId,
            DscId = dsc.Id,
            MovedAt = DateTime.UtcNow,
            FromLocation = "initial_registration",
            ToLocation = location,
            HandlerUserId = req.CustodianUserId ?? currentUserId,
            Remarks = "Initial token registration into custody.",
            CreatedBy = currentUserId
        });

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(dsc.Id, ct);
    }

    public async Task<DscDto> UpdateAsync(Guid id, UpdateDscRequest req, Guid currentUserId, CancellationToken ct = default)
    {
        var dsc = await db.DscRegisters.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (dsc is null) throw new BusinessRuleException("DSC not found.", 404);

        var pan = req.Pan.Trim().ToUpperInvariant();
        if (!PanRegex.IsMatch(pan))
            throw new RequestValidationException(nameof(req.Pan), "Invalid PAN format.");

        if (string.IsNullOrWhiteSpace(req.HolderName))
            throw new RequestValidationException(nameof(req.HolderName), "Holder name is required.");

        if (req.ValidTo < req.ValidFrom)
            throw new RequestValidationException(nameof(req.ValidTo), "Validity end date cannot be earlier than start date.");

        if (req.ClientId.HasValue)
        {
            var clientExists = await db.Clients.AnyAsync(x => x.Id == req.ClientId.Value, ct);
            if (!clientExists) throw new BusinessRuleException("Client not found.", 404);
        }

        dsc.ClientId = req.ClientId;
        dsc.HolderName = req.HolderName.Trim();
        dsc.Pan = pan;
        if (!string.IsNullOrWhiteSpace(req.DscType)) dsc.DscType = req.DscType;
        if (!string.IsNullOrWhiteSpace(req.IssuingAuthority)) dsc.IssuingAuthority = req.IssuingAuthority.Trim();
        dsc.ValidFrom = req.ValidFrom;
        dsc.ValidTo = req.ValidTo;
        dsc.SerialNumber = req.SerialNumber?.Trim();
        dsc.UsbTokenId = req.UsbTokenId?.Trim();
        dsc.Notes = req.Notes?.Trim();
        dsc.UpdatedBy = currentUserId;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        dsc.Status = ComputeStatus(dsc.ValidTo, today);

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(dsc.Id, ct);
    }

    public async Task<DscDto> TransferCustodyAsync(Guid id, TransferCustodyRequest req, Guid currentUserId, CancellationToken ct = default)
    {
        var dsc = await db.DscRegisters.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (dsc is null) throw new BusinessRuleException("DSC not found.", 404);

        if (!DscLocation.All.Contains(req.ToLocation))
            throw new RequestValidationException(nameof(req.ToLocation), $"Invalid location '{req.ToLocation}'.");

        if (req.CustodianUserId.HasValue)
        {
            var userExists = await db.Users.AnyAsync(x => x.Id == req.CustodianUserId.Value, ct);
            if (!userExists) throw new BusinessRuleException("Custodian user not found.", 404);
        }

        var prevLocation = dsc.Location;
        dsc.Location = req.ToLocation;
        dsc.CustodianUserId = req.CustodianUserId;
        dsc.UpdatedBy = currentUserId;

        db.DscMovementLogs.Add(new DscMovementLog
        {
            FirmId = tenant.FirmId,
            DscId = dsc.Id,
            MovedAt = DateTime.UtcNow,
            FromLocation = prevLocation,
            ToLocation = req.ToLocation,
            HandlerUserId = req.CustodianUserId ?? currentUserId,
            Remarks = req.Remarks?.Trim(),
            CreatedBy = currentUserId
        });

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(dsc.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var dsc = await db.DscRegisters.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (dsc is null) return;

        db.DscRegisters.Remove(dsc);
        await db.SaveChangesAsync(ct);
    }

    private static string ComputeStatus(DateOnly validTo, DateOnly today, int warningDays = 30)
    {
        if (validTo < today) return DscStatus.Expired;
        if (validTo <= today.AddDays(warningDays)) return DscStatus.ExpiringSoon;
        return DscStatus.Active;
    }

    private static DscDto ToDto(DscRegister x, DateOnly today)
    {
        var daysRemaining = x.ValidTo.DayNumber - today.DayNumber;
        var computedStatus = ComputeStatus(x.ValidTo, today);

        return new DscDto(
            x.Id,
            x.ClientId,
            x.Client?.Name,
            x.HolderName,
            x.Pan,
            x.DscType,
            x.IssuingAuthority,
            x.ValidFrom,
            x.ValidTo,
            daysRemaining,
            x.SerialNumber,
            x.UsbTokenId,
            x.Location,
            x.CustodianUserId,
            x.CustodianUser?.Name,
            computedStatus,
            x.Notes,
            x.MovementLogs.OrderByDescending(m => m.MovedAt).Select(m => new DscMovementLogDto(
                m.Id,
                m.MovedAt,
                m.FromLocation,
                m.ToLocation,
                m.HandlerUserId,
                m.HandlerUser?.Name,
                m.Remarks
            )).ToList(),
            x.CreatedAt
        );
    }
}
