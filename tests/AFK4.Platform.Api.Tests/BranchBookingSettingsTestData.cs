using AFK4.Platform.Api.Branches;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Branches;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Филиал, который берёт брони у кого угодно.
///
/// По умолчанию клуб просит незнакомого гостя заплатить вперёд и держит для него одну бронь за
/// раз — осторожное поведение ненастроенного филиала. Тестам, которые про другое (конфликты мест,
/// вместимость зала, права, отмена), нужен филиал без этих условий, иначе каждый из них проверял
/// бы заодно и правила приёма гостей.
/// </summary>
internal static class BranchBookingSettingsTestData
{
    public static BranchBookingSettingsEntity AcceptsAnyGuest(
        Guid organizationId,
        Guid branchId,
        DateTimeOffset now) => new()
        {
            BranchId = branchId,
            OrganizationId = organizationId,
            AcceptanceMode = BranchBookingAcceptanceModes.Auto,
            RespondWithinMinutes = BranchBookingSettingsDefaults.RespondWithinMinutes,
            RequirePrepaymentFromNewGuests = false,
            MaxActiveReservationsForNewGuests = BranchBookingSettingsDefaults.MaxActiveReservationsLimit,
            RegularAfterVisits = BranchBookingSettingsDefaults.RegularAfterVisits,
            HoldSeatAfterStartMinutes = BranchBookingSettingsDefaults.HoldSeatAfterStartMinutes,
            KeepPrepaymentOnNoShow = BranchBookingSettingsDefaults.KeepPrepaymentOnNoShow,
            UpdatedAtUtc = now,
            UpdatedByStaffUserId = Guid.NewGuid()
        };

    public static async Task AcceptAnyGuestAsync(
        PlatformDbContext dbContext,
        Guid organizationId,
        Guid branchId,
        DateTimeOffset? now = null)
    {
        dbContext.BranchBookingSettings.Add(
            AcceptsAnyGuest(organizationId, branchId, now ?? DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }
}
