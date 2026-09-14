using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

// Выданный доступ в чужой клуб был невидим и почти неотзываем: списка не существовало вовсе, а
// оборвать доступ умел только тот, кто его выдал. Доступ коллеги, закрывшего ноутбук, не мог
// прервать никто — оставалось ждать, пока истечёт срок.
//
// Фабрика активируется в теле самого теста (CreateClient), а не во вспомогательном методе:
// база теста живёт в AsyncLocal, и значение, выставленное внутри вложенного async-метода, наружу
// не выходит — клиент получил бы чужую пустую базу и отвечал 401 на всё.
public sealed class PlatformSupportAccessListTests
{
    private const string Reason = "Investigate a stuck session for the club";

    private static async Task SeedOrganizationAsync(PlatformApiFactory factory)
    {
        using var seedClient = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(
            factory, seedClient, AFK4.Platform.Api.Identity.OrganizationRoleNames.OrganizationOwner);
    }

    private static Task SignInAsSupportAsync(PlatformApiFactory factory, HttpClient client, string userName) =>
        PlatformAdminTestHelper.AuthorizeAsAsync(
            factory, client, userName: userName, roles: [PlatformAdminRoleNames.PlatformSupport]);

    private static Task<HttpResponseMessage> IssueAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/platform/support-access-grants",
            new CreatePlatformSupportAccessGrantRequest(TestIds.OrganizationId, Reason, 30));

    private static Task<PlatformSupportAccessGrantListItem[]?> ListAsync(HttpClient client) =>
        client.GetFromJsonAsync<PlatformSupportAccessGrantListItem[]>(
            $"/api/platform/support-access-grants?organizationId={TestIds.OrganizationId:D}");

    [Fact]
    public async Task ActiveGrant_IsListedWithWhoIssuedItAndWhy()
    {
        await using var factory = new PlatformApiFactory();
        using var support = factory.CreateClient();
        await SeedOrganizationAsync(factory);
        await SignInAsSupportAsync(factory, support, "support-a@platform.test");
        await IssueAsync(support);

        var listed = await ListAsync(support);

        var grant = Assert.Single(listed!);
        Assert.Equal(Reason, grant.Reason);
        Assert.Equal(PlatformAdminTestHelper.DefaultDisplayName, grant.PlatformAdminDisplayName);
        // Билет не предъявлен — доступ выдан, но внутрь никто не заходил.
        Assert.Null(grant.EnteredAtUtc);
    }

    [Fact]
    public async Task RedeemedGrant_ReportsWhenSupportActuallyEntered()
    {
        await using var factory = new PlatformApiFactory();
        using var support = factory.CreateClient();
        await SeedOrganizationAsync(factory);
        await SignInAsSupportAsync(factory, support, "support-a@platform.test");
        var issued = await IssueAsync(support);
        var issue = await issued.Content.ReadFromJsonAsync<PlatformSupportAccessGrantIssue>();
        await support.PostAsJsonAsync("/api/public/support-access/sessions",
            new RedeemSupportAccessTicketRequest(issue!.Ticket));

        var grant = Assert.Single((await ListAsync(support))!);

        Assert.NotNull(grant.EnteredAtUtc);
    }

    [Fact]
    public async Task RevokedGrant_LeavesTheList()
    {
        await using var factory = new PlatformApiFactory();
        using var support = factory.CreateClient();
        await SeedOrganizationAsync(factory);
        await SignInAsSupportAsync(factory, support, "support-a@platform.test");
        await IssueAsync(support);
        var grant = Assert.Single((await ListAsync(support))!);

        var revoked = await support.DeleteAsync($"/api/platform/support-access-grants/{grant.GrantId:D}");

        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Empty((await ListAsync(support))!);
    }

    [Fact]
    public async Task ExpiredGrant_LeavesTheList()
    {
        await using var factory = new PlatformApiFactory();
        using var support = factory.CreateClient();
        await SeedOrganizationAsync(factory);
        await SignInAsSupportAsync(factory, support, "support-a@platform.test");
        await IssueAsync(support);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            (await db.PlatformSupportAccessGrants.SingleAsync()).ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        Assert.Empty((await ListAsync(support))!);
    }

    [Fact]
    public async Task GrantIntoAnotherClub_IsNotListedHere()
    {
        await using var factory = new PlatformApiFactory();
        using var support = factory.CreateClient();
        await SeedOrganizationAsync(factory);
        await SignInAsSupportAsync(factory, support, "support-a@platform.test");
        await IssueAsync(support);

        var other = await support.GetFromJsonAsync<PlatformSupportAccessGrantListItem[]>(
            $"/api/platform/support-access-grants?organizationId={Guid.NewGuid():D}");

        Assert.Empty(other!);
    }

    // Ответственность за выданный доступ остаётся на выдавшем, поэтому один сотрудник поддержки
    // по-прежнему не обрывает доступ другого.
    [Fact]
    public async Task Support_CannotRevokeAGrantIssuedByAnotherSupportAgent()
    {
        await using var factory = new PlatformApiFactory();
        using var issuer = factory.CreateClient();
        using var other = factory.CreateClient();
        await SeedOrganizationAsync(factory);
        await SignInAsSupportAsync(factory, issuer, "support-a@platform.test");
        await SignInAsSupportAsync(factory, other, "support-b@platform.test");
        await IssueAsync(issuer);
        var grant = Assert.Single((await ListAsync(issuer))!);

        var response = await other.DeleteAsync($"/api/platform/support-access-grants/{grant.GrantId:D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single((await ListAsync(issuer))!);
    }

    // А распорядитель учётных записей — обрывает: он и так может отключить самого выдавшего, так
    // что запрет означал бы ровно одно — доступ ушедшего домой коллеги висит до истечения срока.
    [Fact]
    public async Task AdminWhoManagesAdmins_CanRevokeSomeoneElsesGrant()
    {
        await using var factory = new PlatformApiFactory();
        using var issuer = factory.CreateClient();
        using var admin = factory.CreateClient();
        await SeedOrganizationAsync(factory);
        await SignInAsSupportAsync(factory, issuer, "support-a@platform.test");
        await PlatformAdminTestHelper.AuthorizeAsAsync(
            factory, admin, userName: "admin@platform.test", roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await IssueAsync(issuer);
        var grant = Assert.Single((await ListAsync(issuer))!);

        var response = await admin.DeleteAsync($"/api/platform/support-access-grants/{grant.GrantId:D}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await ListAsync(issuer))!);
    }

    [Fact]
    public async Task ListingRequiresSigningIn()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SeedOrganizationAsync(factory);

        var response = await client.GetAsync(
            $"/api/platform/support-access-grants?organizationId={TestIds.OrganizationId:D}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
