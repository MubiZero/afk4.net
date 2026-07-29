using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Updates;

public sealed class EfUpdateService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IUpdateService
{
    public async Task<UpdateServiceResult<UpdateRolloutDto>> GetRolloutAsync(
        Guid organizationId,
        Guid branchId,
        Guid rolloutId,
        CancellationToken cancellationToken)
    {
        var rollout = await dbContext.UpdateRollouts
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UpdateRolloutId == rolloutId, cancellationToken);
        if (rollout is null)
        {
            return UpdateServiceResult<UpdateRolloutDto>.Missing("Update rollout was not found.");
        }

        var targets = await LoadTargetsAsync(new HashSet<Guid> { rolloutId }, cancellationToken);
        if (!TargetsBranch(targets, rolloutId, organizationId, branchId))
        {
            return UpdateServiceResult<UpdateRolloutDto>.Missing("Update rollout was not found.");
        }

        return UpdateServiceResult<UpdateRolloutDto>.Ok(ToDto(rollout, organizationId, branchId, targets));
    }

    public async Task<UpdateServiceResult<IReadOnlyList<UpdateRolloutStatusDto>>> ListRolloutStatusesAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var rollouts = await dbContext.UpdateRollouts
            .AsNoTracking()
            .OrderByDescending(rollout => rollout.CreatedAtUtc)
            .ThenBy(rollout => rollout.Component)
            .ToListAsync(cancellationToken);
        if (rollouts.Count == 0)
        {
            return UpdateServiceResult<IReadOnlyList<UpdateRolloutStatusDto>>.Ok([]);
        }

        var rolloutIds = rollouts.Select(rollout => rollout.UpdateRolloutId).ToHashSet();
        var targets = await LoadTargetsAsync(rolloutIds, cancellationToken);
        var matchingRollouts = rollouts
            .Where(rollout => TargetsBranch(targets, rollout.UpdateRolloutId, organizationId, branchId))
            .ToList();
        var matchingIds = matchingRollouts.Select(rollout => rollout.UpdateRolloutId).ToHashSet();
        var statuses = await dbContext.DeviceUpdateStatuses
            .AsNoTracking()
            .Where(status =>
                status.OrganizationId == organizationId &&
                status.BranchId == branchId &&
                matchingIds.Contains(status.UpdateRolloutId))
            .ToListAsync(cancellationToken);

        var response = matchingRollouts
            .Select(rollout => new UpdateRolloutStatusDto(
                rollout.UpdateRolloutId,
                organizationId,
                branchId,
                rollout.UpdatePackageId,
                rollout.Component,
                rollout.Version,
                rollout.Channel,
                rollout.State,
                rollout.TargetKind,
                DeviceTargets(targets, rollout.UpdateRolloutId),
                rollout.BatchPercent,
                rollout.CreatedAtUtc,
                rollout.StartsAtUtc,
                rollout.CompletedAtUtc,
                statuses
                    .Where(status => status.UpdateRolloutId == rollout.UpdateRolloutId)
                    .OrderBy(status => status.DeviceId)
                    .ThenBy(status => status.Component)
                    .Select(ToStatusSnapshot)
                    .ToList()))
            .ToList();

        return UpdateServiceResult<IReadOnlyList<UpdateRolloutStatusDto>>.Ok(response);
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
            .Where(device =>
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
                rollout.Channel == request.Channel &&
                rollout.State == UpdateRolloutStateNames.Active &&
                rollout.StartsAtUtc <= request.CheckedAtUtc)
            .ToListAsync(cancellationToken);
        var rolloutIds = activeRollouts.Select(rollout => rollout.UpdateRolloutId).ToHashSet();
        var targets = await LoadTargetsAsync(rolloutIds, cancellationToken);
        var eligibleRollouts = activeRollouts
            .Where(rollout =>
                TargetsDevice(targets, rollout.UpdateRolloutId, request.OrganizationId, request.BranchId, request.DeviceId) &&
                UpdateRolloutBucket.IsEligible(rollout.UpdateRolloutId, request.DeviceId, rollout.BatchPercent))
            .ToList();
        var packageIds = eligibleRollouts.Select(rollout => rollout.UpdatePackageId).ToHashSet();
        var packages = await dbContext.UpdatePackages
            .AsNoTracking()
            .Where(package =>
                packageIds.Contains(package.UpdatePackageId) &&
                package.State == UpdatePackageStateNames.Validated)
            .ToDictionaryAsync(package => package.UpdatePackageId, cancellationToken);
        var installed = request.InstalledComponents
            .GroupBy(component => component.Component, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Version, StringComparer.Ordinal);

        var candidates = eligibleRollouts
            .Where(rollout => packages.ContainsKey(rollout.UpdatePackageId))
            .Select(rollout => (Rollout: rollout, Package: packages[rollout.UpdatePackageId]))
            .Where(candidate => IsComponentEligibleForDeviceRole(candidate.Package.Component, deviceRole))
            .Where(candidate =>
                !installed.TryGetValue(candidate.Package.Component, out var version) ||
                !IsInstalledVersionAtLeastPackage(version, candidate.Package.Version))
            .GroupBy(candidate => candidate.Package.Component, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(candidate => candidate.Package.Version, UpdateVersionComparer.Instance)
                .ThenByDescending(candidate => candidate.Rollout.CreatedAtUtc)
                .First())
            .OrderBy(candidate => candidate.Package.Component, StringComparer.Ordinal)
            .Select(candidate => new ComponentUpdateInstructionDto(
                candidate.Rollout.UpdateRolloutId,
                candidate.Package.UpdatePackageId,
                candidate.Package.Component,
                candidate.Package.Version,
                candidate.Package.Channel,
                candidate.Package.ArtifactUri,
                candidate.Package.Sha256,
                candidate.Package.Signature,
                candidate.Package.SignatureAlgorithm,
                candidate.Package.SizeBytes,
                candidate.Package.ReleaseNotes))
            .ToList();

        var preference = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.OrganizationId == request.OrganizationId && branch.BranchId == request.BranchId)
            .Select(branch => new OrganizationAdminUpdatePreferenceDto(
                branch.OrganizationId,
                branch.BranchId,
                branch.OrganizationAdminMaintenanceWindowStart,
                branch.OrganizationAdminMaintenanceWindowEnd,
                branch.PreferredTimeZone))
            .SingleAsync(cancellationToken);

        return UpdateServiceResult<DeviceUpdateCheckResponse>.Ok(
            new DeviceUpdateCheckResponse(timeProvider.GetUtcNow(), candidates, preference));
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

        var deviceExists = await dbContext.Devices.AsNoTracking().AnyAsync(device =>
            device.OrganizationId == request.OrganizationId &&
            device.BranchId == request.BranchId &&
            device.DeviceId == request.DeviceId, cancellationToken);
        if (!deviceExists)
        {
            return UpdateServiceResult<DeviceUpdateStatusResultDto>.Missing("Device was not found.");
        }

        var rolloutExists = await dbContext.UpdateRollouts.AsNoTracking().AnyAsync(rollout =>
            rollout.UpdateRolloutId == request.UpdateRolloutId &&
            rollout.UpdatePackageId == request.UpdatePackageId, cancellationToken);
        if (!rolloutExists)
        {
            return UpdateServiceResult<DeviceUpdateStatusResultDto>.Missing("Update rollout was not found.");
        }

        var status = await dbContext.DeviceUpdateStatuses.SingleOrDefaultAsync(candidate =>
            candidate.OrganizationId == request.OrganizationId &&
            candidate.BranchId == request.BranchId &&
            candidate.DeviceId == request.DeviceId &&
            candidate.UpdateRolloutId == request.UpdateRolloutId &&
            candidate.UpdatePackageId == request.UpdatePackageId &&
            candidate.Component == request.Component, cancellationToken);
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

        return UpdateServiceResult<DeviceUpdateStatusResultDto>.Ok(new DeviceUpdateStatusResultDto(
            status.DeviceId,
            status.UpdateRolloutId,
            status.UpdatePackageId,
            status.Component,
            status.Status,
            status.Message,
            status.UpdatedAtUtc));
    }

    private async Task<List<UpdateRolloutTargetEntity>> LoadTargetsAsync(
        IReadOnlySet<Guid> rolloutIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.UpdateRolloutTargets
            .AsNoTracking()
            .Where(target => rolloutIds.Contains(target.UpdateRolloutId))
            .ToListAsync(cancellationToken);
    }

    private static bool TargetsBranch(
        IReadOnlyList<UpdateRolloutTargetEntity> targets,
        Guid rolloutId,
        Guid organizationId,
        Guid branchId) => targets.Any(target =>
            target.UpdateRolloutId == rolloutId &&
            (target.TargetKind == PlatformUpdateTargetKindNames.Organization && target.OrganizationId == organizationId ||
             target.TargetKind is PlatformUpdateTargetKindNames.Branch or PlatformUpdateTargetKindNames.Device && target.BranchId == branchId));

    private static bool TargetsDevice(
        IReadOnlyList<UpdateRolloutTargetEntity> targets,
        Guid rolloutId,
        Guid organizationId,
        Guid branchId,
        Guid deviceId) => targets.Any(target =>
            target.UpdateRolloutId == rolloutId &&
            (target.TargetKind == PlatformUpdateTargetKindNames.Organization && target.OrganizationId == organizationId ||
             target.TargetKind == PlatformUpdateTargetKindNames.Branch && target.BranchId == branchId ||
             target.TargetKind == PlatformUpdateTargetKindNames.Device && target.DeviceId == deviceId));

    private static IReadOnlyList<Guid> DeviceTargets(
        IReadOnlyList<UpdateRolloutTargetEntity> targets,
        Guid rolloutId) => targets
            .Where(target => target.UpdateRolloutId == rolloutId && target.DeviceId.HasValue)
            .Select(target => target.DeviceId!.Value)
            .Distinct()
            .OrderBy(deviceId => deviceId)
            .ToList();

    private static UpdateRolloutDto ToDto(
        UpdateRolloutEntity rollout,
        Guid organizationId,
        Guid branchId,
        IReadOnlyList<UpdateRolloutTargetEntity> targets) => new(
            rollout.UpdateRolloutId,
            organizationId,
            branchId,
            rollout.UpdatePackageId,
            rollout.Component,
            rollout.Version,
            rollout.Channel,
            rollout.State,
            rollout.TargetKind,
            DeviceTargets(targets, rollout.UpdateRolloutId),
            rollout.BatchPercent,
            rollout.CreatedAtUtc,
            rollout.StartsAtUtc,
            rollout.CompletedAtUtc);

    private static DeviceUpdateStatusSnapshotDto ToStatusSnapshot(DeviceUpdateStatusEntity status) => new(
        status.DeviceId,
        status.UpdateRolloutId,
        status.UpdatePackageId,
        status.Component,
        status.InstalledVersion,
        status.TargetVersion,
        status.Status,
        status.Message,
        status.UpdatedAtUtc);

    private static string? ValidateStatusReport(DeviceUpdateStatusReportRequest request)
    {
        if (request.OrganizationId == Guid.Empty || request.BranchId == Guid.Empty || request.DeviceId == Guid.Empty)
            return "Organization, branch, and device ids are required.";
        if (!IsSupportedComponent(request.Component)) return "Unsupported update component.";
        if (request.UpdateRolloutId == Guid.Empty || request.UpdatePackageId == Guid.Empty)
            return "Update rollout and package ids are required.";
        if (string.IsNullOrWhiteSpace(request.InstalledVersion)) return "Installed version is required.";
        if (string.IsNullOrWhiteSpace(request.TargetVersion)) return "Target version is required.";
        if (!IsSupportedStatus(request.Status)) return "Unsupported update status.";
        return null;
    }

    internal static bool IsSupportedComponent(string component) => component is
        UpdateComponentNames.OrganizationAdmin or UpdateComponentNames.AgentService or UpdateComponentNames.PlayerShell;

    internal static bool IsSupportedChannel(string channel) => channel is
        UpdateChannelNames.Stable or UpdateChannelNames.Beta or UpdateChannelNames.Internal;

    private static bool IsComponentEligibleForDeviceRole(string component, string role) => component switch
    {
        UpdateComponentNames.AgentService => true,
        UpdateComponentNames.PlayerShell => role == DeviceRoleNames.GamingPc,
        UpdateComponentNames.OrganizationAdmin => role == DeviceRoleNames.ManagerWorkstation,
        _ => false
    };

    private static bool IsSupportedStatus(string status) => status is
        UpdateStatusNames.NotStarted or UpdateStatusNames.Offered or UpdateStatusNames.Downloading or
        UpdateStatusNames.Downloaded or UpdateStatusNames.Installing or UpdateStatusNames.Installed or
        UpdateStatusNames.Superseded or UpdateStatusNames.Failed or UpdateStatusNames.RollbackStarted or
        UpdateStatusNames.RolledBack;

    private static bool IsInstalledVersionAtLeastPackage(string installedVersion, string packageVersion) =>
        UpdateVersionComparer.Instance.Compare(installedVersion, packageVersion) >= 0;
}

internal sealed class UpdateVersionComparer : IComparer<string>
{
    public static UpdateVersionComparer Instance { get; } = new();

    public int Compare(string? left, string? right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal)) return 0;
        if (!TryParse(left, out var leftParts)) return string.Compare(left, right, StringComparison.Ordinal);
        if (!TryParse(right, out var rightParts)) return string.Compare(left, right, StringComparison.Ordinal);
        for (var index = 0; index < leftParts.Length; index++)
        {
            var comparison = leftParts[index].CompareTo(rightParts[index]);
            if (comparison != 0) return comparison;
        }
        return 0;
    }

    private static bool TryParse(string? version, out int[] parts)
    {
        var numeric = new string((version ?? string.Empty)
            .TakeWhile(character => char.IsDigit(character) || character == '.')
            .ToArray()).TrimEnd('.');
        var split = numeric.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 0 || split.Length > 4)
        {
            parts = [];
            return false;
        }
        parts = new int[4];
        for (var index = 0; index < split.Length; index++)
        {
            if (!int.TryParse(split[index], out parts[index])) return false;
        }
        return true;
    }
}
