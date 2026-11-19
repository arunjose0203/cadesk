using CaDesk.Application.Abstractions;
using CaDesk.Application.Configuration;
using CaDesk.Domain.Firms;
using CaDesk.Domain.Users;
using CaDesk.Infrastructure.Persistence;
using CaDesk.Infrastructure.Tenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Api.Auth;

/// <summary>
/// Sign-up, login (with lockout), TOTP MFA, refresh-token rotation, logout, password change,
/// invitation acceptance. Every write runs with the tenant set, so the EF filter and
/// row-level security both apply.
///
/// Every sign-in failure (unknown email, wrong password, locked account) returns the same
/// 401 message, so the API never reveals which emails exist or whether an account is locked.
/// </summary>
public sealed class AuthService(
    AppDbContext db,
    TenantContext tenant,
    PlatformDb platform,
    TokenIssuer tokens,
    JwtOptions jwt,
    IDataProtectionProvider dataProtection,
    IServiceScopeFactory scopes,
    ISettings settings,
    TimeProvider clock,
    ILogger<AuthService> log)
{
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(30);
    private static readonly PasswordHasher<User> Hasher = new();
    private static readonly string DummyHash = Hasher.HashPassword(new User(), "not-a-real-password-0000");

    private IDataProtector Protector => dataProtection.CreateProtector("CaDesk.Mfa.v1");
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------------ sign-up

    public async Task<AuthResult> SignUpAsync(SignUpRequest r, RequestInfo info, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(r.FirmName) || r.FirmName.Length > 200) errors["firmName"] = ["Firm name is required."];
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 200) errors["name"] = ["Your name is required."];
        if (!LooksLikeEmail(r.Email)) errors["email"] = ["Enter a valid email address."];
        if (PasswordProblem(r.Password, r.Email) is { } p) errors["password"] = [p];
        if (errors.Count > 0) throw new RequestValidationException(errors);

        (Guid firmId, Guid userId) created;
        try
        {
            created = await platform.SignUpAsync(r.FirmName, r.CountryCode ?? "IN", r.Name, r.Email,
                Hasher.HashPassword(new User(), r.Password), ct: ct);
        }
        catch (ArgumentException ex)
        {
            throw new RequestValidationException("countryCode", ex.Message);
        }

        log.LogInformation("Firm {FirmId} signed up", created.firmId);
        tenant.Set(created.firmId, created.userId, null);
        return await CompleteLoginAsync(created.userId, info, familyId: null, ct);
    }

    // ------------------------------------------------------------------ login

    public async Task<AuthResult> LoginAsync(LoginRequest r, RequestInfo info, CancellationToken ct)
    {
        var email = (r.Email ?? "").Trim();
        var all = await FindUsersAsync(email, ct);
        if (r.FirmId is { } wanted) all = all.Where(x => x.FirmId == wanted).ToList();

        // Locked accounts are left out BEFORE any password check: no password oracle while locked.
        var open = all.Where(x => x.LockoutUntil is not { } until || until <= Now).ToList();
        if (open.Count == 0)
        {
            Hasher.VerifyHashedPassword(new User(), DummyHash, r.Password ?? ""); // same timing as a real check
            await LogLoginAsync(all.FirstOrDefault()?.FirmId, all.FirstOrDefault()?.UserId, email, false,
                all.Count == 0 ? "unknown_email" : "locked", info, ct);
            throw new AuthFailedException();
        }

        var matched = open.Where(x => PasswordMatches(x.PasswordHash, r.Password)).ToList();
        if (matched.Count == 0)
        {
            foreach (var each in open) await RecordFailureInFirmAsync(each.FirmId, each.UserId, ct);
            await LogLoginAsync(open[0].FirmId, open[0].UserId, email, false, "bad_password", info, ct);
            throw new AuthFailedException();
        }

        // Same email in several firms: offer only firms where the password matched.
        if (matched.Count > 1)
            return new AuthResult
            {
                Status = AuthStatus.ChooseFirm,
                Firms = matched.Select(m => new FirmChoice(m.FirmId, m.FirmName)).ToList(),
            };

        var c = matched[0];
        if (!c.IsActive || c.FirmStatus is FirmStatus.Suspended or FirmStatus.Closed)
        {
            await LogLoginAsync(c.FirmId, c.UserId, email, false, "inactive", info, ct);
            throw new AuthFailedException("This account is not active. Contact your firm's partner.", 403);
        }

        if (c.MfaEnabled)
            return new AuthResult { Status = AuthStatus.MfaRequired, MfaToken = tokens.IssueMfaPending(c.UserId, c.FirmId) };

        tenant.Set(c.FirmId, c.UserId, null);
        await LogLoginAsync(c.FirmId, c.UserId, email, true, "ok", info, ct);
        return await CompleteLoginAsync(c.UserId, info, familyId: null, ct, rehashPassword: r.Password);
    }

    // ------------------------------------------------------------------ MFA (login step 2)

    public async Task<AuthResult> VerifyMfaAsync(MfaVerifyRequest r, RequestInfo info, CancellationToken ct)
    {
        var pending = await tokens.ValidateMfaPendingAsync(r.MfaToken ?? "")
            ?? throw new AuthFailedException("Your sign-in expired. Start again.");
        tenant.Set(pending.FirmId, pending.UserId, null);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == pending.UserId, ct) ?? throw new AuthFailedException();
        if (user.LockoutUntil is { } until && until > Now) throw new AuthFailedException();
        await EnsureActiveAsync(user, ct);

        var mfa = await db.UserMfa.FirstOrDefaultAsync(m => m.UserId == user.Id && m.EnabledAt != null, ct)
            ?? throw new AuthFailedException();

        if (!CheckMfaCode(mfa, r.Code))
        {
            await RecordFailureAsync(db, user.Id, ct);
            await LogLoginAsync(user.FirmId, user.Id, user.Email, false, "mfa_failed", info, ct);
            throw new AuthFailedException("The code is not correct.");
        }
        await db.SaveChangesAsync(ct); // last used step / used recovery code

        await LogLoginAsync(user.FirmId, user.Id, user.Email, true, "ok", info, ct);
        return await CompleteLoginAsync(user.Id, info, familyId: null, ct);
    }

    // ------------------------------------------------------------------ refresh / logout

    public async Task<AuthResult> RefreshAsync(string? refreshToken, RequestInfo info, CancellationToken ct)
    {
        var t = await FindTokenAsync(refreshToken, ct) ?? throw new AuthFailedException("Not signed in.");
        tenant.Set(t.FirmId, t.UserId, null);
        var token = await db.RefreshTokens.AsNoTracking().FirstAsync(x => x.Id == t.Id, ct);

        if (token.RevokedAt is not null) await HandleRevokedTokenAsync(token, ct); // always throws
        if (token.ExpiresAt <= Now) throw new AuthFailedException("Session expired. Sign in again.");

        // Idle sign-out: each refresh issues a new token, so its creation time is the last activity.
        // Never shorter than one access-token lifetime + 5 minutes, or active users would be signed out.
        var idleMinutes = await EffectiveIdleMinutesAsync(ct);
        if (Now - token.CreatedAt > TimeSpan.FromMinutes(idleMinutes))
        {
            await RevokeFamilyAsync(token.FamilyId, "idle_timeout", ct);
            throw new AuthFailedException("You were signed out after a period of inactivity.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null || !user.IsActive || await FirmBlockedAsync(ct))
        {
            await RevokeFamilyAsync(token.FamilyId, "user_inactive", ct);
            throw new AuthFailedException("This account is not active.", 403);
        }

        // Rotation is atomic: only one request can claim (revoke) this token.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var claimed = await db.RefreshTokens
            .Where(x => x.Id == token.Id && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, Now).SetProperty(x => x.RevokedReason, "rotated")
                .SetProperty(x => x.LastUsedAt, Now), ct);
        if (claimed == 0)
        {
            await tx.RollbackAsync(ct);
            throw new AuthFailedException("Session was just refreshed. Retry.", 409);
        }

        var result = await CompleteLoginAsync(user.Id, info, token.FamilyId, ct, recordLogin: false);
        await db.RefreshTokens.Where(x => x.Id == token.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReplacedBy, result.RefreshTokenId), ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task HandleRevokedTokenAsync(RefreshToken token, CancellationToken ct)
    {
        // A rotated token used again within seconds is usually two browser tabs refreshing at once.
        if (token.RevokedReason == "rotated" && Now - token.RevokedAt!.Value < RotationGrace)
            throw new AuthFailedException("Session was just refreshed. Retry.", 409);

        // Otherwise an old token is being replayed: assume theft and end the whole session family.
        await RevokeFamilyAsync(token.FamilyId, "reuse_detected", ct);
        log.LogWarning("Refresh token reuse detected for user {UserId}; session family revoked", token.UserId);
        throw new AuthFailedException("Session ended. Sign in again.");
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        var t = await FindTokenAsync(refreshToken, ct);
        if (t is null) return;
        tenant.Set(t.FirmId, t.UserId, null);
        await RevokeFamilyAsync(t.FamilyId, "logout", ct);
    }

    // ------------------------------------------------------------------ signed-in user

    public async Task<UserProfile> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await LoadUserWithRolesAsync(userId, ct) ?? throw new AuthFailedException("Not signed in.");
        var firm = await db.Firms.FirstAsync(ct);
        var mfa = await db.UserMfa.AnyAsync(m => m.UserId == userId && m.EnabledAt != null, ct);
        return await ProfileAsync(user, firm, mfa, ct);
    }

    /// <summary>Changes the password, signs out every session, and returns a fresh session for this device.</summary>
    public async Task<AuthResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest r, RequestInfo info, CancellationToken ct)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        if (user.LockoutUntil is { } until && until > Now) throw new AuthFailedException("Too many failed attempts. Try again later.", 429);
        if (!PasswordMatches(user.PasswordHash, r.CurrentPassword))
        {
            await RecordFailureAsync(db, userId, ct);
            throw new RequestValidationException("currentPassword", "Current password is not correct.");
        }
        var minLength = await settings.GetAsync(Keys.PasswordMinLength, ct: ct);
        if (PasswordProblem(r.NewPassword, user.Email, minLength) is { } p)
            throw new RequestValidationException("newPassword", p);

        user.PasswordHash = Hasher.HashPassword(user, r.NewPassword);
        await db.SaveChangesAsync(ct);
        await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, Now).SetProperty(x => x.RevokedReason, "password_changed"), ct);
        return await CompleteLoginAsync(userId, info, familyId: null, ct, recordLogin: false);
    }

    // ------------------------------------------------------------------ MFA set-up (signed in)

    public async Task<MfaSetupResult> BeginMfaSetupAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        var existing = await db.UserMfa.FirstOrDefaultAsync(m => m.UserId == userId, ct);
        if (existing?.EnabledAt is not null) throw new BusinessRuleException("Two-step sign-in is already on.");

        var secret = Totp.NewSecret();
        var protectedSecret = Protector.Protect(Base32.Encode(secret));
        if (existing is null)
            db.UserMfa.Add(new UserMfa { UserId = userId, SecretProtected = protectedSecret });
        else
            existing.SecretProtected = protectedSecret;
        await db.SaveChangesAsync(ct);

        return new MfaSetupResult(Base32.Encode(secret), Totp.OtpAuthUri(secret, "CA Desk", user.Email));
    }

    public async Task<MfaEnabledResult> EnableMfaAsync(Guid userId, string code, CancellationToken ct)
    {
        var mfa = await db.UserMfa.FirstOrDefaultAsync(m => m.UserId == userId, ct)
            ?? throw new BusinessRuleException("Start two-step sign-in set-up first.");
        if (mfa.EnabledAt is not null) throw new BusinessRuleException("Two-step sign-in is already on.");

        var secret = Base32.Decode(Protector.Unprotect(mfa.SecretProtected));
        if (!Totp.Verify(secret, code, mfa.LastUsedStep, clock.GetUtcNow(), out var step))
            throw new RequestValidationException("code", "The code is not correct. Check the time on your phone.");

        var codes = SecureTokens.NewRecoveryCodes();
        mfa.EnabledAt = Now;
        mfa.LastUsedStep = step;
        mfa.RecoveryCodesHash = codes.Select(c => SecureTokens.Hash(SecureTokens.NormaliseRecoveryCode(c))).ToList();
        await db.SaveChangesAsync(ct);
        return new MfaEnabledResult(codes);
    }

    public async Task DisableMfaAsync(Guid userId, string code, CancellationToken ct)
    {
        var mfa = await db.UserMfa.FirstOrDefaultAsync(m => m.UserId == userId && m.EnabledAt != null, ct)
            ?? throw new BusinessRuleException("Two-step sign-in is not on.");
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        if (user.LockoutUntil is { } until && until > Now) throw new AuthFailedException("Too many failed attempts. Try again later.", 429);
        if (!CheckMfaCode(mfa, code))
        {
            await RecordFailureAsync(db, userId, ct);
            throw new RequestValidationException("code", "The code is not correct.");
        }
        db.UserMfa.Remove(mfa);
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ invitations

    public async Task<AuthResult> AcceptInvitationAsync(AcceptInviteRequest r, RequestInfo info, CancellationToken ct)
    {
        var hash = SecureTokens.Hash(r.Token ?? "");
        var inv = (await db.Database.SqlQuery<InvitationLookup>($"""
            select id as "Id", firm_id as "FirmId", email::text as "Email", expires_at as "ExpiresAt", accepted_at as "AcceptedAt"
            from auth_find_invitation({hash})
            """).ToListAsync(ct)).FirstOrDefault();
        if (inv is null || inv.AcceptedAt is not null || inv.ExpiresAt <= Now)
            throw new BusinessRuleException("This invitation is not valid or has expired. Ask for a new one.", 410);

        if (string.IsNullOrWhiteSpace(r.Name)) throw new RequestValidationException("name", "Your name is required.");

        var userId = Guid.CreateVersion7();
        tenant.Set(inv.FirmId, userId, null);
        if (await FirmBlockedAsync(ct)) throw new AuthFailedException("This firm's account is not active.", 403);
        var minLength = await settings.GetAsync(Keys.PasswordMinLength, ct: ct);
        if (PasswordProblem(r.Password, inv.Email, minLength) is { } p) throw new RequestValidationException("password", p);

        var invitation = await db.Invitations.FirstAsync(i => i.Id == inv.Id, ct);
        if (await EmailTakenAsync(invitation.Email, ct))
            throw new BusinessRuleException("A user with this email already exists in the firm.");

        var user = new User { Id = userId, Name = r.Name.Trim(), Email = invitation.Email };
        user.PasswordHash = Hasher.HashPassword(user, r.Password);
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = userId, RoleId = invitation.RoleId });
        invitation.AcceptedAt = Now;
        await db.SaveChangesAsync(ct);

        await LogLoginAsync(inv.FirmId, userId, invitation.Email, true, "invitation_accepted", info, ct);
        return await CompleteLoginAsync(userId, info, familyId: null, ct);
    }

    /// <summary>Includes soft-deleted users: the unique index covers them too.</summary>
    internal Task<bool> EmailTakenAsync(string email, CancellationToken ct) =>
        db.Users.IgnoreQueryFilters().AnyAsync(u => u.FirmId == tenant.FirmId && u.Email == email, ct);

    // ================================================================== helpers

    private async Task<AuthResult> CompleteLoginAsync(Guid userId, RequestInfo info, Guid? familyId,
        CancellationToken ct, bool recordLogin = true, string? rehashPassword = null)
    {
        var user = await LoadUserWithRolesAsync(userId, ct) ?? throw new AuthFailedException();
        var firm = await db.Firms.FirstAsync(ct);

        if (recordLogin)
        {
            user.FailedLoginCount = 0;
            user.LockoutUntil = null;
            user.LastLoginAt = Now;
        }
        if (rehashPassword is not null && user.PasswordHash is not null &&
            Hasher.VerifyHashedPassword(user, user.PasswordHash, rehashPassword) == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = Hasher.HashPassword(user, rehashPassword);

        var raw = SecureTokens.New();
        var expires = Now.AddDays(jwt.RefreshTokenDays);
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            FamilyId = familyId ?? Guid.CreateVersion7(),
            TokenHash = SecureTokens.Hash(raw),
            ExpiresAt = expires,
            CreatedIp = info.Ip,
            UserAgent = info.UserAgent is { Length: > 300 } ua ? ua[..300] : info.UserAgent,
            CreatedAt = Now,
            LastUsedAt = Now,
        };
        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync(ct);

        var mfa = await db.UserMfa.AnyAsync(m => m.UserId == user.Id && m.EnabledAt != null, ct);
        var profile = await ProfileAsync(user, firm, mfa, ct);
        return new AuthResult
        {
            Status = AuthStatus.Ok,
            AccessToken = tokens.Issue(user.Id, firm.Id, profile.Roles, profile.Permissions, user.Name,
                mfaSetupRequired: profile.MfaSetupRequired),
            ExpiresIn = tokens.AccessTokenSeconds,
            User = profile,
            RefreshToken = raw,
            RefreshTokenId = refresh.Id,
            RefreshTokenExpires = expires,
        };
    }

    private Task<User?> LoadUserWithRolesAsync(Guid userId, CancellationToken ct) =>
        db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role!).ThenInclude(r => r.Permissions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

    private async Task<UserProfile> ProfileAsync(User user, Firm firm, bool mfaEnabled, CancellationToken ct)
    {
        var roles = user.UserRoles.Where(ur => ur.Role is not null).Select(ur => ur.Role!).ToList();
        var roleNames = roles.Select(r => r.Name).OrderBy(n => n).ToList();
        var mfaRoles = await settings.GetAsync(Keys.MfaRequiredRoles, ct: ct);
        var setupRequired = !mfaEnabled && roleNames.Any(r => mfaRoles.Contains(r, StringComparer.OrdinalIgnoreCase));
        return new UserProfile(user.Id, user.Name, user.Email, firm.Id, firm.Name,
            roleNames,
            roles.SelectMany(r => r.Permissions).Select(p => p.PermissionCode).Distinct().OrderBy(p => p).ToList(),
            mfaEnabled,
            setupRequired,
            await EffectiveIdleMinutesAsync(ct));
    }

    /// <summary>
    /// The firm's inactivity limit, never shorter than one access-token lifetime + 5 minutes.
    /// Counted from the last session refresh, so the real limit can run up to one token lifetime longer.
    /// </summary>
    private async Task<int> EffectiveIdleMinutesAsync(CancellationToken ct) =>
        Math.Max(await settings.GetAsync(Keys.SessionTimeoutMinutes, ct: ct), jwt.AccessTokenMinutes + 5);

    private async Task EnsureActiveAsync(User user, CancellationToken ct)
    {
        if (!user.IsActive || await FirmBlockedAsync(ct))
            throw new AuthFailedException("This account is not active. Contact your firm's partner.", 403);
    }

    private async Task<bool> FirmBlockedAsync(CancellationToken ct) =>
        await db.Firms.AnyAsync(f => f.Status == FirmStatus.Suspended || f.Status == FirmStatus.Closed, ct);

    private bool CheckMfaCode(UserMfa mfa, string? code)
    {
        var secret = Base32.Decode(Protector.Unprotect(mfa.SecretProtected));
        if (Totp.Verify(secret, code, mfa.LastUsedStep, clock.GetUtcNow(), out var step))
        {
            mfa.LastUsedStep = step;
            return true;
        }

        // recovery code: single use
        if (string.IsNullOrWhiteSpace(code)) return false;
        var h = SecureTokens.Hash(SecureTokens.NormaliseRecoveryCode(code));
        if (!mfa.RecoveryCodesHash.Contains(h)) return false;
        mfa.RecoveryCodesHash = mfa.RecoveryCodesHash.Where(x => x != h).ToList();
        return true;
    }

    /// <summary>
    /// One atomic UPDATE, so parallel wrong guesses can't race past the counter.
    /// SQL evaluates both right-hand sides against the row's old values.
    /// </summary>
    private Task RecordFailureAsync(AppDbContext context, Guid userId, CancellationToken ct)
    {
        DateTime? lockUntil = Now.Add(LockoutDuration);
        return context.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.LockoutUntil, u => u.FailedLoginCount + 1 >= MaxFailedLogins ? lockUntil : u.LockoutUntil)
            .SetProperty(u => u.FailedLoginCount, u => u.FailedLoginCount + 1 >= MaxFailedLogins ? 0 : u.FailedLoginCount + 1), ct);
    }

    /// <summary>Failure counts live in each firm; use a separate scope so this request's tenant stays unset.</summary>
    private async Task RecordFailureInFirmAsync(Guid firmId, Guid userId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantSetter>().Set(firmId, null, null);
        await RecordFailureAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), userId, ct);
    }

    private Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct) =>
        db.RefreshTokens.Where(x => x.FamilyId == familyId && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, Now).SetProperty(x => x.RevokedReason, reason), ct);

    private async Task<TokenLookup?> FindTokenAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(refreshToken)) return null;
        var hash = SecureTokens.Hash(refreshToken);
        return (await db.Database.SqlQuery<TokenLookup>($"""
            select id as "Id", firm_id as "FirmId", user_id as "UserId", family_id as "FamilyId",
                   expires_at as "ExpiresAt", revoked_at as "RevokedAt"
            from auth_find_refresh_token({hash})
            """).ToListAsync(ct)).FirstOrDefault();
    }

    private async Task<List<LoginCandidate>> FindUsersAsync(string email, CancellationToken ct)
    {
        if (!LooksLikeEmail(email)) return [];
        return await db.Database.SqlQuery<LoginCandidate>($"""
            select user_id as "UserId", firm_id as "FirmId", firm_name as "FirmName", firm_status as "FirmStatus",
                   password_hash as "PasswordHash", is_active as "IsActive", lockout_until as "LockoutUntil",
                   mfa_enabled as "MfaEnabled"
            from auth_find_users({email}::citext)
            """).ToListAsync(ct);
    }

    private Task LogLoginAsync(Guid? firmId, Guid? userId, string email, bool ok, string reason, RequestInfo info,
        CancellationToken ct)
    {
        var ip = info.Ip;
        var ua = info.UserAgent;
        return db.Database.ExecuteSqlAsync(
            $"select auth_log_login({firmId}::uuid, {userId}::uuid, {email}::citext, {ok}, {reason}::text, {ip}::inet, {ua}::text)",
            ct);
    }

    private static bool PasswordMatches(string? hash, string? password) =>
        hash is not null && password is not null &&
        Hasher.VerifyHashedPassword(new User(), hash, password) != PasswordVerificationResult.Failed;

    internal static bool LooksLikeEmail(string? email) =>
        email is { Length: >= 5 and <= 254 } && email.Contains('@') && !email.Contains(' ') &&
        email.IndexOf('@') > 0 && email.LastIndexOf('.') > email.IndexOf('@') + 1;

    /// <summary>Simple, user-friendly policy: long beats complex. Minimum length is a firm setting (never below 10).</summary>
    internal static string? PasswordProblem(string? password, string? email, int minLength = 10)
    {
        minLength = Math.Max(10, minLength);
        if (password is null || password.Length < minLength) return $"Use at least {minLength} characters.";
        if (password.Length > 128) return "Use at most 128 characters.";
        if (email is not null && password.Contains(email.Split('@')[0], StringComparison.OrdinalIgnoreCase))
            return "Don't use your email name in the password.";
        if (password.Distinct().Count() < 4) return "Use a less repetitive password.";
        return null;
    }
}
