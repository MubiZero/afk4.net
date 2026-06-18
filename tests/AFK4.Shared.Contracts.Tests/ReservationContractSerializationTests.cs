using System.Text.Json;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Reservations;

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
        var cancel = new CancelReservationRequest(organizationId, "client called");

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
    }

    [Fact]
    public void ReservationPermissions_AreStableContractNames()
    {
        Assert.Equal("reservations.view", StaffPermissionNames.ViewReservations);
        Assert.Equal("reservations.manage", StaffPermissionNames.ManageReservations);
    }
}
