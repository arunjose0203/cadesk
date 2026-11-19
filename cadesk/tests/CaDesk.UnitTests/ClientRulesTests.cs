using CaDesk.Application.Clients;
using CaDesk.Domain.Clients;

namespace CaDesk.UnitTests;

public class TaxIdRulesTests
{
    private static readonly TaxIdType[] India =
    [
        new() { CountryCode = "IN", Code = "PAN", Label = "PAN", Pattern = "^[A-Z]{5}[0-9]{4}[A-Z]$", Example = "ABCDE1234F" },
        new() { CountryCode = "IN", Code = "GSTIN", Label = "GSTIN", Pattern = "^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$",
                Checksum = "gstin", IsUnique = true, Example = "27AAPFU0939F1ZV" },
    ];

    private static Dictionary<string, string[]> Check(params (string, string)[] ids) =>
        TaxIdRules.Validate(ids.ToDictionary(x => x.Item1, x => TaxIdRules.Normalise(x.Item2)), India);

    [Theory]
    [InlineData("27AAPFU0939F1ZV")]
    [InlineData("32AAACA1234F1ZJ")]
    [InlineData("32AAACB1234C2ZN")]
    public void Real_gstin_check_digits_pass(string gstin) => Assert.True(TaxIdRules.GstinCheckDigitOk(gstin));

    [Theory]
    [InlineData("27AAPFU0939F1ZA")]   // wrong check digit
    [InlineData("27AAPFU0939F1Z")]    // too short
    public void Wrong_gstin_fails(string gstin) => Assert.False(TaxIdRules.GstinCheckDigitOk(gstin));

    [Fact]
    public void Normalise_strips_spaces_and_dashes_and_upper_cases() =>
        Assert.Equal("ABCDE1234F", TaxIdRules.Normalise(" abcde 1234-f "));

    [Fact]
    public void Valid_pan_and_matching_gstin_pass() =>
        Assert.Empty(Check(("PAN", "AAACA1234F"), ("GSTIN", "32AAACA1234F1ZJ")));

    [Fact]
    public void Bad_pan_format_is_reported_with_example()
    {
        var e = Check(("PAN", "AAAC1234F"));
        Assert.Contains("ABCDE1234F", e["taxIds.PAN"][0]);
    }

    [Fact]
    public void Gstin_with_wrong_check_digit_is_reported() =>
        Assert.Contains("check digit", Check(("GSTIN", "32AAACA1234F1ZK"))["taxIds.GSTIN"][0]);

    [Fact]
    public void Gstin_of_another_pan_is_reported() =>
        Assert.Contains("different PAN", Check(("PAN", "BXMPM4521K"), ("GSTIN", "32AAACA1234F1ZJ"))["taxIds.GSTIN"][0]);

    [Fact]
    public void Id_type_from_another_country_is_reported() =>
        Assert.True(Check(("TRN", "100123456700003")).ContainsKey("taxIds.TRN"));
}

public class CustomFieldRulesTests
{
    private static CustomFieldDefinition Def(string key, string type, bool required = false, List<string>? options = null,
        string? regex = null, bool active = true) =>
        new() { Key = key, Label = key.ToUpperInvariant(), FieldType = type, IsRequired = required, Options = options,
                ValidationRegex = regex, IsActive = active };

    [Fact]
    public void Values_are_normalised()
    {
        var defs = new[]
        {
            Def("turnover", CustomFieldTypes.Number), Def("since", CustomFieldTypes.Date), Def("audit", CustomFieldTypes.Bool),
            Def("plan", CustomFieldTypes.Dropdown, options: ["Basic", "Premium"]), Def("note", CustomFieldTypes.Text),
        };
        var (values, errors) = CustomFieldRules.Validate(new Dictionary<string, string>
        {
            ["turnover"] = "1,25,000.50", ["since"] = "01-04-2020", ["audit"] = "Yes", ["plan"] = "premium", ["note"] = "  hi  ",
        }, defs);

        Assert.Empty(errors);
        Assert.Equal("125000.50", values["turnover"]);
        Assert.Equal("2020-04-01", values["since"]);
        Assert.Equal("true", values["audit"]);
        Assert.Equal("Premium", values["plan"]);
        Assert.Equal("hi", values["note"]);
    }

    [Fact]
    public void Bad_values_unknown_keys_and_missing_required_are_reported()
    {
        var defs = new[]
        {
            Def("turnover", CustomFieldTypes.Number), Def("since", CustomFieldTypes.Date),
            Def("plan", CustomFieldTypes.Dropdown, options: ["Basic"]), Def("partner", CustomFieldTypes.Text, required: true),
            Def("udyam", CustomFieldTypes.Text, regex: "^UDYAM-[A-Z]{2}-[0-9]{2}-[0-9]{7}$"),
        };
        var (_, errors) = CustomFieldRules.Validate(new Dictionary<string, string>
        {
            ["turnover"] = "lots", ["since"] = "31-02-2020", ["plan"] = "Gold", ["udyam"] = "UDYAM-KL", ["nope"] = "x",
        }, defs);

        Assert.Equal(
            new[] { "custom.nope", "custom.partner", "custom.plan", "custom.since", "custom.turnover", "custom.udyam" },
            errors.Keys.Order().ToArray());
    }

    [Fact]
    public void Inactive_fields_are_neither_required_nor_returned()
    {
        var (values, errors) = CustomFieldRules.Validate(new Dictionary<string, string> { ["old"] = "x" },
            [Def("old", CustomFieldTypes.Text, required: true, active: false)]);
        Assert.Empty(errors);
        Assert.Empty(values);
    }

    [Fact]
    public void A_broken_regex_fails_closed_instead_of_throwing()
    {
        var (_, errors) = CustomFieldRules.Validate(new Dictionary<string, string> { ["x"] = "abc" },
            [Def("x", CustomFieldTypes.Text, regex: "([a-z")]);
        Assert.True(errors.ContainsKey("custom.x"));
    }
}

public class NumberFormatTests
{
    [Theory]
    [InlineData(2026, 4, 1, 4, "2026-27")]
    [InlineData(2027, 3, 31, 4, "2026-27")]
    [InlineData(2026, 3, 31, 4, "2025-26")]
    [InlineData(2026, 12, 31, 1, "2026")]
    [InlineData(2099, 6, 1, 4, "2099-00")]
    public void Financial_year(int y, int m, int d, int start, string expected) =>
        Assert.Equal(expected, NumberFormat.FinancialYear(new DateOnly(y, m, d), start));

    [Theory]
    [InlineData("C{SEQ:4}", 7, "C0007")]
    [InlineData("INV/{FY}/{SEQ:5}", 42, "INV/2026-27/00042")]
    [InlineData("{YY}{MM}-{SEQ}", 3, "2611-3")]
    [InlineData("C{SEQ:2}", 12345, "C12345")]   // padding never cuts digits
    public void Format(string pattern, long seq, string expected) =>
        Assert.Equal(expected, NumberFormat.Format(pattern, seq, new DateOnly(2026, 11, 19), 4));

    [Theory]
    [InlineData("C{SEQ:4}", "")]
    [InlineData("INV/{FY}/{SEQ}", "2026-27")]
    [InlineData("{YYYY}-{SEQ}", "2026")]
    [InlineData("{YY}{MM}{SEQ}", "2026-11")]
    public void Period_decides_when_numbering_restarts(string pattern, string expected) =>
        Assert.Equal(expected, NumberFormat.Period(pattern, new DateOnly(2026, 11, 19), 4));
}

public class CsvParserTests
{
    [Fact]
    public void Quotes_escapes_newlines_and_bom()
    {
        var t = CsvParser.Parse("﻿Name,Legal name,PAN\r\n\"Joseph & Sons, Kochi\",\"He said \"\"hi\"\"\",AAJFJ3321Q\r\n\"Two\nlines\",,X\n\n");
        Assert.Equal(new[] { "Name", "Legal name", "PAN" }, t.Headers);
        Assert.Equal(2, t.Rows.Count);
        Assert.Equal("Joseph & Sons, Kochi", t.Rows[0][0]);
        Assert.Equal("He said \"hi\"", t.Rows[0][1]);
        Assert.Equal("Two\nlines", t.Rows[1][0]);
        Assert.Equal("", t.Rows[1][1]);
    }

    [Fact]
    public void Semicolon_files_are_detected()
    {
        var t = CsvParser.Parse("Name;PAN\nA;B\n");
        Assert.Equal(new[] { "Name", "PAN" }, t.Headers);
        Assert.Equal(new[] { "A", "B" }, t.Rows[0]);
    }

    [Fact]
    public void Last_line_without_newline_is_kept() =>
        Assert.Single(CsvParser.Parse("Name\nOnly").Rows);

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]   // no formula injection when opened in Excel
    public void Quote(string value, string expected) => Assert.Equal(expected, CsvParser.Quote(value));
}
