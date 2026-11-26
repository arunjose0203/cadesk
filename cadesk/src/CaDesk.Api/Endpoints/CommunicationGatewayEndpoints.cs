using CaDesk.Api.Auth;
using CaDesk.Application.Notifications;
using CaDesk.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace CaDesk.Api.Endpoints;

public static class CommunicationGatewayEndpoints
{
    public static IEndpointRouteBuilder MapCommunicationGatewayEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/communications").WithTags("CommunicationsGateway");

        // Gateway Status & Health
        g.MapGet("/gateway/status", async (CommunicationGatewayService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetGatewayStatusAsync(ct)))
            .RequirePermission(Perms.SettingsManage);

        // Test Dispatches
        g.MapPost("/gateway/test-whatsapp", async ([FromBody] TestWhatsAppRequest req, CommunicationGatewayService svc, CancellationToken ct) =>
            Results.Ok(await svc.SendTestWhatsAppAsync(req, ct)))
            .RequirePermission(Perms.SettingsManage);

        g.MapPost("/gateway/test-email", async ([FromBody] TestEmailRequest req, CommunicationGatewayService svc, CancellationToken ct) =>
            Results.Ok(await svc.SendTestEmailAsync(req, ct)))
            .RequirePermission(Perms.SettingsManage);

        // Statutory Deadlines Campaign Dispatch
        g.MapPost("/deadlines/dispatch", async ([FromBody] StatutoryDispatchFilter filter, CommunicationGatewayService svc, CancellationToken ct) =>
            Results.Ok(await svc.DispatchStatutoryDeadlinesAsync(filter, ct)))
            .RequirePermission(Perms.SettingsManage);

        // SA 505 Confirmations Dispatch
        g.MapPost("/sa505/dispatch", async ([FromBody] Sa505DispatchFilter filter, CommunicationGatewayService svc, CancellationToken ct) =>
            Results.Ok(await svc.DispatchSa505CampaignAsync(filter, ct)))
            .RequirePermission(Perms.AuditManage);

        // Portal OTP Dispatch
        g.MapPost("/portal/otp", async ([FromBody] SendPortalOtpRequest req, CommunicationGatewayService svc, CancellationToken ct) =>
            Results.Ok(await svc.SendPortalOtpAsync(req, ct)))
            .AllowAnonymous();

        // Invoice Delivery Dispatch
        g.MapPost("/invoices/dispatch", async ([FromBody] DispatchInvoiceNotificationRequest req, CommunicationGatewayService svc, CancellationToken ct) =>
            Results.Ok(await svc.DispatchInvoiceAsync(req, ct)))
            .RequirePermission(Perms.BillingManage);

        // Meta WhatsApp Cloud API Webhook Listener
        g.MapGet("/webhook/whatsapp", (
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? token,
            [FromQuery(Name = "hub.challenge")] string? challenge,
            IConfiguration config) =>
        {
            var expectedToken = config["Notifications:WhatsApp:VerifyToken"];
            if (!string.IsNullOrWhiteSpace(expectedToken) && mode == "subscribe" && token == expectedToken)
            {
                return Results.Content(challenge ?? "", "text/plain");
            }
            return Results.StatusCode(403);
        }).AllowAnonymous();

        g.MapPost("/webhook/whatsapp", async ([FromBody] object payload, CommunicationGatewayService svc, CancellationToken ct) =>
        {
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var ok = await svc.ProcessWhatsAppWebhookAsync(json, ct);
            return Results.Ok(new { status = ok ? "processed" : "ignored" });
        }).AllowAnonymous();

        return app;
    }
}
