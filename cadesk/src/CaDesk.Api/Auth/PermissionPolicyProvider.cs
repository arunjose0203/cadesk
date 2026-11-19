using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CaDesk.Api.Auth;

/// <summary>
/// Builds a policy on the fly for any name like "perm:clients.write":
/// signed in + linked to a firm + has that permission in the access token.
/// Permission changes reach users at their next token refresh (at most 15 minutes).
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "perm:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(AppClaims.FirmId)
                .RequireClaim(AppClaims.Permission, policyName[Prefix.Length..])
                .Build();
        return await base.GetPolicyAsync(policyName);
    }
}

public static class PermissionEndpointExtensions
{
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(PermissionPolicyProvider.Prefix + permission);
}
