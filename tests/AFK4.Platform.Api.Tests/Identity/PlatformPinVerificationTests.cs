using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AFK4.Platform.Api.Tests.Identity;

/// <summary>
/// Самопосадка за игровой ПК. Маршрут остался прежним — `POST /api/public/player/sign-in`, — но
/// проверяет он теперь сетевой PIN личности, а не клубный хеш: PIN принадлежит человеку и работает
/// во всех клубах сети.
///
/// Главное свойство отказа — неразличимость. «Нет такого номера», «PIN не задан», «PIN неверен»,
/// «личность закрыта» и «блокировка» отвечают одним и тем же пустым `401`: иначе по экрану игрового
/// ПК можно проверять, у кого в сети есть аккаунт.
/// </summary>
public sealed class PlatformPinVerificationTests
{
    private const string Phone = "+992900000601";

    private sealed class MovableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now = now.Add(by);
    }

    [Fact]
    public async Task NetworkPin_SignsThePlayerIn_AndTheTokenWorks()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, Phone);
        var club = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);
        await SetPinAsync(factory, person.PlatformPersonId, "1234");

        using var client = factory.CreateClient();
        var response = await SignInAsync(client, club.OrganizationId, Phone, "1234");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<PlatformPersonSessionResponse>();
        Assert.Equal(club.PlayerAccountId, session!.PlayerAccountId);
        Assert.Equal(club.OrganizationId, session.OrganizationId);
        Assert.Equal(person.PlatformPersonId, session.PlatformPersonId);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var me = await client.GetFromJsonAsync<MeDto>("/api/me");
        Assert.Equal(person.PlatformPersonId, me!.Person.PlatformPersonId);
    }

    // Тот же PIN работает в клубе, где человек никогда не был: связь открывается первым же
    // действием, как в Ф3, а не заводится администратором заранее.
    [Fact]
    public async Task NetworkPin_OpensTheClubAccountWhenThereIsNone()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, Phone);
        var (organizationId, branchId) = await PlatformPersonTestData.AddClubWithoutAccountsAsync(factory);
        await SetPinAsync(factory, person.PlatformPersonId, "1234");

        using var client = factory.CreateClient();
        var response = await SignInAsync(client, organizationId, Phone, "1234");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var opened = await db.PlayerAccounts.SingleAsync(
            account => account.OrganizationId == organizationId);
        Assert.Equal(person.PlatformPersonId, opened.PlatformPersonId);
        Assert.Equal(branchId, opened.HomeBranchId);
        Assert.True(opened.CreatedFromApp);
    }

    // Клубный PIN мёртв с первой минуты, а не «пока живёт»: иначе админ одного клуба остаётся с
    // ключом от чужих клубов.
    [Fact]
    public async Task ClubPasswordHash_IsNeverAccepted()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, Phone);
        var club = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);
        await AddClubCredentialAsync(factory, club, "1234");

        using var client = factory.CreateClient();
        var response = await SignInAsync(client, club.OrganizationId, Phone, "1234");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FiveWrongAttempts_LockThePersonForFifteenMinutes()
    {
        var time = new MovableTimeProvider(PlatformPersonTestData.Now);
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(time);
        });
        var person = await PlatformPersonTestData.AddPersonAsync(factory, Phone);
        var club = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);
        await SetPinAsync(factory, person.PlatformPersonId, "1234");

        using var client = factory.CreateClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await SignInAsync(client, club.OrganizationId, Phone, "0000")).StatusCode);
        }

        var locked = await ReadPersonAsync(factory, person.PlatformPersonId);
        Assert.Equal(5, locked.PinFailedCount);
        Assert.NotNull(locked.PinLockedUntilUtc);

        // Верный PIN во время блокировки тоже не пускает — иначе блокировки нет.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SignInAsync(client, club.OrganizationId, Phone, "1234")).StatusCode);

        time.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(
            HttpStatusCode.OK,
            (await SignInAsync(client, club.OrganizationId, Phone, "1234")).StatusCode);
        Assert.Equal(0, (await ReadPersonAsync(factory, person.PlatformPersonId)).PinFailedCount);
    }

    [Fact]
    public async Task CorrectPin_ResetsTheFailureCounter()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, Phone);
        var club = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);
        await SetPinAsync(factory, person.PlatformPersonId, "1234");

        using var client = factory.CreateClient();
        await SignInAsync(client, club.OrganizationId, Phone, "0000");
        await SignInAsync(client, club.OrganizationId, Phone, "1234");

        Assert.Equal(0, (await ReadPersonAsync(factory, person.PlatformPersonId)).PinFailedCount);
    }

    // Любая причина отказа выглядит снаружи одинаково: тело, статус и заголовки совпадают.
    [Fact]
    public async Task EveryRefusal_LooksExactlyTheSame()
    {
        await using var factory = new PlatformApiFactory();
        var (organizationId, _) = await PlatformPersonTestData.AddClubWithoutAccountsAsync(factory);

        var withPin = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000611");
        await SetPinAsync(factory, withPin.PlatformPersonId, "1234");

        var withoutPin = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000612");

        var closed = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000613", isActive: false);
        await SetPinAsync(factory, closed.PlatformPersonId, "1234");

        var banned = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000614");
        await SetPinAsync(factory, banned.PlatformPersonId, "1234");
        await BanAsync(factory, banned.PlatformPersonId);

        using var client = factory.CreateClient();
        var refusals = new List<HttpResponseMessage>
        {
            await SignInAsync(client, organizationId, "+992900000699", "1234"), // номера нет вовсе
            await SignInAsync(client, organizationId, "+992900000612", "1234"), // PIN не задан
            await SignInAsync(client, organizationId, "+992900000611", "9999"), // PIN неверен
            await SignInAsync(client, organizationId, "+992900000613", "1234"), // личность закрыта
            await SignInAsync(client, organizationId, "+992900000614", "1234"), // сетевой запрет
        };

        foreach (var refusal in refusals)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, refusal.StatusCode);
            Assert.Equal(string.Empty, await refusal.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task PhoneNumber_IsMatchedInItsNormalisedForm()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, Phone);
        var club = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);
        await SetPinAsync(factory, person.PlatformPersonId, "1234");

        using var client = factory.CreateClient();

        Assert.Equal(
            HttpStatusCode.OK,
            (await SignInAsync(client, club.OrganizationId, "992 900 000 601", "1234")).StatusCode);
    }

    private static Task<HttpResponseMessage> SignInAsync(
        HttpClient client, Guid organizationId, string phone, string pin) =>
        client.PostAsJsonAsync(
            "/api/public/player/sign-in", new PlayerSignInRequest(organizationId, phone, pin));

    private static async Task SetPinAsync(
        PlatformApiFactory factory, Guid platformPersonId, string pin)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var person = await db.PlatformPersons.SingleAsync(
            candidate => candidate.PlatformPersonId == platformPersonId);
        person.PinHash = new PasswordHasher<PlatformPersonEntity>().HashPassword(person, pin);
        person.PinSetAtUtc = PlatformPersonTestData.Now;
        await db.SaveChangesAsync();
    }

    private static async Task BanAsync(PlatformApiFactory factory, Guid platformPersonId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var person = await db.PlatformPersons.SingleAsync(
            candidate => candidate.PlatformPersonId == platformPersonId);
        person.NetworkBanAtUtc = PlatformPersonTestData.Now;
        person.NetworkBanReason = "перебор чужих карт";
        await db.SaveChangesAsync();
    }

    private static async Task AddClubCredentialAsync(
        PlatformApiFactory factory, PlayerAccountEntity account, string pin)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var credential = new PlayerCredentialEntity
        {
            PlayerCredentialId = Guid.NewGuid(),
            PlayerAccountId = account.PlayerAccountId,
            OrganizationId = account.OrganizationId,
            PhoneVerified = true,
            CreatedAtUtc = PlatformPersonTestData.Now,
            UpdatedAtUtc = PlatformPersonTestData.Now
        };
        credential.PasswordHash = new PasswordHasher<PlayerCredentialEntity>().HashPassword(credential, pin);
        db.PlayerCredentials.Add(credential);
        await db.SaveChangesAsync();
    }

    private static async Task<PlatformPersonEntity> ReadPersonAsync(
        PlatformApiFactory factory, Guid platformPersonId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.PlatformPersons.AsNoTracking().SingleAsync(
            candidate => candidate.PlatformPersonId == platformPersonId);
    }
}
