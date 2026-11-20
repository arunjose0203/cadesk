using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Billing;
using CaDesk.Application.Dsc;
using CaDesk.Domain.Billing;
using CaDesk.Domain.Compliance;
using CaDesk.Domain.Dsc;
using CaDesk.Domain.Portal;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Application.Portal;

public partial class ClientPortalService(IAppDbContext db, ITenantContext tenant)
{
    private static readonly Regex EmailRegex = MyEmailRegex();
    [GeneratedRegex(@"^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$", RegexOptions.IgnoreCase)]
    private static partial Regex MyEmailRegex();

    // ---------------------------------------------------------------- Portal User Management (Firm Staff)

    public async Task<PortalUserDto> CreatePortalUserAsync(CreatePortalUserRequest req, Guid staffUserId, CancellationToken ct = default)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (!EmailRegex.IsMatch(email))
            throw new RequestValidationException(nameof(req.Email), "Invalid email address format.");

        if (string.IsNullOrWhiteSpace(req.Name))
            throw new RequestValidationException(nameof(req.Name), "Name is required.");

        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
            throw new RequestValidationException(nameof(req.Password), "Password must be at least 8 characters long.");

        var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == req.ClientId, ct);
        if (client is null) throw new BusinessRuleException("Client organization not found.", 404);

        var existing = await db.ClientPortalUsers.AnyAsync(x => x.Email.ToLower() == email, ct);
        if (existing) throw new BusinessRuleException("A portal user with this email address already exists.", 409);

        var role = !string.IsNullOrWhiteSpace(req.Role) && PortalRole.All.Contains(req.Role)
            ? req.Role
            : PortalRole.ClientAdmin;

        var user = new ClientPortalUser
        {
            FirmId = tenant.FirmId,
            ClientId = req.ClientId,
            Email = email,
            Name = req.Name.Trim(),
            Phone = req.Phone?.Trim(),
            PasswordHash = HashPassword(req.Password),
            Role = role,
            IsActive = true,
            CreatedBy = staffUserId
        };

        db.ClientPortalUsers.Add(user);
        await db.SaveChangesAsync(ct);

        return ToUserDto(user, client.Name);
    }

    public async Task<List<PortalUserDto>> ListPortalUsersAsync(Guid? clientId = null, CancellationToken ct = default)
    {
        var q = db.ClientPortalUsers.Include(x => x.Client).AsNoTracking();
        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);

        var list = await q.OrderBy(x => x.Name).ToListAsync(ct);
        return list.Select(x => ToUserDto(x, x.Client?.Name)).ToList();
    }

    public async Task<PortalUserDto> SetPortalUserActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var user = await db.ClientPortalUsers.Include(x => x.Client).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) throw new BusinessRuleException("Portal user not found.", 404);

        user.IsActive = isActive;
        await db.SaveChangesAsync(ct);

        return ToUserDto(user, user.Client?.Name);
    }

    public async Task<bool> DeletePortalUserAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.ClientPortalUsers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return false;

        db.ClientPortalUsers.Remove(user);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ---------------------------------------------------------------- Portal Authentication (External Client)

    public async Task<(ClientPortalUser User, ClientSummaryDto Client)> AuthenticateAsync(PortalLoginRequest req, CancellationToken ct = default)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.ClientPortalUsers
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Email.ToLower() == email, ct);

        if (user is null || !VerifyPassword(req.Password, user.PasswordHash))
            throw new BusinessRuleException("Invalid client portal credentials.", 401);

        if (!user.IsActive)
            throw new BusinessRuleException("This client portal account is deactivated. Please contact your CA firm.", 403);

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var client = user.Client!;
        var clientSummary = new ClientSummaryDto(
            client.Id,
            client.Code,
            client.Name,
            client.EntityType,
            client.TaxIds
        );

        return (user, clientSummary);
    }

    // ---------------------------------------------------------------- Service Requests (Desk & Staff)

    public async Task<ServiceRequestDto> CreateRequestAsync(
        Guid clientId,
        Guid? portalUserId,
        CreateServiceRequestInput req,
        string senderName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Subject) || req.Subject.Length < 3)
            throw new RequestValidationException(nameof(req.Subject), "Subject must be at least 3 characters.");

        if (string.IsNullOrWhiteSpace(req.Description))
            throw new RequestValidationException(nameof(req.Description), "Description is required.");

        var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == clientId, ct);
        if (client is null) throw new BusinessRuleException("Client not found.", 404);

        var category = RequestCategory.All.Contains(req.Category) ? req.Category : RequestCategory.General;
        var priority = !string.IsNullOrWhiteSpace(req.Priority) && RequestPriority.All.Contains(req.Priority)
            ? req.Priority
            : RequestPriority.Medium;

        var seq = await GetNextRequestNumberAsync(ct);

        var sr = new ServiceRequest
        {
            FirmId = tenant.FirmId,
            ClientId = clientId,
            PortalUserId = portalUserId,
            RequestNumber = seq,
            Category = category,
            Subject = req.Subject.Trim(),
            Description = req.Description.Trim(),
            Priority = priority,
            Status = RequestStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };

        sr.Messages.Add(new ServiceRequestMessage
        {
            FirmId = tenant.FirmId,
            RequestId = sr.Id,
            SenderType = portalUserId.HasValue ? "client" : "staff",
            SenderName = senderName,
            SenderUserId = portalUserId,
            Message = req.Description.Trim(),
            CreatedAt = DateTime.UtcNow
        });

        db.ServiceRequests.Add(sr);
        await db.SaveChangesAsync(ct);

        return await GetRequestByIdAsync(sr.Id, ct);
    }

    public async Task<List<ServiceRequestDto>> ListRequestsAsync(
        Guid? clientId = null,
        string? status = null,
        string? category = null,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default)
    {
        var q = db.ServiceRequests
            .Include(x => x.Client)
            .Include(x => x.PortalUser)
            .Include(x => x.AssignedUser)
            .Include(x => x.Messages)
            .AsNoTracking();

        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(status) && status != "all") q = q.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(category) && category != "all") q = q.Where(x => x.Category == category);

        var list = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(Math.Min(take, 100))
            .ToListAsync(ct);

        return list.Select(ToRequestDto).ToList();
    }

    public async Task<ServiceRequestDto> GetRequestByIdAsync(Guid id, CancellationToken ct = default)
    {
        var sr = await db.ServiceRequests
            .Include(x => x.Client)
            .Include(x => x.PortalUser)
            .Include(x => x.AssignedUser)
            .Include(x => x.Messages)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (sr is null) throw new BusinessRuleException("Service request not found.", 404);
        return ToRequestDto(sr);
    }

    public async Task<ServiceRequestDto> AddMessageAsync(
        Guid requestId,
        string senderType,
        string senderName,
        Guid? senderUserId,
        AddRequestMessageInput req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Message))
            throw new RequestValidationException(nameof(req.Message), "Message cannot be empty.");

        var sr = await db.ServiceRequests.FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (sr is null) throw new BusinessRuleException("Service request not found.", 404);

        var msg = new ServiceRequestMessage
        {
            FirmId = tenant.FirmId,
            RequestId = sr.Id,
            SenderType = senderType,
            SenderName = senderName,
            SenderUserId = senderUserId,
            Message = req.Message.Trim(),
            AttachmentDocId = req.AttachmentDocId,
            CreatedAt = DateTime.UtcNow
        };

        db.ServiceRequestMessages.Add(msg);
        sr.UpdatedAt = DateTime.UtcNow;

        // If client replies to a resolved request, re-open it
        if (senderType == "client" && sr.Status == RequestStatus.Resolved)
        {
            sr.Status = RequestStatus.InProgress;
        }

        await db.SaveChangesAsync(ct);
        return await GetRequestByIdAsync(requestId, ct);
    }

    public async Task<ServiceRequestDto> UpdateRequestStatusAsync(
        Guid requestId,
        UpdateServiceRequestStatusInput req,
        Guid staffUserId,
        CancellationToken ct = default)
    {
        var sr = await db.ServiceRequests.FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (sr is null) throw new BusinessRuleException("Service request not found.", 404);

        if (!RequestStatus.All.Contains(req.Status))
            throw new RequestValidationException(nameof(req.Status), $"Invalid status '{req.Status}'.");

        sr.Status = req.Status;
        if (!string.IsNullOrWhiteSpace(req.ResolutionNotes)) sr.ResolutionNotes = req.ResolutionNotes.Trim();
        if (req.AssignedUserId.HasValue) sr.AssignedUserId = req.AssignedUserId.Value;

        if (req.Status is RequestStatus.Resolved or RequestStatus.Closed)
        {
            sr.ResolvedAt = DateTime.UtcNow;
        }

        sr.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await GetRequestByIdAsync(requestId, ct);
    }

    // ---------------------------------------------------------------- Client Portal Data Views

    public async Task<ClientPortalDashboardDto> GetPortalDashboardAsync(Guid clientId, CancellationToken ct = default)
    {
        var client = await db.Clients.FirstOrDefaultAsync(x => x.Id == clientId, ct);
        if (client is null) throw new BusinessRuleException("Client not found.", 404);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var openRequests = await db.ServiceRequests
            .Where(x => x.ClientId == clientId && (x.Status == RequestStatus.Submitted || x.Status == RequestStatus.InProgress))
            .CountAsync(ct);

        var pendingFilings = await db.Deadlines
            .Where(x => x.ClientId == clientId && x.Status == "pending" && x.DueDate >= today)
            .CountAsync(ct);

        var invoices = await db.Invoices
            .Where(x => x.ClientId == clientId && x.Status != InvoiceStatus.Cancelled)
            .Select(x => Math.Max(0, x.TotalAmount - x.PaidAmount))
            .ToListAsync(ct);

        var outstanding = invoices.Sum();

        var activeDscs = await db.DscRegisters
            .Where(x => x.ClientId == clientId && x.ValidTo >= today)
            .CountAsync(ct);

        var summary = new ClientSummaryDto(client.Id, client.Code, client.Name, client.EntityType, client.TaxIds);
        return new ClientPortalDashboardDto(summary, openRequests, pendingFilings, outstanding, activeDscs);
    }

    public async Task<List<ClientFilingDto>> GetPortalFilingsAsync(Guid clientId, CancellationToken ct = default)
    {
        var list = await db.Deadlines
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.DueDate)
            .Take(100)
            .ToListAsync(ct);

        return list.Select(x => new ClientFilingDto(
            x.Id,
            x.TypeCode,
            x.PeriodKey,
            x.DueDate,
            x.Status,
            x.FiledOn,
            x.AckNo
        )).ToList();
    }

    public async Task<List<DscDto>> GetPortalDscsAsync(Guid clientId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var list = await db.DscRegisters
            .Include(x => x.Client)
            .Include(x => x.CustodianUser)
            .Include(x => x.MovementLogs).ThenInclude(m => m.HandlerUser)
            .Where(x => x.ClientId == clientId)
            .OrderBy(x => x.ValidTo)
            .ToListAsync(ct);

        return list.Select(x =>
        {
            var daysRemaining = x.ValidTo.DayNumber - today.DayNumber;
            string status = x.ValidTo < today ? DscStatus.Expired : (daysRemaining <= 30 ? DscStatus.ExpiringSoon : DscStatus.Active);
            return new DscDto(
                x.Id, x.ClientId, x.Client?.Name, x.HolderName, x.Pan, x.DscType, x.IssuingAuthority,
                x.ValidFrom, x.ValidTo, daysRemaining, x.SerialNumber, x.UsbTokenId, x.Location,
                x.CustodianUserId, x.CustodianUser?.Name, status, x.Notes,
                x.MovementLogs.OrderByDescending(m => m.MovedAt).Select(m => new DscMovementLogDto(
                    m.Id, m.MovedAt, m.FromLocation, m.ToLocation, m.HandlerUserId, m.HandlerUser?.Name, m.Remarks
                )).ToList(),
                x.CreatedAt
            );
        }).ToList();
    }

    public async Task<List<InvoiceDto>> GetPortalInvoicesAsync(Guid clientId, CancellationToken ct = default)
    {
        var list = await db.Invoices
            .Include(x => x.Client)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .Where(x => x.ClientId == clientId && x.Status != InvoiceStatus.Draft)
            .OrderByDescending(x => x.IssueDate)
            .ToListAsync(ct);

        return list.Select(x =>
        {
            var balanceDue = Math.Max(0, x.TotalAmount - x.PaidAmount);
            return new InvoiceDto(
                x.Id, x.ClientId, x.Client?.Name, x.InvoiceNumber, x.IssueDate, x.DueDate, x.Status,
                x.Currency, x.Subtotal, x.TaxAmount, x.TotalAmount, x.PaidAmount, balanceDue,
                x.Notes, x.Terms,
                x.Items.Select(i => new InvoiceItemDto(i.Id, i.Description, i.SacCode, i.Quantity, i.UnitPrice, i.GstRate, i.Amount, i.TaxAmount, i.TotalAmount, i.TimeEntryId)).ToList(),
                x.Payments.OrderByDescending(p => p.PaymentDate).Select(p => new PaymentDto(p.Id, p.InvoiceId, x.InvoiceNumber, p.ClientId, x.Client?.Name, p.PaymentDate, p.Amount, p.PaymentMode, p.ReferenceNumber, p.Notes, p.CreatedAt)).ToList(),
                x.CreatedAt
            );
        }).ToList();
    }

    // ---------------------------------------------------------------- Helpers

    private async Task<string> GetNextRequestNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var count = await db.ServiceRequests
            .Where(x => x.CreatedAt.Year == year)
            .CountAsync(ct);

        return $"REQ/{year}/{(count + 1):D4}";
    }

    private static PortalUserDto ToUserDto(ClientPortalUser u, string? clientName) =>
        new(u.Id, u.ClientId, clientName, u.Email, u.Name, u.Phone, u.Role, u.IsActive, u.LastLoginAt, u.CreatedAt);

    private static ServiceRequestDto ToRequestDto(ServiceRequest r) =>
        new(
            r.Id,
            r.ClientId,
            r.Client?.Name,
            r.PortalUserId,
            r.PortalUser?.Name,
            r.RequestNumber,
            r.Category,
            r.Subject,
            r.Description,
            r.Priority,
            r.Status,
            r.AssignedUserId,
            r.AssignedUser?.Name,
            r.ResolutionNotes,
            r.ResolvedAt,
            r.CreatedAt,
            r.UpdatedAt,
            r.Messages.OrderBy(m => m.CreatedAt).Select(m => new ServiceRequestMessageDto(
                m.Id, m.SenderType, m.SenderName, m.SenderUserId, m.Message, m.AttachmentDocId, m.CreatedAt
            )).ToList()
        );

    private static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split('.');
            if (parts.Length != 2) return false;

            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] expectedHash = Convert.FromBase64String(parts[1]);

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations: 100_000,
                HashAlgorithmName.SHA256,
                outputLength: 32);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}
