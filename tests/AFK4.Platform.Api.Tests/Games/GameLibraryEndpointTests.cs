using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Tests.Devices;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Games;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Games;

/// <summary>Каталог игр платформы, библиотека филиала и выдача её агенту (спека оболочки, §6.6).</summary>
public sealed class GameLibraryEndpointTests
{
    private static string Library => $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/games";

    private static UpsertCatalogGameRequest Cs2(bool published = true, string? cover = "https://media.afk4.net/games/cs2.webp") =>
        new("Counter-Strike 2", "Командный шутер.", "Шутер", 16, GameLaunchKindNames.Steam, "730", cover, published);

    private static UpsertBranchGameRequest Own(
        string name = "Dota 2",
        string kind = GameLaunchKindNames.Steam,
        string? target = "570",
        string? path = null,
        string? arguments = null,
        Guid? catalogGameId = null,
        bool enabled = true) =>
        new(TestIds.OrganizationId, catalogGameId, name, "MOBA", 12, kind, target, path, arguments, false, enabled);

    [Fact]
    public async Task ThePlatform_KeepsTheCatalog_AndAClubSeesOnlyWhatIsPublished()
    {
        await using var factory = new PlatformApiFactory();
        using var platform = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, platform, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        using var club = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, club, OrganizationRoleNames.OrganizationOwner);

        var published = await CreateCatalogAsync(platform, Cs2());
        await CreateCatalogAsync(platform, Cs2(published: false) with { Name = "Черновик" });

        var catalog = await club.GetFromJsonAsync<CatalogGameDto[]>($"/api/organizations/{TestIds.OrganizationId:D}/game-catalog?query=counter");
        var game = Assert.Single(catalog!);
        Assert.Equal(published.CatalogGameId, game.CatalogGameId);
        Assert.Equal(2, (await platform.GetFromJsonAsync<CatalogGameDto[]>("/api/platform/games"))!.Length);
    }

    [Fact]
    public async Task PlatformSupport_CannotEditTheCatalog()
    {
        await using var factory = new PlatformApiFactory();
        using var platform = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, platform, roles: [PlatformAdminRoleNames.PlatformSupport]);

        Assert.Equal(HttpStatusCode.Forbidden, (await platform.PostAsJsonAsync("/api/platform/games", Cs2())).StatusCode);
    }

    // Игра из каталога: обложка и возраст — каталожные; поменял их каталог — ПК клуба перечитают.
    [Fact]
    public async Task AGameFromTheCatalog_FollowsTheCatalogsCover()
    {
        await using var factory = new PlatformApiFactory();
        using var platform = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, platform, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        using var club = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, club, OrganizationRoleNames.Technician);
        var cs2 = await CreateCatalogAsync(platform, Cs2());

        var added = await club.PostAsJsonAsync(Library, Own("CS2", target: null, catalogGameId: cs2.CatalogGameId));
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        var game = (await added.Content.ReadFromJsonAsync<BranchGameDto>())!;
        Assert.Equal("730", game.LaunchTarget);
        Assert.Equal(16, game.MinAge);
        Assert.Equal(cs2.CoverUrl, game.CoverUrl);

        await platform.PutAsJsonAsync($"/api/platform/games/{cs2.CatalogGameId:D}", Cs2(cover: "https://media.afk4.net/games/cs2-new.webp"));

        var library = await club.GetFromJsonAsync<BranchGameDto[]>(Library);
        Assert.Equal("https://media.afk4.net/games/cs2-new.webp", Assert.Single(library!).CoverUrl);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(2, (await db.BranchGameLibraries.SingleAsync()).Version);
    }

    [Fact]
    public async Task TheLibrary_KeepsItsOrder_AndEveryChangeRaisesTheVersion()
    {
        await using var factory = new PlatformApiFactory();
        using var club = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, club, OrganizationRoleNames.OrganizationOwner);

        var dota = await AddAsync(club, Own());
        var game = await AddAsync(club, Own("Своя игра", GameLaunchKindNames.Executable, null, @"C:\Games\Own\own.exe", "-fullscreen"));

        var reordered = await club.PutAsJsonAsync($"{Library}/order", new ReorderBranchGamesRequest(TestIds.OrganizationId, [game.BranchGameId, dota.BranchGameId]));
        Assert.Equal(HttpStatusCode.OK, reordered.StatusCode);
        Assert.Equal([game.BranchGameId, dota.BranchGameId], (await club.GetFromJsonAsync<BranchGameDto[]>(Library))!.Select(item => item.BranchGameId));

        Assert.Equal(HttpStatusCode.NoContent, (await club.DeleteAsync($"{Library}/{dota.BranchGameId:D}")).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(4, (await db.BranchGameLibraries.SingleAsync()).Version);
        Assert.True(await db.AuditRecords.AnyAsync(record => record.Action == AuditActionNames.RemoveBranchGame));
    }

    // Порядок — всех игр сразу: частичный список перемешал бы библиотеку.
    [Fact]
    public async Task AnIncompleteOrder_IsRefused()
    {
        await using var factory = new PlatformApiFactory();
        using var club = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, club, OrganizationRoleNames.OrganizationOwner);
        var dota = await AddAsync(club, Own());
        await AddAsync(club, Own("CS2", target: "730"));

        var response = await club.PutAsJsonAsync($"{Library}/order", new ReorderBranchGamesRequest(TestIds.OrganizationId, [dota.BranchGameId]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public static TheoryData<UpsertBranchGameRequest> Nonsense => new()
    {
        Own(name: " "),
        Own(target: "cs2"),
        Own(kind: "uplay"),
        Own(kind: GameLaunchKindNames.Epic, target: "Fortnite && del"),
        Own(kind: GameLaunchKindNames.Executable, target: null, path: null),
        Own(kind: GameLaunchKindNames.Executable, target: null, path: @"\\server\share\game.exe"),
        Own(kind: GameLaunchKindNames.Executable, target: null, path: @"C:\Games\game.bat"),
        Own(arguments: "-a\n-b"),
        Own() with { MinAge = 30 }
    };

    // Из Панели не должен приехать запуск, который сломает командную строку на всём зале.
    [Theory]
    [MemberData(nameof(Nonsense))]
    public async Task ANonsenseGame_IsRefused(UpsertBranchGameRequest request)
    {
        await using var factory = new PlatformApiFactory();
        using var club = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, club, OrganizationRoleNames.OrganizationOwner);

        Assert.Equal(HttpStatusCode.BadRequest, (await club.PostAsJsonAsync(Library, request)).StatusCode);
    }

    [Fact]
    public async Task AnOperator_CannotChangeTheLibrary()
    {
        await using var factory = new PlatformApiFactory();
        using var club = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, club, OrganizationRoleNames.Operator);

        Assert.Equal(HttpStatusCode.Forbidden, (await club.PostAsJsonAsync(Library, Own())).StatusCode);
    }

    // Агент узнаёт о правке из сердцебиения и забирает библиотеку своим ключом — только включённое.
    [Fact]
    public async Task TheAgent_SeesTheVersion_AndReadsOnlyEnabledGamesWithItsKey()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        Assert.Equal(0, (await fixture.HeartbeatAsync()).GameLibraryVersion);

        await AddAsync(fixture.Client, Own());
        await AddAsync(fixture.Client, Own("Выключенная", target: "440", enabled: false));
        Assert.Equal(2, (await fixture.HeartbeatAsync()).GameLibraryVersion);

        var message = new HttpRequestMessage(HttpMethod.Get, GameLibraryRoutes.DeviceLibrary(fixture.Device.DeviceId, fixture.Device.OrganizationId, fixture.Device.BranchId));
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, fixture.Device.CredentialSecret);
        var response = await fixture.Client.SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var library = (await response.Content.ReadFromJsonAsync<DeviceGameLibraryDto>())!;
        Assert.Equal(2, library.Version);
        var game = Assert.Single(library.Games);
        Assert.Equal("Dota 2", game.DisplayName);
        Assert.Equal("570", game.LaunchTarget);

        var stranger = new HttpRequestMessage(HttpMethod.Get, GameLibraryRoutes.DeviceLibrary(fixture.Device.DeviceId, fixture.Device.OrganizationId, fixture.Device.BranchId));
        stranger.Headers.Add(DeviceCredentialHeaders.CredentialSecret, "не-тот-ключ");
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.SendAsync(stranger)).StatusCode);
    }

    private static async Task<CatalogGameDto> CreateCatalogAsync(HttpClient client, UpsertCatalogGameRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/platform/games", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CatalogGameDto>())!;
    }

    private static async Task<BranchGameDto> AddAsync(HttpClient client, UpsertBranchGameRequest request)
    {
        var response = await client.PostAsJsonAsync(Library, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BranchGameDto>())!;
    }
}
