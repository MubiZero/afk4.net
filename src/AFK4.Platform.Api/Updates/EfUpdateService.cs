using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Updates;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Updates;

public sealed class EfUpdateService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IUpdateService
{
    public async Task<UpdateServiceResult<UpdatePackageDto>> RegisterPackageAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateUpdatePackageRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidatePackageRequest(request);
        if (validation is not null)
        {
            return UpdateServiceResult<UpdatePackageDto>.Invalid(validation);
        }

        var duplicate = await dbContext.UpdatePackages
            .AsNoTracking()
            .AnyAsync(
                package =>
                    package.OrganizationId == request.OrganizationId &&
                    package.BranchId == branchId &&
                    package.Component == request.Component &&
                    package.Version == request.Version &&
                    package.Channel == request.Channel,
                cancellationToken);

        if (duplicate)
        {
            return UpdateServiceResult<UpdatePackageDto>.Invalid("An update package already exists for this component, version, and channel.");
        }

        var now = timeProvider.GetUtcNow();
        var entity = new UpdatePackageEntity
        {
            UpdatePackageId = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            BranchId = branchId,
            Component = request.Component.Trim(),
            Version = request.Version.Trim(),
            Channel = request.Channel.Trim(),
            ArtifactUri = request.ArtifactUri.Trim(),
            Sha256 = request.Sha256.Trim(),
            Signature = request.Signature.Trim(),
            SignatureAlgorithm = request.SignatureAlgorithm.Trim(),
            SizeBytes = request.SizeBytes,
            State = UpdatePackageStateNames.Registered,
            ReleaseNotes = request.ReleaseNotes.Trim(),
            CreatedByStaffUserId = actorStaffUserId,
            CreatedAtUtc = now
        };

        dbContext.UpdatePackages.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return UpdateServiceResult<UpdatePackageDto>.Ok(ToDto(entity));
    }

    public async Task<UpdateServiceResult<UpdateRolloutDto>> CreateRolloutAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateUpdateRolloutRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateRolloutRequest(request);
        if (validation is not null)
        {
            return UpdateServiceResult<UpdateRolloutDto>.Invalid(validation);
        }

        var package = await dbContext.UpdatePackages
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == branchId &&
                    candidate.UpdatePackageId == request.UpdatePackageId,
                cancellationToken);

        if (package is null)
        {
            return UpdateServiceResult<UpdateRolloutDto>.Missing("Update package was not found.");
        }

        if (!string.Equals(package.Channel, request.Channel, StringComparison.Ordinal))
        {
            return UpdateServiceResult<UpdateRolloutDto>.Invalid("Rollout channel must match the package channel.");
        }

        var now = timeProvider.GetUtcNow();
        var rollout = new UpdateRolloutEntity
        {
            UpdateRolloutId = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            BranchId = branchId,
            UpdatePackageId = package.UpdatePackageId,
            Component = package.Component,
            Version = package.Version,
            Channel = package.Channel,
            State = UpdateRolloutStateNames.Active,
            TargetKind = request.TargetKind.Trim(),
            BatchPercent = request.BatchPercent,
            Reason = request.Reason.Trim(),
            CreatedByStaffUserId = actorStaffUserId,
            CreatedAtUtc = now,
            StartsAtUtc = request.StartsAtUtc,
            CompletedAtUtc = null
        };

        dbContext.UpdateRollouts.Add(rollout);
        foreach (var deviceId in request.TargetKind == UpdateTargetKindNames.Device
            ? request.TargetDeviceIds.Distinct()
            : [])
        {
            dbContext.UpdateRolloutTargets.Add(new UpdateRolloutTargetEntity
            {
                UpdateRolloutTargetId = Guid.NewGuid(),
                UpdateRolloutId = rollout.UpdateRolloutId,
                OrganizationId = request.OrganizationId,
                BranchId = branchId,
                TargetKind = UpdateTargetKindNames.Device,
                DeviceId = deviceId,
                CreatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return UpdateServiceResult<UpdateRolloutDto>.Ok(await ToDtoAsync(rollout, cancellationToken));
    }

    public async Task<UpdateServiceResult<UpdateRolloutDto>> GetRolloutAsync(
        Guid organizationId,
        Guid branchId,
        Guid rolloutId,
        CancellationToken cancellationToken)
    {
        var rollout = await dbContext.UpdateRollouts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == branchId &&
                    candidate.UpdateRolloutId == rolloutId,
                cancellationToken);

        return rollout is null
            ? UpdateServiceResult<UpdateRolloutDto>.Missing("Update rollout was not found.")
            : UpdateServiceResult<UpdateRolloutDto>.Ok(await ToDtoAsync(rollout, cancellationToken));
    }

    public async Task<UpdateServiceResult<IReadOnlyList<UpdateRolloutStatusDto>>> ListRolloutStatusesAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var rollouts = await dbContext.UpdateRollouts
            .AsNoTracking()
            .Where(rollout =>
                rollout.OrganizationId == organizationId &&
                rollout.BranchId == branchId)
            .OrderByDescending(rollout => rollout.CreatedAtUtc)
            .ThenBy(rollout => rollout.Component)
            .ToListAsync(cancellationToken);

        if (rollouts.Count == 0)
        {
            return UpdateServiceResult<IReadOnlyList<UpdateRolloutStatusDto>>.Ok([]);
        }

        var rolloutIds = rollouts
            .Select(rollout => rollout.UpdateRolloutId)
            .ToHashSet();
        var targetRows = await dbContext.UpdateRolloutTargets
            .AsNoTracking()
            .Where(target =>
                target.OrganizationId == organizationId &&
                target.BranchId == branchId &&
                target.DeviceId != null &&
                rolloutIds.Contains(target.UpdateRolloutId))
            .ToListAsync(cancellationToken);
        var targetDeviceIdsByRollout = targetRows
            .GroupBy(target => target.UpdateRolloutId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)group
                    .Select(target => target.DeviceId!.Value)
                    .Distinct()
                    .OrderBy(deviceId => deviceId)
                    .ToList());

        var statusRows = await dbContext.DeviceUpdateStatuses
            .AsNoTracking()
            .Where(status =>
                status.OrganizationId == organizationId &&
                status.BranchId == branchId &&
                rolloutIds.Contains(status.UpdateRolloutId))
            .ToListAsync(cancellationToken);
        var statusesByRollout = statusRows
            .GroupBy(status => status.UpdateRolloutId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DeviceUpdateStatusSnapshotDto>)group
                    .OrderBy(status => status.DeviceId)
                    .ThenBy(status => status.Component)
                    .Select(ToStatusSnapshot)
                    .ToList());

        var response = rollouts
            .Select(rollout => ToStatusDto(
                rollout,
                targetDeviceIdsByRollout.GetValueOrDefault(rollout.UpdateRolloutId) ?? [],
                statusesByRollout.GetValueOrDefault(rollout.UpdateRolloutId) ?? []))
            .ToList();

        return UpdateServiceResult<IReadOnlyList<UpdateRolloutStatusDto>>.Ok(response);
    }

    public async Task<UpdateServiceResult<UpdatePackageDto>> ChangePackageStateAsync(
        Guid organizationId,
        Guid branchId,
        Guid packageId,
        UpdatePackageStateChangeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OrganizationId != organizationId)
        {
            return UpdateServiceResult<UpdatePackageDto>.Invalid("OrganizationId must match the authenticated staff organization.");
        }

        var validation = ValidatePackageStateChange(request);
        if (validation is not null)
        {
            return UpdateServiceResult<UpdatePackageDto>.Invalid(validation);
        }

        var package = await dbContext.UpdatePackages
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == branchId &&
                    candidate.UpdatePackageId == packageId,
                cancellationToken);

        if (package is null)
        {
            return UpdateServiceResult<UpdatePackageDto>.Missing("Update package was not found.");
        }

        if (package.State == UpdatePackageStateNames.Retired)
        {
            return UpdateServiceResult<UpdatePackageDto>.Invalid("Retired update packages are terminal.");
        }

        package.State = request.State.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);

        return UpdateServiceResult<UpdatePackageDto>.Ok(ToDto(package));
    }

    public async Task<UpdateServiceResult<UpdateRolloutDto>> ChangeRolloutStateAsync(
        Guid organizationId,
        Guid branchId,
        Guid rolloutId,
        UpdateRolloutStateChangeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OrganizationId != organizationId)
        {
            return UpdateServiceResult<UpdateRolloutDto>.Invalid("OrganizationId must match the authenticated staff organization.");
        }

        var validation = ValidateRolloutStateChange(request);
        if (validation is not null)
        {
            return UpdateServiceResult<UpdateRolloutDto>.Invalid(validation);
        }

        var rollout = await dbContext.UpdateRollouts
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == branchId &&
                    candidate.UpdateRolloutId == rolloutId,
                cancellationToken);

        if (rollout is null)
        {
            return UpdateServiceResult<UpdateRolloutDto>.Missing("Update rollout was not found.");
        }

        if (IsTerminalRolloutState(rollout.State))
        {
            return UpdateServiceResult<UpdateRolloutDto>.Invalid("Completed, rolled-back, and cancelled rollouts are terminal.");
        }

        var nextState = request.State.Trim();
        if (rollout.State == UpdateRolloutStateNames.RollbackRequested &&
            nextState is not UpdateRolloutStateNames.RolledBack and not UpdateRolloutStateNames.Cancelled)
        {
            return UpdateServiceResult<UpdateRolloutDto>.Invalid("Rollback-requested rollouts can only become rolled-back or cancelled.");
        }

        rollout.State = nextState;
        if (IsTerminalRolloutState(nextState))
        {
            rollout.CompletedAtUtc = timeProvider.GetUtcNow();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return UpdateServiceResult<UpdateRolloutDto>.Ok(await ToDtoAsync(rollout, cancellationToken));
    }

    public async Task<UpdateServiceResult<DeviceUpdateCheckResponse>> CheckForUpdatesAsync(
        DeviceUpdateCheckRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OrganizationId == Guid.Empty || request.BranchId == Guid.Empty || request.DeviceId == Guid.Empty)
        {
            return UpdateServiceResult<DeviceUpdateCheckResponse>.Invalid("Organization, branch, and device ids are required.");
        }

        if (!IsSupportedChannel(request.Channel))
        {
            return UpdateServiceResult<DeviceUpdateCheckResponse>.Invalid("Unsupported update channel.");
        }

        var deviceRole = await dbContext.Devices
            .AsNoTracking()
            .Where(
                device =>
                    device.OrganizationId == request.OrganizationId &&
                    device.BranchId == request.BranchId &&
                    device.DeviceId == request.DeviceId)
            .Select(device => device.Role)
            .SingleOrDefaultAsync(cancellationToken);

        if (deviceRole is null)
        {
            return UpdateServiceResult<DeviceUpdateCheckResponse>.Missing("Device was not found.");
        }

        var activeRollouts = await dbContext.UpdateRollouts
            .AsNoTracking()
            .Where(rollout =>
                rollout.OrganizationId == request.OrganizationId &&
                rollout.BranchId == request.BranchId &&
                rollout.Channel == request.Channel &&
                rollout.State == UpdateRolloutStateNames.Active &&
                rollout.StartsAtUtc <= request.CheckedAtUtc)
            .ToListAsync(cancellationToken);
        var rolloutIds = activeRollouts.Select(rollout => rollout.UpdateRolloutId).ToHashSet();
        var deviceTargetRolloutIds = await dbContext.UpdateRolloutTargets
            .AsNoTracking()
            .Where(target =>
                target.OrganizationId == request.OrganizationId &&
                target.BranchId == request.BranchId &&
                target.DeviceId == request.DeviceId &&
                rolloutIds.Contains(target.UpdateRolloutId))
            .Select(target => target.UpdateRolloutId)
            .ToListAsync(cancellationToken);
        var targetedRolloutIds = deviceTargetRolloutIds.ToHashSet();
        var eligibleRollouts = activeRollouts
            .Where(rollout =>
                rollout.TargetKind == UpdateTargetKindNames.Branch ||
                targetedRolloutIds.Contains(rollout.UpdateRolloutId))
            .ToList();
        var packageIds = eligibleRollouts.Select(rollout => rollout.UpdatePackageId).ToHashSet();
        var packages = await dbContext.UpdatePackages
            .AsNoTracking()
            .Where(package => packageIds.Contains(package.UpdatePackageId))
            .ToDictionaryAsync(package => package.UpdatePackageId, cancellationToken);
        var installedVersions = request.InstalledComponents
            .GroupBy(component => component.Component, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Version, StringComparer.Ordinal);
        var updates = new List<ComponentUpdateInstructionDto>();

        foreach (var rollout in eligibleRollouts.OrderBy(rollout => rollout.CreatedAtUtc))
        {
            if (!packages.TryGetValue(rollout.UpdatePackageId, out var package))
            {
                continue;
            }

            if (!IsComponentEligibleForDeviceRole(package.Component, deviceRole))
            {
                continue;
            }

            if (installedVersions.TryGetValue(package.Component, out var installedVersion) &&
                IsInstalledVersionAtLeastPackage(installedVersion, package.Version))
            {
                continue;
            }

            updates.Add(new ComponentUpdateInstructionDto(
                rollout.UpdateRolloutId,
                package.UpdatePackageId,
                package.Component,
                package.Version,
                package.Channel,
                package.ArtifactUri,
                package.Sha256,
                package.Signature,
                package.SignatureAlgorithm,
                package.SizeBytes,
                package.ReleaseNotes));
        }

        return UpdateServiceResult<DeviceUpdateCheckResponse>.Ok(new DeviceUpdateCheckResponse(
            timeProvider.GetUtcNow(),
            updates));
    }

    public async Task<UpdateServiceResult<DeviceUpdateStatusResultDto>> ReportStatusAsync(
        DeviceUpdateStatusReportRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateStatusReport(request);
        if (validation is not null)
        {
            return UpdateServiceResult<DeviceUpdateStatusResultDto>.Invalid(validation);
        }

        var deviceExists = await dbContext.Devices
            .AsNoTracking()
            .AnyAsync(
                device =>
                    device.OrganizationId == request.OrganizationId &&
                    device.BranchId == request.BranchId &&
                    device.DeviceId == request.DeviceId,
                cancellationToken);

        if (!deviceExists)
        {
            return UpdateServiceResult<DeviceUpdateStatusResultDto>.Missing("Device was not found.");
        }

        var rolloutExists = await dbContext.UpdateRollouts
            .AsNoTracking()
            .AnyAsync(
                rollout =>
                    rollout.OrganizationId == request.OrganizationId &&
                    rollout.BranchId == request.BranchId &&
                    rollout.UpdateRolloutId == request.UpdateRolloutId &&
                    rollout.UpdatePackageId == request.UpdatePackageId,
                cancellationToken);

        if (!rolloutExists)
        {
            return UpdateServiceResult<DeviceUpdateStatusResultDto>.Missing("Update rollout was not found.");
        }

        var status = await dbContext.DeviceUpdateStatuses
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == request.BranchId &&
                    candidate.DeviceId == request.DeviceId &&
                    candidate.UpdateRolloutId == request.UpdateRolloutId &&
                    candidate.UpdatePackageId == request.UpdatePackageId &&
                    candidate.Component == request.Component,
                cancellationToken);

        if (status is null)
        {
            status = new DeviceUpdateStatusEntity
            {
                DeviceUpdateStatusId = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                BranchId = request.BranchId,
                DeviceId = request.DeviceId,
                UpdateRolloutId = request.UpdateRolloutId,
                UpdatePackageId = request.UpdatePackageId,
                Component = request.Component.Trim(),
                FirstReportedAtUtc = request.ObservedAtUtc
            };
            dbContext.DeviceUpdateStatuses.Add(status);
        }

        status.InstalledVersion = request.InstalledVersion.Trim();
        status.TargetVersion = request.TargetVersion.Trim();
        status.Status = request.Status.Trim();
        status.Message = request.Message.Trim();
        status.UpdatedAtUtc = request.ObservedAtUtc;

        await dbContext.SaveChangesAsync(cancellationToken);

        return UpdateServiceResult<DeviceUpdateStatusResultDto>.Ok(ToDto(status));
    }

    private async Task<UpdateRolloutDto> ToDtoAsync(
        UpdateRolloutEntity rollout,
        CancellationToken cancellationToken)
    {
        var targetDeviceIds = rollout.TargetKind == UpdateTargetKindNames.Device
            ? await dbContext.UpdateRolloutTargets
                .AsNoTracking()
                .Where(target => target.UpdateRolloutId == rollout.UpdateRolloutId && target.DeviceId != null)
                .OrderBy(target => target.DeviceId)
                .Select(target => target.DeviceId!.Value)
                .ToListAsync(cancellationToken)
            : [];

        return new UpdateRolloutDto(
            rollout.UpdateRolloutId,
            rollout.OrganizationId,
            rollout.BranchId,
            rollout.UpdatePackageId,
            rollout.Component,
            rollout.Version,
            rollout.Channel,
            rollout.State,
            rollout.TargetKind,
            targetDeviceIds,
            rollout.BatchPercent,
            rollout.CreatedAtUtc,
            rollout.StartsAtUtc,
            rollout.CompletedAtUtc);
    }

    private static UpdateRolloutStatusDto ToStatusDto(
        UpdateRolloutEntity rollout,
        IReadOnlyList<Guid> targetDeviceIds,
        IReadOnlyList<DeviceUpdateStatusSnapshotDto> deviceStatuses)
    {
        return new UpdateRolloutStatusDto(
            rollout.UpdateRolloutId,
            rollout.OrganizationId,
            rollout.BranchId,
            rollout.UpdatePackageId,
            rollout.Component,
            rollout.Version,
            rollout.Channel,
            rollout.State,
            rollout.TargetKind,
            targetDeviceIds,
            rollout.BatchPercent,
            rollout.CreatedAtUtc,
            rollout.StartsAtUtc,
            rollout.CompletedAtUtc,
            deviceStatuses);
    }

    private static UpdatePackageDto ToDto(UpdatePackageEntity package)
    {
        return new UpdatePackageDto(
            package.UpdatePackageId,
            package.OrganizationId,
            package.BranchId,
            package.Component,
            package.Version,
            package.Channel,
            package.ArtifactUri,
            package.Sha256,
            package.Signature,
            package.SignatureAlgorithm,
            package.SizeBytes,
            package.State,
            package.ReleaseNotes,
            package.CreatedAtUtc);
    }

    private static DeviceUpdateStatusResultDto ToDto(DeviceUpdateStatusEntity status)
    {
        return new DeviceUpdateStatusResultDto(
            status.DeviceId,
            status.UpdateRolloutId,
            status.UpdatePackageId,
            status.Component,
            status.Status,
            status.Message,
            status.UpdatedAtUtc);
    }

    private static DeviceUpdateStatusSnapshotDto ToStatusSnapshot(DeviceUpdateStatusEntity status)
    {
        return new DeviceUpdateStatusSnapshotDto(
            status.DeviceId,
            status.UpdateRolloutId,
            status.UpdatePackageId,
            status.Component,
            status.InstalledVersion,
            status.TargetVersion,
            status.Status,
            status.Message,
            status.UpdatedAtUtc);
    }

    private static string? ValidatePackageRequest(CreateUpdatePackageRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return "Organization id is required.";
        }

        if (!IsSupportedComponent(request.Component))
        {
            return "Unsupported update component.";
        }

        if (!IsSupportedChannel(request.Channel))
        {
            return "Unsupported update channel.";
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return "Package version is required.";
        }

        if (!Uri.TryCreate(request.ArtifactUri, UriKind.Absolute, out _))
        {
            return "Package artifact URI must be absolute.";
        }

        if (string.IsNullOrWhiteSpace(request.Sha256))
        {
            return "Package SHA-256 hash is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Signature))
        {
            return "Package signature is required.";
        }

        if (string.IsNullOrWhiteSpace(request.SignatureAlgorithm))
        {
            return "Package signature algorithm is required.";
        }

        if (request.SizeBytes <= 0)
        {
            return "Package size must be positive.";
        }

        return null;
    }

    private static string? ValidateRolloutRequest(CreateUpdateRolloutRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return "Organization id is required.";
        }

        if (!IsSupportedChannel(request.Channel))
        {
            return "Unsupported update channel.";
        }

        if (request.TargetKind is not UpdateTargetKindNames.Branch and not UpdateTargetKindNames.Device)
        {
            return "Unsupported rollout target kind.";
        }

        if (request.TargetKind == UpdateTargetKindNames.Device && request.TargetDeviceIds.Count == 0)
        {
            return "Device-targeted rollouts require at least one device id.";
        }

        if (request.BatchPercent is < 1 or > 100)
        {
            return "Batch percent must be between 1 and 100.";
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return "Rollout reason is required.";
        }

        return null;
    }

    private static string? ValidateStatusReport(DeviceUpdateStatusReportRequest request)
    {
        if (request.OrganizationId == Guid.Empty || request.BranchId == Guid.Empty || request.DeviceId == Guid.Empty)
        {
            return "Organization, branch, and device ids are required.";
        }

        if (!IsSupportedComponent(request.Component))
        {
            return "Unsupported update component.";
        }

        if (request.UpdateRolloutId == Guid.Empty || request.UpdatePackageId == Guid.Empty)
        {
            return "Update rollout and package ids are required.";
        }

        if (string.IsNullOrWhiteSpace(request.InstalledVersion))
        {
            return "Installed version is required.";
        }

        if (string.IsNullOrWhiteSpace(request.TargetVersion))
        {
            return "Target version is required.";
        }

        if (!IsSupportedStatus(request.Status))
        {
            return "Unsupported update status.";
        }

        return null;
    }

    private static string? ValidatePackageStateChange(UpdatePackageStateChangeRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return "Organization id is required.";
        }

        if (request.State is not
            UpdatePackageStateNames.Registered and not
            UpdatePackageStateNames.Validated and not
            UpdatePackageStateNames.Rejected and not
            UpdatePackageStateNames.Retired)
        {
            return "Unsupported update package state.";
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return "State change reason is required.";
        }

        return null;
    }

    private static string? ValidateRolloutStateChange(UpdateRolloutStateChangeRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return "Organization id is required.";
        }

        if (request.State is not
            UpdateRolloutStateNames.Active and not
            UpdateRolloutStateNames.Paused and not
            UpdateRolloutStateNames.Completed and not
            UpdateRolloutStateNames.RollbackRequested and not
            UpdateRolloutStateNames.RolledBack and not
            UpdateRolloutStateNames.Cancelled)
        {
            return "Unsupported update rollout state.";
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return "State change reason is required.";
        }

        return null;
    }

    private static bool IsSupportedComponent(string component)
    {
        return component is
            UpdateComponentNames.OperatorApp or
            UpdateComponentNames.AgentService or
            UpdateComponentNames.PlayerShell;
    }

    private static bool IsComponentEligibleForDeviceRole(string component, string deviceRole)
    {
        return component switch
        {
            UpdateComponentNames.AgentService => true,
            UpdateComponentNames.PlayerShell => deviceRole == DeviceRoleNames.GamingPc,
            UpdateComponentNames.OperatorApp => deviceRole == DeviceRoleNames.ManagerWorkstation,
            _ => false
        };
    }

    private static bool IsSupportedChannel(string channel)
    {
        return channel is
            UpdateChannelNames.Stable or
            UpdateChannelNames.Beta or
            UpdateChannelNames.Internal;
    }

    private static bool IsSupportedStatus(string status)
    {
        return status is
            UpdateStatusNames.NotStarted or
            UpdateStatusNames.Offered or
            UpdateStatusNames.Downloading or
            UpdateStatusNames.Downloaded or
            UpdateStatusNames.Installing or
            UpdateStatusNames.Installed or
            UpdateStatusNames.Superseded or
            UpdateStatusNames.Failed or
            UpdateStatusNames.RollbackStarted or
            UpdateStatusNames.RolledBack;
    }

    private static bool IsTerminalRolloutState(string state)
    {
        return state is
            UpdateRolloutStateNames.Completed or
            UpdateRolloutStateNames.RolledBack or
            UpdateRolloutStateNames.Cancelled;
    }

    private static bool IsInstalledVersionAtLeastPackage(string installedVersion, string packageVersion)
    {
        if (string.Equals(installedVersion, packageVersion, StringComparison.Ordinal))
        {
            return true;
        }

        return TryParseWindowsInstallerVersion(installedVersion, out var installed) &&
            TryParseWindowsInstallerVersion(packageVersion, out var package) &&
            CompareVersionParts(installed, package) >= 0;
    }

    private static string GetWindowsInstallerProductVersion(string version)
    {
        var trimmed = version.Trim();
        var length = 0;
        var dotCount = 0;

        while (length < trimmed.Length)
        {
            var character = trimmed[length];
            if (char.IsDigit(character))
            {
                length++;
                continue;
            }

            if (character == '.' && dotCount < 3)
            {
                dotCount++;
                length++;
                continue;
            }

            break;
        }

        while (length > 0 && trimmed[length - 1] == '.')
        {
            length--;
        }

        return length == 0 ? trimmed : trimmed[..length];
    }

    private static bool TryParseWindowsInstallerVersion(string version, out int[] parts)
    {
        var normalized = GetWindowsInstallerProductVersion(version);
        var split = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 0 || split.Length > 4)
        {
            parts = [];
            return false;
        }

        parts = new int[4];
        for (var index = 0; index < split.Length; index++)
        {
            if (!int.TryParse(split[index], out var part) || part < 0)
            {
                parts = [];
                return false;
            }

            parts[index] = part;
        }

        return true;
    }

    private static int CompareVersionParts(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        for (var index = 0; index < 4; index++)
        {
            var comparison = left[index].CompareTo(right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }
}
