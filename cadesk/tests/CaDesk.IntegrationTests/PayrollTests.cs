using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Clients;
using CaDesk.Application.Payroll;
using CaDesk.Domain.Payroll;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 23: Payroll, Salary TDS & Statutory PF / ESIC Engine.
/// Tests employee salary structures, monthly payroll run execution, statutory deduction splits
/// (EPF 12% + 8.33% EPS / 3.67% EPF, ESIC, PT, Sec 192 TDS), ECR export, and two-lock tenant isolation.
/// </summary>
public class PayrollTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "Corporate Payroll Advisory")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Infosys BPM Services")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Payroll_Run_Processes_Accurate_Statutory_Deductions_And_Ecr()
    {
        var (http, _) = await NewFirmAsync("Deloitte Payroll Services");
        var client = await CreateClientAsync(http, "Wipro Enterprise Solutions");

        // 1. Add Employee 1: PF & ESI Eligible (Gross <= 21,000)
        var emp1Req = new SavePayrollEmployeeRequest(
            ClientId: client.Id,
            EmployeeCode: "EMP001",
            Name: "Rohan Sharma",
            Pan: "ABCDE1234F",
            Uan: "100987654321",
            EsicNumber: "20198765432100001",
            DateOfJoining: new DateOnly(2023, 1, 10),
            Designation: "Associate Analyst",
            Department: "Operations",
            State: "Maharashtra",
            TaxRegime: PayrollTaxRegime.NewRegime,
            MonthlyCtc: 22000m,
            BasicSalary: 10000m,
            Hra: 5000m,
            SpecialAllowance: 3000m,
            OtherAllowances: 1000m,
            IsPfEligible: true,
            IsEsiEligible: true,
            IsPtEligible: true,
            IsActive: true
        );

        var emp1Res = await http.PostAsJsonAsync("/api/payroll/employees", emp1Req);
        emp1Res.EnsureSuccessStatusCode();
        var emp1 = await emp1Res.Content.ReadFromJsonAsync<PayrollEmployeeDto>();
        Assert.NotNull(emp1);
        Assert.Equal(19000m, emp1.GrossSalary);
        Assert.Equal(1200m, emp1.EstimatedPf); // 12% of 10,000
        Assert.Equal(143m, emp1.EstimatedEsi); // 0.75% of 19,000 = 142.5 rounded up to 143
        Assert.Equal(200m, emp1.EstimatedPt);

        // 2. Add Employee 2: High earner (PF capped at 15,000, ESI exempt as Gross > 21,000)
        var emp2Req = new SavePayrollEmployeeRequest(
            ClientId: client.Id,
            EmployeeCode: "EMP002",
            Name: "Priya Nair",
            Pan: "PQRSK5678L",
            Uan: "100987654322",
            EsicNumber: null,
            DateOfJoining: new DateOnly(2022, 6, 15),
            Designation: "Senior Manager",
            Department: "Finance",
            State: "Maharashtra",
            TaxRegime: PayrollTaxRegime.NewRegime,
            MonthlyCtc: 120000m,
            BasicSalary: 50000m,
            Hra: 25000m,
            SpecialAllowance: 20000m,
            OtherAllowances: 5000m,
            IsPfEligible: true,
            IsEsiEligible: true,
            IsPtEligible: true,
            IsActive: true
        );

        var emp2Res = await http.PostAsJsonAsync("/api/payroll/employees", emp2Req);
        emp2Res.EnsureSuccessStatusCode();
        var emp2 = await emp2Res.Content.ReadFromJsonAsync<PayrollEmployeeDto>();
        Assert.NotNull(emp2);
        Assert.Equal(100000m, emp2.GrossSalary);
        Assert.Equal(1800m, emp2.EstimatedPf); // Capped at 15,000 ceiling -> 1,800
        Assert.Equal(0m, emp2.EstimatedEsi);   // Gross 100,000 > 21,000 -> 0

        // 3. Process Payroll Run for October 2024
        var processReq = new ProcessPayrollRunRequest(
            ClientId: client.Id,
            Year: 2024,
            Month: 10,
            Notes: "October 2024 monthly salary run",
            Overrides: null
        );

        var runRes = await http.PostAsJsonAsync("/api/payroll/runs", processReq);
        runRes.EnsureSuccessStatusCode();
        var run = await runRes.Content.ReadFromJsonAsync<PayrollRunDto>();
        Assert.NotNull(run);
        Assert.Equal("2024-10", run.PayrollPeriod);
        Assert.Equal(2, run.TotalEmployees);
        Assert.Equal(119000m, run.TotalGrossPay);
        // Total EE PF = 1200 + 1800 = 3000
        Assert.Equal(3000m, run.TotalEmployeePf);

        // 4. Fetch Payslips
        var payslips = await http.GetFromJsonAsync<List<PayrollPayslipDto>>($"/api/payroll/runs/{run.Id}/payslips");
        Assert.NotNull(payslips);
        Assert.Equal(2, payslips.Count);

        // 5. Export EPFO ECR file
        var ecr = await http.GetFromJsonAsync<EpfoEcrExportDto>($"/api/payroll/runs/{run.Id}/ecr");
        Assert.NotNull(ecr);
        Assert.Equal(2, ecr.EmployeeCount);
        Assert.Contains("#~#100987654321#~#Rohan Sharma#~#19000#~#10000#~#10000#~#10000#~#1200#~#833#~#367#~#0#~#0#~#", ecr.EcrText);

        // 6. Summary check
        var summary = await http.GetFromJsonAsync<PayrollSummaryDto>($"/api/payroll/summary?clientId={client.Id}");
        Assert.NotNull(summary);
        Assert.Equal(2, summary.TotalEmployees);
        Assert.Equal(119000m, summary.TotalMonthlyGross);
    }

    [Fact]
    public async Task Two_Lock_Multi_Tenancy_Isolates_Payroll_Records()
    {
        var (firmA, _) = await NewFirmAsync("Firm A HR Services");
        var (firmB, _) = await NewFirmAsync("Firm B HR Services");

        var clientA = await CreateClientAsync(firmA, "Firm A Client");

        var empReq = new SavePayrollEmployeeRequest(
            ClientId: clientA.Id,
            EmployeeCode: "A001",
            Name: "Ajay Devgan",
            Pan: "AABBA1111A",
            Uan: "100111111111",
            EsicNumber: null,
            DateOfJoining: new DateOnly(2023, 1, 1),
            Designation: "Developer",
            Department: "Tech",
            State: "Maharashtra",
            TaxRegime: PayrollTaxRegime.NewRegime,
            MonthlyCtc: 50000m,
            BasicSalary: 25000m,
            Hra: 10000m,
            SpecialAllowance: 10000m,
            OtherAllowances: 5000m,
            IsPfEligible: true,
            IsEsiEligible: false,
            IsPtEligible: true,
            IsActive: true
        );

        var createRes = await firmA.PostAsJsonAsync("/api/payroll/employees", empReq);
        createRes.EnsureSuccessStatusCode();
        var empA = (await createRes.Content.ReadFromJsonAsync<PayrollEmployeeDto>())!;

        // Firm B tries to get Firm A's employee -> Must be 404
        var resB = await firmB.GetAsync($"/api/payroll/employees/{empA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resB.StatusCode);
    }
}
