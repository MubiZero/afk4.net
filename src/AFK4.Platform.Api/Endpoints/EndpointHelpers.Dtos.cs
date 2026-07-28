using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Dashboard;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Install;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Payments;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Idempotency;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Tenancy;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Reports;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Players;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Shifts;
using AFK4.Platform.Api.Security;
using AFK4.Platform.Api.Tenancy;
using AFK4.Platform.Api.Updates;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Diagnostics;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Branding;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Identity.AccountActivation;
using AFK4.Shared.Contracts.Platform.Operator;
using AFK4.Shared.Contracts.Platform.SupportNotes;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Tariffs;
using AFK4.Shared.Contracts.Updates;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

namespace AFK4.Platform.Api.Endpoints;

internal static partial class EndpointHelpers
{
    public static PlayerReservationDto ToPlayerReservationDto(ReservationDto r) =>
        new(r.ReservationId, r.SeatId, r.SeatName, r.StartsAtUtc, r.EndsAtUtc, r.State, r.Note);

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
            seats.Select(ToSeatDto).ToList());
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
