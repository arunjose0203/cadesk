using System.Text.Json.Nodes;
using CaDesk.Application.Billing;

namespace CaDesk.UnitTests;

/// <summary>Sprint 69: Razorpay payment links and webhooks.</summary>
public class OnlinePaymentRulesTests
{
    // A payment_link.paid webhook body; the signature below was computed independently (Python hmac/sha256).
    private const string Body = "{\"entity\":\"event\",\"event\":\"payment_link.paid\",\"payload\":{\"payment_link\":{\"entity\":{\"id\":\"plink_N1a2b3c4d5\",\"amount_paid\":118000,\"status\":\"paid\",\"reference_id\":\"x\"}},\"payment\":{\"entity\":{\"id\":\"pay_Q9w8e7r6t5\",\"amount\":118000,\"method\":\"upi\",\"created_at\":1793000000}}}}";
    private const string Secret = "whsec_test_123";
    private const string Signature = "a930ca936b422ad4cae39509310eb6f565a0e9500f11d13ea0b6a57cf2dad16b";

    [Fact]
    public void Signature_is_hex_hmac_sha256_of_the_raw_body()
    {
        Assert.True(RazorpayRules.SignatureOk(Body, Signature, Secret));
        Assert.True(RazorpayRules.SignatureOk(Body, Signature.ToUpperInvariant(), Secret));
        Assert.False(RazorpayRules.SignatureOk(Body + " ", Signature, Secret));        // body changed
        Assert.False(RazorpayRules.SignatureOk(Body, Signature, "other-secret"));
        Assert.False(RazorpayRules.SignatureOk(Body, "not-hex", Secret));
        Assert.False(RazorpayRules.SignatureOk(Body, null, Secret));
        Assert.False(RazorpayRules.SignatureOk(Body, Signature, ""));
    }

    [Fact]
    public void Paid_webhook_is_parsed()
    {
        var ev = RazorpayRules.ParseWebhook(Body)!;
        Assert.Equal("payment_link.paid", ev.Event);
        Assert.Equal("plink_N1a2b3c4d5", ev.PaymentLinkId);
        Assert.Equal("pay_Q9w8e7r6t5", ev.PaymentId);
        Assert.Equal(118000, ev.AmountPaise);
        Assert.Equal("upi", ev.Method);
        Assert.Equal(new DateOnly(2026, 10, 26), RazorpayRules.IndiaDate(ev.PaidAt!.Value));
        Assert.Null(RazorpayRules.ParseWebhook("{\"event\":\"payment.captured\",\"payload\":{}}"));
        Assert.Null(RazorpayRules.ParseWebhook("not json"));
    }

    [Theory]
    [InlineData(1180.00, 118000)]
    [InlineData(0.005, 1)]
    [InlineData(99.994, 9999)]
    [InlineData(12345.675, 1234568)]
    public void Rupees_become_whole_paise(double rupees, long paise) => Assert.Equal(paise, RazorpayRules.ToPaise((decimal)rupees));

    [Theory]
    [InlineData("98470 12345", "+919847012345")]
    [InlineData("+91 98470-12345", "+919847012345")]
    [InlineData("09847012345", "+919847012345")]
    [InlineData("971501234567", "+971501234567")]
    [InlineData("123", null)]
    [InlineData(null, null)]
    public void Phone_numbers_get_a_country_code(string? phone, string? contact) => Assert.Equal(contact, RazorpayRules.Contact(phone));

    [Fact]
    public void Reference_id_is_unique_per_link_and_at_most_40_characters()
    {
        var id = Guid.NewGuid();
        var a = RazorpayRules.ReferenceId(id, DateTimeOffset.FromUnixTimeSeconds(1793000000));
        var b = RazorpayRules.ReferenceId(id, DateTimeOffset.FromUnixTimeSeconds(1793000060));
        Assert.True(a.Length <= 40);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Payment_link_request_body()
    {
        var json = RazorpayRules.PaymentLinkJson(new PaymentLinkRequest(118000, "ref-1", "Invoice INV/26-27/041",
            "Kochi Spices", "accounts@kochispices.in", "+919847012345",
            new Dictionary<string, string> { ["invoice_number"] = "INV/26-27/041" }));
        var o = JsonNode.Parse(json)!.AsObject();
        Assert.Equal(118000, o["amount"]!.GetValue<long>());
        Assert.Equal("INR", o["currency"]!.GetValue<string>());
        Assert.False(o["accept_partial"]!.GetValue<bool>());
        Assert.Equal("ref-1", o["reference_id"]!.GetValue<string>());
        Assert.False(o["notify"]!["sms"]!.GetValue<bool>());
        Assert.Equal("+919847012345", o["customer"]!["contact"]!.GetValue<string>());
        Assert.Equal("INV/26-27/041", o["notes"]!["invoice_number"]!.GetValue<string>());

        var bare = JsonNode.Parse(RazorpayRules.PaymentLinkJson(new PaymentLinkRequest(100, "r", "d", null, null, null,
            new Dictionary<string, string>())))!.AsObject();
        Assert.False(bare.ContainsKey("customer"));
        Assert.False(bare.ContainsKey("notes"));
    }

    [Fact]
    public void Create_response_and_errors()
    {
        var c = RazorpayRules.ParseCreated("{\"id\":\"plink_X\",\"short_url\":\"https://rzp.io/i/abc\",\"status\":\"created\"}")!;
        Assert.Equal("plink_X", c.Id);
        Assert.Equal("https://rzp.io/i/abc", c.ShortUrl);
        Assert.Null(RazorpayRules.ParseCreated("{\"error\":{}}"));
        Assert.Equal("reference_id already exists",
            RazorpayRules.ErrorMessage("{\"error\":{\"code\":\"BAD_REQUEST_ERROR\",\"description\":\"reference_id already exists\"}}"));
        Assert.Equal("no reason given", RazorpayRules.ErrorMessage("<html>"));
    }
}
