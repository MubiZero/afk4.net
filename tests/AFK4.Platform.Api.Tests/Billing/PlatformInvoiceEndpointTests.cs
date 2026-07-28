using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class PlatformInvoiceEndpointTests
{
    private static async Task<Guid> CreateOrganizationAsync(HttpClient client, string slug = "inv-club")
    {
        var request = new CreateOrganizationRequest(
            OrganizationSlug: slug,
            OrganizationName: "Invoice Club",
            BranchSlug: "main",
            BranchName: "Main",
            BranchCity: "Dushanbe",
            PlanCode: OrganizationPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Active,
            Limits: new OrganizationLimitsDto(1, 30, 40, 10),
            OwnerUserName: $"owner@{slug}.test",
            OwnerDisplayName: "Owner",
            OrganizationOwnerInviteLifetime: TimeSpan.FromDays(7));
        var response = await client.PostAsJsonAsync("/api/platform/organizations", request);
        var body = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        return body!.Organization.OrganizationId;
    }

    [Fact]
    public async Task GenerateThenListThenMarkPaid_Flow()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var orgId = await CreateOrganizationAsync(client);

        var generate = await client.PostAsync($"/api/platform/organizations/{orgId}/invoices/generate", content: null);
        var invoice = await generate.Content.ReadFromJsonAsync<InvoiceDto>();
        Assert.Equal(HttpStatusCode.OK, generate.StatusCode);

        var list = await client.GetAsync($"/api/platform/organizations/{orgId}/invoices");
        var invoices = await list.Content.ReadFromJsonAsync<List<InvoiceDto>>();
        Assert.Single(invoices!);

        var markPaid = await client.PostAsJsonAsync(
            $"/api/platform/invoices/{invoice!.InvoiceId}/mark-paid",
            new MarkInvoicePaidRequest("wire-123"));
        var paid = await markPaid.Content.ReadFromJsonAsync<InvoiceDto>();
        Assert.Equal(InvoiceStatusNames.Paid, paid!.Status);
    }

    [Fact]
    public async Task VoidInvoice_RequiresReason()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var orgId = await CreateOrganizationAsync(client, "void-club");
        var generate = await client.PostAsync($"/api/platform/organizations/{orgId}/invoices/generate", content: null);
        var invoice = await generate.Content.ReadFromJsonAsync<InvoiceDto>();

        var noReason = await client.PostAsJsonAsync(
            $"/api/platform/invoices/{invoice!.InvoiceId}/void", new VoidInvoiceRequest(""));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var voided = await client.PostAsJsonAsync(
            $"/api/platform/invoices/{invoice.InvoiceId}/void", new VoidInvoiceRequest("test"));
        var body = await voided.Content.ReadFromJsonAsync<InvoiceDto>();
        Assert.Equal(InvoiceStatusNames.Void, body!.Status);
    }

    [Fact]
    public async Task GetInvoices_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/platform/organizations/{Guid.NewGuid()}/invoices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
