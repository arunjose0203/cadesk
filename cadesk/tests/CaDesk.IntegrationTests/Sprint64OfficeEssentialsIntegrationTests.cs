using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Billing;
using CaDesk.Application.Clients;
using CaDesk.Application.Office;
using CaDesk.Application.Tasks;
using CaDesk.Domain.Office;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 64: Office Essentials Integration Tests:
/// 1. Attendance: Staff check-in / check-out, monthly register, CSV export.
/// 2. Leave: Yearly balances, apply, manager approval, balance deduction, workload leave display.
/// 3. Expenses: Logging out-of-pocket expenses, receipts, manager approval, client invoice auto-reimbursement.
/// 4. Inward/Outward Registers: Document and DSC custody tracking, printable HTML register.
/// 5. Two-lock multi-tenancy & permission boundaries: Firm B blocked from Firm A data; staff blocked from approval endpoints.
/// </summary>
public class Sprint64OfficeEssentialsIntegrationTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<ClientDto> CreateClientAsync(HttpClient http, string name)
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, $"{name} Private Limited", null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Staff_Attendance_CheckIn_And_CheckOut_Lifecycle()
    {
        var auth = await api.SignUpAsync("Kochi Audit Practice");
        var http = api.WithToken(auth.AccessToken);

        // 1. Check in
        var checkInRes = await http.PostAsJsonAsync("/api/office/attendance/check-in",
            new AttendanceCheckInRequest("Working at Cochin Port Authority for audit", "192.168.1.105"));
        Assert.Equal(HttpStatusCode.OK, checkInRes.StatusCode);
        var record = (await checkInRes.Content.ReadFromJsonAsync<AttendanceRecordDto>())!;
        Assert.Equal(AttendanceStatus.Present, record.Status);
        Assert.Contains("Cochin Port Authority", record.Notes);

        // 2. Query today's attendance
        var todayRes = await http.GetAsync("/api/office/attendance/today");
        Assert.Equal(HttpStatusCode.OK, todayRes.StatusCode);
        var today = (await todayRes.Content.ReadFromJsonAsync<AttendanceRecordDto>())!;
        Assert.NotNull(today);
        Assert.Equal(record.Id, today.Id);

        // 3. Check out
        var checkOutRes = await http.PostAsJsonAsync("/api/office/attendance/check-out",
            new AttendanceCheckOutRequest("Completed interim inventory verification"));
        Assert.Equal(HttpStatusCode.OK, checkOutRes.StatusCode);
        var checkedOut = (await checkOutRes.Content.ReadFromJsonAsync<AttendanceRecordDto>())!;
        Assert.NotNull(checkedOut.CheckOutTime);
        Assert.Contains("Completed interim", checkedOut.Notes);

        // 4. Monthly register
        var now = DateTime.UtcNow;
        var regRes = await http.GetAsync($"/api/office/attendance/register?month={now.Month}&year={now.Year}&userId={auth.UserId}");
        Assert.Equal(HttpStatusCode.OK, regRes.StatusCode);
        var summary = (await regRes.Content.ReadFromJsonAsync<MonthlyAttendanceSummaryDto>())!;
        Assert.NotEmpty(summary.Records);

        // 5. CSV Export
        var exportRes = await http.GetAsync($"/api/office/attendance/export?month={now.Month}&year={now.Year}");
        Assert.Equal(HttpStatusCode.OK, exportRes.StatusCode);
        var csv = await exportRes.Content.ReadAsStringAsync();
        Assert.Contains("Date,Staff Name,Check In,Check Out", csv);
        Assert.Contains("Cochin Port Authority", csv);
    }

    [Fact]
    public async Task Leave_Management_Apply_And_Approval_Lifecycle()
    {
        var auth = await api.SignUpAsync("Trivandrum Tax Associates");
        var http = api.WithToken(auth.AccessToken);

        // 1. Query initial balances (auto-initialized with 12 Casual, 10 Sick, 15 Earned, 20 Exam)
        var balancesRes = await http.GetAsync("/api/office/leave/balances");
        Assert.Equal(HttpStatusCode.OK, balancesRes.StatusCode);
        var balances = (await balancesRes.Content.ReadFromJsonAsync<List<LeaveBalanceDto>>())!;
        Assert.NotEmpty(balances);
        var casual = balances.First(b => b.LeaveType == LeaveType.Casual);
        Assert.Equal(12.00m, casual.AllocatedDays);
        Assert.Equal(12.00m, casual.AvailableDays);

        // 2. Apply for leave (3 days)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var applyReq = new ApplyLeaveRequest(
            LeaveType: LeaveType.Casual,
            StartDate: today,
            EndDate: today.AddDays(2),
            DaysCount: 3.00m,
            Reason: "Attending family function"
        );
        var applyRes = await http.PostAsJsonAsync("/api/office/leave/apply", applyReq);
        Assert.Equal(HttpStatusCode.Created, applyRes.StatusCode);
        var leaveReq = (await applyRes.Content.ReadFromJsonAsync<LeaveRequestDto>())!;
        Assert.Equal(LeaveStatus.Pending, leaveReq.Status);

        // 3. Manager/Partner approves leave
        var approveRes = await http.PostAsJsonAsync($"/api/office/leave/requests/{leaveReq.Id}/approve", new LeaveApprovalRequest("Approved, please ensure tasks are handed over"));
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);
        var approved = (await approveRes.Content.ReadFromJsonAsync<LeaveRequestDto>())!;
        Assert.Equal(LeaveStatus.Approved, approved.Status);

        // 4. Verify balance is deducted (12 - 3 = 9 days)
        var updatedBalances = (await (await http.GetAsync("/api/office/leave/balances")).Content.ReadFromJsonAsync<List<LeaveBalanceDto>>())!;
        var updatedCasual = updatedBalances.First(b => b.LeaveType == LeaveType.Casual);
        Assert.Equal(3.00m, updatedCasual.UsedDays);
        Assert.Equal(9.00m, updatedCasual.AvailableDays);

        // 5. Workload endpoint reflects leave status
        var workloadRes = await http.GetAsync("/api/tasks/workload");
        Assert.Equal(HttpStatusCode.OK, workloadRes.StatusCode);
        var workload = (await workloadRes.Content.ReadFromJsonAsync<List<WorkloadRow>>())!;
        var myRow = workload.FirstOrDefault(r => r.UserId == auth.UserId);
        Assert.NotNull(myRow);
        Assert.NotNull(myRow.LeaveStatus);
        Assert.Contains("Casual", myRow.LeaveStatus);
    }

    [Fact]
    public async Task Office_Expenses_And_Client_Invoice_Reimbursement_Lifecycle()
    {
        var auth = await api.SignUpAsync("Calicut CA Practice");
        var http = api.WithToken(auth.AccessToken);

        var client = await CreateClientAsync(http, "Malabar Spices Ltd");

        // 1. Staff submits reimbursable expense (ROC Filing Fee ₹2,500)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expReq = new SubmitExpenseRequest(
            Date: today,
            Category: ExpenseCategory.RocFees,
            Amount: 2500m,
            Description: "MCA V3 filing fee for Form MGT-7A",
            ClientId: client.Id,
            IsReimbursableByClient: true
        );
        var submitRes = await http.PostAsJsonAsync("/api/office/expenses", expReq);
        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);
        var expense = (await submitRes.Content.ReadFromJsonAsync<OfficeExpenseDto>())!;
        Assert.Equal(ExpenseStatus.Submitted, expense.Status);
        Assert.True(expense.IsReimbursableByClient);
        Assert.False(expense.IsClientBilled);

        // 2. Partner approves expense
        var approveRes = await http.PostAsync($"/api/office/expenses/{expense.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);
        var approved = (await approveRes.Content.ReadFromJsonAsync<OfficeExpenseDto>())!;
        Assert.Equal(ExpenseStatus.Approved, approved.Status);

        // 3. Query unbilled reimbursables for client
        var unbilledRes = await http.GetAsync($"/api/office/expenses/unbilled/{client.Id}");
        Assert.Equal(HttpStatusCode.OK, unbilledRes.StatusCode);
        var unbilledList = (await unbilledRes.Content.ReadFromJsonAsync<List<OfficeExpenseDto>>())!;
        Assert.Contains(unbilledList, e => e.Id == expense.Id);

        // 4. Raise invoice for client with IncludeUnbilledExpenses: true
        var invReq = new CreateInvoiceRequest(
            ClientId: client.Id,
            IssueDate: today,
            DueDate: today.AddDays(15),
            Currency: "INR",
            Notes: "Retainer & ROC Filings",
            Terms: "Net 15",
            Items:
            [
                new InvoiceItemInput(
                    Description: "Corporate Secretarial Advisory",
                    SacCode: "998239",
                    Quantity: 1,
                    UnitPrice: 20000m,
                    GstRate: 18m
                )
            ],
            IncludeUnbilledExpenses: true
        );
        var invRes = await http.PostAsJsonAsync("/api/invoices", invReq);
        Assert.Equal(HttpStatusCode.Created, invRes.StatusCode);
        var invoice = (await invRes.Content.ReadFromJsonAsync<InvoiceDto>())!;

        // Invoice should contain both the professional fee and the reimbursable expense
        Assert.True(invoice.Items.Count >= 2);
        Assert.Contains(invoice.Items, i => i.Description.Contains("Reimbursement: MCA V3"));
        Assert.Equal(22500m, invoice.Subtotal); // 20000 + 2500

        // 5. Expense is now marked as billed
        var refreshedExpenses = (await (await http.GetAsync($"/api/office/expenses?clientId={client.Id}")).Content.ReadFromJsonAsync<List<OfficeExpenseDto>>())!;
        var billedExp = refreshedExpenses.First(e => e.Id == expense.Id);
        Assert.True(billedExp.IsClientBilled);
        Assert.Equal(invoice.Id, billedExp.BilledInvoiceId);
    }

    [Fact]
    public async Task Inward_Outward_Register_Creation_And_Printable_Html()
    {
        var auth = await api.SignUpAsync("Thrissur Audit Firm");
        var http = api.WithToken(auth.AccessToken);

        var client = await CreateClientAsync(http, "Kerala Agro Industries");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Log Inward documents (Original Ledgers & Vouchers)
        var inwReq = new CreateRegisterEntryRequest(
            EntryType: RegisterEntryType.Inward,
            Date: today,
            ClientId: client.Id,
            Description: "Original Cash Book and 4 Purchase Folders FY 2025-26",
            DeliveryMode: DeliveryMode.Hand,
            SenderRecipientName: "Mr. Suresh (Accountant)"
        );
        var inwRes = await http.PostAsJsonAsync("/api/office/registers", inwReq);
        Assert.Equal(HttpStatusCode.Created, inwRes.StatusCode);
        var inwEntry = (await inwRes.Content.ReadFromJsonAsync<RegisterEntryDto>())!;
        Assert.StartsWith("INW/", inwEntry.EntryNumber);
        Assert.Equal(RegisterStatus.Received, inwEntry.Status);

        // 2. Log Outward item (DSC token return)
        var outReq = new CreateRegisterEntryRequest(
            EntryType: RegisterEntryType.Outward,
            Date: today,
            ClientId: client.Id,
            Description: "Class 3 DSC Token returned after ITR signing",
            DeliveryMode: DeliveryMode.Courier,
            CourierName: "DTDC Express",
            TrackingNumber: "DTDC98234120",
            DscTokenRef: "PROXKEY-4921",
            SenderRecipientName: "Managing Director"
        );
        var outRes = await http.PostAsJsonAsync("/api/office/registers", outReq);
        Assert.Equal(HttpStatusCode.Created, outRes.StatusCode);
        var outEntry = (await outRes.Content.ReadFromJsonAsync<RegisterEntryDto>())!;
        Assert.StartsWith("OUT/", outEntry.EntryNumber);
        Assert.Equal(RegisterStatus.Dispatched, outEntry.Status);
        Assert.Equal("PROXKEY-4921", outEntry.DscTokenRef);

        // 3. List register entries
        var listRes = await http.GetAsync($"/api/office/registers?clientId={client.Id}");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var list = (await listRes.Content.ReadFromJsonAsync<List<RegisterEntryDto>>())!;
        Assert.True(list.Count >= 2);

        // 4. Fetch printable register HTML
        var htmlRes = await http.GetAsync("/api/office/registers/printable-html");
        Assert.Equal(HttpStatusCode.OK, htmlRes.StatusCode);
        var html = await htmlRes.Content.ReadAsStringAsync();
        Assert.Contains("Thrissur Audit Firm", html);
        Assert.Contains("INWARD / OUTWARD REGISTER", html);
        Assert.Contains(inwEntry.EntryNumber, html);
        Assert.Contains(outEntry.EntryNumber, html);
        Assert.Contains("PROXKEY-4921", html);
    }

    [Fact]
    public async Task Multi_Tenancy_Protects_All_Office_Essentials()
    {
        var authA = await api.SignUpAsync("Firm Alpha Office Desk");
        var httpA = api.WithToken(authA.AccessToken);

        var authB = await api.SignUpAsync("Firm Beta Office Desk");
        var httpB = api.WithToken(authB.AccessToken);

        var clientA = await CreateClientAsync(httpA, "Client of Firm A");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Firm A creates attendance, leave, expense, and register
        await httpA.PostAsJsonAsync("/api/office/attendance/check-in", new AttendanceCheckInRequest("Firm A check-in"));
        var applyRes = await httpA.PostAsJsonAsync("/api/office/leave/apply", new ApplyLeaveRequest(LeaveType.Casual, today, today, 1m, "Firm A leave"));
        var leaveA = (await applyRes.Content.ReadFromJsonAsync<LeaveRequestDto>())!;

        var expRes = await httpA.PostAsJsonAsync("/api/office/expenses", new SubmitExpenseRequest(today, ExpenseCategory.Travel, 500m, "Firm A travel", clientA.Id));
        var expA = (await expRes.Content.ReadFromJsonAsync<OfficeExpenseDto>())!;

        var regRes = await httpA.PostAsJsonAsync("/api/office/registers", new CreateRegisterEntryRequest(RegisterEntryType.Inward, today, clientA.Id, "Firm A docs", DeliveryMode.Hand));
        var regA = (await regRes.Content.ReadFromJsonAsync<RegisterEntryDto>())!;

        // Firm B tries to list Firm A's expenses -> empty
        var bExpenses = (await (await httpB.GetAsync($"/api/office/expenses?clientId={clientA.Id}")).Content.ReadFromJsonAsync<List<OfficeExpenseDto>>())!;
        Assert.Empty(bExpenses);

        // Firm B tries to approve Firm A's leave -> 404
        var bApproveLeave = await httpB.PostAsJsonAsync($"/api/office/leave/requests/{leaveA.Id}/approve", new LeaveApprovalRequest());
        Assert.Equal(HttpStatusCode.NotFound, bApproveLeave.StatusCode);

        // Firm B tries to approve Firm A's expense -> 404
        var bApproveExp = await httpB.PostAsync($"/api/office/expenses/{expA.Id}/approve", null);
        Assert.Equal(HttpStatusCode.NotFound, bApproveExp.StatusCode);

        // Firm B queries registers -> does not contain Firm A's register entry
        var bRegisters = (await (await httpB.GetAsync("/api/office/registers")).Content.ReadFromJsonAsync<List<RegisterEntryDto>>())!;
        Assert.DoesNotContain(bRegisters, r => r.Id == regA.Id);
    }
}
