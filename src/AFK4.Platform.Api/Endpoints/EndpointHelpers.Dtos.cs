using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Receipts;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static partial class EndpointHelpers
{
    public static PlayerReservationDto ToPlayerReservationDto(ReservationDto r) =>
        new(
            r.ReservationId,
            r.SeatId,
            r.SeatName,
            r.StartsAtUtc,
            r.EndsAtUtc,
            r.State,
            r.Note,
            r.TariffVersionId,
            r.TariffName,
            r.EstimatedCostMinorUnits,
            r.CurrencyCode,
            r.ReservationGroupId,
            r.RespondByUtc);

    public static ReceiptDto ToDto(ReceiptEntity receipt, Guid? shopOrderId = null)
    {
        return new ReceiptDto(
            receipt.ReceiptId,
            receipt.OrganizationId,
            receipt.BranchId,
            receipt.PosSaleId,
            receipt.ReceiptNumber,
            receipt.ReceiptType,
            new MoneyDto(receipt.CurrencyCode, receipt.TotalMinorUnits),
            receipt.CreatedAtUtc,
            receipt.SessionId,
            shopOrderId);
    }

    public static DeviceSeatAssignmentDto ToDeviceSeatAssignmentDto(DeviceSeatAssignmentEntity assignment)
    {
        return new DeviceSeatAssignmentDto(
            assignment.DeviceSeatAssignmentId,
            assignment.OrganizationId,
            assignment.BranchId,
            assignment.SeatId,
            assignment.DeviceId,
            assignment.AttachedAtUtc,
            assignment.DetachedAtUtc);
    }

    public static StaffUserDto ToStaffUserDto(StaffUserEntity staffUser, IReadOnlyList<string> roleNames)
    {
        return new StaffUserDto(
            staffUser.StaffUserId,
            staffUser.OrganizationId,
            staffUser.UserName,
            staffUser.DisplayName,
            staffUser.IsActive,
            roleNames,
            staffUser.CreatedAtUtc);
    }

    public static async Task RevokeStaffTokensAsync(
        PlatformDbContext dbContext,
        Guid organizationId,
        Guid staffUserId,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken)
    {
        var accessTokens = await dbContext.StaffAccessTokens
            .Where(token =>
                token.OrganizationId == organizationId &&
                token.StaffUserId == staffUserId &&
                token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in accessTokens)
        {
            token.RevokedAtUtc = revokedAtUtc;
        }

        var refreshTokens = await dbContext.StaffRefreshTokens
            .Where(token =>
                token.OrganizationId == organizationId &&
                token.StaffUserId == staffUserId &&
                token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in refreshTokens)
        {
            token.RevokedAtUtc = revokedAtUtc;
        }
    }

    public static BranchProfileDto ToBranchProfileDto(BranchEntity branch)
    {
        return new BranchProfileDto(
            branch.OrganizationId,
            branch.BranchId,
            branch.Name,
            branch.City,
            branch.Description,
            branch.Address,
            branch.Phone,
            branch.Telegram,
            branch.Website,
            branch.Instagram,
            branch.LogoUrl,
            branch.LogoMediaId,
            branch.CoverImageUrl,
            branch.CoverMediaId,
            AFK4.Platform.Api.Branches.BranchPhotos.Deserialize(branch.PhotosJson),
            branch.Latitude,
            branch.Longitude,
            branch.PreferredTimeZone,
            branch.PreferredLocale,
            AFK4.Platform.Api.Branches.BranchWorkingHours.Deserialize(branch.WorkingHoursJson),
            branch.CreatedAtUtc);
    }

    public static ZoneDto ToZoneDto(ZoneEntity zone, IReadOnlyList<SeatEntity> seats)
    {
        return new ZoneDto(
            zone.ZoneId,
            zone.OrganizationId,
            zone.BranchId,
            zone.Name,
            zone.SortOrder,
            zone.CreatedAtUtc,
            seats.Select(ToSeatDto).ToList(),
            zone.HardwareSummary);
    }

    public static SeatDto ToSeatDto(SeatEntity seat)
    {
        return new SeatDto(
            seat.SeatId,
            seat.OrganizationId,
            seat.BranchId,
            seat.ZoneId,
            seat.Name,
            seat.SortOrder,
            seat.CreatedAtUtc);
    }
}
