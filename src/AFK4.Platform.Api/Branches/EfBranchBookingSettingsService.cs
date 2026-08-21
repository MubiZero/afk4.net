using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Branches;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Branches;

public sealed class EfBranchBookingSettingsService(PlatformDbContext dbContext, TimeProvider timeProvider)
    : IBranchBookingSettingsService
{
    public Task<BranchBookingSettingsDto> GetAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken) =>
        BranchBookingSettingsDefaults.ResolveAsync(dbContext, organizationId, branchId, cancellationToken);

    public async Task<BranchBookingSettingsDto> UpdateAsync(
        Guid organizationId,
        Guid branchId,
        Guid staffUserId,
        UpdateBranchBookingSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await dbContext.BranchBookingSettings
            .SingleOrDefaultAsync(
                row => row.BranchId == branchId && row.OrganizationId == organizationId,
                cancellationToken);

        // Строка заводится первой же правкой: до неё филиал жил на значениях по умолчанию, и
        // заводить её заранее каждому филиалу значило бы объявить их решением клуба задним числом.
        if (settings is null)
        {
            settings = new BranchBookingSettingsEntity { BranchId = branchId, OrganizationId = organizationId };
            dbContext.BranchBookingSettings.Add(settings);
        }

        settings.AcceptanceMode = request.AcceptanceMode;
        settings.RespondWithinMinutes = request.RespondWithinMinutes;
        settings.RequirePrepaymentFromNewGuests = request.RequirePrepaymentFromNewGuests;
        settings.MaxActiveReservationsForNewGuests = request.MaxActiveReservationsForNewGuests;
        settings.RegularAfterVisits = request.RegularAfterVisits;
        settings.HoldSeatAfterStartMinutes = request.HoldSeatAfterStartMinutes;
        settings.KeepPrepaymentOnNoShow = request.KeepPrepaymentOnNoShow;
        settings.UpdatedAtUtc = timeProvider.GetUtcNow();
        settings.UpdatedByStaffUserId = staffUserId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return BranchBookingSettingsDefaults.For(organizationId, branchId, settings);
    }
}
