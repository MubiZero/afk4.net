using System.Security.Cryptography;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Install;

public sealed class EfInstallService(
    PlatformDbContext dbContext,
    IFloorMapReadService floorMapReadService,
    IOptions<InstallOptions> options,
    IOptions<SessionLeaseOptions> sessionLeaseOptions,
    TimeProvider timeProvider) : IInstallService
{
    private const int MaxMachineNameLength = 128;
    private const int MinDisplayNameLength = 3;
    private const int MaxDisplayNameLength = 32;
    private const int MaxDevicePublicKeyLength = 4096;
    private const int MaxSeatNameLength = 80;

    public async Task<InstallOperationResult<InstallEnrollResponse>> EnrollForStaffAsync(
        Guid organizationId,
        AuthenticatedInstallEnrollRequest request,
        CancellationToken cancellationToken)
    {
        return await EnrollResolvedAsync(
            organizationId,
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
            return InstallOperationResult<InstallEnrollResponse>.BadRequest("Tenant is not active.");
        }

        var branch = await dbContext.Branches
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.BranchId == branchId,
                cancellationToken);
        if (branch is null)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest("Branch was not found.");
        }

        var normalizedRole = requestedRole.Trim();
        if (!IsValidDeviceRole(normalizedRole))
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest("Device role is invalid.");
        }

        var requiresSeatAssignment = normalizedRole == DeviceRoleNames.GamingPc;

        var machineName = requestedMachineName.Trim();
        if (machineName.Length == 0)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest("Machine name is required.");
        }

        if (machineName.Length > MaxMachineNameLength)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                $"Machine name must be {MaxMachineNameLength} characters or fewer.");
        }

        var providedDisplayName = (requestedDisplayName ?? string.Empty).Trim();
        if (providedDisplayName.Length > 0 && providedDisplayName.Length < MinDisplayNameLength)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                $"Display name must be at least {MinDisplayNameLength} characters.");
        }

        var displayName = providedDisplayName.Length == 0 ? machineName : providedDisplayName;
        if (displayName.Length > MaxDisplayNameLength)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                $"Display name must be {MaxDisplayNameLength} characters or fewer.");
        }

        var devicePublicKey = requestedDevicePublicKey.Trim();
        if (devicePublicKey.Length == 0)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest("Device public key is required.");
        }

        if (devicePublicKey.Length > MaxDevicePublicKeyLength)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                $"Device public key must be {MaxDevicePublicKeyLength} characters or fewer.");
        }

        if (requiresSeatAssignment && (requestedSeatId is null || requestedSeatId == Guid.Empty))
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Seat is required for gaming PC enrollment.");
        }

        if (!requiresSeatAssignment && requestedSeatId is not null && requestedSeatId != Guid.Empty)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Manager workstation enrollment must not target a seat.");
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
                    "Seat was not found in this branch.");
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
                    branchId);
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
            branchId);
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
            staffUserId: staffUserId,
            cancellationToken);
    }

    private async Task<InstallOperationResult<InstallCreateSeatResponse>> CreateSeatResolvedAsync(
        Guid organizationId,
        Guid branchId,
        Guid zoneId,
        string name,
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
                organizationId,
                branchId,
                staffUserId);
        }

        var seatName = name.Trim();
        if (seatName.Length == 0)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                "Seat name is required.",
                organizationId,
                branchId,
                staffUserId);
        }

        if (seatName.Length > MaxSeatNameLength)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                $"Seat name must be {MaxSeatNameLength} characters or fewer.",
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
            branchId: null);
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
