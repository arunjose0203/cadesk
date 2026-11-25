using System.Text;
using CaDesk.Api.Auth;
using CaDesk.Application.Billing;
using CaDesk.Domain.Users;

namespace CaDesk.Api.Endpoints;

/// <summary>Sprint 69: Razorpay payment links on invoices, and Razorpay's webhook.</summary>
public static class OnlinePaymentEndpoints
{
    public static IEndpointRouteBuilder MapOnlinePaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/invoices").WithTags("Online payments");

        g.MapGet("/payments/online-status", async (OnlinePaymentService svc, CancellationToken ct) =>
            Results.Ok(new { setUp = await svc.IsSetUpAsync(ct) }))
            .RequirePermission(Perms.BillingRead);

        // the current link for the balance due, or a new one
        g.MapPost("/{id:guid}/payment-link", async (Guid id, OnlinePaymentService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetOrCreateLinkAsync(id, ct)))
            .RequirePermission(Perms.BillingManage);

        // Razorpay calls this; the firm id in the path picks the firm, the signature proves it is Razorpay.
        // Configure in the Razorpay dashboard: https://<site>/api/webhooks/razorpay/<firm id>, events payment_link.*
        app.MapPost("/api/webhooks/razorpay/{firmId:guid}", async (Guid firmId, HttpRequest req, OnlinePaymentService svc, CancellationToken ct) =>
            {
                string body;
                using (var reader = new StreamReader(req.Body, Encoding.UTF8))
                    body = await reader.ReadToEndAsync(ct);
                var outcome = await svc.HandleWebhookAsync(firmId, body, req.Headers["X-Razorpay-Signature"].ToString(), ct);
                return outcome switch
                {
                    WebhookOutcome.BadSignature => Results.Unauthorized(),
                    WebhookOutcome.UnknownFirm or WebhookOutcome.NotConfigured => Results.NotFound(),
                    _ => Results.Ok(new { outcome = outcome.ToString() }),
                };
            })
            .AllowAnonymous()
            .DisableAntiforgery()
            .RequireRateLimiting(DocumentEndpoints.PortalRateLimit)
            .WithTags("Online payments");

        return app;
    }
}
