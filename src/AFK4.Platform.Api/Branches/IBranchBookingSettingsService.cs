using AFK4.Shared.Contracts.Branches;

namespace AFK4.Platform.Api.Branches;

/// <summary>
/// Чтение и правка настроек приёма гостей у филиала.
/// </summary>
public interface IBranchBookingSettingsService
{
    Task<BranchBookingSettingsDto> GetAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<BranchBookingSettingsDto> UpdateAsync(
        Guid organizationId,
        Guid branchId,
        Guid staffUserId,
        UpdateBranchBookingSettingsRequest request,
        CancellationToken cancellationToken);
}
