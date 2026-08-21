using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Shared.Contracts.Tests;

public sealed class ReservationContractSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ReservationContracts_RoundTripThroughJson()
    {
        var reservationId = Guid.Parse("99999999-1111-4111-8111-999999999999");
        var organizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
        var branchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
        var seatId = Guid.Parse("aaaaaaaa-1111-4111-8111-111111111111");
        var groupId = Guid.Parse("88888888-1111-4111-8111-888888888888");

        var search = new ReservationSearchResultDto(
            [
                new ReservationDto(
                    reservationId,
                    organizationId,
                    branchId,
                    PlayerAccountId: null,
                    seatId,
                    SeatName: "PC-01",
                    ZoneName: "Main",
                    CustomerName: "Aziz P.",
                    PhoneNumber: "+992900000001",
                    StartsAtUtc: DateTimeOffset.Parse("2026-05-21T16:00:00Z"),
                    EndsAtUtc: DateTimeOffset.Parse("2026-05-21T17:00:00Z"),
                    DurationMinutes: 60,
                    State: ReservationStateNames.Confirmed,
                    Source: ReservationSourceNames.Operator,
                    Note: "front desk",
                    CreatedAtUtc: DateTimeOffset.Parse("2026-05-21T10:00:00Z"),
                    UpdatedAtUtc: DateTimeOffset.Parse("2026-05-21T10:00:00Z"),
                    CancelledAtUtc: null,
                    CancelReason: string.Empty,
                    ReservationGroupId: groupId)
            ],
            Limit: 40);
        var create = new CreateReservationRequest(
            organizationId,
            PlayerAccountId: null,
            seatId,
            CustomerName: "Aziz P.",
            PhoneNumber: "+992900000001",
            StartsAtUtc: DateTimeOffset.Parse("2026-05-21T16:00:00Z"),
            DurationMinutes: 60,
            Source: ReservationSourceNames.Operator,
            Note: "front desk");
        var cancel = new CancelReservationRequest(organizationId, "client called", ExpectedVersion: 2);

        var searchCopy = JsonSerializer.Deserialize<ReservationSearchResultDto>(
            JsonSerializer.Serialize(search, Options),
            Options);
        var createCopy = JsonSerializer.Deserialize<CreateReservationRequest>(
            JsonSerializer.Serialize(create, Options),
            Options);
        var cancelCopy = JsonSerializer.Deserialize<CancelReservationRequest>(
            JsonSerializer.Serialize(cancel, Options),
            Options);

        Assert.NotNull(searchCopy);
        Assert.Equal(reservationId, Assert.Single(searchCopy.Reservations).ReservationId);
        Assert.Equal(ReservationStateNames.Confirmed, searchCopy.Reservations[0].State);
        Assert.Equal(groupId, searchCopy.Reservations[0].ReservationGroupId);
        Assert.NotNull(createCopy);
        Assert.Equal(60, createCopy.DurationMinutes);
        Assert.NotNull(cancelCopy);
        Assert.Equal("client called", cancelCopy.Reason);
        Assert.Equal(2, cancelCopy.ExpectedVersion);
    }

    [Fact]
    public void ReservationPermissions_AreStableContractNames()
    {
        Assert.Equal("organization.reservations.view", OrganizationPermissionNames.ViewReservations);
        Assert.Equal("organization.reservations.manage", OrganizationPermissionNames.ManageReservations);
    }

    [Fact]
    public void ReservationDto_RoundTripsVersionAndStartedSession()
    {
        var sessionId = Guid.Parse("77777777-1111-4111-8111-777777777777");
        var reservation = new ReservationDto(
            Guid.Parse("99999999-1111-4111-8111-999999999999"),
            Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            Guid.Parse("66666666-1111-4111-8111-666666666666"),
            Guid.Parse("aaaaaaaa-1111-4111-8111-111111111111"),
            "PC-01",
            "VIP",
            "Aziz P.",
            "+992900000001",
            DateTimeOffset.Parse("2026-07-14T16:00:00Z"),
            DateTimeOffset.Parse("2026-07-14T17:00:00Z"),
            60,
            ReservationStateNames.Confirmed,
            ReservationSourceNames.Operator,
            string.Empty,
            DateTimeOffset.Parse("2026-07-14T10:00:00Z"),
            DateTimeOffset.Parse("2026-07-14T10:00:00Z"),
            null,
            string.Empty,
            null,
            Version: 3,
            StartedSessionId: sessionId);

        var copy = JsonSerializer.Deserialize<ReservationDto>(
            JsonSerializer.Serialize(reservation, Options),
            Options);

        Assert.NotNull(copy);
        Assert.Equal(3, copy.Version);
        Assert.Equal(sessionId, copy.StartedSessionId);
    }

    /// <summary>
    /// Срок ответа на заявку и время ответа доезжают до обеих сторон: до стойки в
    /// <see cref="ReservationDto"/> и до приложения в <see cref="PlayerReservationDto"/>. Иначе у
    /// двух администраторов на разных машинах были бы разные цифры, а у игрока третья.
    /// </summary>
    [Fact]
    public void ReservationContracts_RoundTripTheAnswerDeadline()
    {
        var respondBy = DateTimeOffset.Parse("2026-07-14T10:30:00Z");
        var confirmedAt = DateTimeOffset.Parse("2026-07-14T10:12:00Z");
        var reservation = new ReservationDto(
            Guid.Parse("99999999-1111-4111-8111-999999999999"),
            Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            Guid.Parse("66666666-1111-4111-8111-666666666666"),
            Guid.Parse("aaaaaaaa-1111-4111-8111-111111111111"),
            "PC-01",
            "VIP",
            "Aziz P.",
            "+992900000001",
            DateTimeOffset.Parse("2026-07-14T16:00:00Z"),
            DateTimeOffset.Parse("2026-07-14T17:00:00Z"),
            60,
            ReservationStateNames.Pending,
            ReservationSourceNames.Online,
            string.Empty,
            DateTimeOffset.Parse("2026-07-14T10:00:00Z"),
            DateTimeOffset.Parse("2026-07-14T10:00:00Z"),
            null,
            string.Empty,
            null,
            RespondByUtc: respondBy,
            ConfirmedAtUtc: confirmedAt);
        var playerView = new PlayerReservationDto(
            reservation.ReservationId,
            reservation.SeatId,
            reservation.SeatName,
            reservation.StartsAtUtc,
            reservation.EndsAtUtc,
            reservation.State,
            Note: null,
            RespondByUtc: respondBy);

        var reservationCopy = RoundTrip(reservation);
        var playerCopy = RoundTrip(playerView);

        Assert.Equal(respondBy, reservationCopy.RespondByUtc);
        Assert.Equal(confirmedAt, reservationCopy.ConfirmedAtUtc);
        Assert.Equal(respondBy, playerCopy.RespondByUtc);
    }

    [Fact]
    public void StartReservationSessionRequest_RoundTripsSessionParameters()
    {
        var tariffVersionId = Guid.Parse("55555555-1111-4111-8111-555555555555");
        var request = new StartReservationSessionRequest(
            Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            ExpectedVersion: 4,
            TariffRuleVersionId: "vip-v7",
            IdempotencyKey: "reservation-start-1",
            DurationMode: SessionDurationModes.Fixed,
            DurationMinutes: 120,
            BillingMode: BillingModeNames.PrepaidWallet,
            TariffVersionId: tariffVersionId,
            PlayerPackageId: null);

        var copy = JsonSerializer.Deserialize<StartReservationSessionRequest>(
            JsonSerializer.Serialize(request, Options),
            Options);

        Assert.NotNull(copy);
        Assert.Equal(4, copy.ExpectedVersion);
        Assert.Equal(SessionDurationModes.Fixed, copy.DurationMode);
        Assert.Equal(120, copy.DurationMinutes);
        Assert.Equal(BillingModeNames.PrepaidWallet, copy.BillingMode);
        Assert.Equal(tariffVersionId, copy.TariffVersionId);
        Assert.Null(copy.PlayerPackageId);
        Assert.Equal("reservation-start-1", copy.IdempotencyKey);
    }

    [Fact]
    public void ReservationMutationRequests_RoundTripExpectedVersion()
    {
        var organizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
        var update = new UpdateReservationRequest(
            organizationId,
            PlayerAccountId: null,
            SeatId: null,
            CustomerName: null,
            PhoneNumber: null,
            StartsAtUtc: null,
            DurationMinutes: null,
            Source: null,
            Note: null,
            ExpectedVersion: 7);
        var confirm = new ConfirmReservationRequest(organizationId, ExpectedVersion: 8);
        var seat = new SeatReservationRequest(organizationId, ExpectedVersion: 9);
        var cancel = new CancelReservationRequest(organizationId, "client called", ExpectedVersion: 10);

        Assert.Equal(7, RoundTrip(update).ExpectedVersion);
        Assert.Equal(8, RoundTrip(confirm).ExpectedVersion);
        Assert.Equal(9, RoundTrip(seat).ExpectedVersion);
        Assert.Equal(10, RoundTrip(cancel).ExpectedVersion);
    }

    /// <summary>
    /// Заявка называет личность, если за ней стоит подшитый счёт: карточка заявки — то место,
    /// где клуб решает, сажать ли человека, и спрашивать сеть по его телефону там незачем.
    /// У гостя, записанного одним телефоном, счёта ещё нет — и называть некого.
    /// </summary>
    [Fact]
    public void ReservationDto_RoundTripsThePersonBehindTheAccount()
    {
        var personId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");
        var linked = new ReservationDto(
            Guid.Parse("99999999-1111-4111-8111-999999999999"),
            Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            Guid.Parse("66666666-1111-4111-8111-666666666666"),
            Guid.Parse("aaaaaaaa-1111-4111-8111-111111111111"),
            "PC-01",
            "VIP",
            "Фаррух",
            "+992900000001",
            DateTimeOffset.Parse("2026-08-21T16:00:00Z"),
            DateTimeOffset.Parse("2026-08-21T17:00:00Z"),
            60,
            ReservationStateNames.Pending,
            ReservationSourceNames.Online,
            string.Empty,
            DateTimeOffset.Parse("2026-08-21T10:00:00Z"),
            DateTimeOffset.Parse("2026-08-21T10:00:00Z"),
            null,
            string.Empty,
            null,
            PlatformPersonId: personId);
        var guest = linked with { PlayerAccountId = null, PlatformPersonId = null };

        Assert.Equal(personId, RoundTrip(linked).PlatformPersonId);
        Assert.Null(RoundTrip(guest).PlatformPersonId);
    }

    /// <summary>
    /// Филиал в первом действии: сеть с несколькими филиалами ждёт, что приложение назовёт, куда
    /// человек придёт. Поле необязательное — клиент, который о нём не знает, шлёт запрос без него
    /// и продолжает работать как раньше.
    /// </summary>
    [Fact]
    public void PlayerBookingRequests_CarryTheBranchAndSurviveAClientThatOmitsIt()
    {
        var branchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
        var startsAtUtc = DateTimeOffset.Parse("2026-08-22T16:00:00Z");
        var single = new CreatePlayerReservationRequest(
            SeatId: null, startsAtUtc, startsAtUtc.AddHours(1), Note: null, TariffVersionId: null, branchId);
        var group = new CreatePlayerReservationGroupRequest(
            SeatCount: 3, startsAtUtc, startsAtUtc.AddHours(1), Note: null, TariffVersionId: null, branchId);
        var topUp = new PlayerTopUpIntentRequest(5_000, "TJS", "counter", branchId);

        Assert.Equal(branchId, RoundTrip(single).BranchId);
        Assert.Equal(branchId, RoundTrip(group).BranchId);
        Assert.Equal(branchId, RoundTrip(topUp).BranchId);

        var oldSingle = JsonSerializer.Deserialize<CreatePlayerReservationRequest>(
            """{"startsAtUtc":"2026-08-22T16:00:00Z","endsAtUtc":"2026-08-22T17:00:00Z"}""", Options);
        var oldGroup = JsonSerializer.Deserialize<CreatePlayerReservationGroupRequest>(
            """{"seatCount":3,"startsAtUtc":"2026-08-22T16:00:00Z","endsAtUtc":"2026-08-22T17:00:00Z"}""",
            Options);
        var oldTopUp = JsonSerializer.Deserialize<PlayerTopUpIntentRequest>(
            """{"amountMinorUnits":5000,"currencyCode":"TJS"}""", Options);

        Assert.Null(oldSingle!.BranchId);
        Assert.Null(oldGroup!.BranchId);
        Assert.Null(oldTopUp!.BranchId);
    }

    private static T RoundTrip<T>(T value)
    {
        var copy = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options);
        return Assert.IsType<T>(copy);
    }
}
