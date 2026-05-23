using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Invites;
using AFK4.Shared.Contracts.Platform.Operator;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Tenancy;

public sealed class EfOperatorConnectionResolver(PlatformDbContext dbContext) : IOperatorConnectionResolver
{
    public async Task<PlatformTenantOperationResult<ResolveOperatorConnectionResponse>> ResolveAsync(
        ResolveOperatorConnectionRequest request,
        CancellationToken cancellationToken)
    {
        var hasSlugPair = !string.IsNullOrWhiteSpace(request.OrganizationSlug)
            || !string.IsNullOrWhiteSpace(request.BranchSlug);
        var hasSetupCode = !string.IsNullOrWhiteSpace(request.SetupCode);

        if (!hasSlugPair && !hasSetupCode)
        {
            return PlatformTenantOperationResult<ResolveOperatorConnectionResponse>.BadRequest(
                "Provide either an organization slug + branch slug or a setup code.");
        }

        if (hasSlugPair && hasSetupCode)
        {
            return PlatformTenantOperationResult<ResolveOperatorConnectionResponse>.BadRequest(
                "Provide either an organization slug + branch slug or a setup code, not both.");
        }

        return hasSetupCode
            ? await ResolveBySetupCodeAsync(request.SetupCode!, cancellationToken)
            : await ResolveBySlugPairAsync(request.OrganizationSlug, request.BranchSlug, cancellationToken);
    }

    private async Task<PlatformTenantOperationResult<ResolveOperatorConnectionResponse>> ResolveBySlugPairAsync(
        string? organizationSlugRaw,
        string? branchSlugRaw,
        CancellationToken cancellationToken)
    {
        var organizationSlug = SlugValidator.Normalize(organizationSlugRaw);
        var organizationSlugError = SlugValidator.Validate(organizationSlug, "OrganizationSlug");
        if (organizationSlugError is not null)
        {
            return PlatformTenantOperationResult<ResolveOperatorConnectionResponse>.BadRequest(organizationSlugError);
        }

        var branchSlug = SlugValidator.Normalize(branchSlugRaw);
        var branchSlugError = SlugValidator.Validate(branchSlug, "BranchSlug");
        if (branchSlugError is not null)
        {
            return PlatformTenantOperationResult<ResolveOperatorConnectionResponse>.BadRequest(branchSlugError);
        }

        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(org => org.Slug == organizationSlug, cancellationToken);
        if (organization is null)
        {
            return PlatformTenantOperationResult<ResolveOperatorConnectionResponse>.NotFound(
                "No tenant matched the supplied organization slug.");
        }

        var branch = await dbContext.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == organization.OrganizationId && candidate.Slug == branchSlug,
                cancellationToken);
        if (branch is null)
        {
            return PlatformTenantOperationResult<ResolveOperatorConnectionResponse>.NotFound(
                "No branch matched the supplied branch slug in this tenant.");
        }

        return PlatformTenantOperationResult<ResolveOperatorConnectionResponse>.Success(
            BuildResponse(organization, branch, OperatorConnectionResolutionSources.Slug));
    }

    private async Task<PlatformTenantOperationResult<ResolveOperatorConnectionResponse>> ResolveBySetupCodeAsync(
        string setupCode,
        CancellationToken cancellationToken)
    {
        var normalized = setupCode.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return PlatformTenantOperationResult<ResolveOperatorConnectionResponse>.BadRequest(
                "SetupCode is required.");
        }

        var invite = await dbContext.OwnerInvites
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.NormalizedCode == normalized, cancellationToken);
        if (invite is null)
        {
            return PlatformTenantOperationResult<ResolveOperatorConnectionResponse>.NotFound(
                "Setup code did not match any owner invite.");
        }

        if (invite.Status != OwnerInviteStatusNames.Pending)
        {
            return PlatformTenantOperationResult<ResolveOperatorConnectionResponse>.BadRequest(
                $"Setup code is no longer usable (status = {invite.Status}).");
        }

        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(org => org.OrganizationId == invite.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return PlatformTenantOperationResult<ResolveOperatorConnectionResponse>.NotFound(
                "Tenant no longer exists.");
        }

        var branch = await dbContext.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == invite.OrganizationId && candidate.BranchId == invite.BranchId,
                cancellationToken);
        if (branch is null)
        {
            return PlatformTenantOperationResult<ResolveOperatorConnectionResponse>.NotFound(
                "Branch no longer exists.");
        }

        return PlatformTenantOperationResult<ResolveOperatorConnectionResponse>.Success(
            BuildResponse(organization, branch, OperatorConnectionResolutionSources.SetupCode));
    }

    private static ResolveOperatorConnectionResponse BuildResponse(
        OrganizationEntity organization,
        BranchEntity branch,
        string source)
    {
        return new ResolveOperatorConnectionResponse(
            OrganizationId: organization.OrganizationId,
            OrganizationSlug: organization.Slug,
            OrganizationName: organization.Name,
            OrganizationStatus: organization.Status,
            OrganizationStatusReason: organization.StatusReason,
            BranchId: branch.BranchId,
            BranchSlug: branch.Slug,
            BranchName: branch.Name,
            BranchCity: branch.City,
            Source: source);
    }
}
