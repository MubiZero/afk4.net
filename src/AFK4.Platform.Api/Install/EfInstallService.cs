using System.Security.Cryptography;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity.OwnerCodes;
using AFK4.Platform.Api.Sessions;
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
    IOptions<SessionLeaseOptions> sessionLeaseOptions,
    TimeProvider timeProvider) : IInstallService
{
    private const int MaxResolvedOwnerCodeFailures = 5;
    private const int MaxMachineNameLength = 128;
    private const int MaxDisplayNameLength = 80;
    private const int MaxDevicePublicKeyLength = 4096;
    private const int MaxSeatNameLength = 80;

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

        var normalizedRole = request.Role.Trim();
        if (!IsValidDeviceRole(normalizedRole))
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Device role is invalid.",
                ownerCodeId);
        }

        var requiresSeatAssignment = normalizedRole == DeviceRoleNames.GamingPc;

        var machineName = request.MachineName.Trim();
        if (machineName.Length == 0)
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Machine name is required.",
                ownerCodeId);
        }

        if (machineName.Length > MaxMachineNameLength)
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                $"Machine name must be {MaxMachineNameLength} characters or fewer.",
                ownerCodeId);
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? machineName
            : request.DisplayName.Trim();
        if (displayName.Length > MaxDisplayNameLength)
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                $"Display name must be {MaxDisplayNameLength} characters or fewer.",
                ownerCodeId);
        }

        var devicePublicKey = request.DevicePublicKey.Trim();
        if (devicePublicKey.Length == 0)
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Device public key is required.",
                ownerCodeId);
        }

        if (devicePublicKey.Length > MaxDevicePublicKeyLength)
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                $"Device public key must be {MaxDevicePublicKeyLength} characters or fewer.",
                ownerCodeId);
        }

        if (requiresSeatAssignment && (request.SeatId is null || request.SeatId == Guid.Empty))
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Seat is required for gaming PC enrollment.",
                ownerCodeId);
        }

        if (!requiresSeatAssignment && request.SeatId is not null && request.SeatId != Guid.Empty)
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Manager workstation enrollment must not target a seat.",
                ownerCodeId);
        }

        SeatEntity? seat = null;
        if (requiresSeatAssignment)
        {
            var seatId = request.SeatId!.Value;
            seat = await dbContext.Seats
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.OrganizationId == organizationId &&
                        candidate.BranchId == request.BranchId &&
                        candidate.SeatId == seatId,
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
                        assignment.SeatId == seatId &&
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
        }

        var now = timeProvider.GetUtcNow();
        var deviceId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var credentialSecret = DeviceCredentialSecrets.CreateCredentialSecret();
        var enrollmentState = branch.RequireManualDeviceApproval
            ? DeviceEnrollmentStateNames.Pending
            : DeviceEnrollmentStateNames.Approved;
        dbContext.Devices.Add(new DeviceEntity
        {
            DeviceId = deviceId,
            OrganizationId = organizationId,
            BranchId = request.BranchId,
            MachineName = machineName,
            DisplayName = displayName,
            DevicePublicKey = devicePublicKey,
            Role = normalizedRole,
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

        if (requiresSeatAssignment)
        {
            dbContext.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = request.BranchId,
                SeatId = seat!.SeatId,
                DeviceId = deviceId,
                AttachedAtUtc = now
            });
        }

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
            now)
        {
            LeaseSigningPublicKeyPem = ResolveLeaseSigningPublicKeyPem(),
            UpdatePackageSigningPublicKeyPem = options.Value.UpdatePackageSigningPublicKeyPem
        };

        return InstallOperationResult<InstallEnrollResponse>.Success(
            response,
            organizationId,
            request.BranchId,
            ownerCodeId);
    }

    public async Task<InstallOperationResult<InstallCreateSeatResponse>> CreateSeatAsync(
        InstallCreateSeatRequest request,
        CancellationToken cancellationToken)
    {
        var lookup = await ownerCodeService.LookupActiveAsync(request.OwnerCode, cancellationToken);
        if (!lookup.Succeeded)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                lookup.Error ?? "Owner code is invalid.",
                lookup.OwnerCodeId);
        }

        var organizationId = lookup.OrganizationId!.Value;
        var ownerCodeId = lookup.OwnerCodeId!.Value;
        var staffUserId = lookup.StaffUserId!.Value;
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
        if (organization is null || organization.Status != TenantStatusNames.Active)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                "Tenant is not active.",
                ownerCodeId,
                organizationId,
                staffUserId: staffUserId);
        }

        var branchExists = await dbContext.Branches.AnyAsync(
            branch => branch.OrganizationId == organizationId && branch.BranchId == request.BranchId,
            cancellationToken);
        if (!branchExists)
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                "Branch was not found for this owner code.",
                ownerCodeId,
                organizationId,
                staffUserId: staffUserId);
        }

        var zoneExists = await dbContext.Zones.AnyAsync(
            zone =>
                zone.OrganizationId == organizationId &&
                zone.BranchId == request.BranchId &&
                zone.ZoneId == request.ZoneId,
            cancellationToken);
        if (!zoneExists)
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                "Zone was not found for this branch.",
                ownerCodeId,
                organizationId,
                request.BranchId,
                staffUserId);
        }

        var seatName = request.Name.Trim();
        if (seatName.Length == 0)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                "Seat name is required.",
                ownerCodeId,
                organizationId,
                request.BranchId,
                staffUserId);
        }

        if (seatName.Length > MaxSeatNameLength)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                $"Seat name must be {MaxSeatNameLength} characters or fewer.",
                ownerCodeId,
                organizationId,
                request.BranchId,
                staffUserId);
        }

        var normalizedName = seatName.ToUpperInvariant();
        var existingSeat = await dbContext.Seats.SingleOrDefaultAsync(
            seat =>
                seat.OrganizationId == organizationId &&
                seat.BranchId == request.BranchId &&
                seat.ZoneId == request.ZoneId &&
                seat.Name.ToUpper() == normalizedName,
            cancellationToken);
        if (existingSeat is not null)
        {
            var existingResponse = new InstallCreateSeatResponse(
                existingSeat.OrganizationId,
                existingSeat.BranchId,
                existingSeat.ZoneId,
                existingSeat.SeatId,
                existingSeat.Name,
                existingSeat.SortOrder);
            return InstallOperationResult<InstallCreateSeatResponse>.Success(
                existingResponse,
                organizationId,
                request.BranchId,
                ownerCodeId,
                staffUserId);
        }

        var nextSortOrder = await dbContext.Seats
            .Where(seat =>
                seat.OrganizationId == organizationId &&
                seat.BranchId == request.BranchId &&
                seat.ZoneId == request.ZoneId)
            .Select(seat => (int?)seat.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;
        var now = timeProvider.GetUtcNow();
        var created = new SeatEntity
        {
            SeatId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = request.BranchId,
            ZoneId = request.ZoneId,
            Name = seatName,
            SortOrder = nextSortOrder + 1,
            CreatedAtUtc = now
        };
        dbContext.Seats.Add(created);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new InstallCreateSeatResponse(
            created.OrganizationId,
            created.BranchId,
            created.ZoneId,
            created.SeatId,
            created.Name,
            created.SortOrder);
        return InstallOperationResult<InstallCreateSeatResponse>.Success(
            response,
            organizationId,
            request.BranchId,
            ownerCodeId,
            staffUserId);
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

    private static bool IsValidDeviceRole(string role) =>
        role is DeviceRoleNames.GamingPc or DeviceRoleNames.ManagerWorkstation;

    private string ResolveLeaseSigningPublicKeyPem()
    {
        if (!string.IsNullOrWhiteSpace(options.Value.LeaseSigningPublicKeyPem))
        {
            return options.Value.LeaseSigningPublicKeyPem;
        }

        if (string.IsNullOrWhiteSpace(sessionLeaseOptions.Value.SigningPrivateKeyPem))
        {
            return string.Empty;
        }

        using var signingKey = ECDsa.Create();
        signingKey.ImportFromPem(sessionLeaseOptions.Value.SigningPrivateKeyPem);
        return signingKey.ExportSubjectPublicKeyInfoPem();
    }
}
