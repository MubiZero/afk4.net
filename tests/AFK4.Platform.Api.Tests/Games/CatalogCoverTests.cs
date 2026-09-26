using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Media;
using AFK4.Platform.Api.Tests.Fakes;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Ads;
using AFK4.Shared.Contracts.Games;
using AFK4.Shared.Contracts.Media;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Games;

/// <summary>
/// Обложки каталога: сами из Steam (владелец, 2026-09-26: «пусть подтягивается красивая картинка»)
/// и загрузкой в хранилище платформы — без ссылок на чужие сайты.
/// </summary>
public sealed class CatalogCoverTests
{
    private static UpsertCatalogGameRequest Game(string kind = GameLaunchKindNames.Steam, string? target = null, string? cover = null) =>
        new("Counter-Strike 2", "Командный шутер.", "Шутер", 16, kind, target, cover, true);

    [Fact]
    public async Task ASteamGameWithoutACover_GetsTheStoreImage_CopiedIntoOurStorage()
    {
        await using var factory = new PlatformApiFactory();
        using var platform = factory.CreateClient();
        await SignInAsCatalogEditorAsync(factory, platform);
        var appId = NewAppId();
        Steam(factory).Known[appId] = FakeSteamCoverSource.Jpeg;

        var game = await CreateAsync(platform, Game(target: appId));

        Assert.StartsWith($"https://media.test/platform/catalog-cover/steam-{appId}-", game.CoverUrl);
        Assert.EndsWith(".jpg", game.CoverUrl);
        Assert.Single(StoredCoversOf(factory, appId));
    }

    // Обложку, которую выбрали руками, Steam не перебивает; не Steam — не у кого спрашивать.
    [Fact]
    public async Task AChosenCover_OrAGameOutsideSteam_IsLeftAlone()
    {
        await using var factory = new PlatformApiFactory();
        using var platform = factory.CreateClient();
        await SignInAsCatalogEditorAsync(factory, platform);
        var appId = NewAppId();
        Steam(factory).Known[appId] = FakeSteamCoverSource.Jpeg;

        var chosen = await CreateAsync(platform, Game(target: appId, cover: "https://media.test/platform/catalog-cover/own.webp"));
        var local = await CreateAsync(platform, Game(kind: GameLaunchKindNames.Executable, target: null) with { Name = "Minecraft" });

        Assert.Equal("https://media.test/platform/catalog-cover/own.webp", chosen.CoverUrl);
        Assert.Null(local.CoverUrl);
        Assert.Empty(StoredCoversOf(factory, appId));
    }

    // Steam не знает игру или не ответил — игра всё равно создаётся, просто без картинки.
    [Fact]
    public async Task AnUnknownSteamGame_IsStillCreated_WithoutACover()
    {
        await using var factory = new PlatformApiFactory();
        using var platform = factory.CreateClient();
        await SignInAsCatalogEditorAsync(factory, platform);

        var game = await CreateAsync(platform, Game(target: NewAppId()));

        Assert.Null(game.CoverUrl);
    }

    [Fact]
    public async Task EditingASteamGameWithoutACover_PullsTheImageToo()
    {
        await using var factory = new PlatformApiFactory();
        using var platform = factory.CreateClient();
        await SignInAsCatalogEditorAsync(factory, platform);
        var appId = NewAppId();
        var game = await CreateAsync(platform, Game(target: appId) with { Name = "Dota 2" });
        Assert.Null(game.CoverUrl);

        Steam(factory).Known[appId] = FakeSteamCoverSource.Jpeg;
        var response = await platform.PutAsJsonAsync($"/api/platform/games/{game.CatalogGameId:D}", Game(target: appId) with { Name = "Dota 2" });

        var updated = (await response.Content.ReadFromJsonAsync<CatalogGameDto>())!;
        Assert.StartsWith($"https://media.test/platform/catalog-cover/steam-{appId}-", updated.CoverUrl);
    }

    [Fact]
    public async Task TheSteamButton_ReturnsTheStoredImage_OrSaysSteamHasNone()
    {
        await using var factory = new PlatformApiFactory();
        using var platform = factory.CreateClient();
        await SignInAsCatalogEditorAsync(factory, platform);
        var appId = NewAppId();
        Steam(factory).Known[appId] = FakeSteamCoverSource.Jpeg;

        var found = await platform.PostAsJsonAsync(PlatformMediaRoutes.SteamCover, new SteamCoverRequest($" {appId} "));
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.StartsWith($"https://media.test/platform/catalog-cover/steam-{appId}-",
            (await found.Content.ReadFromJsonAsync<PlatformMediaUploadedDto>())!.Url);

        Assert.Equal(HttpStatusCode.NotFound,
            (await platform.PostAsJsonAsync(PlatformMediaRoutes.SteamCover, new SteamCoverRequest(NewAppId()))).StatusCode);
        // Номер приложения — только цифры: адрес Steam собирается из него, и ничего другого туда не попадёт.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await platform.PostAsJsonAsync(PlatformMediaRoutes.SteamCover, new SteamCoverRequest("730/../../x"))).StatusCode);
    }

    [Fact]
    public async Task APlatformImage_IsUploaded_WhenItIsReallyAnImage()
    {
        await using var factory = new PlatformApiFactory();
        using var platform = factory.CreateClient();
        await SignInAsCatalogEditorAsync(factory, platform);

        var uploaded = await UploadAsync(platform, PlatformMediaPurposeNames.CatalogCover, FakeSteamCoverSource.Jpeg);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        var url = (await uploaded.Content.ReadFromJsonAsync<PlatformMediaUploadedDto>())!.Url;
        Assert.Matches("^https://media.test/platform/catalog-cover/[0-9a-f]{32}\\.jpg$", url);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await UploadAsync(platform, PlatformMediaPurposeNames.CatalogCover, "<svg onload=alert(1)>"u8.ToArray())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await UploadAsync(platform, "club-logo", FakeSteamCoverSource.Jpeg)).StatusCode);
    }

    // Картинку рекламы сервер копирует себе с пределом в 2 МБ — крупную не берём уже на входе,
    // иначе она упала бы только при одобрении.
    [Fact]
    public async Task AnAdImage_IsHeldToTheAdLimit()
    {
        await using var factory = new PlatformApiFactory();
        using var platform = factory.CreateClient();
        await SignInAsCatalogEditorAsync(factory, platform);
        var large = new byte[AdLimits.ImageMaxBytes + 1];
        FakeSteamCoverSource.Jpeg.CopyTo(large, 0);

        var response = await UploadAsync(platform, PlatformMediaPurposeNames.AdCreative, large);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(PlatformMediaErrorCodeNames.TooLarge, await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(platform, PlatformMediaPurposeNames.CatalogCover, large)).StatusCode);
    }

    [Fact]
    public async Task Support_UploadsNothing()
    {
        await using var factory = new PlatformApiFactory();
        using var platform = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, platform, roles: [PlatformAdminRoleNames.PlatformSupport]);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await UploadAsync(platform, PlatformMediaPurposeNames.CatalogCover, FakeSteamCoverSource.Jpeg)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await UploadAsync(platform, PlatformMediaPurposeNames.AdCreative, FakeSteamCoverSource.Jpeg)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await platform.PostAsJsonAsync(PlatformMediaRoutes.SteamCover, new SteamCoverRequest("730"))).StatusCode);
    }

    private static FakeSteamCoverSource Steam(PlatformApiFactory factory) =>
        (FakeSteamCoverSource)factory.Services.GetRequiredService<ISteamCoverSource>();

    private static FakeMediaStorage Storage(PlatformApiFactory factory) =>
        (FakeMediaStorage)factory.Services.GetRequiredService<IMediaStorage>();

    // Клиент создаёт сам тест: хост общий, а база теста выбирается через AsyncLocal, который из
    // async-помощника обратно в тест не возвращается.
    private static Task SignInAsCatalogEditorAsync(PlatformApiFactory factory, HttpClient client) =>
        PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

    // Хост и его подставные Steam и хранилище общие на все тесты — у каждого теста свой номер игры.
    private static string NewAppId() => Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString();

    private static IEnumerable<string> StoredCoversOf(PlatformApiFactory factory, string appId) =>
        Storage(factory).Objects.Keys.Where(key => key.StartsWith($"platform/catalog-cover/steam-{appId}-"));

    private static async Task<CatalogGameDto> CreateAsync(HttpClient client, UpsertCatalogGameRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/platform/games", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CatalogGameDto>())!;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string purpose, byte[] bytes)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var form = new MultipartFormDataContent { { new StringContent(purpose), "purpose" }, { file, "file", "cover.jpg" } };
        return client.PostAsync(PlatformMediaRoutes.Upload, form);
    }
}
