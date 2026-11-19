using System.Net;
using System.Text.Json.Serialization;

namespace CaDesk.Api.Auth;

public sealed record SignUpRequest(string FirmName, string? CountryCode, string Name, string Email, string Password);
public sealed record LoginRequest(string Email, string Password, Guid? FirmId);
public sealed record MfaVerifyRequest(string MfaToken, string Code);
public sealed record MfaCodeRequest(string Code);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record AcceptInviteRequest(string Token, string Name, string Password);

public sealed record UserProfile(
    Guid Id, string Name, string Email, Guid FirmId, string FirmName,
    IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions, bool MfaEnabled,
    bool MfaSetupRequired = false,       // firm requires two-step sign-in for one of this user's roles
    int SessionTimeoutMinutes = 30);     // web app signs out after this much inactivity

public sealed record FirmChoice(Guid FirmId, string FirmName);

public static class AuthStatus
{
    public const string Ok = "ok";
    public const string MfaRequired = "mfa_required";
    public const string ChooseFirm = "choose_firm";
}

/// <summary>
/// Result of sign-up / login / MFA / refresh. The refresh token never goes in the JSON body:
/// the endpoint puts it in an HttpOnly cookie.
/// </summary>
public sealed record AuthResult
{
    public required string Status { get; init; }
    public string? AccessToken { get; init; }
    public int? ExpiresIn { get; init; }
    public UserProfile? User { get; init; }
    public string? MfaToken { get; init; }
    public IReadOnlyList<FirmChoice>? Firms { get; init; }

    [JsonIgnore] public string? RefreshToken { get; init; }
    [JsonIgnore] public Guid RefreshTokenId { get; init; }
    [JsonIgnore] public DateTime? RefreshTokenExpires { get; init; }
}

public sealed record MfaSetupResult(string Secret, string OtpAuthUri);
public sealed record MfaEnabledResult(IReadOnlyList<string> RecoveryCodes);

public sealed record RequestInfo(IPAddress? Ip, string? UserAgent);

/// <summary>Login failed. Message is deliberately generic so it never reveals which part was wrong.</summary>
public sealed class AuthFailedException(string message = "Email or password is incorrect.", int statusCode = 401)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>Row shape returned by the auth_find_users() SQL function.</summary>
public sealed class LoginCandidate
{
    public Guid UserId { get; set; }
    public Guid FirmId { get; set; }
    public string FirmName { get; set; } = "";
    public string FirmStatus { get; set; } = "";
    public string? PasswordHash { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LockoutUntil { get; set; }
    public bool MfaEnabled { get; set; }
}

public sealed class TokenLookup
{
    public Guid Id { get; set; }
    public Guid FirmId { get; set; }
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public sealed class InvitationLookup
{
    public Guid Id { get; set; }
    public Guid FirmId { get; set; }
    public string Email { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
}
