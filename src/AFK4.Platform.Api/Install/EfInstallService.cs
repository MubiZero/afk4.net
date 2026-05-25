using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity.OwnerCodes;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Install;

public sealed class EfInstallService(
    PlatformDbContext dbContext,
    IOwnerCodeService ownerCodeService,
    IFloorMapReadService floorMapReadService,
    IOptions<InstallOptions> options,
    TimeProvider timeProvider) : IInstallService
{
    private const int MaxResolvedOwnerCodeFailures = 5;

    public async Task<InstallOperationResult<InstallDiscoverResponse>> DiscoverAsync(
        InstallDiscoverRequest request,
        CancellationToken cancellationToken)
    {
        var lookup = await ownerCodeService.LookupActiveAsync(request.OwnerCode, cancellationToken);
        if (!lookup.Succeeded)
        {
            return InstallOperationResult<InstallDiscoverResponse>.BadRequest(
                lookup.Error ?? "Owner code is invalid.",
                lookup.OwnerCodeId);
        }

        var staffUser = await dbContext.StaffUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.StaffUserId == lookup.StaffUserId &&
                    candidate.OrganizationId == lookup.OrganizationId &&
                    candidate.IsActive,
                cancellationToken);
        if (staffUser is null)
        {
            return InstallOperationResult<InstallDiscoverResponse>.BadRequest(
                "Owner code staff user was not found.",
                lookup.OwnerCodeId);
        }

        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == lookup.OrganizationId, cancellationToken);
        if (organization is null || organization.Status != TenantStatusNames.Active)
        {
            return InstallOperationResult<InstallDiscoverResponse>.BadRequest(
                "Tenant is not active.",
                lookup.OwnerCodeId,
                lookup.OrganizationId);
        }

        var branches = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.OrganizationId == lookup.OrganizationId)
            .OrderBy(branch => branch.Name)
            .ToListAsync(cancellationToken);

        var branchDtos = new List<InstallBranchDto>(branches.Count);
        foreach (var branch in branches)
        {
            var floorMapResult = await floorMapReadService.GetFloorMapAsync(branch.BranchId, cancellationToken);
            var floorMap = floorMapResult?.FloorMap;
            if (floorMap is null)
            {
                floorMap = new FloorMapDto(branch.BranchId, branch.Name, []);
            }

            var occupiedSeatIds = floorMap.Seats
                .Where(seat => seat.DeviceId is not null)
                .Select(seat => seat.SeatId)
                .ToHashSet();
            var freeSeatIds = floorMap.Seats
                .Where(seat => !occupiedSeatIds.Contains(seat.SeatId))
                .Select(seat => seat.SeatId)
                .ToArray();

            branchDtos.Add(new InstallBranchDto(
                branch.BranchId,
                branch.Slug,
                branch.Name,
                floorMap,
                freeSeatIds));
        }

        var response = new InstallDiscoverResponse(staffUser.DisplayName, branchDtos);
        return InstallOperationResult<InstallDiscoverResponse>.Success(
            response,
            lookup.OrganizationId!.Value,
            null,
            lookup.OwnerCodeId!.Value);
    }

    public async Task<InstallOperationResult<InstallEnrollResponse>> EnrollAsync(
        InstallEnrollRequest request,
        CancellationToken cancellationToken)
    {
        var lookup = await ownerCodeService.LookupActiveAsync(request.OwnerCode, cancellationToken);
        if (!lookup.Succeeded)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                lookup.Error ?? "Owner code is invalid.",
                lookup.OwnerCodeId);
        }

        var organizationId = lookup.OrganizationId!.Value;
        var ownerCodeId = lookup.OwnerCodeId!.Value;
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
        if (organization is null || organization.Status != TenantStatusNames.Active)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Tenant is not active.",
                ownerCodeId);
        }

        var branch = await dbContext.Branches
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.BranchId == request.BranchId,
                cancellationToken);
        if (branch is null)
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Branch was not found for this owner code.",
                ownerCodeId);
        }

        if (!IsValidDeviceRole(request.Role))
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Device role is invalid.",
                ownerCodeId);
        }

        if (string.IsNullOrWhiteSpace(request.MachineName))
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Machine name is required.",
                ownerCodeId);
        }

        var seat = await dbContext.Seats
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == request.BranchId &&
                    candidate.SeatId == request.SeatId,
                cancellationToken);
        if (seat is null)
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Seat was not found in this branch.",
                ownerCodeId);
        }

        var hasActiveAssignment = await dbContext.DeviceSeatAssignments
            .AnyAsync(
                assignment =>
                    assignment.OrganizationId == organizationId &&
                    assignment.BranchId == request.BranchId &&
                    assignment.SeatId == request.SeatId &&
                    assignment.DetachedAtUtc == null,
                cancellationToken);
        if (hasActiveAssignment)
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
            return InstallOperationResult<InstallEnrollResponse>.Conflict(
                "Seat already has an active device assignment.",
                organizationId,
                request.BranchId,
                ownerCodeId);
        }

        var now = timeProvider.GetUtcNow();
        var deviceId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var credentialSecret = DeviceCredentialSecrets.CreateCredentialSecret();
        var enrollmentState = branch.RequireManualDeviceApproval
            ? DeviceEnrollmentStateNames.Pending
            : DeviceEnrollmentStateNames.Approved;
        var machineName = request.MachineName.Trim();
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? machineName
            : request.DisplayName.Trim();

        dbContext.Devices.Add(new DeviceEntity
        {
            DeviceId = deviceId,
            OrganizationId = organizationId,
            BranchId = request.BranchId,
            MachineName = machineName,
            DisplayName = displayName,
            Role = request.Role.Trim(),
            EnrollmentState = enrollmentState,
            EnrolledViaOwnerCodeId = ownerCodeId,
            AgentVersion = string.Empty,
            ShellVersion = string.Empty,
            EnrolledAtUtc = now
        });

        dbContext.DeviceCredentials.Add(new DeviceCredentialEntity
        {
            CredentialId = credentialId,
            OrganizationId = organizationId,
            BranchId = request.BranchId,
            DeviceId = deviceId,
            SecretHash = DeviceCredentialSecrets.HashSecret(credentialSecret),
            CreatedAtUtc = now
        });

        dbContext.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = request.BranchId,
            SeatId = request.SeatId,
            DeviceId = deviceId,
            AttachedAtUtc = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new InstallEnrollResponse(
            organizationId,
            request.BranchId,
            deviceId,
            credentialId,
            credentialSecret,
            enrollmentState,
            options.Value.ApiBaseUrl.TrimEnd('/'),
            options.Value.UpdateChannel,
            now);

        return InstallOperationResult<InstallEnrollResponse>.Success(
            response,
            organizationId,
            request.BranchId,
            ownerCodeId);
    }

    private async Task RecordResolvedOwnerCodeFailureAsync(Guid ownerCodeId, CancellationToken cancellationToken)
    {
        var ownerCode = await dbContext.OwnerCodes
            .SingleOrDefaultAsync(candidate => candidate.OwnerCodeId == ownerCodeId, cancellationToken);
        if (ownerCode is null || ownerCode.RevokedAtUtc is not null)
        {
            return;
        }

        ownerCode.FailedAttemptCount++;
        if (ownerCode.FailedAttemptCount >= MaxResolvedOwnerCodeFailures)
        {
            ownerCode.RevokedAtUtc = timeProvider.GetUtcNow();
            ownerCode.RevokedReason = "brute_force_detected";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsValidDeviceRole(string role)
    {
        var normalized = role.Trim();
        return normalized is DeviceRoleNames.GamingPc or DeviceRoleNames.ManagerWorkstation;
    }
}
