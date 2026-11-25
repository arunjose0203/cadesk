using CaDesk.Application.Tds;
using CaDesk.Domain.Tds;
using Xunit;

namespace CaDesk.UnitTests;

public class NsdlRpuBuilderUnitTests
{
    private static TdsDeductorProfileDto ValidProfile(Guid? clientId = null) => new(
        ClientId: clientId ?? Guid.NewGuid(),
        Tan: "MUMN77777A",
        Pan: "AAAAA1111A",
        DeductorType: "K",
        DeductorName: "Malabar Traders Pvt Ltd",
        BranchDivision: "Kochi",
        Gstin: "32AAAAA1111A1Z5",
        FlatBlock: "14 MG Road",
        PremisesName: "Menon Chambers",
        StreetRoad: "Ernakulam",
        AreaLocality: null,
        CityDistrict: "Kochi",
        StateCode: "16",
        Pincode: "682016",
        CountryName: "INDIA",
        Email: "accounts@malabar.test",
        StdCode: "0484",
        Phone: "2351122",
        Mobile: "9847012345",
        CountryCallingCode: "91",
        RespName: "Arun Menon",
        RespDesignation: "Director",
        RespPan: "AAAPA1234A",
        RespFlatBlock: "14 MG Road",
        RespPremisesName: null,
        RespStreetRoad: "Ernakulam",
        RespAreaLocality: null,
        RespCityDistrict: "Kochi",
        RespStateCode: "16",
        RespPincode: "682016",
        RespCountryName: "INDIA",
        RespEmail: "arun@malabar.test",
        RespMobile: "9847012345",
        RespCountryCallingCode: "91",
        FiledPreviousRegular: false,
        PreviousTokenNo: null
    );

    [Fact]
    public void Build_26Q_Act2025_Form140_EmitsExactProteanFieldCountsAndSectionCodes()
    {
        var cid = Guid.NewGuid();
        var ch = new TdsChallan
        {
            Id = Guid.NewGuid(),
            ClientId = cid,
            Quarter = "Q1",
            FinancialYear = "2026-27",
            BsrCode = "0210045",
            ChallanSerialNumber = "00123",
            ChallanDate = new DateOnly(2026, 5, 7),
            SectionCode = "94J",
            MinorHead = TdsMinorHead.Regular200,
            TdsAmount = 10_000m,
            TotalChallanAmount = 10_000m,
        };
        var ded = new TdsDeduction
        {
            Id = Guid.NewGuid(),
            ClientId = cid,
            ChallanId = ch.Id,
            FormType = TdsFormType.Form26Q,
            Quarter = "Q1",
            FinancialYear = "2026-27",
            DeducteePan = "ABCDE1234F",
            DeducteeName = "Suresh Nair & Co",
            Section = TdsSection.Sec194JFees,
            PaymentDate = new DateOnly(2026, 4, 20),
            DeductionDate = new DateOnly(2026, 4, 20),
            AmountPaid = 100_000m,
            TdsRate = 10.0m,
            TdsAmount = 10_000m,
            TotalTaxDeducted = 10_000m,
            BookingStatus = TdsBookingStatus.MappedToChallan,
        };

        var res = NsdlRpuBuilder.Build(
            ValidProfile(cid), "26Q", "2026-27", "Q1", [ch], [ded], new DateOnly(2026, 7, 15));

        Assert.True(res.ValidForFvu, string.Join("; ", res.Issues.Where(i => i.Severity == "error").Select(i => i.Message)));
        Assert.Equal("140Q1.txt", res.FileName);
        Assert.Equal("140", res.OfficialFormNumber);
        Assert.Contains("\r\n", res.Content);

        var lines = res.Content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length);

        var fh = lines[0].Split('^');
        var bh = lines[1].Split('^');
        var cd = lines[2].Split('^');
        var dd = lines[3].Split('^');

        Assert.Equal(18, fh.Length);
        Assert.Equal(72, bh.Length);
        Assert.Equal(30, cd.Length);
        Assert.Equal(45, dd.Length);

        Assert.Equal("NS1", fh[2]);
        Assert.Equal("140", bh[4]);
        Assert.Equal("202728", bh[15]);
        Assert.Equal("202627", bh[16]);
        Assert.Equal("INDIA", bh[19]);
        Assert.Equal("10000.00", bh[46]);
        Assert.Equal("C", cd[12]);
        Assert.Equal("0210045", cd[14]);
        Assert.Equal("1027", dd[14]);
        Assert.Equal("10.0000", dd[27]);
    }

    [Fact]
    public void Build_24Q_And_27Q_Act1961_EmitsLegacy54FieldDdAndSectionCodes()
    {
        var cid = Guid.NewGuid();
        var ch = new TdsChallan
        {
            Id = Guid.NewGuid(),
            ClientId = cid,
            Quarter = "Q1",
            FinancialYear = "2025-26",
            BsrCode = "0510002",
            ChallanSerialNumber = "00456",
            ChallanDate = new DateOnly(2025, 5, 7),
            SectionCode = "195",
            MinorHead = TdsMinorHead.Regular200,
            TdsAmount = 20_000m,
            TotalChallanAmount = 20_000m,
        };
        var d27 = new TdsDeduction
        {
            Id = Guid.NewGuid(),
            ClientId = cid,
            ChallanId = ch.Id,
            FormType = TdsFormType.Form27Q,
            Quarter = "Q1",
            FinancialYear = "2025-26",
            DeducteePan = "AAACX9999K",
            DeducteeName = "Global Tech Inc",
            Section = TdsSection.Sec195,
            PaymentDate = new DateOnly(2025, 4, 25),
            DeductionDate = new DateOnly(2025, 4, 25),
            AmountPaid = 100_000m,
            TdsRate = 20.0m,
            TdsAmount = 20_000m,
            TotalTaxDeducted = 20_000m,
            RemittanceCode = "21",
            CountryCode = "267",
            TaxRateSource = "A",
            OptedOutNewRegime = false,
            BookingStatus = TdsBookingStatus.MappedToChallan,
        };

        var r27 = NsdlRpuBuilder.Build(
            ValidProfile(cid), "27Q", "2025-26", "Q1", [ch], [d27], new DateOnly(2025, 7, 15));

        Assert.True(r27.ValidForFvu);
        Assert.Equal("27QQ1.txt", r27.FileName);
        var lines = r27.Content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(41, lines[2].Split('^').Length);
        var dd = lines[3].Split('^');
        Assert.Equal(54, dd.Length);
        Assert.Equal("1", dd[3]); // Challan ref first in Act 1961
        Assert.Equal("1", dd[4]);
        Assert.Equal("195", dd[32]);
        Assert.Equal("21", dd[35]);
        Assert.Equal("267", dd[37]);
        Assert.Equal("N", dd[48]);
    }

    [Fact]
    public void Build_FlagsFvuErrorsForUnmappedDeductionShortChallanOutOfQuarterDateAndPaise()
    {
        var cid = Guid.NewGuid();
        var ch = new TdsChallan
        {
            Id = Guid.NewGuid(),
            ClientId = cid,
            Quarter = "Q1",
            FinancialYear = "2026-27",
            BsrCode = "0210045",
            ChallanSerialNumber = "00123",
            ChallanDate = new DateOnly(2026, 5, 7),
            TdsAmount = 500.50m, // fractional rupee violates FVU CD rule
            TotalChallanAmount = 500.50m,
        };
        var mappedShort = new TdsDeduction
        {
            Id = Guid.NewGuid(),
            ClientId = cid,
            ChallanId = ch.Id,
            FormType = TdsFormType.Form26Q,
            Quarter = "Q1",
            FinancialYear = "2026-27",
            DeducteePan = "INVALID",
            DeducteeName = "Bad Vendor",
            Section = TdsSection.Sec194H,
            PaymentDate = new DateOnly(2026, 8, 10), // Outside Q1
            DeductionDate = new DateOnly(2026, 8, 10),
            AmountPaid = 10_000m,
            TdsRate = 5m,
            TdsAmount = 500m,
            TotalTaxDeducted = 1_000m, // Exceeds 500.50
        };
        var unmapped = new TdsDeduction
        {
            Id = Guid.NewGuid(),
            ClientId = cid,
            ChallanId = null,
            FormType = TdsFormType.Form26Q,
            Quarter = "Q1",
            FinancialYear = "2026-27",
            DeducteePan = "ABCDE1234F",
            DeducteeName = "Unlinked Party",
            Section = TdsSection.Sec194H,
            PaymentDate = new DateOnly(2026, 4, 10),
            DeductionDate = new DateOnly(2026, 4, 10),
            AmountPaid = 5_000m,
            TdsRate = 5m,
            TdsAmount = 250m,
            TotalTaxDeducted = 250m,
        };

        var res = NsdlRpuBuilder.Build(
            ValidProfile(cid) with { Tan = "BADTAN" }, "26Q", "2026-27", "Q1", [ch], [mappedShort, unmapped], new DateOnly(2026, 9, 1));

        Assert.False(res.ValidForFvu);
        var codes = res.Issues.Where(i => i.Severity == "error").Select(i => i.Code).ToHashSet();
        Assert.Contains("BH-TAN", codes);
        Assert.Contains("CD-PAISE", codes);
        Assert.Contains("CD-SHORT", codes);
        Assert.Contains("DD-CHALLAN", codes);
        Assert.Contains("DD-PAN", codes);
        Assert.Contains("DD-QTR-PAY", codes);
    }

    [Fact]
    public void Form16Importer_ParsesNsdlTxtCsvAndTracesText()
    {
        // Roundtrip parse NSDL Act 2025 output
        var cid = Guid.NewGuid();
        var ch = new TdsChallan
        {
            Id = Guid.NewGuid(), ClientId = cid, Quarter = "Q1", FinancialYear = "2026-27",
            BsrCode = "0210045", ChallanSerialNumber = "00999", ChallanDate = new DateOnly(2026, 5, 7),
            TdsAmount = 2000m, TotalChallanAmount = 2000m,
        };
        var ded = new TdsDeduction
        {
            Id = Guid.NewGuid(), ClientId = cid, ChallanId = ch.Id, FormType = "24Q", Quarter = "Q1", FinancialYear = "2026-27",
            DeducteePan = "ABCDE1234F", DeducteeName = "Kiran Kurian", Section = TdsSection.Sec192Salary,
            PaymentDate = new DateOnly(2026, 4, 30), DeductionDate = new DateOnly(2026, 4, 30),
            AmountPaid = 50_000m, TdsRate = 4m, TdsAmount = 2000m, TotalTaxDeducted = 2000m,
        };
        var exported = NsdlRpuBuilder.Build(ValidProfile(cid), "24Q", "2026-27", "Q1", [ch], [ded], new DateOnly(2026, 7, 15));
        var parsedTxt = Form16Importer.Parse(exported.Content);

        Assert.Equal("nsdl_rpu_txt", parsedTxt.DetectedFormat);
        Assert.Equal("24Q", parsedTxt.FormType);
        Assert.Equal("2026-27", parsedTxt.FinancialYear);
        Assert.Single(parsedTxt.Challans);
        Assert.Single(parsedTxt.Deductions);
        Assert.Equal("Kiran Kurian", parsedTxt.Deductions[0].DeducteeName);

        // Parse TRACES Form 16A text
        var tracesText = """
            FORM NO. 16A
            Certificate No.: TRACES987654
            TAN of Deductor: MUMN77777A
            PAN of the Deductee: ABCDE1234F
            Assessment Year: 2027-28
            Quarter: Q2
            Section 194J
            Deductee Name: Cochin Legal Advisors LLP
            Amount paid/credited: 150,000.00
            Total Tax Deposited: 15,000.00
            BSR Code: 0510002
            Challan Serial Number: 00321
            Date: 07-10-2026
            """;
        var parsedTraces = Form16Importer.Parse(tracesText);
        Assert.Equal("traces_certificate_text", parsedTraces.DetectedFormat);
        Assert.Equal("MUMN77777A", parsedTraces.DeductorTan);
        Assert.Equal("2026-27", parsedTraces.FinancialYear);
        Assert.Equal("Q2", parsedTraces.Quarter);
        Assert.Single(parsedTraces.Deductions);
        Assert.Equal(150_000m, parsedTraces.Deductions[0].AmountPaid);
        Assert.Equal(15_000m, parsedTraces.Deductions[0].TdsAmount);

        // Parse CSV template rows (ensuring Deductee PAN does not hijack Deductee Name and canonical sections round-trip)
        var csv = """
            Form,Financial Year,Quarter,Deductee PAN,Deductee Name,Section,Payment Date,Deduction Date,Amount Paid,Rate,TDS Amount,Certificate No,BSR Code,Challan Serial No,Challan Date
            26Q,2026-27,Q1,AAACP1234D,Kochi Contractors Pvt Ltd,194C_company,2026-05-15,2026-05-15,250000,2,5000,CERT001,0510025,00412,2026-06-05
            """;
        var parsedCsv = Form16Importer.Parse(csv);
        Assert.Equal("form16a_csv", parsedCsv.DetectedFormat);
        Assert.Single(parsedCsv.Deductions);
        Assert.Equal("AAACP1234D", parsedCsv.Deductions[0].DeducteePan);
        Assert.Equal("Kochi Contractors Pvt Ltd", parsedCsv.Deductions[0].DeducteeName);
        Assert.Equal(TdsSection.Sec194CCompany, parsedCsv.Deductions[0].Section);
        Assert.Single(parsedCsv.Challans);

        // Parse FVU .err
        var errs = NsdlRpuBuilder.ParseFvuErrFile("3^CD^1^1^^^T-FV-3141^Invalid BSR Code\r\n4^DD^1^1^1^^T-FV-4012^Invalid PAN");
        Assert.Equal(2, errs.Count);
        Assert.Equal("T-FV-3141", errs[0].ErrorCode);
        Assert.Equal(4, errs[1].LineNumber);
    }
}
