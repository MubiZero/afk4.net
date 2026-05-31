using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Invites;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Tenancy;

public sealed class EfPlatformTenantService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    IOwnerInviteCodeGenerator inviteCodeGenerator,
    IStaffTokenService staffTokenService,
    IOptions<PlatformTenantOptions> tenantOptions) : IPlatformTenantService
{
    private const int MinPasswordLength = 8;
    private const int MaxUserNameLength = 256;
    private const int MaxDisplayNameLength = 160;
    private const int MaxBranchCityLength = 120;
    private const int MaxOrganizationNameLength = 160;
    private const int MaxBranchNameLength = 160;
    private const int MaxPlanCodeLength = 64;

    private const int MaxStatusReasonLength = 500;

    private static readonly HashSet<string> AllowedSubscriptionStatuses = new(StringComparer.Ordinal)
    {
        SubscriptionStatusNames.Trial,
        SubscriptionStatusNames.Active,
        SubscriptionStatusNames.PastDue,
        SubscriptionStatusNames.Cancelled
    };

    private static readonly HashSet<string> AllowedTenantStatuses = new(StringComparer.Ordinal)
    {
        TenantStatusNames.Active,
        TenantStatusNames.Suspended,
        TenantStatusNames.DeletionPending
    };

    private readonly PasswordHasher<StaffUserEntity> staffPasswordHasher = new();
    private readonly PlatformTenantOptions options = tenantOptions.Value;

    public async Task<PlatformTenantOperationResult<CreateTenantResponse>> CreateAsync(
        CreateTenantRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        var orgSlug = SlugValidator.Normalize(request.OrganizationSlug);
        var orgSlugError = SlugValidator.Validate(orgSlug, "OrganizationSlug");
        if (orgSlugError is not null)
        {
            return PlatformTenantOperationResult<CreateTenantResponse>.BadRequest(orgSlugError);
        }

        var branchSlug = SlugValidator.Normalize(request.BranchSlug);
        var branchSlugError = SlugValidator.Validate(branchSlug, "BranchSlug");
        if (branchSlugError is not null)
        {
            return PlatformTenantOperationResult<CreateTenantResponse>.BadRequest(branchSlugError);
        }

        var nameError = ValidateRequiredText(request.OrganizationName, "OrganizationName", MaxOrganizationNameLength);
        if (nameError is not null)
        {
            return PlatformTenantOperationResult<CreateTenantResponse>.BadRequest(nameError);
        }

        var branchNameError = ValidateRequiredText(request.BranchName, "BranchName", MaxBranchNameLength);
        if (branchNameError is not null)
        {
            return PlatformTenantOperationResult<CreateTenantResponse>.BadRequest(branchNameError);
        }

        var branchCityError = ValidateRequiredText(request.BranchCity, "BranchCity", MaxBranchCityLength);
        if (branchCityError is not null)
        {
            return PlatformTenantOperationResult<CreateTenantResponse>.BadRequest(branchCityError);
        }

        var planError = ValidatePlanCode(request.PlanCode);
        if (planError is not null)
        {
            return PlatformTenantOperationResult<CreateTenantResponse>.BadRequest(planError);
        }

        var subscriptionError = ValidateSubscriptionStatus(request.SubscriptionStatus);
        if (subscriptionError is not null)
        {
            return PlatformTenantOperationResult<CreateTenantResponse>.BadRequest(subscriptionError);
        }

        var limitsError = ValidateLimits(request.Limits);
        if (limitsError is not null)
        {
            return PlatformTenantOperationResult<CreateTenantResponse>.BadRequest(limitsError);
        }

        var ownerUserNameError = ValidateOptionalUserName(request.OwnerUserName);
        if (ownerUserNameError is not null)
        {
            return PlatformTenantOperationResult<CreateTenantResponse>.BadRequest(ownerUserNameError);
        }

        var ownerDisplayNameError = ValidateOptionalDisplayName(request.OwnerDisplayName);
        if (ownerDisplayNameError is not null)
        {
            return PlatformTenantOperationResult<CreateTenantResponse>.BadRequest(ownerDisplayNameError);
        }

        var lifetime = request.OwnerInviteLifetime ?? options.DefaultOwnerInviteLifetime;
        var lifetimeError = ValidateLifetime(lifetime);
        if (lifetimeError is not null)
        {
            return PlatformTenantOperationResult<CreateTenantResponse>.BadRequest(lifetimeError);
        }

        var orgSlugTaken = await dbContext.Organizations
            .AnyAsync(candidate => candidate.Slug == orgSlug, cancellationToken);
        if (orgSlugTaken)
        {
            return PlatformTenantOperationResult<CreateTenantResponse>.Conflict(
                $"Organization slug '{orgSlug}' is already in use.");
        }

        var now = timeProvider.GetUtcNow();
        var organization = new OrganizationEntity
        {
            OrganizationId = Guid.NewGuid(),
            Slug = orgSlug,
            Name = request.OrganizationName.Trim(),
            Status = TenantStatusNames.Active,
            StatusReason = null,
            StatusChangedAtUtc = now,
            PlanCode = request.PlanCode.Trim(),
            SubscriptionStatus = request.SubscriptionStatus.Trim(),
            LimitsJson = SerializeLimits(request.Limits),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var branch = new BranchEntity
        {
            BranchId = Guid.NewGuid(),
            OrganizationId = organization.OrganizationId,
            Slug = branchSlug,
            Name = request.BranchName.Trim(),
            City = request.BranchCity.Trim(),
            CreatedAtUtc = now
        };

        var invite = BuildOwnerInvite(
            organization.OrganizationId,
            branch.BranchId,
            platformAdminUserId,
            request.OwnerUserName,
            request.OwnerDisplayName,
            lifetime,
            now);

        var defaultZone = new ZoneEntity
        {
            ZoneId = Guid.NewGuid(),
            OrganizationId = organization.OrganizationId,
            BranchId = branch.BranchId,
            Name = "Main Hall",
            SortOrder = 1,
            CreatedAtUtc = now
        };

        dbContext.Organizations.Add(organization);
        dbContext.Branches.Add(branch);
        dbContext.Zones.Add(defaultZone);
        dbContext.OwnerInvites.Add(invite);
        var catalogPlan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(plan => plan.PlanCode == organization.PlanCode, cancellationToken);
        var subscriptionInterval = catalogPlan?.BillingInterval ?? "monthly";
        dbContext.TenantSubscriptions.Add(new TenantSubscriptionEntity
        {
            TenantSubscriptionId = Guid.NewGuid(),
            OrganizationId = organization.OrganizationId,
            PlanCode = organization.PlanCode,
            Status = organization.SubscriptionStatus,
            CurrentPeriodStartUtc = now,
            CurrentPeriodEndUtc = subscriptionInterval == "yearly" ? now.AddYears(1) : now.AddMonths(1),
            NextInvoiceUtc = subscriptionInterval == "yearly" ? now.AddYears(1) : now.AddMonths(1),
            AmountMinorUnits = catalogPlan?.PriceMinorUnits ?? 0,
            CurrencyCode = catalogPlan?.CurrencyCode ?? "RUB",
            BillingInterval = subscriptionInterval,
            CancelAtPeriodEnd = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var detail = await BuildTenantDetailAsync(organization.OrganizationId, cancellationToken);
        var inviteDto = ToInviteDto(invite);
        return PlatformTenantOperationResult<CreateTenantResponse>.Success(
            new CreateTenantResponse(detail!, inviteDto));
    }

    public async Task<IReadOnlyList<TenantSummaryDto>> ListAsync(CancellationToken cancellationToken)
    {
        var orgs = await dbContext.Organizations
            .AsNoTracking()
            .OrderBy(org => org.Name)
            .ToListAsync(cancellationToken);
        if (orgs.Count == 0)
        {
            return [];
        }

        var orgIds = orgs.Select(org => org.OrganizationId).ToHashSet();
        var branchCounts = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => orgIds.Contains(branch.OrganizationId))
            .GroupBy(branch => branch.OrganizationId)
            .Select(group => new { OrganizationId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var branchCountsById = branchCounts.ToDictionary(item => item.OrganizationId, item => item.Count);

        return orgs.Select(org => new TenantSummaryDto(
            OrganizationId: org.OrganizationId,
            Slug: org.Slug,
            Name: org.Name,
            Status: org.Status,
            PlanCode: org.PlanCode,
            SubscriptionStatus: org.SubscriptionStatus,
            BranchCount: branchCountsById.GetValueOrDefault(org.OrganizationId),
            CreatedAtUtc: org.CreatedAtUtc,
            UpdatedAtUtc: org.UpdatedAtUtc)).ToList();
    }

    public Task<TenantDetailDto?> GetAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return BuildTenantDetailAsync(organizationId, cancellationToken);
    }

    public async Task<PlatformTenantOperationResult<OwnerInviteDto>> CreateOrRotateOwnerInviteAsync(
        Guid organizationId,
        CreateOwnerInviteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformTenantOperationResult<OwnerInviteDto>.BadRequest("OrganizationId is required.");
        }

        if (request.BranchId == Guid.Empty)
        {
            return PlatformTenantOperationResult<OwnerInviteDto>.BadRequest("BranchId is required.");
        }

        var ownerUserNameError = ValidateOptionalUserName(request.OwnerUserName);
        if (ownerUserNameError is not null)
        {
            return PlatformTenantOperationResult<OwnerInviteDto>.BadRequest(ownerUserNameError);
        }

        var ownerDisplayNameError = ValidateOptionalDisplayName(request.OwnerDisplayName);
        if (ownerDisplayNameError is not null)
        {
            return PlatformTenantOperationResult<OwnerInviteDto>.BadRequest(ownerDisplayNameError);
        }

        var lifetime = request.Lifetime ?? options.DefaultOwnerInviteLifetime;
        var lifetimeError = ValidateLifetime(lifetime);
        if (lifetimeError is not null)
        {
            return PlatformTenantOperationResult<OwnerInviteDto>.BadRequest(lifetimeError);
        }

        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (organization is null)
        {
            return PlatformTenantOperationResult<OwnerInviteDto>.NotFound("Tenant was not found.");
        }

        var branch = await dbContext.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.BranchId == request.BranchId,
                cancellationToken);
        if (branch is null)
        {
            return PlatformTenantOperationResult<OwnerInviteDto>.NotFound("Branch was not found in this tenant.");
        }

        var now = timeProvider.GetUtcNow();
        var pendingInvites = await dbContext.OwnerInvites
            .Where(invite =>
                invite.OrganizationId == organizationId &&
                invite.BranchId == request.BranchId &&
                invite.Status == OwnerInviteStatusNames.Pending)
            .ToListAsync(cancellationToken);
        foreach (var pending in pendingInvites)
        {
            pending.Status = OwnerInviteStatusNames.Revoked;
            pending.RevokedAtUtc = now;
            pending.RevokedByPlatformAdminUserId = platformAdminUserId;
            pending.RevokedReason = "Rotated by platform admin.";
        }

        var freshInvite = BuildOwnerInvite(
            organizationId,
            request.BranchId,
            platformAdminUserId,
            request.OwnerUserName,
            request.OwnerDisplayName,
            lifetime,
            now);
        dbContext.OwnerInvites.Add(freshInvite);

        await dbContext.SaveChangesAsync(cancellationToken);

        return PlatformTenantOperationResult<OwnerInviteDto>.Success(ToInviteDto(freshInvite));
    }

    public async Task<PlatformTenantOperationResult<StaffSignInResponse>> AcceptOwnerInviteAsync(
        AcceptOwnerInviteRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return PlatformTenantOperationResult<StaffSignInResponse>.BadRequest("Code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.UserName) || request.UserName.Trim().Length > MaxUserNameLength)
        {
            return PlatformTenantOperationResult<StaffSignInResponse>.BadRequest(
                $"UserName is required and must contain {MaxUserNameLength} characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > MaxDisplayNameLength)
        {
            return PlatformTenantOperationResult<StaffSignInResponse>.BadRequest(
                $"DisplayName is required and must contain {MaxDisplayNameLength} characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < MinPasswordLength)
        {
            return PlatformTenantOperationResult<StaffSignInResponse>.BadRequest(
                $"Password must contain at least {MinPasswordLength} characters.");
        }

        var normalizedCode = NormalizeInviteCode(request.Code);
        var invite = await dbContext.OwnerInvites
            .SingleOrDefaultAsync(candidate => candidate.NormalizedCode == normalizedCode, cancellationToken);
        if (invite is null)
        {
            return PlatformTenantOperationResult<StaffSignInResponse>.NotFound("Invite was not found.");
        }

        var now = timeProvider.GetUtcNow();
        if (invite.Status != OwnerInviteStatusNames.Pending)
        {
            return PlatformTenantOperationResult<StaffSignInResponse>.BadRequest(
                $"Invite is no longer pending (status = {invite.Status}).");
        }

        if (invite.ExpiresAtUtc <= now)
        {
            invite.Status = OwnerInviteStatusNames.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            return PlatformTenantOperationResult<StaffSignInResponse>.BadRequest("Invite has expired.");
        }

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(org => org.OrganizationId == invite.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return PlatformTenantOperationResult<StaffSignInResponse>.BadRequest("Tenant no longer exists.");
        }

        if (organization.Status != TenantStatusNames.Active)
        {
            return PlatformTenantOperationResult<StaffSignInResponse>.BadRequest(
                $"Tenant is not active (status = {organization.Status}).");
        }

        var branch = await dbContext.Branches
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == invite.OrganizationId && candidate.BranchId == invite.BranchId,
                cancellationToken);
        if (branch is null)
        {
            return PlatformTenantOperationResult<StaffSignInResponse>.BadRequest("Branch no longer exists.");
        }

        var requestedUserName = request.UserName.Trim();
        var normalizedUserName = requestedUserName.ToUpperInvariant();
        var userNameTaken = await dbContext.StaffUsers.AnyAsync(
            candidate =>
                candidate.OrganizationId == invite.OrganizationId &&
                candidate.NormalizedUserName == normalizedUserName,
            cancellationToken);
        if (userNameTaken)
        {
            return PlatformTenantOperationResult<StaffSignInResponse>.Conflict(
                "UserName is already in use in this tenant.");
        }

        var staffUser = new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(),
            OrganizationId = invite.OrganizationId,
            UserName = requestedUserName,
            NormalizedUserName = normalizedUserName,
            DisplayName = request.DisplayName.Trim(),
            IsActive = true,
            CreatedAtUtc = now
        };
        staffUser.PasswordHash = staffPasswordHasher.HashPassword(staffUser, request.Password);
        dbContext.StaffUsers.Add(staffUser);

        dbContext.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(),
            StaffUserId = staffUser.StaffUserId,
            OrganizationId = invite.OrganizationId,
            BranchId = invite.BranchId,
            RoleName = StaffRoleNames.Owner
        });

        invite.Status = OwnerInviteStatusNames.Accepted;
        invite.AcceptedAtUtc = now;
        invite.AcceptedByStaffUserId = staffUser.StaffUserId;

        await dbContext.SaveChangesAsync(cancellationToken);

        var signInResponse = await staffTokenService.IssueAsync(staffUser, cancellationToken);
        return PlatformTenantOperationResult<StaffSignInResponse>.Success(signInResponse);
    }

    public async Task<PlatformTenantOperationResult<TenantDetailDto>> UpdateStatusAsync(
        Guid organizationId,
        UpdateTenantStatusRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformTenantOperationResult<TenantDetailDto>.BadRequest("OrganizationId is required.");
        }

        var statusError = ValidateTenantStatus(request.Status);
        if (statusError is not null)
        {
            return PlatformTenantOperationResult<TenantDetailDto>.BadRequest(statusError);
        }

        var normalizedStatus = request.Status.Trim();
        var reasonError = ValidateStatusReason(normalizedStatus, request.Reason);
        if (reasonError is not null)
        {
            return PlatformTenantOperationResult<TenantDetailDto>.BadRequest(reasonError);
        }

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (organization is null)
        {
            return PlatformTenantOperationResult<TenantDetailDto>.NotFound("Tenant was not found.");
        }

        var trimmedReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var now = timeProvider.GetUtcNow();
        if (organization.Status != normalizedStatus || organization.StatusReason != trimmedReason)
        {
            organization.Status = normalizedStatus;
            organization.StatusReason = trimmedReason;
            organization.StatusChangedAtUtc = now;
            organization.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var detail = await BuildTenantDetailAsync(organizationId, cancellationToken);
        return PlatformTenantOperationResult<TenantDetailDto>.Success(detail!);
    }

    public async Task<PlatformTenantOperationResult<TenantDetailDto>> UpdatePlanAsync(
        Guid organizationId,
        UpdateTenantPlanRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformTenantOperationResult<TenantDetailDto>.BadRequest("OrganizationId is required.");
        }

        var planError = ValidatePlanCode(request.PlanCode);
        if (planError is not null)
        {
            return PlatformTenantOperationResult<TenantDetailDto>.BadRequest(planError);
        }

        var subscriptionError = ValidateSubscriptionStatus(request.SubscriptionStatus);
        if (subscriptionError is not null)
        {
            return PlatformTenantOperationResult<TenantDetailDto>.BadRequest(subscriptionError);
        }

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (organization is null)
        {
            return PlatformTenantOperationResult<TenantDetailDto>.NotFound("Tenant was not found.");
        }

        var trimmedPlan = request.PlanCode.Trim();
        var trimmedSubscription = request.SubscriptionStatus.Trim();
        var now = timeProvider.GetUtcNow();
        if (organization.PlanCode != trimmedPlan || organization.SubscriptionStatus != trimmedSubscription)
        {
            organization.PlanCode = trimmedPlan;
            organization.SubscriptionStatus = trimmedSubscription;
            organization.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var detail = await BuildTenantDetailAsync(organizationId, cancellationToken);
        return PlatformTenantOperationResult<TenantDetailDto>.Success(detail!);
    }

    public async Task<PlatformTenantOperationResult<OwnerInviteDto>> RevokeOwnerInviteAsync(
        Guid ownerInviteId,
        RevokeOwnerInviteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (ownerInviteId == Guid.Empty)
        {
            return PlatformTenantOperationResult<OwnerInviteDto>.BadRequest("OwnerInviteId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return PlatformTenantOperationResult<OwnerInviteDto>.BadRequest("Reason is required.");
        }

        var trimmedReason = request.Reason.Trim();
        if (trimmedReason.Length > MaxStatusReasonLength)
        {
            return PlatformTenantOperationResult<OwnerInviteDto>.BadRequest(
                $"Reason must contain {MaxStatusReasonLength} characters or fewer.");
        }

        var invite = await dbContext.OwnerInvites
            .SingleOrDefaultAsync(candidate => candidate.OwnerInviteId == ownerInviteId, cancellationToken);
        if (invite is null)
        {
            return PlatformTenantOperationResult<OwnerInviteDto>.NotFound("Owner invite was not found.");
        }

        if (invite.Status != OwnerInviteStatusNames.Pending)
        {
            return PlatformTenantOperationResult<OwnerInviteDto>.BadRequest(
                $"Owner invite is not pending (status = {invite.Status}).");
        }

        var now = timeProvider.GetUtcNow();
        invite.Status = OwnerInviteStatusNames.Revoked;
        invite.RevokedAtUtc = now;
        invite.RevokedByPlatformAdminUserId = platformAdminUserId;
        invite.RevokedReason = trimmedReason;
        await dbContext.SaveChangesAsync(cancellationToken);

        return PlatformTenantOperationResult<OwnerInviteDto>.Success(ToInviteDto(invite));
    }

    public async Task<PlatformTenantOperationResult<TenantDetailDto>> UpdateLimitsAsync(
        Guid organizationId,
        UpdateTenantLimitsRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformTenantOperationResult<TenantDetailDto>.BadRequest("OrganizationId is required.");
        }

        if (request.Limits is null)
        {
            return PlatformTenantOperationResult<TenantDetailDto>.BadRequest("Limits payload is required.");
        }

        var limitsError = ValidateLimits(request.Limits);
        if (limitsError is not null)
        {
            return PlatformTenantOperationResult<TenantDetailDto>.BadRequest(limitsError);
        }

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (organization is null)
        {
            return PlatformTenantOperationResult<TenantDetailDto>.NotFound("Tenant was not found.");
        }

        var serializedLimits = SerializeLimits(request.Limits);
        var now = timeProvider.GetUtcNow();
        if (organization.LimitsJson != serializedLimits)
        {
            organization.LimitsJson = serializedLimits;
            organization.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var detail = await BuildTenantDetailAsync(organizationId, cancellationToken);
        return PlatformTenantOperationResult<TenantDetailDto>.Success(detail!);
    }

    private static string? ValidateTenantStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Status is required.";
        }

        return AllowedTenantStatuses.Contains(status.Trim())
            ? null
            : $"Status must be one of: {string.Join(", ", AllowedTenantStatuses)}.";
    }

    private static string? ValidateStatusReason(string normalizedStatus, string? reason)
    {
        var requiresReason = normalizedStatus is TenantStatusNames.Suspended or TenantStatusNames.DeletionPending;
        if (requiresReason && string.IsNullOrWhiteSpace(reason))
        {
            return $"Reason is required when transitioning to '{normalizedStatus}'.";
        }

        if (!string.IsNullOrWhiteSpace(reason) && reason.Trim().Length > MaxStatusReasonLength)
        {
            return $"Reason must contain {MaxStatusReasonLength} characters or fewer.";
        }

        return null;
    }

    private OwnerInviteEntity BuildOwnerInvite(
        Guid organizationId,
        Guid branchId,
        Guid platformAdminUserId,
        string? ownerUserName,
        string? ownerDisplayName,
        TimeSpan lifetime,
        DateTimeOffset now)
    {
        var code = inviteCodeGenerator.Generate();
        return new OwnerInviteEntity
        {
            OwnerInviteId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            Code = code,
            NormalizedCode = NormalizeInviteCode(code),
            Status = OwnerInviteStatusNames.Pending,
            OwnerUserName = string.IsNullOrWhiteSpace(ownerUserName) ? null : ownerUserName.Trim(),
            OwnerDisplayName = string.IsNullOrWhiteSpace(ownerDisplayName) ? null : ownerDisplayName.Trim(),
            ExpiresAtUtc = now.Add(lifetime),
            CreatedByPlatformAdminUserId = platformAdminUserId,
            CreatedAtUtc = now
        };
    }

    private async Task<TenantDetailDto?> BuildTenantDetailAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (organization is null)
        {
            return null;
        }

        var branches = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.OrganizationId == organizationId)
            .OrderBy(branch => branch.Name)
            .ToListAsync(cancellationToken);

        return new TenantDetailDto(
            OrganizationId: organization.OrganizationId,
            Slug: organization.Slug,
            Name: organization.Name,
            Status: organization.Status,
            StatusReason: organization.StatusReason,
            StatusChangedAtUtc: organization.StatusChangedAtUtc,
            PlanCode: organization.PlanCode,
            SubscriptionStatus: organization.SubscriptionStatus,
            Limits: DeserializeLimits(organization.LimitsJson),
            Branches: branches.Select(branch => new TenantBranchDto(
                BranchId: branch.BranchId,
                Slug: branch.Slug,
                Name: branch.Name,
                City: branch.City,
                CreatedAtUtc: branch.CreatedAtUtc)).ToList(),
            CreatedAtUtc: organization.CreatedAtUtc,
            UpdatedAtUtc: organization.UpdatedAtUtc);
    }

    internal static string NormalizeInviteCode(string code) => code.Trim().ToLowerInvariant();

    public async Task<PlatformTenantOperationResult<IReadOnlyList<OwnerInviteSummaryDto>>> ListOwnerInvitesAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformTenantOperationResult<IReadOnlyList<OwnerInviteSummaryDto>>.BadRequest(
                "OrganizationId is required.");
        }

        var tenantExists = await dbContext.Organizations
            .AsNoTracking()
            .AnyAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (!tenantExists)
        {
            return PlatformTenantOperationResult<IReadOnlyList<OwnerInviteSummaryDto>>.NotFound(
                "Tenant was not found.");
        }

        var invites = await dbContext.OwnerInvites
            .AsNoTracking()
            .Where(invite => invite.OrganizationId == organizationId)
            .OrderByDescending(invite => invite.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        IReadOnlyList<OwnerInviteSummaryDto> summaries = invites.Select(ToInviteSummaryDto).ToList();
        return PlatformTenantOperationResult<IReadOnlyList<OwnerInviteSummaryDto>>.Success(summaries);
    }

    private static OwnerInviteDto ToInviteDto(OwnerInviteEntity entity) =>
        new(
            OwnerInviteId: entity.OwnerInviteId,
            OrganizationId: entity.OrganizationId,
            BranchId: entity.BranchId,
            Code: entity.Code,
            Status: entity.Status,
            OwnerUserName: entity.OwnerUserName,
            OwnerDisplayName: entity.OwnerDisplayName,
            ExpiresAtUtc: entity.ExpiresAtUtc,
            AcceptedAtUtc: entity.AcceptedAtUtc,
            RevokedAtUtc: entity.RevokedAtUtc,
            RevokedReason: entity.RevokedReason,
            CreatedAtUtc: entity.CreatedAtUtc);

    private static OwnerInviteSummaryDto ToInviteSummaryDto(OwnerInviteEntity entity) =>
        new(
            OwnerInviteId: entity.OwnerInviteId,
            OrganizationId: entity.OrganizationId,
            BranchId: entity.BranchId,
            CodeSuffix: SuffixCode(entity.Code),
            Status: entity.Status,
            OwnerUserName: entity.OwnerUserName,
            OwnerDisplayName: entity.OwnerDisplayName,
            ExpiresAtUtc: entity.ExpiresAtUtc,
            AcceptedAtUtc: entity.AcceptedAtUtc,
            RevokedAtUtc: entity.RevokedAtUtc,
            RevokedReason: entity.RevokedReason,
            CreatedAtUtc: entity.CreatedAtUtc);

    private static string SuffixCode(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }
        return code.Length <= 4 ? code : code[^4..];
    }

    private static string SerializeLimits(TenantLimitsDto? limits)
    {
        var safe = limits ?? new TenantLimitsDto(null, null, null, null);
        return JsonSerializer.Serialize(safe);
    }

    private static TenantLimitsDto DeserializeLimits(string limitsJson)
    {
        if (string.IsNullOrWhiteSpace(limitsJson) || limitsJson == "{}")
        {
            return new TenantLimitsDto(null, null, null, null);
        }

        try
        {
            return JsonSerializer.Deserialize<TenantLimitsDto>(limitsJson)
                ?? new TenantLimitsDto(null, null, null, null);
        }
        catch (JsonException)
        {
            return new TenantLimitsDto(null, null, null, null);
        }
    }

    private static string? ValidateRequiredText(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"{fieldName} is required.";
        }

        return value.Trim().Length > maxLength
            ? $"{fieldName} must contain {maxLength} characters or fewer."
            : null;
    }

    private static string? ValidatePlanCode(string planCode)
    {
        if (string.IsNullOrWhiteSpace(planCode))
        {
            return "PlanCode is required.";
        }

        return planCode.Trim().Length > MaxPlanCodeLength
            ? $"PlanCode must contain {MaxPlanCodeLength} characters or fewer."
            : null;
    }

    private static string? ValidateSubscriptionStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "SubscriptionStatus is required.";
        }

        return AllowedSubscriptionStatuses.Contains(status.Trim())
            ? null
            : $"SubscriptionStatus must be one of: {string.Join(", ", AllowedSubscriptionStatuses)}.";
    }

    private static string? ValidateLimits(TenantLimitsDto? limits)
    {
        if (limits is null)
        {
            return null;
        }

        if (limits.MaxBranches is < 0 ||
            limits.MaxDevicesPerBranch is < 0 ||
            limits.MaxConcurrentSessions is < 0 ||
            limits.MaxStaffUsersPerBranch is < 0)
        {
            return "Tenant limits must be non-negative when provided.";
        }

        return null;
    }

    private static string? ValidateOptionalUserName(string? userName)
    {
        if (string.IsNullOrEmpty(userName))
        {
            return null;
        }

        return userName.Trim().Length > MaxUserNameLength
            ? $"OwnerUserName must contain {MaxUserNameLength} characters or fewer."
            : null;
    }

    private static string? ValidateOptionalDisplayName(string? displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return null;
        }

        return displayName.Trim().Length > MaxDisplayNameLength
            ? $"OwnerDisplayName must contain {MaxDisplayNameLength} characters or fewer."
            : null;
    }

    private string? ValidateLifetime(TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            return "Owner invite lifetime must be positive.";
        }

        return lifetime > options.MaxOwnerInviteLifetime
            ? $"Owner invite lifetime must not exceed {options.MaxOwnerInviteLifetime}."
            : null;
    }
}
