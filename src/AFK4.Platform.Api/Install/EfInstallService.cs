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
    private const int MinDisplayNameLength = 3;
    private const int MaxDisplayNameLength = 32;
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
            branchDtos.Add(await BuildBranchDtoAsync(branch, cancellationToken));
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

        var result = await EnrollResolvedAsync(
            organizationId,
            enrolledViaOwnerCodeId: ownerCodeId,
            request.BranchId,
            request.SeatId,
            request.Role,
            request.DisplayName,
            request.MachineName,
            request.DevicePublicKey,
            cancellationToken);

        if (!result.Succeeded)
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
        }

        return result;
    }

    public async Task<InstallOperationResult<InstallEnrollResponse>> EnrollForStaffAsync(
        Guid organizationId,
        AuthenticatedInstallEnrollRequest request,
        CancellationToken cancellationToken)
    {
        return await EnrollResolvedAsync(
            organizationId,
            enrolledViaOwnerCodeId: null,
            request.BranchId,
            request.SeatId,
            request.Role,
            request.DisplayName,
            request.MachineName,
            request.DevicePublicKey,
            cancellationToken);
    }

    private async Task<InstallOperationResult<InstallEnrollResponse>> EnrollResolvedAsync(
        Guid organizationId,
        Guid? enrolledViaOwnerCodeId,
        Guid branchId,
        Guid? requestedSeatId,
        string requestedRole,
        string? requestedDisplayName,
        string requestedMachineName,
        string requestedDevicePublicKey,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
        if (organization is null || organization.Status != TenantStatusNames.Active)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Tenant is not active.",
                enrolledViaOwnerCodeId);
        }

        var branch = await dbContext.Branches
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.BranchId == branchId,
                cancellationToken);
        if (branch is null)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Branch was not found.",
                enrolledViaOwnerCodeId);
        }

        var normalizedRole = requestedRole.Trim();
        if (!IsValidDeviceRole(normalizedRole))
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Device role is invalid.",
                enrolledViaOwnerCodeId);
        }

        var requiresSeatAssignment = normalizedRole == DeviceRoleNames.GamingPc;

        var machineName = requestedMachineName.Trim();
        if (machineName.Length == 0)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Machine name is required.",
                enrolledViaOwnerCodeId);
        }

        if (machineName.Length > MaxMachineNameLength)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                $"Machine name must be {MaxMachineNameLength} characters or fewer.",
                enrolledViaOwnerCodeId);
        }

        var providedDisplayName = (requestedDisplayName ?? string.Empty).Trim();
        if (providedDisplayName.Length > 0 && providedDisplayName.Length < MinDisplayNameLength)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                $"Display name must be at least {MinDisplayNameLength} characters.",
                enrolledViaOwnerCodeId);
        }

        var displayName = providedDisplayName.Length == 0 ? machineName : providedDisplayName;
        if (displayName.Length > MaxDisplayNameLength)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                $"Display name must be {MaxDisplayNameLength} characters or fewer.",
                enrolledViaOwnerCodeId);
        }

        var devicePublicKey = requestedDevicePublicKey.Trim();
        if (devicePublicKey.Length == 0)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Device public key is required.",
                enrolledViaOwnerCodeId);
        }

        if (devicePublicKey.Length > MaxDevicePublicKeyLength)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                $"Device public key must be {MaxDevicePublicKeyLength} characters or fewer.",
                enrolledViaOwnerCodeId);
        }

        if (requiresSeatAssignment && (requestedSeatId is null || requestedSeatId == Guid.Empty))
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Seat is required for gaming PC enrollment.",
                enrolledViaOwnerCodeId);
        }

        if (!requiresSeatAssignment && requestedSeatId is not null && requestedSeatId != Guid.Empty)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Manager workstation enrollment must not target a seat.",
                enrolledViaOwnerCodeId);
        }

        SeatEntity? seat = null;
        if (requiresSeatAssignment)
        {
            var seatId = requestedSeatId!.Value;
            seat = await dbContext.Seats
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.OrganizationId == organizationId &&
                        candidate.BranchId == branchId &&
                        candidate.SeatId == seatId,
                    cancellationToken);
            if (seat is null)
            {
                return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                    "Seat was not found in this branch.",
                    enrolledViaOwnerCodeId);
            }

            var hasActiveAssignment = await dbContext.DeviceSeatAssignments
                .AnyAsync(
                    assignment =>
                        assignment.OrganizationId == organizationId &&
                        assignment.BranchId == branchId &&
                        assignment.SeatId == seatId &&
                        assignment.DetachedAtUtc == null,
                    cancellationToken);
            if (hasActiveAssignment)
            {
                return InstallOperationResult<InstallEnrollResponse>.Conflict(
                    "Seat already has an active device assignment.",
                    organizationId,
                    branchId,
                    enrolledViaOwnerCodeId);
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
            BranchId = branchId,
            MachineName = machineName,
            DisplayName = displayName,
            DevicePublicKey = devicePublicKey,
            Role = normalizedRole,
            EnrollmentState = enrollmentState,
            EnrolledViaOwnerCodeId = enrolledViaOwnerCodeId,
            AgentVersion = string.Empty,
            ShellVersion = string.Empty,
            EnrolledAtUtc = now
        });

        dbContext.DeviceCredentials.Add(new DeviceCredentialEntity
        {
            CredentialId = credentialId,
            OrganizationId = organizationId,
            BranchId = branchId,
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
                BranchId = branchId,
                SeatId = seat!.SeatId,
                DeviceId = deviceId,
                AttachedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new InstallEnrollResponse(
            organizationId,
            branchId,
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
            branchId,
            enrolledViaOwnerCodeId);
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

        var result = await CreateSeatResolvedAsync(
            organizationId,
            request.BranchId,
            request.ZoneId,
            request.Name,
            ownerCodeId: ownerCodeId,
            staffUserId: staffUserId,
            cancellationToken);

        if (!result.Succeeded)
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
        }

        return result;
    }

    public async Task<InstallOperationResult<InstallCreateSeatResponse>> CreateSeatForStaffAsync(
        Guid organizationId,
        Guid? staffUserId,
        AuthenticatedInstallCreateSeatRequest request,
        CancellationToken cancellationToken)
    {
        return await CreateSeatResolvedAsync(
            organizationId,
            request.BranchId,
            request.ZoneId,
            request.Name,
            ownerCodeId: null,
            staffUserId: staffUserId,
            cancellationToken);
    }

    private async Task<InstallOperationResult<InstallCreateSeatResponse>> CreateSeatResolvedAsync(
        Guid organizationId,
        Guid branchId,
        Guid zoneId,
        string name,
        Guid? ownerCodeId,
        Guid? staffUserId,
        CancellationToken cancellationToken)
    {
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
            branch => branch.OrganizationId == organizationId && branch.BranchId == branchId,
            cancellationToken);
        if (!branchExists)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                "Branch was not found.",
                ownerCodeId,
                organizationId,
                staffUserId: staffUserId);
        }

        var zoneExists = await dbContext.Zones.AnyAsync(
            zone =>
                zone.OrganizationId == organizationId &&
                zone.BranchId == branchId &&
                zone.ZoneId == zoneId,
            cancellationToken);
        if (!zoneExists)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                "Zone was not found for this branch.",
                ownerCodeId,
                organizationId,
                branchId,
                staffUserId);
        }

        var seatName = name.Trim();
        if (seatName.Length == 0)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                "Seat name is required.",
                ownerCodeId,
                organizationId,
                branchId,
                staffUserId);
        }

        if (seatName.Length > MaxSeatNameLength)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                $"Seat name must be {MaxSeatNameLength} characters or fewer.",
                ownerCodeId,
                organizationId,
                branchId,
                staffUserId);
        }

        var normalizedName = seatName.ToUpperInvariant();
        var existingSeat = await dbContext.Seats.SingleOrDefaultAsync(
            seat =>
                seat.OrganizationId == organizationId &&
                seat.BranchId == branchId &&
                seat.ZoneId == zoneId &&
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
                branchId,
                ownerCodeId,
                staffUserId);
        }

        var nextSortOrder = await dbContext.Seats
            .Where(seat =>
                seat.OrganizationId == organizationId &&
                seat.BranchId == branchId &&
                seat.ZoneId == zoneId)
            .Select(seat => (int?)seat.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;
        var now = timeProvider.GetUtcNow();
        var created = new SeatEntity
        {
            SeatId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            ZoneId = zoneId,
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
            branchId,
            ownerCodeId,
            staffUserId);
    }

    public async Task<InstallOperationResult<InstallDiscoverResponse>> DiscoverForStaffAsync(
        Guid organizationId,
        IReadOnlySet<Guid> branchIds,
        string ownerDisplayName,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
        if (organization is null || organization.Status != TenantStatusNames.Active)
        {
            return InstallOperationResult<InstallDiscoverResponse>.BadRequest(
                "Tenant is not active.",
                organizationId: organizationId);
        }

        var allowedBranchIds = branchIds.ToArray();
        var branches = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.OrganizationId == organizationId && allowedBranchIds.Contains(branch.BranchId))
            .OrderBy(branch => branch.Name)
            .ToListAsync(cancellationToken);

        var branchDtos = new List<InstallBranchDto>(branches.Count);
        foreach (var branch in branches)
        {
            branchDtos.Add(await BuildBranchDtoAsync(branch, cancellationToken));
        }

        var response = new InstallDiscoverResponse(ownerDisplayName, branchDtos);
        return InstallOperationResult<InstallDiscoverResponse>.Success(
            response,
            organizationId,
            branchId: null,
            ownerCodeId: null);
    }

    private async Task<InstallBranchDto> BuildBranchDtoAsync(BranchEntity branch, CancellationToken cancellationToken)
    {
        var floorMapResult = await floorMapReadService.GetFloorMapAsync(branch.BranchId, cancellationToken);
        var floorMap = floorMapResult?.FloorMap ?? new FloorMapDto(branch.BranchId, branch.Name, []);

        var occupiedSeatIds = floorMap.Seats
            .Where(seat => seat.DeviceId is not null)
            .Select(seat => seat.SeatId)
            .ToHashSet();
        var freeSeatIds = floorMap.Seats
            .Where(seat => !occupiedSeatIds.Contains(seat.SeatId))
            .Select(seat => seat.SeatId)
            .ToArray();

        return new InstallBranchDto(
            branch.BranchId,
            branch.Slug,
            branch.Name,
            floorMap,
            freeSeatIds);
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
