using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Tariffs;

namespace AFK4.OrganizationAdmin.App.PilotSetup;

public sealed class UnconfiguredOperatorPilotSetupApiClient : IOperatorPilotSetupApiClient
{
    private const string Message = "Operator pilot setup API client is not configured.";

    public Task<IReadOnlyList<StaffUserDto>> GetStaffUsersAsync(Guid branchId, CancellationToken cancellationToken)
    {
        return Throw<IReadOnlyList<StaffUserDto>>();
    }

    public Task<StaffInviteDto> CreateStaffInviteAsync(
        Guid branchId,
        CreateStaffInviteRequest request,
        CancellationToken cancellationToken)
    {
        return Throw<StaffInviteDto>();
    }

    public Task<IReadOnlyList<ZoneDto>> GetLayoutZonesAsync(Guid branchId, CancellationToken cancellationToken)
    {
        return Throw<IReadOnlyList<ZoneDto>>();
    }

    public Task<ZoneDto> CreateZoneAsync(
        Guid branchId,
        CreateZoneRequest request,
        CancellationToken cancellationToken)
    {
        return Throw<ZoneDto>();
    }

    public Task<SeatDto> CreateSeatAsync(
        Guid branchId,
        CreateSeatRequest request,
        CancellationToken cancellationToken)
    {
        return Throw<SeatDto>();
    }

    public Task<TariffDto> CreateTariffAsync(
        Guid branchId,
        CreateTariffRequest request,
        CancellationToken cancellationToken)
    {
        return Throw<TariffDto>();
    }

    public Task<TariffVersionDto> CreateTariffVersionAsync(
        Guid branchId,
        Guid tariffId,
        CreateTariffVersionRequest request,
        CancellationToken cancellationToken)
    {
        return Throw<TariffVersionDto>();
    }

    public Task<PosProductCategoryDto> CreateProductCategoryAsync(
        Guid branchId,
        CreateProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        return Throw<PosProductCategoryDto>();
    }

    public Task<PosProductDto> CreateProductAsync(
        Guid branchId,
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        return Throw<PosProductDto>();
    }

    public Task<DeviceSeatAssignmentDto> AssignDeviceSeatAsync(
        Guid branchId,
        Guid deviceId,
        AssignDeviceSeatRequest request,
        CancellationToken cancellationToken)
    {
        return Throw<DeviceSeatAssignmentDto>();
    }

    private static Task<T> Throw<T>()
    {
        throw new InvalidOperationException(Message);
    }
}
