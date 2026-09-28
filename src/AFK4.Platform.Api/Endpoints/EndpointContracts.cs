using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;

// These endpoint-scoped result records are intentionally left in the global namespace:
// the minimal-API endpoints in Program.cs (and the test project) reference them unqualified.

public sealed record HealthResponse(string Status, DateTimeOffset ServerTimeUtc);

public sealed record PlayerScopedEndpointResult(
    PlayerAccountEntity? Player,
    Guid BranchId,
    StaffAuthorizationResult? Authorization,
    IResult? Result);

public sealed record ReservationScopedEndpointResult(
    ReservationEntity? Reservation,
    IResult? Result);

public sealed record ScopedEntityEndpointResult<TEntity>(
    TEntity? Entity,
    Guid BranchId,
    StaffAuthorizationResult? Authorization,
    IResult? Result)
    where TEntity : class;

public sealed record DeviceMutationScope(
    DeviceEntity? Device,
    StaffAuthorizationResult? Authorization,
    IResult? ErrorResult);

public sealed record DeviceSeatAssignmentOperationResult(
    DeviceSeatAssignmentEntity? Assignment,
    IResult? ErrorResult,
    IReadOnlyList<Guid> ChangedDeviceIds,
    DateTimeOffset ObservedAtUtc);
