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
using AFK4.Shared.Contracts.Platform.Tenants;
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
    public static async Task<PlayerAccountEntity?> LoadPlayerForStaffAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        return await dbContext.PlayerAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                player =>
                    player.OrganizationId == organizationId &&
                    player.PlayerAccountId == playerAccountId,
                cancellationToken);
    }

    public static async Task<ReservationScopedEndpointResult> LoadReservationForStaffAsync(
        PlatformDbContext dbContext,
        IStaffContextAccessor staffContextAccessor,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        if (staffContextAccessor.Current is null)
        {
            return new ReservationScopedEndpointResult(null, Results.Unauthorized());
        }

        var reservation = await dbContext.Reservations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == staffContextAccessor.Current.OrganizationId &&
                    candidate.ReservationId == reservationId,
                cancellationToken);

        return reservation is null
            ? new ReservationScopedEndpointResult(null, Results.NotFound())
            : new ReservationScopedEndpointResult(reservation, null);
    }

    public static async Task<PlayerScopedEndpointResult> LoadPlayerScopedEndpointAsync(
        PlatformDbContext dbContext,
        IStaffContextAccessor staffContextAccessor,
        StaffAuthorizationService authorizationService,
        Guid playerAccountId,
        string permission,
        CancellationToken cancellationToken)
    {
        if (staffContextAccessor.Current is null)
        {
            return new PlayerScopedEndpointResult(null, Guid.Empty, null, Results.Unauthorized());
        }

        var staffContext = staffContextAccessor.Current;
        var player = await LoadPlayerForStaffAsync(
            dbContext,
            playerAccountId,
            staffContext.OrganizationId,
            cancellationToken);

        if (player is not null && !staffContext.BranchIds.Contains(player.HomeBranchId))
        {
            var fallbackBranchId = staffContext.BranchIds.OrderBy(branch => branch).FirstOrDefault();
            if (fallbackBranchId == Guid.Empty)
            {
                return new PlayerScopedEndpointResult(null, Guid.Empty, null, Results.StatusCode(StatusCodes.Status403Forbidden));
            }

            var fallbackAuthorization = await authorizationService.RequireBranchPermissionAsync(
                fallbackBranchId,
                permission,
                cancellationToken);

            if (!fallbackAuthorization.IsAuthenticated)
            {
                return new PlayerScopedEndpointResult(null, fallbackBranchId, fallbackAuthorization, Results.Unauthorized());
            }

            return fallbackAuthorization.IsAllowed
                ? new PlayerScopedEndpointResult(null, fallbackBranchId, fallbackAuthorization, Results.NotFound())
                : new PlayerScopedEndpointResult(null, fallbackBranchId, fallbackAuthorization, null);
        }

        var branchId = player?.HomeBranchId ?? staffContext.BranchIds.OrderBy(branch => branch).FirstOrDefault();
        if (branchId == Guid.Empty)
        {
            return new PlayerScopedEndpointResult(null, Guid.Empty, null, Results.StatusCode(StatusCodes.Status403Forbidden));
        }

        var authorization = await authorizationService.RequireBranchPermissionAsync(
            branchId,
            permission,
            cancellationToken);

        if (!authorization.IsAuthenticated)
        {
            return new PlayerScopedEndpointResult(player, branchId, authorization, Results.Unauthorized());
        }

        if (!authorization.IsAllowed)
        {
            return new PlayerScopedEndpointResult(player, branchId, authorization, null);
        }

        return player is null
            ? new PlayerScopedEndpointResult(null, branchId, authorization, Results.NotFound())
            : new PlayerScopedEndpointResult(player, branchId, authorization, null);
    }

    /// <summary>
    /// Money mutations (top-up, debt payment, manual correction, package purchase, refund) are not
    /// allowed on a deactivated player account — a server-side mirror of the operator UI gate, and the
    /// same stance POS already takes on sales to inactive players. Read-only pre-check that mutates
    /// nothing: returns a 400 result when the player is inactive, otherwise null. The online top-up
    /// webhook is intentionally NOT routed through here (funds are already captured upstream, so a
    /// blocked credit would strand the payer's money).
    /// </summary>
    internal static IResult? RejectInactivePlayerMoneyAction(PlayerAccountEntity? player)
        => player is { IsActive: false }
            ? Results.BadRequest(new { Error = "Player account is inactive." })
            : null;

    public static Task<ScopedEntityEndpointResult<ShiftEntity>> LoadShiftScopedEndpointAsync(
        PlatformDbContext dbContext,
        IStaffContextAccessor staffContextAccessor,
        StaffAuthorizationService authorizationService,
        Guid shiftId,
        string permission,
        CancellationToken cancellationToken)
    {
        return LoadScopedEntityEndpointAsync(
            dbContext,
            staffContextAccessor,
            authorizationService,
            permission,
            (organizationId, token) => dbContext.Shifts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    shift => shift.OrganizationId == organizationId && shift.ShiftId == shiftId,
                    token),
            shift => shift.BranchId,
            cancellationToken);
    }

    public static Task<ScopedEntityEndpointResult<PosSaleEntity>> LoadPosSaleScopedEndpointAsync(
        PlatformDbContext dbContext,
        IStaffContextAccessor staffContextAccessor,
        StaffAuthorizationService authorizationService,
        Guid saleId,
        string permission,
        CancellationToken cancellationToken)
    {
        return LoadScopedEntityEndpointAsync(
            dbContext,
            staffContextAccessor,
            authorizationService,
            permission,
            (organizationId, token) => dbContext.PosSales
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    sale => sale.OrganizationId == organizationId && sale.PosSaleId == saleId,
                    token),
            sale => sale.BranchId,
            cancellationToken);
    }

    public static Task<ScopedEntityEndpointResult<ReceiptEntity>> LoadReceiptScopedEndpointAsync(
        PlatformDbContext dbContext,
        IStaffContextAccessor staffContextAccessor,
        StaffAuthorizationService authorizationService,
        Guid receiptId,
        string permission,
        CancellationToken cancellationToken)
    {
        return LoadScopedEntityEndpointAsync(
            dbContext,
            staffContextAccessor,
            authorizationService,
            permission,
            (organizationId, token) => dbContext.Receipts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    receipt => receipt.OrganizationId == organizationId && receipt.ReceiptId == receiptId,
                    token),
            receipt => receipt.BranchId,
            cancellationToken);
    }

    public static async Task<ScopedEntityEndpointResult<TEntity>> LoadScopedEntityEndpointAsync<TEntity>(
        PlatformDbContext dbContext,
        IStaffContextAccessor staffContextAccessor,
        StaffAuthorizationService authorizationService,
        string permission,
        Func<Guid, CancellationToken, Task<TEntity?>> loadEntityAsync,
        Func<TEntity, Guid> getBranchId,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        if (staffContextAccessor.Current is null)
        {
            return new ScopedEntityEndpointResult<TEntity>(null, Guid.Empty, null, Results.Unauthorized());
        }

        var staffContext = staffContextAccessor.Current;
        var entity = await loadEntityAsync(staffContext.OrganizationId, cancellationToken);

        if (entity is not null && !staffContext.BranchIds.Contains(getBranchId(entity)))
        {
            var fallbackBranchId = staffContext.BranchIds.OrderBy(branch => branch).FirstOrDefault();
            if (fallbackBranchId == Guid.Empty)
            {
                return new ScopedEntityEndpointResult<TEntity>(
                    null,
                    Guid.Empty,
                    null,
                    Results.StatusCode(StatusCodes.Status403Forbidden));
            }

            var fallbackAuthorization = await authorizationService.RequireBranchPermissionAsync(
                fallbackBranchId,
                permission,
                cancellationToken);

            if (!fallbackAuthorization.IsAuthenticated)
            {
                return new ScopedEntityEndpointResult<TEntity>(
                    null,
                    fallbackBranchId,
                    fallbackAuthorization,
                    Results.Unauthorized());
            }

            return fallbackAuthorization.IsAllowed
                ? new ScopedEntityEndpointResult<TEntity>(null, fallbackBranchId, fallbackAuthorization, Results.NotFound())
                : new ScopedEntityEndpointResult<TEntity>(null, fallbackBranchId, fallbackAuthorization, null);
        }

        var branchId = entity is null
            ? staffContext.BranchIds.OrderBy(branch => branch).FirstOrDefault()
            : getBranchId(entity);
        if (branchId == Guid.Empty)
        {
            return new ScopedEntityEndpointResult<TEntity>(
                null,
                Guid.Empty,
                null,
                Results.StatusCode(StatusCodes.Status403Forbidden));
        }

        var authorization = await authorizationService.RequireBranchPermissionAsync(
            branchId,
            permission,
            cancellationToken);

        if (!authorization.IsAuthenticated)
        {
            return new ScopedEntityEndpointResult<TEntity>(entity, branchId, authorization, Results.Unauthorized());
        }

        if (!authorization.IsAllowed)
        {
            return new ScopedEntityEndpointResult<TEntity>(entity, branchId, authorization, null);
        }

        return entity is null
            ? new ScopedEntityEndpointResult<TEntity>(null, branchId, authorization, Results.NotFound())
            : new ScopedEntityEndpointResult<TEntity>(entity, branchId, authorization, null);
    }
}
