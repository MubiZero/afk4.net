using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Platform.Announcements;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class AnnouncementFeedEndpointTests
{
    // Лента отбирает анонсы по НАСТОЯЩИМ часам, поэтому и окна показа задаются от них.
    // Замороженная дата здесь была миной: 11 августа реальное время въехало в окно анонса,
    // засеянного как «ещё не начался», и тест начал падать сам по себе.
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly string FeedUrl = $"/api/organizations/{TestIds.OrganizationId:D}/announcements";

    [Fact]
    public async Task Get_RequiresAStaffSession()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(FeedUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_ShowsAPublishedAnnouncementInsideItsWindow()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedAnnouncementAsync(factory);

        var items = await client.GetFromJsonAsync<AnnouncementFeedItemDto[]>(FeedUrl);

        var item = Assert.Single(items!);
        Assert.Equal("Плановые работы", item.Title);
        Assert.False(item.IsRead);
    }

    [Fact]
    public async Task Get_HidesDraftsAndWithdrawnAnnouncements()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedAnnouncementAsync(factory, status: AnnouncementStatus.Draft);
        await SeedAnnouncementAsync(factory, status: AnnouncementStatus.Withdrawn);

        var items = await client.GetFromJsonAsync<AnnouncementFeedItemDto[]>(FeedUrl);

        Assert.Empty(items!);
    }

    [Fact]
    public async Task Get_HidesAnnouncementsOutsideTheShowWindow()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        // Ещё не начался и уже кончился — оба не показываются: анонс про субботние работы не
        // должен торчать до декабря, и не должен появляться раньше времени.
        await SeedAnnouncementAsync(factory, from: Now.AddDays(1), until: Now.AddDays(2));
        await SeedAnnouncementAsync(factory, from: Now.AddDays(-5), until: Now.AddDays(-1));

        var items = await client.GetFromJsonAsync<AnnouncementFeedItemDto[]>(FeedUrl);

        Assert.Empty(items!);
    }

    [Fact]
    public async Task Get_HidesAnAnnouncementAddressedToAnotherPlan()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SetPlanCodeAsync(factory, "starter");
        await SeedAnnouncementAsync(factory,
            audienceKind: AnnouncementAudienceKinds.Plans, planCodesJson: "[\"pro\"]");

        var items = await client.GetFromJsonAsync<AnnouncementFeedItemDto[]>(FeedUrl);

        Assert.Empty(items!);
    }

    [Fact]
    public async Task Get_SortsTheMostImportantFirst()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedAnnouncementAsync(factory, title: "К сведению", severity: AnnouncementSeverity.Info);
        await SeedAnnouncementAsync(factory, title: "Авария", severity: AnnouncementSeverity.Critical);
        await SeedAnnouncementAsync(factory, title: "Предупреждение", severity: AnnouncementSeverity.Warning);

        var items = await client.GetFromJsonAsync<AnnouncementFeedItemDto[]>(FeedUrl);

        Assert.Equal(["Авария", "Предупреждение", "К сведению"], items!.Select(item => item.Title));
    }

    [Fact]
    public async Task Read_MarksItForThisStaffUserOnly()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var announcementId = await SeedAnnouncementAsync(factory);

        var response = await client.PostAsync($"{FeedUrl}/{announcementId}/read", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var items = await client.GetFromJsonAsync<AnnouncementFeedItemDto[]>(FeedUrl);
        Assert.True(Assert.Single(items!).IsRead);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var reads = await db.AnnouncementReads.ToArrayAsync();
        // Отметка привязана к сотруднику: у его семерых коллег полоса ещё висит. Если бы она
        // писалась на клуб, первый закрывший скрыл бы сообщение от всех.
        var read = Assert.Single(reads);
        Assert.Equal(TestIds.TechnicianStaffUserId, read.StaffUserId);
    }

    [Fact]
    public async Task Read_IsIdempotent()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var announcementId = await SeedAnnouncementAsync(factory);

        await client.PostAsync($"{FeedUrl}/{announcementId}/read", null);
        var second = await client.PostAsync($"{FeedUrl}/{announcementId}/read", null);

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await db.AnnouncementReads.CountAsync());
    }

    [Fact]
    public async Task Read_RefusesAnAnnouncementAddressedToSomebodyElse()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SetPlanCodeAsync(factory, "starter");
        var announcementId = await SeedAnnouncementAsync(factory,
            audienceKind: AnnouncementAudienceKinds.Plans, planCodesJson: "[\"pro\"]");

        var response = await client.PostAsync($"{FeedUrl}/{announcementId}/read", null);

        // Чужой id в адресе не должен создавать отметку о сообщении, которого клуб не видел.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(0, await db.AnnouncementReads.CountAsync());
    }

    private static async Task<Guid> SeedAnnouncementAsync(
        PlatformApiFactory factory,
        string title = "Плановые работы",
        string severity = AnnouncementSeverity.Info,
        string status = AnnouncementStatus.Published,
        string audienceKind = AnnouncementAudienceKinds.All,
        string planCodesJson = "[]",
        DateTimeOffset? from = null,
        DateTimeOffset? until = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var announcementId = Guid.NewGuid();

        db.PlatformAnnouncements.Add(new PlatformAnnouncementEntity
        {
            PlatformAnnouncementId = announcementId,
            Title = title,
            Body = "Текст сообщения",
            Severity = severity,
            ShowFromUtc = from ?? Now.AddDays(-1),
            ShowUntilUtc = until ?? Now.AddDays(5),
            AudienceKind = audienceKind,
            AudiencePlanCodesJson = planCodesJson,
            AudienceOrganizationIdsJson = "[]",
            Status = status,
            CreatedByPlatformAdminUserId = Guid.NewGuid(),
            CreatedAtUtc = Now.AddDays(-2),
            UpdatedAtUtc = Now.AddDays(-2),
            PublishedAtUtc = status == AnnouncementStatus.Draft ? null : Now.AddDays(-1)
        });

        await db.SaveChangesAsync();
        return announcementId;
    }

    private static async Task SetPlanCodeAsync(PlatformApiFactory factory, string planCode)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await db.Organizations.SingleAsync(row => row.OrganizationId == TestIds.OrganizationId);
        organization.PlanCode = planCode;
        await db.SaveChangesAsync();
    }
}
