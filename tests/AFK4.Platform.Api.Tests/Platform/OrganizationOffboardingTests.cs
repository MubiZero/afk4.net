using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

/// <summary>
/// Офбординг проверяется с ОБЕИХ сторон: что личное исчезло и что финансовое уцелело. Обратная
/// проверка здесь важнее прямой — снести лишнее гораздо легче, чем недоснести, а заметно это
/// станет через месяцы.
/// </summary>
public sealed class OrganizationOffboardingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Purge_RemovesEverythingPersonal()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var club = await SeedClubAsync(factory, dueDaysAgo: 1);

        var response = await client.PostAsJsonAsync($"/api/platform/organizations/{club.OrganizationId}/purge",
            new PurgeOrganizationRequest(club.Slug));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        Assert.False(await db.PlayerAccounts.AnyAsync(row => row.OrganizationId == club.OrganizationId));
        Assert.False(await db.LedgerEntries.AnyAsync(row => row.OrganizationId == club.OrganizationId));
        Assert.False(await db.StaffUsers.AnyAsync(row => row.OrganizationId == club.OrganizationId));
        Assert.False(await db.StaffRoleAssignments.AnyAsync(row => row.OrganizationId == club.OrganizationId));
        Assert.False(await db.Sessions.AnyAsync(row => row.OrganizationId == club.OrganizationId));
        Assert.False(await db.Reservations.AnyAsync(row => row.OrganizationId == club.OrganizationId));
        Assert.False(await db.PosSales.AnyAsync(row => row.OrganizationId == club.OrganizationId));
        Assert.False(await db.PosProducts.AnyAsync(row => row.OrganizationId == club.OrganizationId));
        Assert.False(await db.Devices.AnyAsync(row => row.OrganizationId == club.OrganizationId));
        Assert.False(await db.Branches.AnyAsync(row => row.OrganizationId == club.OrganizationId));
        Assert.False(await db.Seats.AnyAsync(row => row.OrganizationId == club.OrganizationId));
        Assert.False(await db.NewsItems.AnyAsync(row => row.OrganizationId == club.OrganizationId));
    }

    [Fact]
    public async Task Purge_KeepsWhatTheMoneyTrailNeeds()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var club = await SeedClubAsync(factory, dueDaysAgo: 1);

        await client.PostAsJsonAsync($"/api/platform/organizations/{club.OrganizationId}/purge",
            new PurgeOrganizationRequest(club.Slug));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        // «Сколько этот клуб заплатил в марте» — ответ обязан остаться.
        var organization = await db.Organizations.SingleAsync(row => row.OrganizationId == club.OrganizationId);
        Assert.Equal(OrganizationStatusNames.Purged, organization.Status);
        // Момент стирания фиксируется; точное значение задаёт часы приложения, а не тест.
        Assert.NotNull(organization.PurgedAtUtc);
        Assert.Null(organization.PurgeEligibleAtUtc);
        Assert.Equal(club.Slug, organization.Slug);

        Assert.True(await db.Invoices.AnyAsync(row => row.OrganizationId == club.OrganizationId));
        Assert.True(await db.OrganizationSubscriptions.AnyAsync(row => row.OrganizationId == club.OrganizationId));
        Assert.True(await db.SubscriptionDailySnapshots.AnyAsync(row => row.OrganizationId == club.OrganizationId));
        Assert.True(await db.OrganizationSupportNotes.AnyAsync(row => row.OrganizationId == club.OrganizationId));
    }

    [Fact]
    public async Task Purge_KeepsThePlatformAuditIncludingItsOwnRecord()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var club = await SeedClubAsync(factory, dueDaysAgo: 1);

        await client.PostAsJsonAsync($"/api/platform/organizations/{club.OrganizationId}/purge",
            new PurgeOrganizationRequest(club.Slug));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        // Запись о самом стирании не должна себя же уничтожить.
        Assert.True(await db.AuditRecords.AnyAsync(record =>
            record.OrganizationId == club.OrganizationId
            && record.Action == "platform.organizations.purge.completed"
            && record.Outcome == "Succeeded"));
        Assert.True(await db.AuditRecords.AnyAsync(record =>
            record.OrganizationId == club.OrganizationId
            && record.Action == "platform.organizations.purge.requested"));

        // А журнал действий сотрудников клуба — это персональные данные, он уходит вместе с людьми.
        Assert.False(await db.AuditRecords.AnyAsync(record =>
            record.OrganizationId == club.OrganizationId && record.ActorPlatformAdminUserId == null));
    }

    [Fact]
    public async Task Purge_RefusesAMistypedSlug()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var club = await SeedClubAsync(factory, dueDaysAgo: 1);

        var response = await client.PostAsJsonAsync($"/api/platform/organizations/{club.OrganizationId}/purge",
            new PurgeOrganizationRequest("not-this-club"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(OffboardingErrorCodes.SlugMismatch, await ReadCodeAsync(response));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await db.PlayerAccounts.AnyAsync(row => row.OrganizationId == club.OrganizationId));
    }

    [Fact]
    public async Task Purge_RefusesBeforeTheGracePeriodEnds()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var club = await SeedClubAsync(factory, dueDaysAgo: -5);

        var response = await client.PostAsJsonAsync($"/api/platform/organizations/{club.OrganizationId}/purge",
            new PurgeOrganizationRequest(club.Slug));

        // Срок — это время, за которое клуб успевает передумать и забрать выгрузку.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(OffboardingErrorCodes.PurgeNotDue, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Purge_RefusesAClubThatIsNotLeaving()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var club = await SeedClubAsync(factory, dueDaysAgo: 1, status: OrganizationStatusNames.Active);

        var response = await client.PostAsJsonAsync($"/api/platform/organizations/{club.OrganizationId}/purge",
            new PurgeOrganizationRequest(club.Slug));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(OffboardingErrorCodes.NotDeletionPending, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Purge_RefusesToRunTwice()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var club = await SeedClubAsync(factory, dueDaysAgo: 1);

        await client.PostAsJsonAsync($"/api/platform/organizations/{club.OrganizationId}/purge",
            new PurgeOrganizationRequest(club.Slug));
        var second = await client.PostAsJsonAsync($"/api/platform/organizations/{club.OrganizationId}/purge",
            new PurgeOrganizationRequest(club.Slug));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(OffboardingErrorCodes.OrganizationPurged, await ReadCodeAsync(second));
    }

    [Fact]
    public async Task Export_ReturnsAnArchiveAndLeavesATrail()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var club = await SeedClubAsync(factory, dueDaysAgo: 1);

        var response = await client.GetAsync($"/api/platform/organizations/{club.OrganizationId}/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
        Assert.Equal(
            ["players.csv", "products.csv", "reservations.csv", "sales.csv", "sessions.csv"],
            archive.Entries.Select(entry => entry.Name).OrderBy(name => name, StringComparer.Ordinal));

        var players = ReadEntry(archive, "players.csv");
        Assert.Contains("Игрок; с точкой с запятой", players);
        // Поле с разделителем обязано быть в кавычках, иначе оно разъезжается на две колонки.
        Assert.Contains("\"Игрок; с точкой с запятой\"", players);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        // Выгрузка — вынос персональных данных наружу, она обязана оставлять след.
        Assert.True(await db.AuditRecords.AnyAsync(record => record.Action == "platform.organizations.data.export"));
    }

    [Fact]
    public async Task Offboarding_RequiresItsOwnPermission()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);
        var club = await SeedClubAsync(factory, dueDaysAgo: 1);

        var purge = await client.PostAsJsonAsync($"/api/platform/organizations/{club.OrganizationId}/purge",
            new PurgeOrganizationRequest(club.Slug));
        var export = await client.GetAsync($"/api/platform/organizations/{club.OrganizationId}/export");

        Assert.Equal(HttpStatusCode.Forbidden, purge.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, export.StatusCode);
    }

    [Fact]
    public async Task Offboarding_ReportsWhetherPurgeIsAllowedYet()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var early = await SeedClubAsync(factory, dueDaysAgo: -5);
        var due = await SeedClubAsync(factory, dueDaysAgo: 1);

        var earlyState = await client.GetFromJsonAsync<OrganizationOffboardingDto>(
            $"/api/platform/organizations/{early.OrganizationId}/offboarding");
        var dueState = await client.GetFromJsonAsync<OrganizationOffboardingDto>(
            $"/api/platform/organizations/{due.OrganizationId}/offboarding");

        Assert.False(earlyState!.CanPurge);
        Assert.True(dueState!.CanPurge);
    }

    private static string ReadEntry(System.IO.Compression.ZipArchive archive, string name)
    {
        using var stream = archive.Entries.Single(entry => entry.Name == name).Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static async Task<OrganizationEntity> SeedClubAsync(
        PlatformApiFactory factory,
        int dueDaysAgo,
        string status = OrganizationStatusNames.DeletionPending)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();

        var organization = new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = "club-" + organizationId.ToString("N")[..8],
            Name = "Уходящий клуб",
            Status = status,
            PlanCode = "starter",
            SubscriptionStatus = SubscriptionStatusNames.Trial,
            PurgeEligibleAtUtc = Now.AddDays(-dueDaysAgo),
            CreatedAtUtc = Now.AddYears(-1),
            UpdatedAtUtc = Now
        };
        db.Organizations.Add(organization);
        db.Branches.Add(new BranchEntity
        {
            BranchId = branchId,
            OrganizationId = organizationId,
            Name = "Главный",
            CreatedAtUtc = Now
        });
        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId,
            OrganizationId = organizationId,
            BranchId = branchId,
            ZoneId = Guid.NewGuid(),
            Name = "PC-1",
            CreatedAtUtc = Now
        });
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = deviceId,
            OrganizationId = organizationId,
            BranchId = branchId,
            MachineName = "PC-1",
            DisplayName = "PC-1",
            EnrolledAtUtc = Now
        });
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = staffUserId,
            OrganizationId = organizationId,
            UserName = $"staff-{staffUserId:N}@club.test",
            NormalizedUserName = $"STAFF-{staffUserId:N}@CLUB.TEST",
            DisplayName = "Оператор",
            IsActive = true,
            CreatedAtUtc = Now
        });
        db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(),
            StaffUserId = staffUserId,
            OrganizationId = organizationId,
            BranchId = branchId,
            RoleName = OrganizationRoleNames.Operator
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId,
            OrganizationId = organizationId,
            HomeBranchId = branchId,
            // Имя с разделителем: проверка экранирования в выгрузке должна опираться на реальные
            // данные, а не на выдуманный «безопасный» случай.
            DisplayName = "Игрок; с точкой с запятой",
            PhoneNumber = "+992900000001",
            IsActive = true,
            CreatedAtUtc = Now
        });
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            PlayerAccountId = playerId,
            EntryType = "topup",
            CurrencyCode = "TJS",
            AmountMinorUnits = 5000,
            CreatedAtUtc = Now
        });
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            SeatId = seatId,
            DeviceId = deviceId,
            CreatedByStaffUserId = staffUserId,
            PlayerAccountId = playerId,
            State = "closed",
            RequestedAtUtc = Now.AddHours(-3),
            StartedAtUtc = Now.AddHours(-3),
            EndedAtUtc = Now.AddHours(-1)
        });
        db.Reservations.Add(new ReservationEntity
        {
            ReservationId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            SeatId = seatId,
            CustomerName = "Гость",
            PhoneNumber = "+992900000002",
            State = "confirmed",
            Source = "operator",
            StartsAtUtc = Now.AddDays(1),
            EndsAtUtc = Now.AddDays(1).AddHours(2),
            CreatedByStaffUserId = staffUserId,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        db.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            ShiftId = shiftId,
            CreatedByStaffUserId = staffUserId,
            State = "paid",
            CurrencyCode = "TJS",
            TotalMinorUnits = 1500,
            CreatedAtUtc = Now
        });
        db.PosProducts.Add(new PosProductEntity
        {
            ProductId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            CategoryId = Guid.NewGuid(),
            Name = "Кола",
            Sku = "COLA",
            CurrencyCode = "TJS",
            PriceMinorUnits = 500,
            IsActive = true,
            CreatedAtUtc = Now
        });
        db.NewsItems.Add(new NewsItemEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            Title = "Турнир",
            Body = "В субботу",
            IsPublished = true,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });

        // Финансовое и след платформы — то, что обязано пережить стирание.
        db.Invoices.Add(new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = organizationId,
            Number = 1,
            PeriodStartUtc = Now.AddMonths(-1),
            PeriodEndUtc = Now,
            AmountMinorUnits = 100000,
            CurrencyCode = "TJS",
            Status = "paid",
            IssuedAtUtc = Now.AddMonths(-1),
            DueAtUtc = Now,
            CreatedAtUtc = Now.AddMonths(-1),
            UpdatedAtUtc = Now
        });
        db.OrganizationSubscriptions.Add(new OrganizationSubscriptionEntity
        {
            OrganizationSubscriptionId = Guid.NewGuid(),
            OrganizationId = organizationId,
            PlanCode = "starter",
            Status = "active",
            CurrentPeriodStartUtc = Now.AddMonths(-1),
            CurrentPeriodEndUtc = Now.AddMonths(1),
            CreatedAtUtc = Now.AddYears(-1),
            UpdatedAtUtc = Now
        });
        db.SubscriptionDailySnapshots.Add(new SubscriptionDailySnapshotEntity
        {
            SubscriptionDailySnapshotId = Guid.NewGuid(),
            OrganizationId = organizationId,
            SnapshotDate = new DateOnly(2026, 8, 1),
            PlanCode = "starter",
            Status = "active",
            CreatedAtUtc = Now
        });
        db.OrganizationSupportNotes.Add(new OrganizationSupportNoteEntity
        {
            OrganizationSupportNoteId = Guid.NewGuid(),
            OrganizationId = organizationId,
            AuthorPlatformAdminUserId = Guid.NewGuid(),
            Body = "Клуб уходит",
            CreatedAtUtc = Now
        });

        // Аудит клуба (без актора-платформы) — уходит; аудит платформы — остаётся.
        db.AuditRecords.Add(new AuditRecordEntity
        {
            AuditRecordId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            ActorStaffUserId = staffUserId,
            Action = "pos.sale.paid",
            TargetType = "PosSale",
            Outcome = "Succeeded",
            SourceApp = "operator",
            CreatedAtUtc = Now
        });
        db.AuditRecords.Add(new AuditRecordEntity
        {
            AuditRecordId = Guid.NewGuid(),
            OrganizationId = organizationId,
            ActorPlatformAdminUserId = Guid.NewGuid(),
            Action = "platform.organizations.status.update",
            TargetType = "Organization",
            Outcome = "Succeeded",
            SourceApp = "platform",
            CreatedAtUtc = Now
        });

        await db.SaveChangesAsync();
        return organization;
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
