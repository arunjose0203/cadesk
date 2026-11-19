using System.Text.Json;
using CaDesk.Application.Configuration;
using CaDesk.Domain.Configuration;

namespace CaDesk.UnitTests;

public class ConfigResolverTests
{
    private static readonly Guid Firm = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly SettingsContext Ctx = new(Firm, "IN", "GROWTH", ClientId, UserId);

    private static ConfigDefinition Def(string key = "reminder.schedule_days", ConfigScope lowest = ConfigScope.Client,
        string defaultValue = "[7,3,0]", bool cap = false) =>
        new() { Key = key, LowestScope = lowest, DefaultValue = defaultValue, PlanValueIsCap = cap, IsActive = true };

    private static ConfigValue V(ConfigScope scope, string value, bool locked = false, string key = "reminder.schedule_days") => new()
    {
        Key = key, Scope = scope, Value = value, IsLocked = locked,
        ScopeRef = ConfigResolver.RefFor(scope, Ctx)!,
        FirmId = scope >= ConfigScope.Firm ? Firm : null,
    };

    private static string Raw(ResolvedValue r) => r.Value.GetRawText();

    [Fact]
    public void Uses_the_default_when_nothing_is_set()
    {
        var r = ConfigResolver.Resolve(Def(), [], Ctx);
        Assert.Equal("[7,3,0]", Raw(r));
        Assert.Null(r.Source);
    }

    [Fact]
    public void Most_specific_level_wins()
    {
        var values = new[] { V(ConfigScope.Platform, "[1]"), V(ConfigScope.Country, "[2]"), V(ConfigScope.Firm, "[3]"), V(ConfigScope.Client, "[4]") };
        var r = ConfigResolver.Resolve(Def(), values, Ctx);
        Assert.Equal("[4]", Raw(r));
        Assert.Equal(ConfigScope.Client, r.Source);

        var atFirm = ConfigResolver.Resolve(Def(), values, Ctx, ConfigScope.Firm);
        Assert.Equal("[3]", Raw(atFirm));
    }

    [Fact]
    public void Levels_below_the_lowest_scope_are_ignored()
    {
        var def = Def(lowest: ConfigScope.Firm);
        var values = new[] { V(ConfigScope.Firm, "[3]"), V(ConfigScope.Client, "[4]"), V(ConfigScope.User, "[5]") };
        Assert.Equal("[3]", Raw(ConfigResolver.Resolve(def, values, Ctx)));
    }

    [Fact]
    public void A_lock_stops_every_level_below()
    {
        var values = new[] { V(ConfigScope.Platform, "true", locked: true, key: "ai.review_required"), V(ConfigScope.Firm, "false", key: "ai.review_required") };
        var r = ConfigResolver.Resolve(Def("ai.review_required", ConfigScope.Firm, "true"), values, Ctx);
        Assert.Equal("true", Raw(r));
        Assert.True(r.IsLocked);
        Assert.Equal(ConfigScope.Platform, r.LockedAt);
    }

    [Fact]
    public void A_firm_lock_stops_client_values_but_not_the_firm_itself()
    {
        var values = new[] { V(ConfigScope.Firm, "[10]", locked: true), V(ConfigScope.Client, "[1]") };
        var r = ConfigResolver.Resolve(Def(), values, Ctx);
        Assert.Equal("[10]", Raw(r));
        Assert.True(r.IsLocked);

        var atFirm = ConfigResolver.Resolve(Def(), values, Ctx, ConfigScope.Firm);
        Assert.False(atFirm.IsLocked); // the firm can still change its own value
    }

    [Fact]
    public void Plan_value_caps_numbers_instead_of_being_a_default()
    {
        var def = Def("ai.monthly_page_quota", ConfigScope.Firm, "200", cap: true);
        var plan = V(ConfigScope.Plan, "1000", key: "ai.monthly_page_quota");

        Assert.Equal("200", Raw(ConfigResolver.Resolve(def, [plan], Ctx)));         // default, below cap
        var high = ConfigResolver.Resolve(def, [plan, V(ConfigScope.Firm, "5000", key: "ai.monthly_page_quota")], Ctx);
        Assert.Equal("1000", Raw(high));
        Assert.True(high.Capped);
    }

    [Fact]
    public void Values_for_other_clients_or_users_are_not_used()
    {
        var other = new ConfigValue { Key = "reminder.schedule_days", Scope = ConfigScope.Client, ScopeRef = Guid.NewGuid().ToString(), Value = "[9]", FirmId = Firm };
        Assert.Equal("[7,3,0]", Raw(ConfigResolver.Resolve(Def(), [other], Ctx)));
    }
}

public class ConfigValidatorTests
{
    private static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();
    private static bool Valid(string value, string schema) => ConfigValidator.Validate(J(value), J(schema)).Count == 0;

    private const string Days = """{"type":"array","items":{"type":"integer","minimum":0,"maximum":60},"minItems":1,"maxItems":6,"uniqueItems":true}""";

    [Theory]
    [InlineData("[7,3,0]", true)]
    [InlineData("[]", false)]                   // minItems
    [InlineData("[1,2,3,4,5,6,7]", false)]      // maxItems
    [InlineData("[3,3]", false)]                // uniqueItems
    [InlineData("[61]", false)]                 // item maximum
    [InlineData("[1.5]", false)]                // item must be integer
    [InlineData("\"7,3,0\"", false)]            // wrong type
    public void Validates_reminder_days(string value, bool ok) => Assert.Equal(ok, Valid(value, Days));

    [Theory]
    [InlineData("\"email\"", true)]
    [InlineData("\"fax\"", false)]
    public void Validates_enums(string value, bool ok) =>
        Assert.Equal(ok, Valid(value, """{"type":"string","enum":["email","whatsapp","sms"]}"""));

    [Theory]
    [InlineData("\"INV/{FY}/{SEQ:5}\"", true)]
    [InlineData("\"INV-{FY}\"", false)]         // must contain {SEQ}
    [InlineData("\"{SEQ}\"", true)]
    public void Validates_patterns(string value, bool ok) =>
        Assert.Equal(ok, Valid(value, """{"type":"string","minLength":3,"maxLength":40,"pattern":"\\{SEQ(:[1-9])?\\}"}"""));

    [Theory]
    [InlineData("18", true)]
    [InlineData("18.5", true)]
    [InlineData("-1", false)]
    [InlineData("101", false)]
    public void Validates_number_ranges(string value, bool ok) =>
        Assert.Equal(ok, Valid(value, """{"type":"number","minimum":0,"maximum":100}"""));

    [Fact]
    public void Errors_are_plain_english()
    {
        var errors = ConfigValidator.Validate(J("[99]"), J(Days));
        Assert.Contains(errors, e => e.Contains("at most 60"));
    }
}
