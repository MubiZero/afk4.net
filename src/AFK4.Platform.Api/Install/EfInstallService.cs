using System.Security.Cryptography;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AFK4.Platform.Api.Identity;

namespace AFK4.Platform.Api.Install;

public sealed class EfInstallService(
    PlatformDbContext dbContext,
    IFloorMapReadService floorMapReadService,
    IOptions<InstallOptions> options,
    IOptions<SessionLeaseOptions> sessionLeaseOptions,
    TimeProvider timeProvider,
    IDeviceBoundPlayerTokens? deviceTokens = null) : IInstallService
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
        if (organization is null || organization.Status != OrganizationStatusNames.Active)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest("Organization is not active.");
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

        // Эта же машина, зарегистрированная раньше. Опознаём по её ключу: он лежит на диске и
        // переживает повторный запуск мастера. Без этого каждый повтор — а повторяют после
        // обрыва связи, отказа установщика или просто по ошибке — заводил на платформе ещё одно
        // устройство: у клуба копились призраки, а занятое ими место мастер отказывался отдавать
        // той самой машине, которая его и заняла.
        var existingDevice = await dbContext.Devices.SingleOrDefaultAsync(
            candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.BranchId == branchId &&
                candidate.DevicePublicKey == devicePublicKey,
            cancellationToken);

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

            // Место, занятое этой же машиной, для неё не занято.
            var occupyingDeviceId = await dbContext.DeviceSeatAssignments
                .Where(assignment =>
                    assignment.OrganizationId == organizationId &&
                    assignment.BranchId == branchId &&
                    assignment.SeatId == seatId &&
                    assignment.DetachedAtUtc == null)
                .Select(assignment => (Guid?)assignment.DeviceId)
                .FirstOrDefaultAsync(cancellationToken);
            if (occupyingDeviceId is not null && occupyingDeviceId != existingDevice?.DeviceId)
            {
                return InstallOperationResult<InstallEnrollResponse>.Conflict(
                    "Seat already has an active device assignment.",
                    organizationId,
                    branchId,
                    InstallErrorCodeNames.SeatOccupied);
            }
        }

        var now = timeProvider.GetUtcNow();
        var deviceId = existingDevice?.DeviceId ?? Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var credentialSecret = DeviceCredentialSecrets.CreateCredentialSecret();
        var enrollmentState = branch.RequireManualDeviceApproval
            ? DeviceEnrollmentStateNames.Pending
            : DeviceEnrollmentStateNames.Approved;
        if (existingDevice is null)
        {
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
        }
        else
        {
            existingDevice.MachineName = machineName;
            existingDevice.DisplayName = displayName;
            existingDevice.Role = normalizedRole;
            existingDevice.EnrollmentState = enrollmentState;
            existingDevice.EnrolledAtUtc = now;

            // Прежний ключ мог утечь — тем и опасен повтор после неудачи. Отзываем его: с этого
            // момента машина говорит только новым.
            await RevokeActiveCredentialsAsync(deviceId, now, cancellationToken);
            // Вход игрока, выданный этой машине до переустановки, не переживает её.
            if (deviceTokens is not null)
            {
                await deviceTokens.RevokeForDeviceAsync(deviceId, cancellationToken);
            }
        }

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
            await AttachToSeatAsync(organizationId, branchId, seat!.SeatId, deviceId, now, cancellationToken);
        }
        else if (existingDevice is not null)
        {
            // Машина была игровой, а стала рабочим местом управляющего: место надо освободить,
            // иначе оно навсегда числится занятым тем, кого за ним больше нет.
            await DetachFromSeatsAsync(organizationId, deviceId, now, cancellationToken);
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

    private async Task RevokeActiveCredentialsAsync(
        Guid deviceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var active = await dbContext.DeviceCredentials
            .Where(credential => credential.DeviceId == deviceId && credential.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var credential in active)
        {
            credential.RevokedAtUtc = now;
        }
    }

    /// <summary>Привязать к месту, ничего не трогая, если машина уже за ним и числится.</summary>
    private async Task AttachToSeatAsync(
        Guid organizationId,
        Guid branchId,
        Guid seatId,
        Guid deviceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var current = await dbContext.DeviceSeatAssignments
            .Where(assignment => assignment.DeviceId == deviceId && assignment.DetachedAtUtc == null)
            .ToListAsync(cancellationToken);
        if (current.Any(assignment => assignment.SeatId == seatId))
        {
            return;
        }

        // Машину переставили на другое место: прежнее освобождаем, иначе она числится за двумя.
        foreach (var assignment in current)
        {
            assignment.DetachedAtUtc = now;
        }

        dbContext.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            SeatId = seatId,
            DeviceId = deviceId,
            AttachedAtUtc = now
        });
    }

    private async Task DetachFromSeatsAsync(
        Guid organizationId,
        Guid deviceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var current = await dbContext.DeviceSeatAssignments
            .Where(assignment =>
                assignment.OrganizationId == organizationId &&
                assignment.DeviceId == deviceId &&
                assignment.DetachedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var assignment in current)
        {
            assignment.DetachedAtUtc = now;
        }
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
        if (organization is null || organization.Status != OrganizationStatusNames.Active)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                "Organization is not active.",
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
        Guid staffUserId,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
        if (organization is null || organization.Status != OrganizationStatusNames.Active)
        {
            return InstallOperationResult<InstallDiscoverResponse>.BadRequest(
                "Organization is not active.",
                organizationId: organizationId);
        }

        var allowedBranchIds = branchIds.ToArray();
        var branches = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.OrganizationId == organizationId && allowedBranchIds.Contains(branch.BranchId))
            .OrderBy(branch => branch.Name)
            .ToListAsync(cancellationToken);

        // Что в клубе уже настроено — двумя запросами на весь список залов, а не по одному на зал:
        // мастеру это нужно, чтобы не спрашивать про тариф и сотрудников там, где они давно есть.
        var branchesWithTariff = await dbContext.Tariffs
            .AsNoTracking()
            .Where(tariff => tariff.OrganizationId == organizationId
                && allowedBranchIds.Contains(tariff.BranchId)
                && tariff.IsActive)
            .Select(tariff => tariff.BranchId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var branchesWithStaff = await dbContext.StaffRoleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.OrganizationId == organizationId
                && allowedBranchIds.Contains(assignment.BranchId)
                // Владелец есть в клубе с первого дня, а тот, кто сейчас ставит, — это техник или
                // сам управляющий: ни один из них не отвечает на вопрос «есть ли кому работать в зале».
                && assignment.RoleName != OrganizationRoleNames.OrganizationOwner
                && assignment.StaffUserId != staffUserId)
            .Select(assignment => assignment.BranchId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var branchDtos = new List<InstallBranchDto>(branches.Count);
        foreach (var branch in branches)
        {
            var dto = await BuildBranchDtoAsync(branch, cancellationToken);
            branchDtos.Add(dto with
            {
                HasTariff = branchesWithTariff.Contains(branch.BranchId),
                HasStaffBesidesOwner = branchesWithStaff.Contains(branch.BranchId),
            });
        }

        var brandingConfigured = !string.IsNullOrWhiteSpace(organization.LogoUrl)
            || !string.IsNullOrWhiteSpace(organization.AccentColor);

        var response = new InstallDiscoverResponse(ownerDisplayName, branchDtos, brandingConfigured);
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
