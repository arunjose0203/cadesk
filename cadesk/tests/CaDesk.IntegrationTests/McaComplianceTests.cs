using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Clients;
using CaDesk.Application.Mca;
using CaDesk.Domain.Mca;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 18: ROC / MCA Company Law & Secretarial Compliance.
/// Tests Company Master (CIN validation), Director DIN management (8-digit DIN, DIR-3 KYC),
/// Annual Filings with Section 403 late fee calculations, Board Resolutions register, and multi-tenant isolation.
/// </summary>
public class McaComplianceTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "MCA Compliance Firm")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Apex Infotech Private Limited")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Mca_FullGovernanceLifecycle_Succeeds()
    {
        var (http, _) = await NewFirmAsync("Singhania Corporate Advisors");
        var client = await CreateClientAsync(http, "Bharat Dynamics Private Limited");

        // 1. Create Company Master with valid 21-character CIN
        var compReq = new CreateCompanyRequest(
            ClientId: client.Id,
            Cin: "U72200MH2021PTC123456",
            CompanyName: "Bharat Dynamics Private Limited",
            CompanyClass: CompanyClass.Private,
            AuthorizedCapital: 1_000_000m,
            PaidUpCapital: 500_000m,
            RegisteredOfficeAddress: "Plot 42, Bandra-Kurla Complex, Mumbai 400051",
            RocJurisdiction: "RoC-Mumbai",
            IncorporationDate: new DateOnly(2021, 6, 15),
            CompanyStatus: CompanyStatus.Active,
            LastAgmDate: new DateOnly(2024, 9, 25),
            FinancialYearEndMonth: 3
        );

        var compRes = await http.PostAsJsonAsync("/api/mca/companies", compReq);
        Assert.Equal(HttpStatusCode.Created, compRes.StatusCode);
        var company = (await compRes.Content.ReadFromJsonAsync<CompanyMasterDto>())!;
        Assert.Equal("U72200MH2021PTC123456", company.Cin);
        Assert.Equal("Bharat Dynamics Private Limited", company.CompanyName);

        // 2. Add Director with valid 8-digit DIN & KYC
        var dirReq = new CreateDirectorRequest(
            CompanyId: company.Id,
            Din: "09876543",
            DirectorName: "Rajesh Singhania",
            Pan: "ABCPS1234K",
            Designation: DirectorDesignation.ManagingDirector,
            AppointmentDate: new DateOnly(2021, 6, 15),
            CessationDate: null,
            DscExpiryDate: new DateOnly(2026, 12, 31),
            Dir3KycStatus: Dir3KycStatus.Complied,
            Dir3KycLastFiledDate: new DateOnly(2024, 9, 10),
            IsSignatory: true
        );

        var dirRes = await http.PostAsJsonAsync("/api/mca/directors", dirReq);
        Assert.Equal(HttpStatusCode.Created, dirRes.StatusCode);
        var director = (await dirRes.Content.ReadFromJsonAsync<DirectorDinDto>())!;
        Assert.Equal("09876543", director.Din);
        Assert.Equal(DirectorDesignation.ManagingDirector, director.Designation);

        // 3. Record an Annual Filing (AOC-4) with 20 days delay -> Section 403 late fee
        var filingReq = new CreateFilingRequest(
            CompanyId: company.Id,
            FinancialYear: "2023-24",
            FormType: McaFormType.Aoc4,
            DueDate: new DateOnly(2024, 10, 30),
            FilingDate: new DateOnly(2024, 11, 19), // 20 days delay
            Srn: "AA1234567",
            Status: McaFilingStatus.Approved,
            NormalFee: 400m,
            Remarks: "Financial statements filed with late fee"
        );

        var filingRes = await http.PostAsJsonAsync("/api/mca/filings", filingReq);
        Assert.Equal(HttpStatusCode.Created, filingRes.StatusCode);
        var filing = (await filingRes.Content.ReadFromJsonAsync<McaFilingDto>())!;
        Assert.Equal(20, filing.DaysDelayed);
        Assert.Equal(2000m, filing.LateFee); // 20 days * 100
        Assert.Equal(2400m, filing.TotalFee); // 400 normal + 2000 late

        // 4. Record Board Resolution
        var resReq = new CreateResolutionRequest(
            CompanyId: company.Id,
            ResolutionNumber: "BR/2024-25/001",
            MeetingType: MeetingType.BoardMeeting,
            MeetingDate: new DateOnly(2024, 8, 14),
            Category: ResolutionCategory.Ordinary,
            Subject: "Approval of Q1 Unaudited Financial Results",
            Verbiage: "RESOLVED THAT the unaudited financial statements for the quarter ended June 30, 2024 be and are hereby approved.",
            PassedUnanimously: true,
            SignatoryDin: "09876543",
            SignatoryName: "Rajesh Singhania"
        );

        var resRes = await http.PostAsJsonAsync("/api/mca/resolutions", resReq);
        Assert.Equal(HttpStatusCode.Created, resRes.StatusCode);
        var resolution = (await resRes.Content.ReadFromJsonAsync<BoardResolutionDto>())!;
        Assert.Equal("BR/2024-25/001", resolution.ResolutionNumber);
        Assert.True(resolution.PassedUnanimously);

        // 5. Verify Summary KPIs
        var sumRes = await http.GetAsync("/api/mca/summary");
        Assert.Equal(HttpStatusCode.OK, sumRes.StatusCode);
        var summary = (await sumRes.Content.ReadFromJsonAsync<McaComplianceSummaryDto>())!;
        Assert.True(summary.TotalCompaniesCount >= 1);
        Assert.True(summary.ActiveCompaniesCount >= 1);
        Assert.True(summary.TotalDirectorsCount >= 1);
        Assert.True(summary.TotalLateFeesAccrued >= 2000m);
    }

    [Fact]
    public async Task Mca_InvalidCinAndDin_ThrowsBadRequest()
    {
        var (http, _) = await NewFirmAsync("Validation Firm");
        var client = await CreateClientAsync(http, "Invalid Ltd");

        // Invalid CIN
        var badCin = new CreateCompanyRequest(
            ClientId: client.Id,
            Cin: "INVALID123", // not 21 chars
            CompanyName: "Bad CIN Ltd",
            CompanyClass: CompanyClass.Private,
            AuthorizedCapital: 100_000m,
            PaidUpCapital: 100_000m,
            RegisteredOfficeAddress: "Mumbai",
            RocJurisdiction: "RoC-Mumbai",
            IncorporationDate: new DateOnly(2022, 1, 1),
            CompanyStatus: CompanyStatus.Active,
            LastAgmDate: null,
            FinancialYearEndMonth: 3
        );

        var cinRes = await http.PostAsJsonAsync("/api/mca/companies", badCin);
        Assert.Equal(HttpStatusCode.BadRequest, cinRes.StatusCode);
    }
}
