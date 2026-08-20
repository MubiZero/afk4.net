using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Reservations;

namespace AFK4.Platform.Api.Tests.Players;

/// <summary>
/// Второй рубеж приватности: кто вправе спросить. Клуб получает агрегат про человека, с которым
/// у него уже есть связь или живая заявка; про всех остальных — по точному номеру и не иначе.
/// Всё, что не «связь» и не «точный номер», обязано отвечать одинаково и ничего не выдавать.
/// </summary>
public sealed class ReputationAccessTests
{
    [Fact]
    public async Task LinkedPerson_IsAnsweredWithTheAggregate()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var personId = await ReputationTestData.AddPersonAsync(factory, "+992900000101");
        await ReputationTestData.AddAccountAsync(factory, TestIds.OrganizationId, TestIds.BranchId, personId);
        await ReputationTestData.AddSnapshotAsync(factory, personId, visits: 14, noShows: 0);

        var response = await client.GetAsync(ReputationTestData.ReputationRoute(personId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PlayerReputationDto>();
        Assert.NotNull(body);
        Assert.Equal(14, body.NetworkVisits);
        Assert.Equal(0, body.NetworkNoShows);
        Assert.False(body.NetworkBanned);
        Assert.Equal(ReputationTestData.SnapshotAt, body.CalculatedAtUtc);
    }

    /// <summary>
    /// Гость позвонил, его записали по одному номеру — счёта у него ещё нет, а заявка живая.
    /// Это второе законное основание спросить, и оно не сводится к связи.
    /// </summary>
    [Fact]
    public async Task LiveRequestByPhone_IsAnsweredWithTheAggregate()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var personId = await ReputationTestData.AddPersonAsync(factory, "+992900000102");
        await ReputationTestData.AddSnapshotAsync(factory, personId, visits: 3, noShows: 1);
        await ReputationTestData.AddLiveRequestByPhoneAsync(
            factory, TestIds.OrganizationId, TestIds.BranchId, "+992 90 000-01-02");

        var response = await client.GetAsync(ReputationTestData.ReputationRoute(personId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PlayerReputationDto>();
        Assert.Equal(3, body!.NetworkVisits);
        Assert.Equal(1, body.NetworkNoShows);
    }

    /// <summary>Заявка, которую уже сняли, основанием не остаётся: спрашивать больше не о чем.</summary>
    [Fact]
    public async Task CancelledRequest_IsNoLongerABasis()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var personId = await ReputationTestData.AddPersonAsync(factory, "+992900000103");
        await ReputationTestData.AddSnapshotAsync(factory, personId, visits: 9, noShows: 0);
        await ReputationTestData.AddLiveRequestByPhoneAsync(
            factory, TestIds.OrganizationId, TestIds.BranchId, "+992900000103",
            state: ReservationStateNames.Cancelled);

        var response = await client.GetAsync(ReputationTestData.ReputationRoute(personId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Ключевое свойство: «человека нет» и «человек есть, но он не наш» отвечают одним и тем же.
    /// Иначе перебор идентификаторов превращается в справочник чужой клиентуры.
    /// </summary>
    [Fact]
    public async Task StrangerAndNobody_AnswerIdentically()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var strangerId = await ReputationTestData.AddPersonAsync(factory, "+992900000104");
        var (otherOrganizationId, otherBranchId) = await ReputationTestData.AddOtherClubAsync(factory);
        await ReputationTestData.AddAccountAsync(factory, otherOrganizationId, otherBranchId, strangerId);
        await ReputationTestData.AddSnapshotAsync(factory, strangerId, visits: 42, noShows: 7);

        var stranger = await ReadAsync(client, ReputationTestData.ReputationRoute(strangerId));
        var nobody = await ReadAsync(client, ReputationTestData.ReputationRoute(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, stranger.Status);
        Assert.Equal(nobody, stranger);
    }

    [Fact]
    public async Task StaffWithoutViewPlayers_IsRefusedBeforeAnythingIsLookedUp()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        var personId = await ReputationTestData.AddPersonAsync(factory, "+992900000105");
        await ReputationTestData.AddAccountAsync(factory, TestIds.OrganizationId, TestIds.BranchId, personId);

        var byId = await client.GetAsync(ReputationTestData.ReputationRoute(personId));
        var byPhone = await client.PostAsJsonAsync(
            ReputationTestData.LookupRoute(), new PlayerReputationLookupRequest("+992900000105"));

        Assert.Equal(HttpStatusCode.Forbidden, byId.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byPhone.StatusCode);
    }

    /// <summary>
    /// Поддержка платформы ходит в клуб по сессионному токену и только в помеченные маршруты.
    /// Репутация не помечена намеренно: чужая клиентура — не предмет обращения в поддержку.
    /// </summary>
    [Fact]
    public async Task PlatformSupportSession_DoesNotReachReputation()
    {
        await using var factory = new PlatformApiFactory();
        var session = await SupportAccessTestHelper.OpenSessionAsync(factory);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            PlatformSupportAccessGrantService.GrantHeaderName, session.SessionToken);

        var personId = await ReputationTestData.AddPersonAsync(factory, "+992900000106");
        await ReputationTestData.AddAccountAsync(
            factory, session.OrganizationId, session.BranchId, personId);

        var response = await client.GetAsync(
            $"/api/organizations/{session.OrganizationId:D}/branches/{session.BranchId:D}/players/reputation/{personId:D}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Спрос по точному номеру. Незнакомый сети номер отвечает ровно тем же, чем зарегистрированный
    /// без единого визита: иначе публичная регистрация становится справочником «кто играет в сети».
    /// </summary>
    [Fact]
    public async Task UnknownNumberAndFreshPerson_AnswerIdentically()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        // У этого человека снимок есть — иначе таблица была бы пуста и «на когда» отвечало бы
        // запасное значение, одинаковое просто потому, что снимков нет вовсе.
        var veteranId = await ReputationTestData.AddPersonAsync(factory, "+992900000107");
        await ReputationTestData.AddSnapshotAsync(factory, veteranId, visits: 31, noShows: 4);

        await ReputationTestData.AddPersonAsync(factory, "+992900000108");

        var fresh = await LookupAsync(client, "+992900000108");
        var unknown = await LookupAsync(client, "+992900000909");

        Assert.Equal(HttpStatusCode.OK, fresh.Status);
        Assert.Equal(unknown, fresh);
    }

    [Fact]
    public async Task ExactNumber_IsAnsweredWithTheAggregateEvenWithoutAnyTie()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var personId = await ReputationTestData.AddPersonAsync(factory, "+992900000110", networkBanned: true);
        await ReputationTestData.AddSnapshotAsync(factory, personId, visits: 14, noShows: 2);

        var response = await client.PostAsJsonAsync(
            ReputationTestData.LookupRoute(), new PlayerReputationLookupRequest("+992 90 000-01-10"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PlayerReputationDto>();
        Assert.Equal(14, body!.NetworkVisits);
        Assert.Equal(2, body.NetworkNoShows);
        Assert.True(body.NetworkBanned);
    }

    /// <summary>
    /// Через платформу ищут только по целому номеру. Огрызок номера — не номер: он не может
    /// принадлежать никому, поэтому отличающийся ответ на него ничего ни о ком не выдаёт.
    /// Поиск по части номера остаётся тем, чем был, — поиском по своим игрокам внутри клуба.
    /// </summary>
    [Theory]
    [InlineData("99000")]
    [InlineData("+992 900")]
    [InlineData("")]
    public async Task PartialNumber_IsNotASearch(string partial)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        await ReputationTestData.AddPersonAsync(factory, "+992900000111");

        var response = await client.PostAsJsonAsync(
            ReputationTestData.LookupRoute(), new PlayerReputationLookupRequest(partial));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Перебор номеров упирается в ту же ручку, что и вход с регистрацией: маршрутную политику
    /// лимита. Без неё аудит фиксировал бы перебор, но не мешал бы ему.
    /// </summary>
    [Fact]
    public async Task NumberProbing_RunsOutOfPermits()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var statuses = new List<HttpStatusCode>();
        for (var index = 0; index < 40; index++)
        {
            var response = await client.PostAsJsonAsync(
                ReputationTestData.LookupRoute(),
                new PlayerReputationLookupRequest($"+9929000012{index:D2}"));
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    private sealed record Answer(HttpStatusCode Status, string Body);

    private static async Task<Answer> ReadAsync(HttpClient client, string route)
    {
        var response = await client.GetAsync(route);
        return new Answer(response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<Answer> LookupAsync(HttpClient client, string phone)
    {
        var response = await client.PostAsJsonAsync(
            ReputationTestData.LookupRoute(), new PlayerReputationLookupRequest(phone));
        return new Answer(response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
