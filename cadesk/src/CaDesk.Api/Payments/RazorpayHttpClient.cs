using System.Net.Http.Headers;
using System.Text;
using CaDesk.Application.Abstractions;
using CaDesk.Application.Billing;

namespace CaDesk.Api.Payments;

/// <summary>Razorpay's REST API (base address https://api.razorpay.com/), with the firm's own keys.</summary>
public sealed class RazorpayHttpClient(HttpClient http) : IRazorpayClient
{
    public async Task<PaymentLinkCreated> CreatePaymentLinkAsync(RazorpayCredentials credentials, PaymentLinkRequest request, CancellationToken ct)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, "v1/payment_links");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.KeyId}:{credentials.KeySecret}")));
        msg.Content = new StringContent(RazorpayRules.PaymentLinkJson(request), Encoding.UTF8, "application/json");

        HttpResponseMessage res;
        try
        {
            res = await http.SendAsync(msg, ct);
        }
        catch (HttpRequestException)
        {
            throw new BusinessRuleException("Razorpay could not be reached. Try again in a minute.", 502);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new BusinessRuleException("Razorpay did not answer in time. Try again in a minute.", 504);
        }

        using (res)
        {
            var text = await res.Content.ReadAsStringAsync(ct);
            if ((int)res.StatusCode == 401)
                throw new BusinessRuleException("Razorpay rejected the key ID or key secret. Check them under Settings, in the Payments section.", 502);
            if (!res.IsSuccessStatusCode)
                throw new BusinessRuleException($"Razorpay refused the payment link: {RazorpayRules.ErrorMessage(text)}", 502);
            return RazorpayRules.ParseCreated(text)
                ?? throw new BusinessRuleException("Razorpay sent an unexpected reply.", 502);
        }
    }
}
