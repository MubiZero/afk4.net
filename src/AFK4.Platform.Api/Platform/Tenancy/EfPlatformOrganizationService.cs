using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Identity.AccountActivation;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Updates;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Tenancy;

public sealed class EfPlatformOrganizationService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    IOrganizationOwnerInviteCodeGenerator inviteCodeGenerator,
    INotificationService notifications,
    IOptions<PlatformOrganizationOptions> organizationOptions) : IPlatformOrganizationService
{
    private const int MinPasswordLength = 8;
    private const int MaxUserNameLength = 256;
    private const int MaxDisplayNameLength = 160;
    private const int MaxBranchCityLength = 120;
    private const int MaxOrganizationNameLength = 160;
    private const int MaxBranchNameLength = 160;
    private const int MaxPlanCodeLength = 64;

    private const int MaxStatusReasonLength = 500;
    private const int MaxContactEmailLength = 256;
    private const int MaxContactPhoneLength = 32;
    private const int MaxLegalDetailsLength = 1024;
    private const int MaxPinnedVersionLength = 32;

    private static readonly HashSet<string> AllowedSubscriptionStatuses = new(StringComparer.Ordinal)
    {
        SubscriptionStatusNames.Trial,
        SubscriptionStatusNames.Active,
        SubscriptionStatusNames.PastDue,
        SubscriptionStatusNames.Cancelled
    };

    private static readonly HashSet<string> AllowedOrganizationStatuses = new(StringComparer.Ordinal)
    {
        OrganizationStatusNames.Active,
        OrganizationStatusNames.Suspended,
        OrganizationStatusNames.DeletionPending
    };

    private static readonly HashSet<string> AllowedUpdateChannels = new(StringComparer.Ordinal)
    {
        UpdateChannelNames.Stable,
        UpdateChannelNames.Beta,
        UpdateChannelNames.Internal
    };

    private readonly PasswordHasher<StaffUserEntity> staffPasswordHasher = new();
    private readonly PlatformOrganizationOptions options = organizationOptions.Value;

    public async Task<PlatformOrganizationOperationResult<CreateOrganizationResponse>> CreateAsync(
        CreateOrganizationRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        var orgSlug = SlugValidator.Normalize(request.OrganizationSlug);
        var orgSlugError = SlugValidator.Validate(orgSlug, "OrganizationSlug");
        if (orgSlugError is not null)
        {
            return PlatformOrganizationOperationResult<CreateOrganizationResponse>.BadRequest(orgSlugError);
        }

        var branchSlug = SlugValidator.Normalize(request.BranchSlug);
        var branchSlugError = SlugValidator.Validate(branchSlug, "BranchSlug");
        if (branchSlugError is not null)
        {
            return PlatformOrganizationOperationResult<CreateOrganizationResponse>.BadRequest(branchSlugError);
        }

        var nameError = ValidateRequiredText(request.OrganizationName, "OrganizationName", MaxOrganizationNameLength);
        if (nameError is not null)
        {
            return PlatformOrganizationOperationResult<CreateOrganizationResponse>.BadRequest(nameError);
        }

        var branchNameError = ValidateRequiredText(request.BranchName, "BranchName", MaxBranchNameLength);
        if (branchNameError is not null)
        {
            return PlatformOrganizationOperationResult<CreateOrganizationResponse>.BadRequest(branchNameError);
        }

        var branchCityError = ValidateRequiredText(request.BranchCity, "BranchCity", MaxBranchCityLength);
        if (branchCityError is not null)
        {
            return PlatformOrganizationOperationResult<CreateOrganizationResponse>.BadRequest(branchCityError);
        }

        var planError = ValidatePlanCode(request.PlanCode);
        if (planError is not null)
        {
            return PlatformOrganizationOperationResult<CreateOrganizationResponse>.BadRequest(planError);
        }

        var subscriptionError = ValidateSubscriptionStatus(request.SubscriptionStatus);
        if (subscriptionError is not null)
        {
            return PlatformOrganizationOperationResult<CreateOrganizationResponse>.BadRequest(subscriptionError);
        }

        var limitsError = ValidateLimits(request.Limits);
        if (limitsError is not null)
        {
            return PlatformOrganizationOperationResult<CreateOrganizationResponse>.BadRequest(limitsError);
        }

        var ownerUserNameError = ValidateOptionalUserName(request.OwnerUserName);
        if (ownerUserNameError is not null)
        {
            return PlatformOrganizationOperationResult<CreateOrganizationResponse>.BadRequest(ownerUserNameError);
        }

        var ownerDisplayNameError = ValidateOptionalDisplayName(request.OwnerDisplayName);
        if (ownerDisplayNameError is not null)
        {
            return PlatformOrganizationOperationResult<CreateOrganizationResponse>.BadRequest(ownerDisplayNameError);
        }

        var lifetime = request.OrganizationOwnerInviteLifetime ?? options.DefaultOrganizationOwnerInviteLifetime;
        var lifetimeError = ValidateLifetime(lifetime);
        if (lifetimeError is not null)
        {
            return PlatformOrganizationOperationResult<CreateOrganizationResponse>.BadRequest(lifetimeError);
        }

        var orgSlugTaken = await dbContext.Organizations
            .AnyAsync(candidate => candidate.Slug == orgSlug, cancellationToken);
        if (orgSlugTaken)
        {
            return PlatformOrganizationOperationResult<CreateOrganizationResponse>.Conflict(
                $"Organization slug '{orgSlug}' is already in use.");
        }

        var now = timeProvider.GetUtcNow();
        var organization = new OrganizationEntity
        {
            OrganizationId = Guid.NewGuid(),
            Slug = orgSlug,
            Name = request.OrganizationName.Trim(),
            Status = OrganizationStatusNames.Active,
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

        var invite = BuildOrganizationOwnerInvite(
            organization.OrganizationId,
            branch.BranchId,
            platformAdminUserId,
            request.OwnerUserName,
            request.OwnerDisplayName,
            ownerEmail: null,
            lifetime,
            now);

        var defaultZone = new ZoneEntity
        {
            ZoneId = Guid.NewGuid(),
            OrganizationId = organization.OrganizationId,
            BranchId = branch.BranchId,
            Name = "Общий зал",
            SortOrder = 1,
            CreatedAtUtc = now
        };

        dbContext.Organizations.Add(organization);
        dbContext.Branches.Add(branch);
        dbContext.Zones.Add(defaultZone);
        dbContext.OrganizationOwnerInvites.Add(invite);
        var catalogPlan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(plan => plan.PlanCode == organization.PlanCode, cancellationToken);
        var subscriptionInterval = catalogPlan?.BillingInterval ?? "monthly";
        dbContext.OrganizationSubscriptions.Add(new OrganizationSubscriptionEntity
        {
            OrganizationSubscriptionId = Guid.NewGuid(),
            OrganizationId = organization.OrganizationId,
            PlanCode = organization.PlanCode,
            Status = organization.SubscriptionStatus,
            CurrentPeriodStartUtc = now,
            CurrentPeriodEndUtc = subscriptionInterval == "yearly" ? now.AddYears(1) : now.AddMonths(1),
            NextInvoiceUtc = subscriptionInterval == "yearly" ? now.AddYears(1) : now.AddMonths(1),
            AmountMinorUnits = catalogPlan?.PriceMinorUnits ?? 0,
            CurrencyCode = catalogPlan?.CurrencyCode ?? "TJS",
            BillingInterval = subscriptionInterval,
            CancelAtPeriodEnd = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var detail = await BuildOrganizationDetailAsync(organization.OrganizationId, cancellationToken);
        var inviteDto = ToInviteDto(invite);
        return PlatformOrganizationOperationResult<CreateOrganizationResponse>.Success(
            new CreateOrganizationResponse(detail!, inviteDto));
    }

    public async Task<IReadOnlyList<OrganizationSummaryDto>> ListAsync(CancellationToken cancellationToken)
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
        var now = timeProvider.GetUtcNow();
        var recentErrorSince = now.AddDays(-7);
        var inviteCutoff = now.AddDays(3);
        var recentErrorsById = await dbContext.AuditRecords.AsNoTracking()
            .Where(record => orgIds.Contains(record.OrganizationId) && record.Outcome == AuditOutcome.Denied && record.CreatedAtUtc >= recentErrorSince)
            .GroupBy(record => record.OrganizationId)
            .Select(group => new { OrganizationId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.OrganizationId, item => item.Count, cancellationToken);
        var expiringInvitesById = await dbContext.OrganizationOwnerInvites.AsNoTracking()
            .Where(invite => orgIds.Contains(invite.OrganizationId) && invite.Status == OrganizationOwnerInviteStatusNames.Pending && invite.ExpiresAtUtc > now && invite.ExpiresAtUtc <= inviteCutoff)
            .GroupBy(invite => invite.OrganizationId)
            .Select(group => new { OrganizationId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.OrganizationId, item => item.Count, cancellationToken);
        var rolloutAttentionById = await dbContext.DeviceUpdateStatuses.AsNoTracking()
            .Where(status => orgIds.Contains(status.OrganizationId) && status.Status == UpdateStatusNames.Failed)
            .GroupBy(status => status.OrganizationId)
            .Select(group => new { OrganizationId = group.Key, Count = group.Select(status => status.UpdateRolloutId).Distinct().Count() })
            .ToDictionaryAsync(item => item.OrganizationId, item => item.Count, cancellationToken);

        return orgs.Select(org => new OrganizationSummaryDto(
            OrganizationId: org.OrganizationId,
            Slug: org.Slug,
            Name: org.Name,
            Status: org.Status,
            PlanCode: org.PlanCode,
            SubscriptionStatus: org.SubscriptionStatus,
            BranchCount: branchCountsById.GetValueOrDefault(org.OrganizationId),
            CreatedAtUtc: org.CreatedAtUtc,
            UpdatedAtUtc: org.UpdatedAtUtc,
            RecentErrorCount: recentErrorsById.GetValueOrDefault(org.OrganizationId),
            ExpiringOwnerInviteCount: expiringInvitesById.GetValueOrDefault(org.OrganizationId),
            RolloutAttentionCount: rolloutAttentionById.GetValueOrDefault(org.OrganizationId))).ToList();
    }

    public Task<OrganizationDetailDto?> GetAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return BuildOrganizationDetailAsync(organizationId, cancellationToken);
    }

    public async Task<PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>> CreateOrRotateOrganizationOwnerInviteAsync(
        Guid organizationId,
        CreateOrganizationOwnerInviteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest("OrganizationId is required.");
        }

        if (request.BranchId == Guid.Empty)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest("BranchId is required.");
        }

        var ownerUserNameError = ValidateOptionalUserName(request.OwnerUserName);
        if (ownerUserNameError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest(ownerUserNameError);
        }

        var ownerDisplayNameError = ValidateOptionalDisplayName(request.OwnerDisplayName);
        if (ownerDisplayNameError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest(ownerDisplayNameError);
        }

        var lifetime = request.Lifetime ?? options.DefaultOrganizationOwnerInviteLifetime;
        var lifetimeError = ValidateLifetime(lifetime);
        if (lifetimeError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest(lifetimeError);
        }

        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (organization is null)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.NotFound("Organization was not found.");
        }

        var branch = await dbContext.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.BranchId == request.BranchId,
                cancellationToken);
        if (branch is null)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.NotFound("Branch was not found in this organization.");
        }

        var now = timeProvider.GetUtcNow();
        var pendingInvites = await dbContext.OrganizationOwnerInvites
            .Where(invite =>
                invite.OrganizationId == organizationId &&
                invite.BranchId == request.BranchId &&
                invite.Status == OrganizationOwnerInviteStatusNames.Pending)
            .ToListAsync(cancellationToken);
        foreach (var pending in pendingInvites)
        {
            pending.Status = OrganizationOwnerInviteStatusNames.Revoked;
            pending.RevokedAtUtc = now;
            pending.RevokedByPlatformAdminUserId = platformAdminUserId;
            pending.RevokedReason = "Rotated by platform admin.";
        }

        var freshInvite = BuildOrganizationOwnerInvite(
            organizationId,
            request.BranchId,
            platformAdminUserId,
            request.OwnerUserName,
            request.OwnerDisplayName,
            request.OwnerEmail,
            lifetime,
            now);
        dbContext.OrganizationOwnerInvites.Add(freshInvite);

        await dbContext.SaveChangesAsync(cancellationToken);

        await SendOrganizationOwnerInviteEmailAsync(freshInvite, $"owner-invite:{freshInvite.OrganizationOwnerInviteId:N}", cancellationToken);

        return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.Success(ToInviteDto(freshInvite));
    }

    public async Task<PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>> ResendOrganizationOwnerInviteAsync(
        Guid organizationOwnerInviteId,
        CancellationToken cancellationToken)
    {
        if (organizationOwnerInviteId == Guid.Empty)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest("OrganizationOwnerInviteId is required.");
        }

        var invite = await dbContext.OrganizationOwnerInvites
            .SingleOrDefaultAsync(candidate => candidate.OrganizationOwnerInviteId == organizationOwnerInviteId, cancellationToken);
        if (invite is null)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.NotFound("Owner invite was not found.");
        }

        if (invite.Status != OrganizationOwnerInviteStatusNames.Pending)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest("Only pending invites can be resent.");
        }

        if (string.IsNullOrWhiteSpace(invite.OwnerEmail))
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest("This invite has no email address on file.");
        }

        var now = timeProvider.GetUtcNow();
        await SendOrganizationOwnerInviteEmailAsync(invite, $"owner-invite-resend:{invite.OrganizationOwnerInviteId:N}:{now.UtcTicks}", cancellationToken);

        return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.Success(ToInviteDto(invite));
    }

    public async Task<PlatformOrganizationOperationResult<OrganizationOwnerAccountActivationResult>> AcceptOrganizationOwnerInviteAsync(
        AcceptOrganizationOwnerInviteRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerAccountActivationResult>.BadRequest("Code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.UserName) || request.UserName.Trim().Length > MaxUserNameLength)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerAccountActivationResult>.BadRequest(
                $"UserName is required and must contain {MaxUserNameLength} characters or fewer.");
        }

        if (request.DisplayName is { } providedDisplayName && providedDisplayName.Trim().Length > MaxDisplayNameLength)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerAccountActivationResult>.BadRequest(
                $"DisplayName must contain {MaxDisplayNameLength} characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < MinPasswordLength)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerAccountActivationResult>.BadRequest(
                $"Password must contain at least {MinPasswordLength} characters.");
        }

        var normalizedCode = NormalizeInviteCode(request.Code);
        var invite = await dbContext.OrganizationOwnerInvites
            .SingleOrDefaultAsync(candidate => candidate.NormalizedCode == normalizedCode, cancellationToken);
        if (invite is null)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerAccountActivationResult>.NotFound("Invite was not found.");
        }

        var now = timeProvider.GetUtcNow();
        if (invite.Status != OrganizationOwnerInviteStatusNames.Pending)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerAccountActivationResult>.BadRequest(
                $"Invite is no longer pending (status = {invite.Status}).");
        }

        if (invite.ExpiresAtUtc <= now)
        {
            invite.Status = OrganizationOwnerInviteStatusNames.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            return PlatformOrganizationOperationResult<OrganizationOwnerAccountActivationResult>.BadRequest("Invite has expired.");
        }

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(org => org.OrganizationId == invite.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerAccountActivationResult>.BadRequest("Organization no longer exists.");
        }

        if (organization.Status != OrganizationStatusNames.Active)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerAccountActivationResult>.BadRequest(
                $"Organization is not active (status = {organization.Status}).");
        }

        var branch = await dbContext.Branches
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == invite.OrganizationId && candidate.BranchId == invite.BranchId,
                cancellationToken);
        if (branch is null)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerAccountActivationResult>.BadRequest("Branch no longer exists.");
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
            return PlatformOrganizationOperationResult<OrganizationOwnerAccountActivationResult>.Conflict(
                "UserName is already in use in this organization.");
        }

        var staffUser = new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(),
            OrganizationId = invite.OrganizationId,
            UserName = requestedUserName,
            NormalizedUserName = normalizedUserName,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? DeriveDisplayNameFromLogin(requestedUserName)
                : request.DisplayName.Trim(),
            // Carries the invite's email forward so later flows (owner-transfer self-check, owner
            // notifications) have a real address to compare against instead of guessing from UserName.
            Email = string.IsNullOrWhiteSpace(invite.OwnerEmail) ? null : invite.OwnerEmail,
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
            RoleName = OrganizationRoleNames.OrganizationOwner
        });

        invite.Status = OrganizationOwnerInviteStatusNames.Accepted;
        invite.AcceptedAtUtc = now;
        invite.AcceptedByStaffUserId = staffUser.StaffUserId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return PlatformOrganizationOperationResult<OrganizationOwnerAccountActivationResult>.Success(
            new OrganizationOwnerAccountActivationResult(
                invite.OrganizationId,
                invite.BranchId,
                "sign_in_to_organization_admin"));
    }

    private static string DeriveDisplayNameFromLogin(string login)
    {
        var atIndex = login.IndexOf('@');
        var localPart = atIndex > 0 ? login[..atIndex] : login;
        return localPart.Trim();
    }

    public async Task<PlatformOrganizationOperationResult<OrganizationDetailDto>> UpdateStatusAsync(
        Guid organizationId,
        UpdateOrganizationStatusRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.BadRequest("OrganizationId is required.");
        }

        var statusError = ValidateOrganizationStatus(request.Status);
        if (statusError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.BadRequest(statusError);
        }

        var normalizedStatus = request.Status.Trim();
        var reasonError = ValidateStatusReason(normalizedStatus, request.Reason);
        if (reasonError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.BadRequest(reasonError);
        }

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (organization is null)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.NotFound("Organization was not found.");
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

        var detail = await BuildOrganizationDetailAsync(organizationId, cancellationToken);
        return PlatformOrganizationOperationResult<OrganizationDetailDto>.Success(detail!);
    }

    public async Task<PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>> RevokeOrganizationOwnerInviteAsync(
        Guid organizationOwnerInviteId,
        RevokeOrganizationOwnerInviteRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (organizationOwnerInviteId == Guid.Empty)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest("OrganizationOwnerInviteId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest("Reason is required.");
        }

        var trimmedReason = request.Reason.Trim();
        if (trimmedReason.Length > MaxStatusReasonLength)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest(
                $"Reason must contain {MaxStatusReasonLength} characters or fewer.");
        }

        var invite = await dbContext.OrganizationOwnerInvites
            .SingleOrDefaultAsync(candidate => candidate.OrganizationOwnerInviteId == organizationOwnerInviteId, cancellationToken);
        if (invite is null)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.NotFound("Owner invite was not found.");
        }

        if (invite.Status != OrganizationOwnerInviteStatusNames.Pending)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest(
                $"Owner invite is not pending (status = {invite.Status}).");
        }

        var now = timeProvider.GetUtcNow();
        invite.Status = OrganizationOwnerInviteStatusNames.Revoked;
        invite.RevokedAtUtc = now;
        invite.RevokedByPlatformAdminUserId = platformAdminUserId;
        invite.RevokedReason = trimmedReason;
        await dbContext.SaveChangesAsync(cancellationToken);

        return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.Success(ToInviteDto(invite));
    }

    public async Task<PlatformOrganizationOperationResult<OrganizationDetailDto>> UpdateLimitsAsync(
        Guid organizationId,
        UpdateOrganizationLimitsRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.BadRequest("OrganizationId is required.");
        }

        if (request.Limits is null)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.BadRequest("Limits payload is required.");
        }

        var limitsError = ValidateLimits(request.Limits);
        if (limitsError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.BadRequest(limitsError);
        }

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (organization is null)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.NotFound("Organization was not found.");
        }

        var serializedLimits = SerializeLimits(request.Limits);
        var now = timeProvider.GetUtcNow();
        if (organization.LimitsJson != serializedLimits)
        {
            organization.LimitsJson = serializedLimits;
            organization.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var detail = await BuildOrganizationDetailAsync(organizationId, cancellationToken);
        return PlatformOrganizationOperationResult<OrganizationDetailDto>.Success(detail!);
    }

    public async Task<PlatformOrganizationOperationResult<OrganizationDetailDto>> UpdateProfileAsync(
        Guid organizationId,
        UpdateOrganizationProfileRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.BadRequest("OrganizationId is required.");
        }

        var nameError = ValidateRequiredText(request.Name, "Name", MaxOrganizationNameLength);
        if (nameError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.BadRequest(nameError);
        }

        var contactEmailError = ValidateOptionalText(request.ContactEmail, "ContactEmail", MaxContactEmailLength);
        if (contactEmailError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.BadRequest(contactEmailError);
        }

        var contactPhoneError = ValidateOptionalText(request.ContactPhone, "ContactPhone", MaxContactPhoneLength);
        if (contactPhoneError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.BadRequest(contactPhoneError);
        }

        var legalDetailsError = ValidateOptionalText(request.LegalDetails, "LegalDetails", MaxLegalDetailsLength);
        if (legalDetailsError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.BadRequest(legalDetailsError);
        }

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (organization is null)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.NotFound("Organization was not found.");
        }

        var trimmedName = request.Name.Trim();
        var trimmedContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim();
        var trimmedContactPhone = string.IsNullOrWhiteSpace(request.ContactPhone) ? null : request.ContactPhone.Trim();
        var trimmedLegalDetails = string.IsNullOrWhiteSpace(request.LegalDetails) ? null : request.LegalDetails.Trim();

        var now = timeProvider.GetUtcNow();
        if (organization.Name != trimmedName
            || organization.ContactEmail != trimmedContactEmail
            || organization.ContactPhone != trimmedContactPhone
            || organization.LegalDetails != trimmedLegalDetails)
        {
            organization.Name = trimmedName;
            organization.ContactEmail = trimmedContactEmail;
            organization.ContactPhone = trimmedContactPhone;
            organization.LegalDetails = trimmedLegalDetails;
            organization.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var detail = await BuildOrganizationDetailAsync(organizationId, cancellationToken);
        return PlatformOrganizationOperationResult<OrganizationDetailDto>.Success(detail!);
    }

    public async Task<PlatformOrganizationOperationResult<OrganizationDetailDto>> UpdateUpdateChannelAsync(
        Guid organizationId,
        UpdateOrganizationUpdateChannelRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.BadRequest("OrganizationId is required.");
        }

        var channelError = ValidateUpdateChannel(request.Channel);
        if (channelError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.BadRequest(channelError);
        }

        var pinnedVersionError = ValidateOptionalText(request.PinnedClientVersion, "PinnedClientVersion", MaxPinnedVersionLength);
        if (pinnedVersionError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.BadRequest(pinnedVersionError);
        }

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (organization is null)
        {
            return PlatformOrganizationOperationResult<OrganizationDetailDto>.NotFound("Organization was not found.");
        }

        var normalizedChannel = request.Channel.Trim();
        var trimmedPinnedVersion = string.IsNullOrWhiteSpace(request.PinnedClientVersion) ? null : request.PinnedClientVersion.Trim();
        var now = timeProvider.GetUtcNow();
        if (organization.UpdateChannel != normalizedChannel || organization.PinnedClientVersion != trimmedPinnedVersion)
        {
            organization.UpdateChannel = normalizedChannel;
            organization.PinnedClientVersion = trimmedPinnedVersion;
            organization.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var detail = await BuildOrganizationDetailAsync(organizationId, cancellationToken);
        return PlatformOrganizationOperationResult<OrganizationDetailDto>.Success(detail!);
    }

    public async Task<PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>> TransferOrganizationOwnerAsync(
        Guid organizationId,
        TransferOrganizationOwnerRequest request,
        Guid platformAdminUserId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest("OrganizationId is required.");
        }

        var newOwnerEmailError = ValidateRequiredText(request.NewOwnerEmail, "NewOwnerEmail", MaxContactEmailLength);
        if (newOwnerEmailError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest(newOwnerEmailError);
        }

        var reasonError = ValidateRequiredText(request.Reason, "Reason", MaxStatusReasonLength);
        if (reasonError is not null)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest(reasonError);
        }

        var organizationExists = await dbContext.Organizations
            .AsNoTracking()
            .AnyAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (!organizationExists)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.NotFound("Organization was not found.");
        }

        var currentOwner = await (
            from assignment in dbContext.StaffRoleAssignments
            join staff in dbContext.StaffUsers on assignment.StaffUserId equals staff.StaffUserId
            where assignment.OrganizationId == organizationId
                && assignment.RoleName == OrganizationRoleNames.OrganizationOwner
                && staff.IsActive
            select new { Staff = staff, assignment.BranchId })
            .FirstOrDefaultAsync(cancellationToken);
        if (currentOwner is null)
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.NotFound(
                "Organization has no active owner to transfer.");
        }

        // The login (UserName) is not guaranteed to be an email, and Email is only populated when the
        // invite the owner accepted carried one (see AcceptOrganizationOwnerInviteAsync). To recognise a
        // self-transfer reliably, check every address we actually have on record for this owner instead
        // of guessing from a single field: the staff row's Email/UserName, and the email of the invite
        // they originally accepted (covers rows created before Email started being persisted at accept).
        var acceptedInviteEmail = await dbContext.OrganizationOwnerInvites
            .AsNoTracking()
            .Where(invite => invite.AcceptedByStaffUserId == currentOwner.Staff.StaffUserId)
            .Select(invite => invite.OwnerEmail)
            .FirstOrDefaultAsync(cancellationToken);

        var normalizedNewOwnerEmail = request.NewOwnerEmail.Trim();
        var knownCurrentOwnerAddresses = new[] { currentOwner.Staff.Email, currentOwner.Staff.UserName, acceptedInviteEmail }
            .Where(address => !string.IsNullOrWhiteSpace(address));
        if (knownCurrentOwnerAddresses.Any(address => string.Equals(normalizedNewOwnerEmail, address, StringComparison.OrdinalIgnoreCase)))
        {
            return PlatformOrganizationOperationResult<OrganizationOwnerInviteDto>.BadRequest(
                "NewOwnerEmail must differ from the current owner's address.");
        }

        // Issuing the new owner's invite and revoking the previous owner's access must be atomic:
        // if only one side lands, the organization is either left ownerless or double-owned.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var inviteResult = await CreateOrRotateOrganizationOwnerInviteAsync(
            organizationId,
            new CreateOrganizationOwnerInviteRequest(
                BranchId: currentOwner.BranchId,
                OwnerUserName: null,
                OwnerDisplayName: null,
                Lifetime: null,
                OwnerEmail: normalizedNewOwnerEmail),
            platformAdminUserId,
            cancellationToken);

        if (!inviteResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return inviteResult;
        }

        currentOwner.Staff.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return inviteResult;
    }

    private static string? ValidateUpdateChannel(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return "Channel is required.";
        }

        return AllowedUpdateChannels.Contains(channel.Trim())
            ? null
            : $"Channel must be one of: {string.Join(", ", AllowedUpdateChannels)}.";
    }

    private static string? ValidateOrganizationStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Status is required.";
        }

        return AllowedOrganizationStatuses.Contains(status.Trim())
            ? null
            : $"Status must be one of: {string.Join(", ", AllowedOrganizationStatuses)}.";
    }

    private static string? ValidateStatusReason(string normalizedStatus, string? reason)
    {
        var requiresReason = normalizedStatus is OrganizationStatusNames.Suspended or OrganizationStatusNames.DeletionPending;
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

    private OrganizationOwnerInviteEntity BuildOrganizationOwnerInvite(
        Guid organizationId,
        Guid branchId,
        Guid platformAdminUserId,
        string? ownerUserName,
        string? ownerDisplayName,
        string? ownerEmail,
        TimeSpan lifetime,
        DateTimeOffset now)
    {
        var code = inviteCodeGenerator.Generate();
        return new OrganizationOwnerInviteEntity
        {
            OrganizationOwnerInviteId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            Code = code,
            NormalizedCode = NormalizeInviteCode(code),
            Status = OrganizationOwnerInviteStatusNames.Pending,
            OwnerUserName = string.IsNullOrWhiteSpace(ownerUserName) ? null : ownerUserName.Trim(),
            OwnerDisplayName = string.IsNullOrWhiteSpace(ownerDisplayName) ? null : ownerDisplayName.Trim(),
            OwnerEmail = string.IsNullOrWhiteSpace(ownerEmail) ? null : ownerEmail.Trim(),
            ExpiresAtUtc = now.Add(lifetime),
            CreatedByPlatformAdminUserId = platformAdminUserId,
            CreatedAtUtc = now
        };
    }

    private async Task SendOrganizationOwnerInviteEmailAsync(OrganizationOwnerInviteEntity invite, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(invite.OwnerEmail))
        {
            return;
        }

        var request = new NotificationRequest(
            TemplateKey: NotificationTemplateKeys.OrganizationOwnerInvite,
            Category: NotificationCategory.Transactional,
            Recipient: new NotificationRecipient(Locale: string.Empty, EmailAddress: invite.OwnerEmail),
            Tokens: new Dictionary<string, string>
            {
                ["displayName"] = string.IsNullOrWhiteSpace(invite.OwnerDisplayName) ? "owner" : invite.OwnerDisplayName!,
                ["code"] = invite.Code,
            },
            IdempotencyKey: idempotencyKey,
            OrganizationId: invite.OrganizationId,
            BranchId: invite.BranchId);

        await notifications.SendAsync(request, cancellationToken);
    }

    private async Task<OrganizationDetailDto?> BuildOrganizationDetailAsync(Guid organizationId, CancellationToken cancellationToken)
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

        return new OrganizationDetailDto(
            OrganizationId: organization.OrganizationId,
            Slug: organization.Slug,
            Name: organization.Name,
            Status: organization.Status,
            StatusReason: organization.StatusReason,
            StatusChangedAtUtc: organization.StatusChangedAtUtc,
            PlanCode: organization.PlanCode,
            SubscriptionStatus: organization.SubscriptionStatus,
            Limits: DeserializeLimits(organization.LimitsJson),
            Branches: branches.Select(branch => new OrganizationBranchDto(
                BranchId: branch.BranchId,
                Slug: branch.Slug,
                Name: branch.Name,
                City: branch.City,
                CreatedAtUtc: branch.CreatedAtUtc)).ToList(),
            CreatedAtUtc: organization.CreatedAtUtc,
            UpdatedAtUtc: organization.UpdatedAtUtc,
            ContactEmail: organization.ContactEmail,
            ContactPhone: organization.ContactPhone,
            LegalDetails: organization.LegalDetails,
            UpdateChannel: organization.UpdateChannel,
            PinnedClientVersion: organization.PinnedClientVersion);
    }

    internal static string NormalizeInviteCode(string code) => code.Trim().ToLowerInvariant();

    public async Task<PlatformOrganizationOperationResult<IReadOnlyList<OrganizationOwnerInviteSummaryDto>>> ListOrganizationOwnerInvitesAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return PlatformOrganizationOperationResult<IReadOnlyList<OrganizationOwnerInviteSummaryDto>>.BadRequest(
                "OrganizationId is required.");
        }

        var organizationExists = await dbContext.Organizations
            .AsNoTracking()
            .AnyAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (!organizationExists)
        {
            return PlatformOrganizationOperationResult<IReadOnlyList<OrganizationOwnerInviteSummaryDto>>.NotFound(
                "Organization was not found.");
        }

        var invites = await dbContext.OrganizationOwnerInvites
            .AsNoTracking()
            .Where(invite => invite.OrganizationId == organizationId)
            .OrderByDescending(invite => invite.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        IReadOnlyList<OrganizationOwnerInviteSummaryDto> summaries = invites.Select(ToInviteSummaryDto).ToList();
        return PlatformOrganizationOperationResult<IReadOnlyList<OrganizationOwnerInviteSummaryDto>>.Success(summaries);
    }

    private static OrganizationOwnerInviteDto ToInviteDto(OrganizationOwnerInviteEntity entity) =>
        new(
            OrganizationOwnerInviteId: entity.OrganizationOwnerInviteId,
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

    private static OrganizationOwnerInviteSummaryDto ToInviteSummaryDto(OrganizationOwnerInviteEntity entity) =>
        new(
            OrganizationOwnerInviteId: entity.OrganizationOwnerInviteId,
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

    private static string SerializeLimits(OrganizationLimitsDto? limits)
    {
        var safe = limits ?? new OrganizationLimitsDto(null, null, null, null);
        return JsonSerializer.Serialize(safe);
    }

    private static OrganizationLimitsDto DeserializeLimits(string limitsJson)
    {
        if (string.IsNullOrWhiteSpace(limitsJson) || limitsJson == "{}")
        {
            return new OrganizationLimitsDto(null, null, null, null);
        }

        try
        {
            return JsonSerializer.Deserialize<OrganizationLimitsDto>(limitsJson)
                ?? new OrganizationLimitsDto(null, null, null, null);
        }
        catch (JsonException)
        {
            return new OrganizationLimitsDto(null, null, null, null);
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

    private static string? ValidateOptionalText(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
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

    private static string? ValidateLimits(OrganizationLimitsDto? limits)
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
            return "Organization limits must be non-negative when provided.";
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

        return lifetime > options.MaxOrganizationOwnerInviteLifetime
            ? $"Owner invite lifetime must not exceed {options.MaxOrganizationOwnerInviteLifetime}."
            : null;
    }
}
