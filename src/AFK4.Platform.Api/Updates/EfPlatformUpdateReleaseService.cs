using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Updates;

public sealed class EfPlatformUpdateReleaseService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IPlatformUpdateReleaseService
{
    public async Task<UpdateServiceResult<PlatformUpdatePackageDto>> RegisterPackageAsync(
        Guid actorPlatformAdminUserId,
        CreatePlatformUpdatePackageRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidatePackage(request);
        if (error is not null) return UpdateServiceResult<PlatformUpdatePackageDto>.Invalid(error);

        var duplicate = await dbContext.UpdatePackages.AsNoTracking().AnyAsync(package =>
            package.Component == request.Component &&
            package.Version == request.Version &&
            package.Channel == request.Channel, cancellationToken);
        if (duplicate)
            return UpdateServiceResult<PlatformUpdatePackageDto>.RequestConflict(
                "An update package already exists for this component, version, and channel.");

        var entity = new UpdatePackageEntity
        {
            UpdatePackageId = Guid.NewGuid(),
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
            CreatedByPlatformAdminUserId = actorPlatformAdminUserId,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        dbContext.UpdatePackages.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return UpdateServiceResult<PlatformUpdatePackageDto>.Ok(ToDto(entity));
    }

    public async Task<IReadOnlyList<PlatformUpdatePackageDto>> ListPackagesAsync(CancellationToken cancellationToken)
    {
        var packages = await dbContext.UpdatePackages.AsNoTracking()
            .OrderByDescending(package => package.CreatedAtUtc)
            .ThenBy(package => package.Component)
            .ToListAsync(cancellationToken);
        return packages.Select(ToDto).ToList();
    }

    public async Task<UpdateServiceResult<PlatformUpdatePackageDto>> ChangePackageStateAsync(
        Guid actorPlatformAdminUserId,
        Guid packageId,
        ChangePlatformUpdatePackageStateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.State is not UpdatePackageStateNames.Validated and not UpdatePackageStateNames.Rejected and not UpdatePackageStateNames.Retired)
            return UpdateServiceResult<PlatformUpdatePackageDto>.Invalid("Unsupported update package state.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return UpdateServiceResult<PlatformUpdatePackageDto>.Invalid("State change reason is required.");

        var package = await dbContext.UpdatePackages.SingleOrDefaultAsync(
            candidate => candidate.UpdatePackageId == packageId,
            cancellationToken);
        if (package is null) return UpdateServiceResult<PlatformUpdatePackageDto>.Missing("Update package was not found.");
        if (package.State is UpdatePackageStateNames.Rejected or UpdatePackageStateNames.Retired)
            return UpdateServiceResult<PlatformUpdatePackageDto>.Invalid("Rejected and retired packages are terminal.");

        var now = timeProvider.GetUtcNow();
        package.State = request.State;
        if (request.State == UpdatePackageStateNames.Validated)
        {
            package.ValidatedByPlatformAdminUserId = actorPlatformAdminUserId;
            package.ValidatedAtUtc = now;
        }
        if (request.State == UpdatePackageStateNames.Retired) package.RetiredAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return UpdateServiceResult<PlatformUpdatePackageDto>.Ok(ToDto(package));
    }

    public async Task<UpdateServiceResult<PlatformUpdateRolloutDto>> CreateRolloutAsync(
        Guid actorPlatformAdminUserId,
        CreatePlatformUpdateRolloutRequest request,
        CancellationToken cancellationToken)
    {
        var error = await ValidateRolloutAsync(request, cancellationToken);
        if (error is not null) return UpdateServiceResult<PlatformUpdateRolloutDto>.Invalid(error);
        var package = await dbContext.UpdatePackages.SingleOrDefaultAsync(
            candidate => candidate.UpdatePackageId == request.UpdatePackageId,
            cancellationToken);
        if (package is null) return UpdateServiceResult<PlatformUpdateRolloutDto>.Missing("Update package was not found.");
        if (package.State != UpdatePackageStateNames.Validated)
            return UpdateServiceResult<PlatformUpdateRolloutDto>.Invalid("Only validated packages can start a rollout.");
        if (package.Channel != request.Channel)
            return UpdateServiceResult<PlatformUpdateRolloutDto>.Invalid("Rollout channel must match the package channel.");

        var now = timeProvider.GetUtcNow();
        var rollout = new UpdateRolloutEntity
        {
            UpdateRolloutId = Guid.NewGuid(),
            UpdatePackageId = package.UpdatePackageId,
            Component = package.Component,
            Version = package.Version,
            Channel = package.Channel,
            State = UpdateRolloutStateNames.Active,
            TargetKind = request.TargetKind,
            BatchPercent = request.BatchPercent,
            Reason = request.Reason.Trim(),
            CreatedByPlatformAdminUserId = actorPlatformAdminUserId,
            CreatedAtUtc = now,
            StartsAtUtc = request.StartsAtUtc
        };
        dbContext.UpdateRollouts.Add(rollout);
        dbContext.UpdateRolloutTargets.AddRange(CreateTargets(rollout.UpdateRolloutId, request, now));
        await dbContext.SaveChangesAsync(cancellationToken);
        return UpdateServiceResult<PlatformUpdateRolloutDto>.Ok(await ToDtoAsync(rollout, cancellationToken));
    }

    public async Task<IReadOnlyList<PlatformUpdateRolloutDto>> ListRolloutsAsync(CancellationToken cancellationToken)
    {
        var rollouts = await dbContext.UpdateRollouts.AsNoTracking()
            .OrderByDescending(rollout => rollout.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var result = new List<PlatformUpdateRolloutDto>();
        foreach (var rollout in rollouts) result.Add(await ToDtoAsync(rollout, cancellationToken));
        return result;
    }

    public async Task<UpdateServiceResult<PlatformUpdateRolloutDto>> ChangeRolloutStateAsync(
        Guid rolloutId,
        ChangePlatformUpdateRolloutStateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.State is not UpdateRolloutStateNames.Active and not UpdateRolloutStateNames.Paused and not
            UpdateRolloutStateNames.Completed and not UpdateRolloutStateNames.RollbackRequested and not
            UpdateRolloutStateNames.RolledBack and not UpdateRolloutStateNames.Cancelled)
            return UpdateServiceResult<PlatformUpdateRolloutDto>.Invalid("Unsupported update rollout state.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return UpdateServiceResult<PlatformUpdateRolloutDto>.Invalid("State change reason is required.");

        var rollout = await dbContext.UpdateRollouts.SingleOrDefaultAsync(
            candidate => candidate.UpdateRolloutId == rolloutId,
            cancellationToken);
        if (rollout is null) return UpdateServiceResult<PlatformUpdateRolloutDto>.Missing("Update rollout was not found.");
        if (rollout.State is UpdateRolloutStateNames.Completed or UpdateRolloutStateNames.RolledBack or UpdateRolloutStateNames.Cancelled)
            return UpdateServiceResult<PlatformUpdateRolloutDto>.Invalid("Completed, rolled-back, and cancelled rollouts are terminal.");

        rollout.State = request.State;
        rollout.Reason = request.Reason.Trim();
        if (request.State is UpdateRolloutStateNames.Completed or UpdateRolloutStateNames.RolledBack or UpdateRolloutStateNames.Cancelled)
            rollout.CompletedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return UpdateServiceResult<PlatformUpdateRolloutDto>.Ok(await ToDtoAsync(rollout, cancellationToken));
    }

    private async Task<string?> ValidateRolloutAsync(CreatePlatformUpdateRolloutRequest request, CancellationToken cancellationToken)
    {
        if (!EfUpdateService.IsSupportedChannel(request.Channel)) return "Unsupported update channel.";
        if (request.TargetKind is not PlatformUpdateTargetKindNames.Organization and not PlatformUpdateTargetKindNames.Branch and not PlatformUpdateTargetKindNames.Device)
            return "Unsupported rollout target kind.";
        if (request.BatchPercent is < 1 or > 100) return "Batch percent must be between 1 and 100.";
        if (string.IsNullOrWhiteSpace(request.Reason)) return "Rollout reason is required.";
        if (request.TargetKind == PlatformUpdateTargetKindNames.Organization && (request.OrganizationIds.Count == 0 || request.BranchIds.Count != 0 || request.DeviceIds.Count != 0))
            return "Organization rollouts require only organization targets.";
        if (request.TargetKind == PlatformUpdateTargetKindNames.Branch && (request.BranchIds.Count == 0 || request.DeviceIds.Count != 0))
            return "Branch rollouts require branch targets and no device targets.";
        if (request.TargetKind == PlatformUpdateTargetKindNames.Device && request.DeviceIds.Count == 0)
            return "Device rollouts require device targets.";
        if (request.OrganizationIds.Count > 0 && await dbContext.Organizations.CountAsync(entity => request.OrganizationIds.Contains(entity.OrganizationId), cancellationToken) != request.OrganizationIds.Distinct().Count())
            return "One or more organization targets were not found.";
        if (request.BranchIds.Count > 0 && await dbContext.Branches.CountAsync(entity => request.BranchIds.Contains(entity.BranchId), cancellationToken) != request.BranchIds.Distinct().Count())
            return "One or more branch targets were not found.";
        if (request.DeviceIds.Count > 0 && await dbContext.Devices.CountAsync(entity => request.DeviceIds.Contains(entity.DeviceId), cancellationToken) != request.DeviceIds.Distinct().Count())
            return "One or more device targets were not found.";
        return null;
    }

    private static IEnumerable<UpdateRolloutTargetEntity> CreateTargets(
        Guid rolloutId,
        CreatePlatformUpdateRolloutRequest request,
        DateTimeOffset now)
    {
        if (request.TargetKind == PlatformUpdateTargetKindNames.Organization)
            return request.OrganizationIds.Distinct().Select(id => Target(rolloutId, request.TargetKind, id, null, null, now));
        if (request.TargetKind == PlatformUpdateTargetKindNames.Branch)
            return request.BranchIds.Distinct().Select(id => Target(rolloutId, request.TargetKind, null, id, null, now));
        return request.DeviceIds.Distinct().Select(id => Target(rolloutId, request.TargetKind, null, null, id, now));
    }

    private static UpdateRolloutTargetEntity Target(Guid rolloutId, string kind, Guid? organizationId, Guid? branchId, Guid? deviceId, DateTimeOffset now) => new()
    {
        UpdateRolloutTargetId = Guid.NewGuid(),
        UpdateRolloutId = rolloutId,
        TargetKind = kind,
        OrganizationId = organizationId,
        BranchId = branchId,
        DeviceId = deviceId,
        CreatedAtUtc = now
    };

    private async Task<PlatformUpdateRolloutDto> ToDtoAsync(UpdateRolloutEntity rollout, CancellationToken cancellationToken)
    {
        var targets = await dbContext.UpdateRolloutTargets.AsNoTracking()
            .Where(target => target.UpdateRolloutId == rollout.UpdateRolloutId)
            .ToListAsync(cancellationToken);
        return new PlatformUpdateRolloutDto(
            rollout.UpdateRolloutId, rollout.UpdatePackageId, rollout.Component, rollout.Version, rollout.Channel,
            rollout.State, rollout.TargetKind,
            targets.Where(target => target.OrganizationId.HasValue).Select(target => target.OrganizationId!.Value).Order().ToList(),
            targets.Where(target => target.BranchId.HasValue).Select(target => target.BranchId!.Value).Order().ToList(),
            targets.Where(target => target.DeviceId.HasValue).Select(target => target.DeviceId!.Value).Order().ToList(),
            rollout.BatchPercent, rollout.Reason, rollout.CreatedByPlatformAdminUserId, rollout.CreatedAtUtc,
            rollout.StartsAtUtc, rollout.CompletedAtUtc);
    }

    private static PlatformUpdatePackageDto ToDto(UpdatePackageEntity package) => new(
        package.UpdatePackageId, package.Component, package.Version, package.Channel, package.ArtifactUri,
        package.Sha256, package.Signature, package.SignatureAlgorithm, package.SizeBytes, package.State,
        package.ReleaseNotes, package.CreatedByPlatformAdminUserId, package.CreatedAtUtc,
        package.ValidatedByPlatformAdminUserId, package.ValidatedAtUtc, package.RetiredAtUtc);

    private static string? ValidatePackage(CreatePlatformUpdatePackageRequest request)
    {
        if (!EfUpdateService.IsSupportedComponent(request.Component)) return "Unsupported update component.";
        if (!EfUpdateService.IsSupportedChannel(request.Channel)) return "Unsupported update channel.";
        if (string.IsNullOrWhiteSpace(request.Version)) return "Package version is required.";
        if (!Uri.TryCreate(request.ArtifactUri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return "Package artifact URI must be absolute HTTPS.";
        if (request.Sha256.Length != 64 || !request.Sha256.All(Uri.IsHexDigit)) return "Package SHA-256 hash is invalid.";
        if (string.IsNullOrWhiteSpace(request.Signature)) return "Package signature is required.";
        if (request.SignatureAlgorithm != UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363)
            return "Unsupported package signature algorithm.";
        if (request.SizeBytes <= 0) return "Package size must be positive.";
        return null;
    }
}
