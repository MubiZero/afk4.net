using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Platform.Announcements;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAnnouncementEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset From = new(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Until = new(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Get_RequiresManageAnnouncements_AndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);

        var response = await client.GetAsync("/api/platform/announcements");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await db.AuditRecords.AnyAsync(record => record.Action == "platform.announcements.view"));
    }

    [Fact]
    public async Task Post_CreatesADraftThatSendsNothing()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await SeedClubAsync(factory, "starter");

        var created = await CreateAsync(client, AnnouncementSeverity.Critical);

        Assert.Equal(AnnouncementStatus.Draft, created.Status);
        Assert.False(created.EmailDispatched);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        // Правка черновика не шлёт ничего никому — письма ставятся только публикацией.
        Assert.False(await db.NotificationOutbox.AnyAsync(
            row => row.TemplateKey == NotificationTemplateKeys.PlatformAnnouncement));
    }

    [Fact]
    public async Task Publish_QueuesOneOwnerEmailForAWarning()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await SeedClubAsync(factory, "starter");

        var created = await CreateAsync(client, AnnouncementSeverity.Warning);
        var response = await client.PostAsync($"/api/platform/announcements/{created.AnnouncementId}/publish", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var published = await response.Content.ReadFromJsonAsync<PlatformAnnouncementDto>();
        Assert.Equal(AnnouncementStatus.Published, published!.Status);
        Assert.True(published.EmailDispatched);
        Assert.Equal(1, await CountAnnouncementEmailsAsync(factory));
    }

    [Fact]
    public async Task Publish_SendsNoEmailForAnInfoAnnouncement()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await SeedClubAsync(factory, "starter");

        var created = await CreateAsync(client, AnnouncementSeverity.Info);
        await client.PostAsync($"/api/platform/announcements/{created.AnnouncementId}/publish", null);

        // «К сведению» письмом владельцу не шлют.
        Assert.Equal(0, await CountAnnouncementEmailsAsync(factory));
    }

    [Fact]
    public async Task Republishing_AfterWithdrawal_DoesNotSendASecondEmail()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await SeedClubAsync(factory, "starter");

        var created = await CreateAsync(client, AnnouncementSeverity.Critical);
        await client.PostAsync($"/api/platform/announcements/{created.AnnouncementId}/publish", null);
        await client.PostAsync($"/api/platform/announcements/{created.AnnouncementId}/withdraw", null);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // Вернуть анонс в работу — обычный сценарий; статус переводится напрямую, потому что
            // публикация допускается только из черновика.
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var row = await db.PlatformAnnouncements.SingleAsync();
            row.Status = AnnouncementStatus.Draft;
            await db.SaveChangesAsync();
        }

        await client.PostAsync($"/api/platform/announcements/{created.AnnouncementId}/publish", null);

        // То же сообщение, а не второе письмо владельцу.
        Assert.Equal(1, await CountAnnouncementEmailsAsync(factory));
    }

    [Fact]
    public async Task Publish_ByPlanAudience_WritesOnlyToClubsOnThatPlan()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var starter = await SeedClubAsync(factory, "starter");
        await SeedClubAsync(factory, "pro");

        var response = await client.PostAsJsonAsync("/api/platform/announcements", new SavePlatformAnnouncementRequest(
            "Тариф меняется", "Подробности внутри", AnnouncementSeverity.Warning, From, Until,
            AnnouncementAudienceKinds.Plans, ["starter"], []));
        var created = await response.Content.ReadFromJsonAsync<PlatformAnnouncementDto>();
        await client.PostAsync($"/api/platform/announcements/{created!.AnnouncementId}/publish", null);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var recipients = await db.NotificationOutbox
            .Where(row => row.TemplateKey == NotificationTemplateKeys.PlatformAnnouncement)
            .Select(row => row.OrganizationId)
            .ToArrayAsync();

        Assert.Equal([starter], recipients);
    }

    [Fact]
    public async Task Put_EditsThePublishedTextWithoutResendingTheEmail()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await SeedClubAsync(factory, "starter");

        var created = await CreateAsync(client, AnnouncementSeverity.Warning);
        await client.PostAsync($"/api/platform/announcements/{created.AnnouncementId}/publish", null);

        var response = await client.PutAsJsonAsync($"/api/platform/announcements/{created.AnnouncementId}",
            new SavePlatformAnnouncementRequest("Плановые работы", "Исправленная опечатка",
                AnnouncementSeverity.Warning, From, Until, AnnouncementAudienceKinds.All, [], []));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<PlatformAnnouncementDto>();
        Assert.Equal("Исправленная опечатка", updated!.Body);
        // Исправленная опечатка не превращается во второе письмо владельцу.
        Assert.Equal(1, await CountAnnouncementEmailsAsync(factory));
    }

    [Fact]
    public async Task Put_RefusesToChangeTheAudienceOfAPublishedAnnouncement()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await SeedClubAsync(factory, "starter");

        var created = await CreateAsync(client, AnnouncementSeverity.Warning);
        await client.PostAsync($"/api/platform/announcements/{created.AnnouncementId}/publish", null);

        var response = await client.PutAsJsonAsync($"/api/platform/announcements/{created.AnnouncementId}",
            new SavePlatformAnnouncementRequest("Плановые работы", "Текст",
                AnnouncementSeverity.Warning, From, Until, AnnouncementAudienceKinds.Plans, ["starter"], []));

        // Письма уже ушли по прежнему списку: смена аудитории задним числом сделала бы след аудита
        // неправдой.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AnnouncementErrorCodes.PublishedAudienceLocked, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Put_RefusesAWithdrawnAnnouncement()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await SeedClubAsync(factory, "starter");

        var created = await CreateAsync(client, AnnouncementSeverity.Info);
        await client.PostAsync($"/api/platform/announcements/{created.AnnouncementId}/publish", null);
        await client.PostAsync($"/api/platform/announcements/{created.AnnouncementId}/withdraw", null);

        var response = await client.PutAsJsonAsync($"/api/platform/announcements/{created.AnnouncementId}",
            new SavePlatformAnnouncementRequest("Другой заголовок", "Другой текст",
                AnnouncementSeverity.Info, From, Until, AnnouncementAudienceKinds.All, [], []));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AnnouncementErrorCodes.WithdrawnAnnouncement, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Post_RejectsAWindowThatEndsBeforeItStarts()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var response = await client.PostAsJsonAsync("/api/platform/announcements", new SavePlatformAnnouncementRequest(
            "Задом наперёд", "Текст", AnnouncementSeverity.Info, Until, From,
            AnnouncementAudienceKinds.All, [], []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AnnouncementErrorCodes.InvalidAnnouncement, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Post_RejectsAnUnknownPlanInTheAudience()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var response = await client.PostAsJsonAsync("/api/platform/announcements", new SavePlatformAnnouncementRequest(
            "Тариф", "Текст", AnnouncementSeverity.Info, From, Until,
            AnnouncementAudienceKinds.Plans, ["no_such_plan"], []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AnnouncementErrorCodes.UnknownPlan, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Publish_RefusesAnAlreadyPublishedAnnouncement_AndWritesTheRefusalToAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await SeedClubAsync(factory, "starter");

        var created = await CreateAsync(client, AnnouncementSeverity.Info);
        await client.PostAsync($"/api/platform/announcements/{created.AnnouncementId}/publish", null);
        var response = await client.PostAsync($"/api/platform/announcements/{created.AnnouncementId}/publish", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AnnouncementErrorCodes.InvalidTransition, await ReadCodeAsync(response));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var refusal = await db.AuditRecords
            .Where(record => record.Action == "platform.announcements.publish" && record.Outcome == "Denied")
            .SingleAsync();
        Assert.Contains(AnnouncementErrorCodes.InvalidTransition, refusal.DetailsJson);
    }

    private static async Task<PlatformAnnouncementDto> CreateAsync(HttpClient client, string severity)
    {
        var response = await client.PostAsJsonAsync("/api/platform/announcements", new SavePlatformAnnouncementRequest(
            "Плановые работы", "В субботу с 2:00 до 4:00 платформа будет недоступна.",
            severity, From, Until, AnnouncementAudienceKinds.All, [], []));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PlatformAnnouncementDto>())!;
    }

    private static async Task<int> CountAnnouncementEmailsAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.NotificationOutbox
            .CountAsync(row => row.TemplateKey == NotificationTemplateKeys.PlatformAnnouncement);
    }

    private static async Task<Guid> SeedClubAsync(PlatformApiFactory factory, string planCode)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organizationId = Guid.NewGuid();

        if (!await db.SubscriptionPlans.AnyAsync(plan => plan.PlanCode == planCode))
        {
            db.SubscriptionPlans.Add(new SubscriptionPlanEntity
            {
                PlanCode = planCode,
                Name = planCode,
                PriceMinorUnits = 1000,
                CurrencyCode = "TJS",
                IsActive = true,
                CreatedAtUtc = Now,
                UpdatedAtUtc = Now
            });
        }

        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = "club-" + organizationId.ToString("N")[..8],
            Name = "Клуб " + planCode,
            Status = OrganizationStatusNames.Active,
            PlanCode = planCode,
            SubscriptionStatus = SubscriptionStatusNames.Trial,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });

        // Письмо адресовано владельцу: без активного владельца с почтой резолвер вернул бы null и
        // тест «письмо ушло» был бы зелёным ни на чём.
        var ownerId = Guid.NewGuid();
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = ownerId,
            OrganizationId = organizationId,
            UserName = $"owner-{ownerId:N}@club.test",
            NormalizedUserName = $"OWNER-{ownerId:N}@CLUB.TEST",
            DisplayName = "Владелец",
            Email = $"owner-{ownerId:N}@club.test",
            IsActive = true,
            CreatedAtUtc = Now
        });
        db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(),
            StaffUserId = ownerId,
            OrganizationId = organizationId,
            RoleName = OrganizationRoleNames.OrganizationOwner
        });

        await db.SaveChangesAsync();
        return organizationId;
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
