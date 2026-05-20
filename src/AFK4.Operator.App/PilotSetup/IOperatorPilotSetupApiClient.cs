using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Tariffs;

namespace AFK4.Operator.App.PilotSetup;

public interface IOperatorPilotSetupApiClient
{
    Task<IReadOnlyList<StaffUserDto>> GetStaffUsersAsync(Guid branchId, CancellationToken cancellationToken);

    Task<StaffUserDto> CreateStaffUserAsync(
        Guid branchId,
        CreateStaffUserRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ZoneDto>> GetLayoutZonesAsync(Guid branchId, CancellationToken cancellationToken);

    Task<ZoneDto> CreateZoneAsync(
        Guid branchId,
        CreateZoneRequest request,
        CancellationToken cancellationToken);

    Task<SeatDto> CreateSeatAsync(
        Guid branchId,
        CreateSeatRequest request,
        CancellationToken cancellationToken);

    Task<TariffDto> CreateTariffAsync(
        Guid branchId,
        CreateTariffRequest request,
        CancellationToken cancellationToken);

    Task<TariffVersionDto> CreateTariffVersionAsync(
        Guid branchId,
        Guid tariffId,
        CreateTariffVersionRequest request,
        CancellationToken cancellationToken);

    Task<PosProductCategoryDto> CreateProductCategoryAsync(
        Guid branchId,
        CreateProductCategoryRequest request,
        CancellationToken cancellationToken);

    Task<PosProductDto> CreateProductAsync(
        Guid branchId,
        CreateProductRequest request,
        CancellationToken cancellationToken);

    Task<DeviceSeatAssignmentDto> AssignDeviceSeatAsync(
        Guid branchId,
        Guid deviceId,
        AssignDeviceSeatRequest request,
        CancellationToken cancellationToken);
}
