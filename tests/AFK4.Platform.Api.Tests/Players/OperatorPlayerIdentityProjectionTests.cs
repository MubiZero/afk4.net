using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Players;
using AFK4.Platform.Api.Tests.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Players;

/// <summary>
/// Кого стойка видит перед собой. Идентификатор личности едет в тех проекциях, где у клуба и так
/// есть основание спросить сеть, — иначе админке пришлось бы спрашивать репутацию по номеру и
/// писать телефон в аудит там, где основание очевидно. Пометка «из приложения» объясняет
/// оператору, откуда вообще взялась карточка, которую он не заводил.
/// </summary>
public sealed class OperatorPlayerIdentityProjectionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-21T09:00:00Z");

    [Fact]
    public async Task PlayerSearch_NamesThePersonBehindTheCard_AndWhereTheCardCameFrom()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var person = await PlatformPersonTestData.AddPersonAsync(
            factory, "+992900000701", displayName: "Guest FromApp");
        var opened = await EnsureMembershipAsync(factory, person.PlatformPersonId);
        var deskCardId = await AddDeskCardAsync(factory, "Guest FromDesk", "+992900000702");

        var response = await client.GetAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/players?query=Guest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<PlayerSearchResultDto>>();
        Assert.NotNull(body);

        var fromApp = body.Single(result => result.PlayerAccountId == opened.PlayerAccountId);
        Assert.Equal(person.PlatformPersonId, fromApp.PlatformPersonId);
        Assert.True(fromApp.CreatedFromApp);

        var fromDesk = body.Single(result => result.PlayerAccountId == deskCardId);
        Assert.Null(fromDesk.PlatformPersonId);
        Assert.False(fromDesk.CreatedFromApp);
    }

    /// <summary>
    /// Карточка, заведённая руками на стойке, личности не знает — и наврать об этом ответ не
    /// вправе: оператор по ней сеть не спрашивает.
    /// </summary>
    [Fact]
    public async Task DeskCreatedPlayer_IsAnsweredWithoutAPerson_AndNotAsComingFromTheApp()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/players",
            new CreatePlayerAccountRequest(
                TestIds.OrganizationId,
                DisplayName: "Walk-in guest",
                PhoneNumber: "+992900000703",
                IdempotencyKey: Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PlayerAccountDto>();
        Assert.NotNull(created);
        Assert.Null(created.PlatformPersonId);
        Assert.False(created.CreatedFromApp);
    }

    /// <summary>
    /// Заявка от подшитого к личности счёта называет личность: карточка заявки — ровно то место,
    /// где клуб решает, сажать ли этого человека, и основание спросить сеть у него уже есть.
    /// </summary>
    [Fact]
    public async Task ReservationList_NamesThePersonBehindTheAccount()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000704");
        var opened = await EnsureMembershipAsync(factory, person.PlatformPersonId);
        var linkedReservationId = await AddReservationAsync(factory, opened.PlayerAccountId, null);
        var guestReservationId = await AddReservationAsync(factory, null, "+992900000705");

        var response = await client.GetAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/reservations" +
            "?fromUtc=2026-08-21T00:00:00Z&toUtc=2026-08-21T23:59:59Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReservationSearchResultDto>();
        Assert.NotNull(body);

        var linked = body.Reservations.Single(row => row.ReservationId == linkedReservationId);
        Assert.Equal(person.PlatformPersonId, linked.PlatformPersonId);

        // Гость, записанный на стойке одним телефоном, счёта ещё не имеет — называть некого.
        var guest = body.Reservations.Single(row => row.ReservationId == guestReservationId);
        Assert.Null(guest.PlatformPersonId);
    }

    private static async Task<PlayerAccountEntity> EnsureMembershipAsync(
        PlatformApiFactory factory,
        Guid platformPersonId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IPlayerClubMembershipService>()
            .EnsureAsync(platformPersonId, TestIds.OrganizationId, TestIds.BranchId, CancellationToken.None);
        Assert.True(result.Succeeded);
        return result.Account!;
    }

    private static async Task<Guid> AddDeskCardAsync(
        PlatformApiFactory factory,
        string displayName,
        string phoneNumber)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var accountId = Guid.NewGuid();
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = accountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = displayName,
            PhoneNumber = phoneNumber,
            IsActive = true,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return accountId;
    }

    private static async Task<Guid> AddReservationAsync(
        PlatformApiFactory factory,
        Guid? playerAccountId,
        string? phoneNumber)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var reservationId = Guid.NewGuid();
        db.Reservations.Add(new ReservationEntity
        {
            ReservationId = reservationId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = playerAccountId,
            CustomerName = "Гость",
            PhoneNumber = phoneNumber,
            StartsAtUtc = Now.AddHours(2),
            EndsAtUtc = Now.AddHours(3),
            State = ReservationStateNames.Pending,
            Source = ReservationSourceNames.Operator,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return reservationId;
    }
}
