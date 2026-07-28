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
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class StaffEndpoints
{
    public static void MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("branches/{branchId:guid}/staff", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageBranchStaff,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ViewStaffUsers,
                    "StaffUser",
                    null,
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var organizationId = authorization.StaffContext!.OrganizationId;
            var roleAssignments = await dbContext.StaffRoleAssignments
                .AsNoTracking()
                .Where(roleAssignment =>
                    roleAssignment.OrganizationId == organizationId &&
                    roleAssignment.BranchId == branchId)
                .OrderBy(roleAssignment => roleAssignment.RoleName)
                .ToListAsync(cancellationToken);
            var staffUserIds = roleAssignments.Select(roleAssignment => roleAssignment.StaffUserId).ToHashSet();
            var staffUsers = await dbContext.StaffUsers
                .AsNoTracking()
                .Where(staffUser =>
                    staffUser.OrganizationId == organizationId &&
                    staffUserIds.Contains(staffUser.StaffUserId))
                .OrderBy(staffUser => staffUser.DisplayName)
                .ToListAsync(cancellationToken);
            var rolesByStaffUserId = roleAssignments
                .GroupBy(roleAssignment => roleAssignment.StaffUserId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(roleAssignment => roleAssignment.RoleName).ToList() as IReadOnlyList<string>);
            var response = staffUsers
                .Select(staffUser => ToStaffUserDto(
                    staffUser,
                    rolesByStaffUserId.GetValueOrDefault(staffUser.StaffUserId) ?? []))
                .ToList();

            await WriteAuditAsync(
                auditRecordWriter,
                organizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewStaffUsers,
                "StaffUser",
                null,
                AuditOutcome.Succeeded,
                new { Count = response.Count },
                cancellationToken);

            return Results.Ok(response);
        });

        app.MapPatch("branches/{branchId:guid}/staff/{staffUserId:guid}/roles", async (
            Guid branchId,
            Guid staffUserId,
            UpdateStaffUserRolesRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageRoles,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.UpdateStaffRoles,
                    "StaffUser",
                    staffUserId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.RoleNames, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var validation = ValidateOrganizationRoleNames(request.RoleNames);
            if (validation is not null)
            {
                return Results.BadRequest(new { Error = validation });
            }

            var staffUser = await dbContext.StaffUsers
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.OrganizationId == request.OrganizationId &&
                        candidate.StaffUserId == staffUserId,
                    cancellationToken);

            if (staffUser is null)
            {
                return Results.NotFound();
            }

            var existingAssignments = await dbContext.StaffRoleAssignments
                .Where(roleAssignment =>
                    roleAssignment.OrganizationId == request.OrganizationId &&
                    roleAssignment.BranchId == branchId &&
                    roleAssignment.StaffUserId == staffUserId)
                .ToListAsync(cancellationToken);

            if (existingAssignments.Count == 0)
            {
                return Results.NotFound();
            }

            var roleNames = request.RoleNames
                .Select(roleName => roleName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(roleName => roleName, StringComparer.Ordinal)
                .ToList();
            var requestedRoleSet = roleNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var assignmentsToRemove = existingAssignments
                .Where(roleAssignment => !requestedRoleSet.Contains(roleAssignment.RoleName))
                .ToList();

            dbContext.StaffRoleAssignments.RemoveRange(assignmentsToRemove);

            var existingRoleSet = existingAssignments
                .Where(roleAssignment => requestedRoleSet.Contains(roleAssignment.RoleName))
                .Select(roleAssignment => roleAssignment.RoleName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var roleName in roleNames.Where(roleName => !existingRoleSet.Contains(roleName)))
            {
                dbContext.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
                {
                    StaffRoleAssignmentId = Guid.NewGuid(),
                    StaffUserId = staffUserId,
                    OrganizationId = request.OrganizationId,
                    BranchId = branchId,
                    RoleName = roleName
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToStaffUserDto(staffUser, roleNames);

            await WriteAuditAsync(
                auditRecordWriter,
                request.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.UpdateStaffRoles,
                "StaffUser",
                staffUserId.ToString("D"),
                AuditOutcome.Succeeded,
                new { staffUser.UserName, response.RoleNames },
                cancellationToken);

            return Results.Ok(response);
        });

        app.MapPatch("branches/{branchId:guid}/staff/{staffUserId:guid}/profile", async (
            Guid branchId,
            Guid staffUserId,
            UpdateStaffUserProfileRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageBranchStaff,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.UpdateStaffProfile,
                    "StaffUser",
                    staffUserId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.UserName, request.DisplayName, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var validation = ValidateUpdateStaffUserProfileRequest(request);
            if (validation is not null)
            {
                return Results.BadRequest(new { Error = validation });
            }

            var staffUser = await dbContext.StaffUsers
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.OrganizationId == request.OrganizationId &&
                        candidate.StaffUserId == staffUserId,
                    cancellationToken);

            if (staffUser is null)
            {
                return Results.NotFound();
            }

            var roleNames = await dbContext.StaffRoleAssignments
                .Where(roleAssignment =>
                    roleAssignment.OrganizationId == request.OrganizationId &&
                    roleAssignment.BranchId == branchId &&
                    roleAssignment.StaffUserId == staffUserId)
                .Select(roleAssignment => roleAssignment.RoleName)
                .OrderBy(roleName => roleName)
                .ToListAsync(cancellationToken);

            if (roleNames.Count == 0)
            {
                return Results.NotFound();
            }

            var userName = request.UserName.Trim();
            var normalizedUserName = userName.ToUpperInvariant();
            var displayName = request.DisplayName.Trim();
            var duplicateUserNameExists = await dbContext.StaffUsers
                .AnyAsync(
                    candidate =>
                        candidate.OrganizationId == request.OrganizationId &&
                        candidate.StaffUserId != staffUserId &&
                        candidate.NormalizedUserName == normalizedUserName,
                    cancellationToken);

            if (duplicateUserNameExists)
            {
                return Results.Conflict(new { Error = "Staff user name already exists in the organization." });
            }

            var previousUserName = staffUser.UserName;
            var previousDisplayName = staffUser.DisplayName;
            staffUser.UserName = userName;
            staffUser.NormalizedUserName = normalizedUserName;
            staffUser.DisplayName = displayName;

            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToStaffUserDto(staffUser, roleNames);

            await WriteAuditAsync(
                auditRecordWriter,
                request.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.UpdateStaffProfile,
                "StaffUser",
                staffUserId.ToString("D"),
                AuditOutcome.Succeeded,
                new { PreviousUserName = previousUserName, PreviousDisplayName = previousDisplayName, response.UserName, response.DisplayName },
                cancellationToken);

            return Results.Ok(response);
        });

        app.MapPatch("branches/{branchId:guid}/staff/{staffUserId:guid}/state", async (
            Guid branchId,
            Guid staffUserId,
            UpdateStaffUserStateRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageBranchStaff,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.UpdateStaffState,
                    "StaffUser",
                    staffUserId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.IsActive, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            if (!request.IsActive && staffUserId == authorization.StaffContext.StaffUserId)
            {
                return Results.BadRequest(new { Error = "Staff user cannot deactivate the current authenticated account." });
            }

            var staffUser = await dbContext.StaffUsers
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.OrganizationId == request.OrganizationId &&
                        candidate.StaffUserId == staffUserId,
                    cancellationToken);

            if (staffUser is null)
            {
                return Results.NotFound();
            }

            var roleNames = await dbContext.StaffRoleAssignments
                .Where(roleAssignment =>
                    roleAssignment.OrganizationId == request.OrganizationId &&
                    roleAssignment.BranchId == branchId &&
                    roleAssignment.StaffUserId == staffUserId)
                .Select(roleAssignment => roleAssignment.RoleName)
                .OrderBy(roleName => roleName)
                .ToListAsync(cancellationToken);

            if (roleNames.Count == 0)
            {
                return Results.NotFound();
            }

            var previousIsActive = staffUser.IsActive;
            staffUser.IsActive = request.IsActive;

            if (!request.IsActive)
            {
                await RevokeStaffTokensAsync(dbContext, request.OrganizationId, staffUserId, timeProvider.GetUtcNow(), cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToStaffUserDto(staffUser, roleNames);

            await WriteAuditAsync(
                auditRecordWriter,
                request.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.UpdateStaffState,
                "StaffUser",
                staffUserId.ToString("D"),
                AuditOutcome.Succeeded,
                new { staffUser.UserName, PreviousIsActive = previousIsActive, response.IsActive },
                cancellationToken);

            return Results.Ok(response);
        });

        app.MapPost("branches/{branchId:guid}/staff/{staffUserId:guid}/password-reset", async (
            Guid branchId,
            Guid staffUserId,
            ResetStaffUserPasswordRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageBranchStaff,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ResetStaffPassword,
                    "StaffUser",
                    staffUserId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var validation = ValidateStaffPassword(request.NewPassword);
            if (validation is not null)
            {
                return Results.BadRequest(new { Error = validation });
            }

            var staffUser = await dbContext.StaffUsers
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.OrganizationId == request.OrganizationId &&
                        candidate.StaffUserId == staffUserId,
                    cancellationToken);

            if (staffUser is null)
            {
                return Results.NotFound();
            }

            var roleNames = await dbContext.StaffRoleAssignments
                .Where(roleAssignment =>
                    roleAssignment.OrganizationId == request.OrganizationId &&
                    roleAssignment.BranchId == branchId &&
                    roleAssignment.StaffUserId == staffUserId)
                .Select(roleAssignment => roleAssignment.RoleName)
                .OrderBy(roleName => roleName)
                .ToListAsync(cancellationToken);

            if (roleNames.Count == 0)
            {
                return Results.NotFound();
            }

            var hasher = new PasswordHasher<StaffUserEntity>();
            staffUser.PasswordHash = hasher.HashPassword(staffUser, request.NewPassword);
            await RevokeStaffTokensAsync(dbContext, request.OrganizationId, staffUserId, timeProvider.GetUtcNow(), cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToStaffUserDto(staffUser, roleNames);

            await WriteAuditAsync(
                auditRecordWriter,
                request.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ResetStaffPassword,
                "StaffUser",
                staffUserId.ToString("D"),
                AuditOutcome.Succeeded,
                new { staffUser.UserName, TokensRevoked = true },
                cancellationToken);

            return Results.Ok(response);
        });

    }
}
