using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AFK4.Platform.Api.Tests.Identity;

/// <summary>
/// Самостоятельная регистрация: человек заводит себя сам, дома, без клуба и без администратора.
///
/// Главное свойство маршрута — не «работает», а «молчит»: ответ на знакомый и незнакомый номер
/// обязан совпадать байт в байт и по факту отправки SMS. Иначе приложение превращается в
/// справочник «кто где играет», и проверить это можно с любого телефона.
/// </summary>
public sealed class PlatformRegistrationEndpointTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-08-19T09:00:00Z");

    private sealed class RecordingSmsTransport : ISmsTransport
    {
        public List<SmsMessage> Sent { get; } = [];

        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>Часы, которые можно двигать: кулдаун и часовой лимит иначе не проверить.</summary>
    private static PlatformApiFactory FactoryWith(
        RecordingSmsTransport recording, MovableTimeProvider? time = null) =>
        new(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(recording);
            if (time is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(time);
            }
        });

    private static string CodeFrom(SmsMessage message) => message.Variables["code-1"];

    private static Task<HttpResponseMessage> StartAsync(HttpClient client, string phone) =>
        client.PostAsJsonAsync("/api/public/register/start", new RegistrationStartRequest(phone));

    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, string phone, string code) =>
        client.PostAsJsonAsync("/api/public/register/confirm", new RegistrationConfirmRequest(phone, code));

    private static async Task<PlatformPersonSessionResponse> RegisterAsync(
        PlatformApiFactory factory, HttpClient client, RecordingSmsTransport recording, string phone)
    {
        var started = await StartAsync(client, phone);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        var confirmed = await ConfirmAsync(client, phone, CodeFrom(recording.Sent[^1]));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        return (await confirmed.Content.ReadFromJsonAsync<PlatformPersonSessionResponse>())!;
    }

    private static void Authorize(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    // Разный ответ знакомому и незнакомому номеру — это и есть утечка «кто где играет». Сравниваем
    // не статусы, а полные тела: одинаковый код с разным телом ничего не спасает.
    [Fact]
    public async Task Start_AnswersByteForByte_TheSame_ForKnownAndUnknownNumbers()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        var known = await PlatformPersonTestData.AddPersonAsync(factory, "+992900001001");
        using var client = factory.CreateClient();

        var knownResponse = await StartAsync(client, known.PhoneNumber);
        var unknownResponse = await StartAsync(client, "+992900001002");

        Assert.Equal(knownResponse.StatusCode, unknownResponse.StatusCode);
        Assert.Equal(
            await knownResponse.Content.ReadAsStringAsync(),
            await unknownResponse.Content.ReadAsStringAsync());

        // И по факту отправки тоже: незнакомцу, которому SMS не ушла, ответ приходил бы быстрее.
        Assert.Equal(2, recording.Sent.Count);
    }

    [Fact]
    public async Task Confirm_OnAnUnknownNumber_CreatesThePerson_AndSignsHimIn()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();

        var session = await RegisterAsync(factory, client, recording, "+992900001010");

        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(session.RefreshToken));
        // Клуба у человека, зарегистрировавшегося дома, нет — и это нормальное состояние.
        Assert.Null(session.PlayerAccountId);
        Assert.Null(session.OrganizationId);
        // Имя ещё не спрошено: экран «как вас зовут» показывает клиент, а решает сервер.
        Assert.False(session.ProfileCompleted);
        Assert.True(session.PhoneVerified);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var person = await db.PlatformPersons.SingleAsync(p => p.PhoneNumber == "+992900001010");
        Assert.Equal(person.PlatformPersonId, session.PlatformPersonId);
        Assert.NotNull(person.PhoneVerifiedAtUtc);
        // PIN при регистрации не спрашивается — его задают позже и ровно в нужную секунду.
        Assert.Null(person.PinHash);
    }

    [Fact]
    public async Task Confirm_OnAKnownNumber_SignsIntoTheSamePerson_WithoutCreatingASecond()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        var existing = await PlatformPersonTestData.AddPersonAsync(factory, "+992900001020", "Фаррух");
        using var client = factory.CreateClient();

        var session = await RegisterAsync(factory, client, recording, existing.PhoneNumber);

        Assert.Equal(existing.PlatformPersonId, session.PlatformPersonId);
        Assert.True(session.ProfileCompleted);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await db.PlatformPersons.CountAsync(p => p.PhoneNumber == existing.PhoneNumber));
    }

    // У человека уже есть счёт в клубе — регистрация не заводит ему второго и не трогает деньги.
    [Fact]
    public async Task Confirm_OnAKnownNumber_LeavesHisClubsAlone()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        var existing = await PlatformPersonTestData.AddPersonAsync(factory, "+992900001025");
        var club = await PlatformPersonTestData.AddClubAsync(factory, existing.PlatformPersonId);
        using var client = factory.CreateClient();

        var session = await RegisterAsync(factory, client, recording, existing.PhoneNumber);
        Authorize(client, session.AccessToken);

        var me = await client.GetFromJsonAsync<MeDto>("/api/me");
        var only = Assert.Single(me!.Clubs);
        Assert.Equal(club.PlayerAccountId, only.PlayerAccountId);
    }

    [Fact]
    public async Task Confirm_WithAWrongCode_RegistersNobody()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();

        await StartAsync(client, "+992900001030");
        var confirmed = await ConfirmAsync(client, "+992900001030", "000000");

        Assert.Equal(HttpStatusCode.BadRequest, confirmed.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.PlatformPersons.AnyAsync(p => p.PhoneNumber == "+992900001030"));
    }

    [Fact]
    public async Task Confirm_WithoutAnyCode_RegistersNobody()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();

        var confirmed = await ConfirmAsync(client, "+992900001035", "123456");

        Assert.Equal(HttpStatusCode.Gone, confirmed.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.PlatformPersons.AnyAsync(p => p.PhoneNumber == "+992900001035"));
    }

    // Код одноразовый: второй раз тем же кодом не входят.
    [Fact]
    public async Task Confirm_Twice_WithTheSameCode_IsRefused()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();

        await StartAsync(client, "+992900001040");
        var code = CodeFrom(recording.Sent[^1]);

        Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(client, "+992900001040", code)).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await ConfirmAsync(client, "+992900001040", code)).StatusCode);
    }

    // Номер, который не может принадлежать никому, ничего о людях не выдаёт — про него можно
    // отвечать честно.
    [Fact]
    public async Task Start_WithNonsensePhone_IsRejected_AndSendsNothing()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();

        var response = await StartAsync(client, "не номер");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_phone", await response.Content.ReadAsStringAsync());
        Assert.Empty(recording.Sent);
    }

    // Второй запрос в ту же минуту — это «код уже у тебя», а не второй SMS за счёт клуба.
    [Fact]
    public async Task Start_Twice_WithinTheCooldown_SendsOnlyOneSms_ButAnswersTheSame()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();

        var first = await StartAsync(client, "+992900001050");
        var second = await StartAsync(client, "+992900001050");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(
            await first.Content.ReadAsStringAsync(),
            await second.Content.ReadAsStringAsync());
        Assert.Single(recording.Sent);
    }

    // Счётчик отправок сегодня висит на клубном счёте, которого у незнакомца нет. Если оставить
    // его там, регистрация станет бесплатной рассылкой SMS на любой номер за счёт клубов.
    [Fact]
    public async Task Start_LimitsSendsPerHour_EvenForANumberNobodyKnows()
    {
        var recording = new RecordingSmsTransport();
        var time = new MovableTimeProvider(Start);
        await using var factory = FactoryWith(recording, time);
        using var client = factory.CreateClient();

        var options = factory.Services.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<PhoneOtpOptions>>().Value;

        var bodies = new List<string>();
        for (var attempt = 0; attempt < options.MaxSendsPerHour + 2; attempt++)
        {
            var response = await StartAsync(client, "+992900001060");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            bodies.Add(await response.Content.ReadAsStringAsync());
            time.Advance(options.ResendCooldown + TimeSpan.FromSeconds(1));
        }

        Assert.Equal(options.MaxSendsPerHour, recording.Sent.Count);
        // Исчерпанный лимит выглядит ровно как отправленный код: иначе по нему считают чужие SMS.
        Assert.All(bodies, body => Assert.Equal(bodies[0], body));
    }

    // Час прошёл — счётчик отпускает, иначе номер выгорал бы навсегда.
    [Fact]
    public async Task Start_AllowsSendingAgain_AfterTheHourHasPassed()
    {
        var recording = new RecordingSmsTransport();
        var time = new MovableTimeProvider(Start);
        await using var factory = FactoryWith(recording, time);
        using var client = factory.CreateClient();

        var options = factory.Services.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<PhoneOtpOptions>>().Value;

        for (var attempt = 0; attempt < options.MaxSendsPerHour; attempt++)
        {
            await StartAsync(client, "+992900001070");
            time.Advance(options.ResendCooldown + TimeSpan.FromSeconds(1));
        }

        Assert.Equal(options.MaxSendsPerHour, recording.Sent.Count);

        time.Advance(TimeSpan.FromHours(1));
        await StartAsync(client, "+992900001070");

        Assert.Equal(options.MaxSendsPerHour + 1, recording.Sent.Count);
    }

    [Fact]
    public async Task Profile_SetsTheNameAndTheLanguage()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();

        var session = await RegisterAsync(factory, client, recording, "+992900001080");
        Authorize(client, session.AccessToken);

        var response = await client.PatchAsJsonAsync(
            "/api/me", new UpdateMyProfileRequest("Фаррух", "tg"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var person = await response.Content.ReadFromJsonAsync<MePersonDto>();
        Assert.Equal("Фаррух", person!.DisplayName);
        Assert.Equal("tg", person.PreferredLocale);

        var me = await client.GetFromJsonAsync<MeDto>("/api/me");
        Assert.Equal("Фаррух", me!.Person.DisplayName);
        Assert.Empty(me.Clubs);
    }

    [Fact]
    public async Task Profile_WithoutAToken_IsRefused()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            "/api/me", new UpdateMyProfileRequest("Фаррух", "ru"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithoutAName_IsRefused()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();

        var session = await RegisterAsync(factory, client, recording, "+992900001090");
        Authorize(client, session.AccessToken);

        var response = await client.PatchAsJsonAsync(
            "/api/me", new UpdateMyProfileRequest("   ", "ru"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Язык, которого у нас нет, тихо превратился бы в русский — а человек думал бы, что выбрал.
    [Fact]
    public async Task Profile_WithALanguageWeDoNotSpeak_IsRefused()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();

        var session = await RegisterAsync(factory, client, recording, "+992900001095");
        Authorize(client, session.AccessToken);

        var response = await client.PatchAsJsonAsync(
            "/api/me", new UpdateMyProfileRequest("Фаррух", "zz"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Час без клуба — и человек, зарегистрировавшийся дома, оказался бы выкинут обратно на SMS.
    [Fact]
    public async Task Refresh_KeepsAClublessPersonSignedIn()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        using var client = factory.CreateClient();

        var session = await RegisterAsync(factory, client, recording, "+992900001100");

        var refreshed = await client.PostAsJsonAsync(
            "/api/public/player/refresh", new PlayerRefreshRequest(session.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var next = await refreshed.Content.ReadFromJsonAsync<PlatformPersonSessionResponse>();
        Assert.Equal(session.PlatformPersonId, next!.PlatformPersonId);
        Assert.NotEqual(session.AccessToken, next.AccessToken);

        Authorize(client, next.AccessToken);
        var me = await client.GetFromJsonAsync<MeDto>("/api/me");
        Assert.Equal(session.PlatformPersonId, me!.Person.PlatformPersonId);
    }

    // Старое приложение читает у продления ровно те же восемь полей, что и вчера.
    [Fact]
    public async Task Refresh_StillCarriesTheClubFields_ForSomebodyWhoHasAClub()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900001110");
        var club = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);
        using var client = factory.CreateClient();

        var session = await RegisterAsync(factory, client, recording, person.PhoneNumber);

        var refreshed = await client.PostAsJsonAsync(
            "/api/public/player/refresh", new PlayerRefreshRequest(session.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var legacy = await refreshed.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        Assert.Equal(club.PlayerAccountId, legacy!.PlayerAccountId);
        Assert.Equal(club.OrganizationId, legacy.OrganizationId);
        Assert.False(string.IsNullOrWhiteSpace(legacy.AccessToken));
    }

    // Клуб закрыл человеку карточку — вход в платформу это не закрывает.
    [Fact]
    public async Task Confirm_ForADeactivatedPerson_IsRefused()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = FactoryWith(recording);
        var person = await PlatformPersonTestData.AddPersonAsync(
            factory, "+992900001120", isActive: false);
        using var client = factory.CreateClient();

        await StartAsync(client, person.PhoneNumber);
        var confirmed = await ConfirmAsync(client, person.PhoneNumber, CodeFrom(recording.Sent[^1]));

        Assert.Equal(HttpStatusCode.Forbidden, confirmed.StatusCode);
    }
}
