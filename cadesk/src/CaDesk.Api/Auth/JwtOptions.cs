using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CaDesk.Api.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "cadesk";
    public string Audience { get; init; } = "cadesk-api";

    /// <summary>At least 32 characters. Set from an environment variable / secret in real environments.</summary>
    public string SigningKey { get; init; } = "";
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 14;

    public SymmetricSecurityKey Key() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public static class AppClaims
{
    public const string Subject = "sub";
    public const string FirmId = "firm_id";
    public const string Role = "role";
    public const string Permission = "perm";
    public const string Name = "name";
    public const string MfaSetup = "mfa_setup";   // "required": token only allows setting up two-step sign-in
}

/// <summary>Issues access tokens and the short-lived "MFA pending" token used between password and code.</summary>
public sealed class TokenIssuer(JwtOptions options, TimeProvider clock)
{
    private const string MfaAudience = "cadesk-mfa";

    public string Issue(Guid userId, Guid? firmId, IEnumerable<string> roles, IEnumerable<string> permissions, string? name = null,
        bool mfaSetupRequired = false)
    {
        var claims = new Dictionary<string, object>
        {
            [AppClaims.Subject] = userId.ToString(),
            [AppClaims.Role] = roles.ToArray(),
            [AppClaims.Permission] = permissions.ToArray(),
        };
        if (firmId is { } f) claims[AppClaims.FirmId] = f.ToString();
        if (name is not null) claims[AppClaims.Name] = name;
        if (mfaSetupRequired) claims[AppClaims.MfaSetup] = "required";

        return Create(options.Audience, TimeSpan.FromMinutes(options.AccessTokenMinutes), claims);
    }

    public int AccessTokenSeconds => options.AccessTokenMinutes * 60;

    /// <summary>Proves the password step passed; valid 5 minutes, only for /api/auth/mfa/verify.</summary>
    public string IssueMfaPending(Guid userId, Guid firmId) =>
        Create(MfaAudience, TimeSpan.FromMinutes(5), new Dictionary<string, object>
        {
            [AppClaims.Subject] = userId.ToString(),
            [AppClaims.FirmId] = firmId.ToString(),
        });

    public async Task<(Guid UserId, Guid FirmId)?> ValidateMfaPendingAsync(string token)
    {
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = options.Issuer,
            ValidAudience = MfaAudience,
            IssuerSigningKey = options.Key(),
            ValidateLifetime = false, // checked below against the server clock
        });
        if (!result.IsValid || result.SecurityToken is not JsonWebToken jwtToken) return null;
        if (jwtToken.ValidTo <= clock.GetUtcNow().UtcDateTime) return null;

        var sub = result.Claims.TryGetValue(AppClaims.Subject, out var s) ? s?.ToString() : null;
        var firm = result.Claims.TryGetValue(AppClaims.FirmId, out var fi) ? fi?.ToString() : null;
        return Guid.TryParse(sub, out var u) && Guid.TryParse(firm, out var fm) ? (u, fm) : null;
    }

    private string Create(string audience, TimeSpan lifetime, Dictionary<string, object> claims)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(lifetime),
            Claims = claims,
            SigningCredentials = new SigningCredentials(options.Key(), SecurityAlgorithms.HmacSha256),
        });
    }
}

public static class ClaimsPrincipalExtensions
{
    public static Guid? UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(AppClaims.Subject)?.Value, out var id) ? id : null;

    /// <summary>The signed-in user's id; refuses rather than recording the action against an empty id.</summary>
    public static Guid RequiredUserId(this ClaimsPrincipal user) =>
        user.UserId() ?? throw new AuthFailedException("Not signed in.");
}
