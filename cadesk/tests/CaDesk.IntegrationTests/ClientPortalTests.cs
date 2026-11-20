using System.Net;
using System.Net.Http.Json;
using CaDesk.Application.Clients;
using CaDesk.Application.Portal;
using CaDesk.Domain.Portal;
using Xunit;

namespace CaDesk.IntegrationTests;

/// <summary>
/// Sprint 13: Client Self-Service Portal & External Auth.
/// Tests portal user creation, external portal authentication, service requests & ticket messaging,
/// and strict tenant isolation between firms.
/// </summary>
public class ClientPortalTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly Dictionary<string, string> StandardPan = new() { ["PAN"] = "AAACB1234F" };

    private async Task<(HttpClient Client, Guid UserId)> NewFirmAsync(string name = "Portal Firm")
    {
        var auth = await api.SignUpAsync(name);
        return (api.WithToken(auth.AccessToken), auth.UserId);
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient http, string name = "Titan Industries")
    {
        var res = await http.PostAsJsonAsync("/api/clients", new SaveClientRequest(
            null, name, null, null, "Company", StandardPan, null, null, null, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task Portal_FullLifecycle_UserCreation_Auth_RequestAndMessageFlow()
    {
        var (staffHttp, staffUserId) = await NewFirmAsync("Audit Partners LLP");
        var client = await CreateClientAsync(staffHttp, "Nexus Retail Pvt Ltd");
        var email = $"director-{Guid.NewGuid():N}@nexusretail.example";

        // 1. Staff creates a portal user for the client
        var createPortalUserRes = await staffHttp.PostAsJsonAsync("/api/portal/users", new CreatePortalUserRequest(
            client.Id,
            email,
            "Sanjay Gupta",
            "+919876543210",
            "SecurePass#2026",
            PortalRole.ClientAdmin
        ));
        Assert.Equal(HttpStatusCode.Created, createPortalUserRes.StatusCode);
        var portalUser = (await createPortalUserRes.Content.ReadFromJsonAsync<PortalUserDto>())!;
        Assert.Equal("Sanjay Gupta", portalUser.Name);
        Assert.Equal(email, portalUser.Email);
        Assert.True(portalUser.IsActive);

        // 2. Client logs in via external portal endpoint
        var loginRes = await staffHttp.PostAsJsonAsync("/api/portal/auth/login", new PortalLoginRequest(
            email,
            "SecurePass#2026"
        ));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var authResult = (await loginRes.Content.ReadFromJsonAsync<PortalAuthResult>())!;
        Assert.NotNull(authResult.AccessToken);
        Assert.Equal(client.Id, authResult.Client.Id);

        // Create client authenticated client
        var clientHttp = api.WithToken(authResult.AccessToken);

        // 3. Client checks their dashboard
        var dashRes = await clientHttp.GetAsync("/api/portal/dashboard");
        Assert.Equal(HttpStatusCode.OK, dashRes.StatusCode);
        var dashboard = (await dashRes.Content.ReadFromJsonAsync<ClientPortalDashboardDto>())!;
        Assert.Equal(client.Id, dashboard.Client.Id);
        Assert.Equal(0, dashboard.OpenRequestsCount);

        // 4. Client submits a service request
        var createReqRes = await clientHttp.PostAsJsonAsync("/api/portal/requests", new CreateServiceRequestInput(
            RequestCategory.Certification,
            "Urgent: Net Worth Certificate for Bank Loan",
            "We require a certified net worth statement for our consortium bank credit line renewal.",
            RequestPriority.High
        ));
        Assert.Equal(HttpStatusCode.Created, createReqRes.StatusCode);
        var createdSr = (await createReqRes.Content.ReadFromJsonAsync<ServiceRequestDto>())!;
        Assert.Equal(RequestStatus.Submitted, createdSr.Status);
        Assert.Equal(RequestPriority.High, createdSr.Priority);
        Assert.StartsWith("REQ/", createdSr.RequestNumber);
        Assert.Single(createdSr.Messages);

        // 5. Staff views the request in the firm service desk
        var deskList = await staffHttp.GetFromJsonAsync<List<ServiceRequestDto>>("/api/service-requests");
        Assert.NotNull(deskList);
        Assert.Contains(deskList, x => x.Id == createdSr.Id);

        // 6. Staff assigns and replies to the request
        var replyRes = await staffHttp.PostAsJsonAsync($"/api/service-requests/{createdSr.Id}/messages", new AddRequestMessageInput(
            "Please upload the updated audited balance sheet and property title deeds under Documents.",
            null
        ));
        Assert.Equal(HttpStatusCode.OK, replyRes.StatusCode);

        // Update status to in_progress
        var statusRes = await staffHttp.PutAsJsonAsync($"/api/service-requests/{createdSr.Id}/status", new UpdateServiceRequestStatusInput(
            RequestStatus.InProgress,
            null,
            staffUserId
        ));
        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);

        // 7. Client sees the updated ticket with 2 messages and in_progress status
        var clientSr = await clientHttp.GetFromJsonAsync<ServiceRequestDto>($"/api/portal/requests/{createdSr.Id}");
        Assert.NotNull(clientSr);
        Assert.Equal(RequestStatus.InProgress, clientSr.Status);
        Assert.Equal(2, clientSr.Messages.Count);
        Assert.Equal("staff", clientSr.Messages[1].SenderType);
    }

    [Fact]
    public async Task Portal_TenantIsolation_CrossFirmAccessBlocked()
    {
        var (firmA, _) = await NewFirmAsync("Firm A CA");
        var (firmB, _) = await NewFirmAsync("Firm B CA");
        var clientA = await CreateClientAsync(firmA, "Client Alpha");

        // Create portal user in Firm A
        var userRes = await firmA.PostAsJsonAsync("/api/portal/users", new CreatePortalUserRequest(
            clientA.Id, "cfo@alpha.example", "CFO Alpha", null, "AlphaPass#123", PortalRole.ClientAdmin
        ));
        var userA = (await userRes.Content.ReadFromJsonAsync<PortalUserDto>())!;

        // Firm B cannot list or view Firm A's portal users
        var listB = await firmB.GetFromJsonAsync<List<PortalUserDto>>("/api/portal/users");
        Assert.NotNull(listB);
        Assert.DoesNotContain(listB, x => x.Id == userA.Id);

        // Firm B cannot delete Firm A's portal user
        var delRes = await firmB.DeleteAsync($"/api/portal/users/{userA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, delRes.StatusCode);
    }
}
