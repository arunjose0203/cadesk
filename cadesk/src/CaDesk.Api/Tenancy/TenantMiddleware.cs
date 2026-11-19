using CaDesk.Api.Auth;
using CaDesk.Application.Abstractions;
using CaDesk.Infrastructure.Audit;
using Microsoft.AspNetCore.Authorization;

namespace CaDesk.Api.Tenancy;

/// <summary>
/// Reads firm_id from the signed token and sets the tenant for this request.
/// An authenticated user without a valid firm_id is refused (403).
/// Anonymous requests pass through with no tenant: they can't see firm data.
/// Anonymous endpoints (sign-up, login, refresh ...) are skipped even if a token is sent:
/// they pick the firm themselves.
/// </summary>
public sealed class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, ITenantSetter tenant, RequestContext request)
    {
        request.Ip = ctx.Connection.RemoteIpAddress; // for the audit log (real client IP behind the proxy)
        var anonymousEndpoint = ctx.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        if (ctx.User.Identity?.IsAuthenticated == true && !anonymousEndpoint)
        {
            var firm = ctx.User.FindFirst(AppClaims.FirmId)?.Value;
            if (!Guid.TryParse(firm, out var firmId) || firmId == Guid.Empty)
            {
                await Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                        title: "No firm in token", detail: "This token is not linked to a firm.")
                    .ExecuteAsync(ctx);
                return;
            }

            Guid? userId = Guid.TryParse(ctx.User.FindFirst(AppClaims.Subject)?.Value, out var u) ? u : null;
            tenant.Set(firmId, userId, ctx.User.FindFirst(AppClaims.Role)?.Value);
        }

        await next(ctx);
    }
}
