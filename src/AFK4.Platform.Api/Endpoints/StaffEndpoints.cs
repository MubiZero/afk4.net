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
using AFK4.Platform.Api.Platform.Entitlements;
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
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ManageBranchStaff);

        app.MapPatch("branches/{branchId:guid}/staff/{staffUserId:guid}/roles", async (
            Guid branchId,
            Guid staffUserId,
            UpdateStaffUserRolesRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IPlanLimitGuard planLimitGuard,
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

            // Назначений в этом филиале нет — это добавление человека из сети в филиал. Место он
            // занимает так же, как новый сотрудник, поэтому и лимит тарифа тот же.
            var addingToBranch = existingAssignments.Count == 0;
            if (addingToBranch)
            {
                var planLimit = await planLimitGuard.CheckStaffUserAsync(request.OrganizationId, branchId, cancellationToken);
                if (planLimit is not null)
                {
                    return Results.Conflict(new { Error = "The plan's staff limit for this branch is reached.", planLimit.Code, PlanLimit = planLimit });
                }
            }

            var requestedRoleNames = request.RoleNames
                .Select(roleName => roleName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var requestedRoleSet = requestedRoleNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            // Роль владельца здесь не выдаётся и не снимается: её в запросе быть не может (её не
            // пропускает проверка ролей), и строка владельца стиралась бы как «не запрошенная» —
            // владелец, отметивший себе «управляющего», терял права владельца в филиале. Она
            // передаётся только передачей организации.
            var assignmentsToRemove = existingAssignments
                .Where(roleAssignment =>
                    roleAssignment.RoleName != OrganizationRoleNames.OrganizationOwner &&
                    !requestedRoleSet.Contains(roleAssignment.RoleName))
                .ToList();
            var roleNames = requestedRoleNames
                .Concat(existingAssignments
                    .Where(roleAssignment => roleAssignment.RoleName == OrganizationRoleNames.OrganizationOwner)
                    .Select(roleAssignment => roleAssignment.RoleName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(roleName => roleName, StringComparer.Ordinal)
                .ToList();

            dbContext.StaffRoleAssignments.RemoveRange(assignmentsToRemove);

            var existingRoleSet = existingAssignments
                .Where(roleAssignment => requestedRoleSet.Contains(roleAssignment.RoleName))
                .Select(roleAssignment => roleAssignment.RoleName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var roleName in requestedRoleNames.Where(roleName => !existingRoleSet.Contains(roleName)))
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
                new { staffUser.UserName, response.RoleNames, AddedToBranch = addingToBranch },
                cancellationToken);

            return Results.Ok(response);
        });

        // Кого можно добавить в этот филиал: сотрудники организации без назначения здесь — в том
        // числе те, у кого назначений не осталось вовсе. Иначе такого человека не показывал ни
        // один список, и вернуть его было нечем. Только владельцу: добавление выдаёт роли.
        app.MapGet("branches/{branchId:guid}/staff/candidates", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
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
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var organizationId = authorization.StaffContext!.OrganizationId;
            var assignments = await dbContext.StaffRoleAssignments
                .AsNoTracking()
                .Where(roleAssignment => roleAssignment.OrganizationId == organizationId)
                .Select(roleAssignment => new { roleAssignment.StaffUserId, roleAssignment.BranchId })
                .Distinct()
                .ToListAsync(cancellationToken);
            var inThisBranch = assignments
                .Where(assignment => assignment.BranchId == branchId)
                .Select(assignment => assignment.StaffUserId)
                .ToHashSet();
            var branchNames = await dbContext.Branches
                .AsNoTracking()
                .Where(branch => branch.OrganizationId == organizationId)
                .ToDictionaryAsync(branch => branch.BranchId, branch => branch.Name, cancellationToken);
            var staffUsers = await dbContext.StaffUsers
                .AsNoTracking()
                .Where(staffUser => staffUser.OrganizationId == organizationId)
                .OrderBy(staffUser => staffUser.DisplayName)
                .ToListAsync(cancellationToken);

            var response = staffUsers
                .Where(staffUser => !inThisBranch.Contains(staffUser.StaffUserId))
                .Select(staffUser => new StaffBranchCandidateDto(
                    staffUser.StaffUserId,
                    staffUser.UserName,
                    staffUser.DisplayName,
                    staffUser.IsActive,
                    assignments
                        .Where(assignment => assignment.StaffUserId == staffUser.StaffUserId)
                        .Select(assignment => branchNames.GetValueOrDefault(assignment.BranchId, ""))
                        .Where(name => name.Length > 0)
                        .OrderBy(name => name, StringComparer.CurrentCulture)
                        .ToList()))
                .ToList();

            return Results.Ok(response);
        });

        // Снять сотрудника с филиала: убрать все его роли здесь. Пустой набор ролей эндпоинт ролей
        // не сохраняет, поэтому снятие — отдельное действие. Себя снять нельзя (так владелец
        // потерял бы доступ к филиалу, которым управляет), владельца — тоже: его роль передаётся
        // только передачей организации. Снятый с последнего филиала остаётся в списке кандидатов
        // и возвращается добавлением.
        app.MapDelete("branches/{branchId:guid}/staff/{staffUserId:guid}", async (
            Guid branchId,
            Guid staffUserId,
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

            var organizationId = authorization.StaffContext!.OrganizationId;
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter, organizationId, branchId, authorization.StaffContext.StaffUserId,
                    AuditActionNames.RemoveStaffFromBranch, "StaffUser", staffUserId.ToString("D"),
                    AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (staffUserId == authorization.StaffContext.StaffUserId)
            {
                return Results.Conflict(new { Error = "You cannot remove yourself from a branch.", Code = "staff_remove_self" });
            }

            var assignments = await dbContext.StaffRoleAssignments
                .Where(roleAssignment =>
                    roleAssignment.OrganizationId == organizationId &&
                    roleAssignment.BranchId == branchId &&
                    roleAssignment.StaffUserId == staffUserId)
                .ToListAsync(cancellationToken);

            if (assignments.Count == 0)
            {
                return Results.NotFound();
            }

            if (assignments.Any(roleAssignment => roleAssignment.RoleName == OrganizationRoleNames.OrganizationOwner))
            {
                return Results.Conflict(new { Error = "An organization owner cannot be removed from a branch.", Code = "staff_remove_owner" });
            }

            dbContext.StaffRoleAssignments.RemoveRange(assignments);
            await dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter, organizationId, branchId, authorization.StaffContext.StaffUserId,
                AuditActionNames.RemoveStaffFromBranch, "StaffUser", staffUserId.ToString("D"),
                AuditOutcome.Succeeded, new { RoleNames = assignments.Select(roleAssignment => roleAssignment.RoleName).ToList() },
                cancellationToken);

            return Results.NoContent();
        });
        // Не AllowPlatformSupportAccess: смена ролей сотрудника может выдать денежные права
        // (например BranchManager), а грант поддержки не отзывается вместе с этим доступом —
        // это постоянный обход временной границы. См. docs/runbooks/support-mode.md.

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
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ManageBranchStaff);

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
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ManageBranchStaff);

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

            var validation = ValidateStaffPin(request.NewPassword);
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
        // Не AllowPlatformSupportAccess: сброс пароля сотрудника открывает вход под его логином
        // (у персонала клуба нет 2FA), а через него — тот же обход денежных прав, что и смена
        // ролей выше. См. docs/runbooks/support-mode.md.

    }
}
