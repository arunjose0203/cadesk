using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Clients;
using CaDesk.Application.Tds;
using CaDesk.Domain.Tds;
using Xunit;

namespace CaDesk.IntegrationTests;

public class TdsReturnExportTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Tds_ProfileExportSaveToVaultAndForm16Import_EndToEndWorks()
    {
        var auth = await api.SignUpAsync("Cochin TDS Partners");
        var http = api.WithToken(auth.AccessToken);

        var cRes = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, "Backwater Logistics Pvt Ltd", null, "IN", "Company",
            new Dictionary<string, string> { ["PAN"] = "AAACB1234F", ["TAN"] = "COCH12345A" },
            "accounts@backwater.test", "9847011223", null, null));
        cRes.EnsureSuccessStatusCode();
        var client = (await cRes.Content.ReadFromJsonAsync<ClientDto>())!;

        // 1. Save Deductor Profile
        var profileReq = new SaveTdsDeductorProfileRequest(
            Tan: "COCH12345A",
            Pan: "AAACB1234F",
            DeductorType: "K",
            DeductorName: "Backwater Logistics Pvt Ltd",
            BranchDivision: "Kochi",
            Gstin: null,
            FlatBlock: "22 Marine Drive",
            PremisesName: "Harbour Towers",
            StreetRoad: "Shanmugham Road",
            AreaLocality: "Ernakulam",
            CityDistrict: "Kochi",
            StateCode: "16",
            Pincode: "682031",
            CountryName: "INDIA",
            Email: "accounts@backwater.test",
            StdCode: "0484",
            Phone: "2359988",
            Mobile: "9847011223",
            CountryCallingCode: "91",
            RespName: "Meera Nair",
            RespDesignation: "CFO",
            RespPan: "AAAPN1234B",
            RespFlatBlock: "22 Marine Drive",
            RespPremisesName: null,
            RespStreetRoad: "Ernakulam",
            RespAreaLocality: null,
            RespCityDistrict: "Kochi",
            RespStateCode: "16",
            RespPincode: "682031",
            RespCountryName: "INDIA",
            RespEmail: "meera@backwater.test",
            RespMobile: "9847011223",
            RespCountryCallingCode: "91");
        var putRes = await http.PutAsJsonAsync($"/api/tds/profiles/{client.Id}", profileReq);
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);

        // 2. Record 24Q Salary + 26Q Professional Deductions & Challan for FY 2026-27 Q1
        var dedRes = await http.PostAsJsonAsync("/api/tds/deductions", new RecordTdsDeductionRequest(
            client.Id, TdsFormType.Form24Q, "Q1", "2026-27", "ABCDE1234F", "Ravi Varma",
            TdsSection.Sec192Salary, new DateOnly(2026, 4, 30), 100_000m, new DateOnly(2026, 4, 30),
            null, false, CustomRate: 10m));
        Assert.Equal(HttpStatusCode.Created, dedRes.StatusCode);
        var ded = (await dedRes.Content.ReadFromJsonAsync<TdsDeductionDto>())!;
        Assert.Equal("24Q", ded.FormType);

        var chalRes = await http.PostAsJsonAsync("/api/tds/challans", new CreateTdsChallanRequest(
            client.Id, "Q1", "2026-27", "0510001", new DateOnly(2026, 5, 7), "00011",
            "192", TdsMinorHead.Regular200, 10_000m, 0m, 0m, 0m, 0m, "April Salary TDS"));
        Assert.Equal(HttpStatusCode.Created, chalRes.StatusCode);
        var challan = (await chalRes.Content.ReadFromJsonAsync<TdsChallanDto>())!;

        var mapRes = await http.PostAsJsonAsync("/api/tds/challans/map",
            new MapDeductionsToChallanRequest(challan.Id, [ded.Id]));
        Assert.Equal(HttpStatusCode.OK, mapRes.StatusCode);

        // 3. Export Form 138 (24Q for Tax Year 2026-27) NSDL RPU .txt
        var expRes = await http.PostAsJsonAsync("/api/tds/returns/export",
            new ExportTdsReturnRequest(client.Id, "24Q", "2026-27", "Q1", new DateOnly(2026, 7, 15)));
        Assert.Equal(HttpStatusCode.OK, expRes.StatusCode);
        var exported = (await expRes.Content.ReadFromJsonAsync<TdsReturnExportResult>())!;
        Assert.True(exported.ValidForFvu);
        Assert.Equal("138Q1.txt", exported.FileName);
        Assert.Equal("138", exported.OfficialFormNumber);
        Assert.Contains("^FH^SL1^R^", exported.Content);
        Assert.Contains("^1002^", exported.Content);

        // 4. Save generated .txt to Client Documents Vault (TDS_RETURN)
        var saveDocRes = await http.PostAsJsonAsync($"/api/clients/{client.Id}/documents/generated", new
        {
            fileName = exported.FileName,
            content = exported.Content,
            typeCode = "TDS_RETURN",
            fy = "2026-27",
            name = "Form 138 (24Q) Q1 RPU"
        });
        Assert.Equal(HttpStatusCode.Created, saveDocRes.StatusCode);

        // 5. Import Form 16A CSV (preview then commit)
        var csv = """
            Form,FY,Quarter,Deductee PAN,Deductee Name,Section,Payment Date,Deduction Date,Amount Paid,Rate,TDS Deducted,BSR Code,Challan Serial,Challan Date
            26Q,2026-27,Q1,AAACF5678G,Kerala Legal LLP,194J_fees,2026-04-15,2026-04-15,50000,10,5000,0210045,00022,2026-05-07
            """;
        var prevRes = await http.PostAsJsonAsync("/api/tds/import/form16",
            new ImportForm16Request(client.Id, csv, Commit: false));
        Assert.Equal(HttpStatusCode.OK, prevRes.StatusCode);
        var preview = (await prevRes.Content.ReadFromJsonAsync<ImportForm16Result>())!;
        Assert.False(preview.Committed);
        Assert.Single(preview.Deductions);

        var commitRes = await http.PostAsJsonAsync("/api/tds/import/form16",
            new ImportForm16Request(client.Id, csv, Commit: true));
        Assert.Equal(HttpStatusCode.OK, commitRes.StatusCode);
        var committed = (await commitRes.Content.ReadFromJsonAsync<ImportForm16Result>())!;
        Assert.True(committed.Committed);
        Assert.Equal(1, committed.ImportedChallansCount);
        Assert.Equal(1, committed.ImportedDeductionsCount);

        // Now 26Q Export also succeeds!
        var exp26 = await http.PostAsJsonAsync("/api/tds/returns/export",
            new ExportTdsReturnRequest(client.Id, "26Q", "2026-27", "Q1", new DateOnly(2026, 7, 15)));
        var res26 = (await exp26.Content.ReadFromJsonAsync<TdsReturnExportResult>())!;
        Assert.True(res26.ValidForFvu);
        Assert.Equal("140Q1.txt", res26.FileName);
    }
}
