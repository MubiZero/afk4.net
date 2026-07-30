using System.Text.Json;
using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Shared.Contracts.Tests.Platform;

public sealed class OrganizationContractSerializationTests
{
    [Fact]
    public void OrganizationSummaryDto_RoundTripsThroughJson()
    {
        var summary = new OrganizationSummaryDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            Slug: "demo-org",
            Name: "Demo Org",
            Status: OrganizationStatusNames.Active,
            PlanCode: OrganizationPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Trial,
            BranchCount: 1,
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:10:00Z"),
            RecentErrorCount: 2,
            ExpiringOwnerInviteCount: 1,
            RolloutAttentionCount: 1);

        var json = JsonSerializer.Serialize(summary);
        var copy = JsonSerializer.Deserialize<OrganizationSummaryDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(summary.OrganizationId, copy.OrganizationId);
        Assert.Equal(summary.Slug, copy.Slug);
        Assert.Equal(summary.Status, copy.Status);
        Assert.Equal(summary.PlanCode, copy.PlanCode);
        Assert.Equal(summary.SubscriptionStatus, copy.SubscriptionStatus);
        Assert.Equal(summary.BranchCount, copy.BranchCount);
        Assert.Equal(summary.RecentErrorCount, copy.RecentErrorCount);
        Assert.Equal(summary.ExpiringOwnerInviteCount, copy.ExpiringOwnerInviteCount);
        Assert.Equal(summary.RolloutAttentionCount, copy.RolloutAttentionCount);
    }

    [Fact]
    public void OrganizationDetailDto_RoundTripsBranchesAndLimits()
    {
        var detail = new OrganizationDetailDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            Slug: "demo-org",
            Name: "Demo Org",
            Status: OrganizationStatusNames.Suspended,
            StatusReason: "Outstanding invoice",
            StatusChangedAtUtc: DateTimeOffset.Parse("2026-05-23T08:30:00Z"),
            PlanCode: OrganizationPlanCodeNames.Growth,
            SubscriptionStatus: SubscriptionStatusNames.PastDue,
            Limits: new OrganizationLimitsDto(
                MaxBranches: 3,
                MaxDevicesPerBranch: 60,
                MaxConcurrentSessions: 80,
                MaxStaffUsersPerBranch: 20),
            Branches: [
                new OrganizationBranchDto(
                    BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
                    Slug: "demo-branch",
                    Name: "Demo Branch",
                    City: "Dushanbe",
                    CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z"))
            ],
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:30:00Z"));

        var json = JsonSerializer.Serialize(detail);
        var copy = JsonSerializer.Deserialize<OrganizationDetailDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(detail.Status, copy.Status);
        Assert.Equal(detail.StatusReason, copy.StatusReason);
        Assert.Equal(detail.StatusChangedAtUtc, copy.StatusChangedAtUtc);
        Assert.Equal(detail.PlanCode, copy.PlanCode);
        Assert.Equal(detail.Limits.MaxBranches, copy.Limits.MaxBranches);
        Assert.Equal(detail.Limits.MaxStaffUsersPerBranch, copy.Limits.MaxStaffUsersPerBranch);
        Assert.Single(copy.Branches);
        Assert.Equal("demo-branch", copy.Branches[0].Slug);
    }

    [Fact]
    public void CreateOrganizationRequest_RoundTripsOptionalOrganizationOwnerInviteFields()
    {
        var request = new CreateOrganizationRequest(
            OrganizationSlug: "new-club",
            OrganizationName: "New Club",
            BranchSlug: "main",
            BranchName: "Main Branch",
            BranchCity: "Khujand",
            PlanCode: OrganizationPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Trial,
            Limits: new OrganizationLimitsDto(2, 40, 60, 12),
            OwnerUserName: "owner@new-club.test",
            OwnerDisplayName: "Owner",
            OrganizationOwnerInviteLifetime: TimeSpan.FromDays(7));

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<CreateOrganizationRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.OrganizationSlug, copy.OrganizationSlug);
        Assert.Equal(request.BranchSlug, copy.BranchSlug);
        Assert.Equal(request.OwnerUserName, copy.OwnerUserName);
        Assert.Equal(request.OrganizationOwnerInviteLifetime, copy.OrganizationOwnerInviteLifetime);
        Assert.NotNull(copy.Limits);
        Assert.Equal(2, copy.Limits!.MaxBranches);
    }

    [Fact]
    public void UpdateOrganizationStatusRequest_RoundTripsThroughJson()
    {
        var request = new UpdateOrganizationStatusRequest(
            Status: OrganizationStatusNames.Suspended,
            Reason: "Payment overdue");

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<UpdateOrganizationStatusRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.Status, copy.Status);
        Assert.Equal(request.Reason, copy.Reason);
    }

    [Fact]
    public void UpdateOrganizationLimitsRequest_RoundTripsNullableFields()
    {
        var request = new UpdateOrganizationLimitsRequest(
            new OrganizationLimitsDto(
                MaxBranches: null,
                MaxDevicesPerBranch: 100,
                MaxConcurrentSessions: null,
                MaxStaffUsersPerBranch: 50));

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<UpdateOrganizationLimitsRequest>(json);

        Assert.NotNull(copy);
        Assert.Null(copy.Limits.MaxBranches);
        Assert.Equal(100, copy.Limits.MaxDevicesPerBranch);
        Assert.Null(copy.Limits.MaxConcurrentSessions);
        Assert.Equal(50, copy.Limits.MaxStaffUsersPerBranch);
    }

    [Fact]
    public void CreateOrganizationResponse_RoundTripsOrganizationAndInvite()
    {
        var response = new AFK4.Shared.Contracts.Platform.Organizations.CreateOrganizationResponse(
            Organization: new OrganizationDetailDto(
                OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
                Slug: "demo-club",
                Name: "Demo Club",
                Status: OrganizationStatusNames.Active,
                StatusReason: null,
                StatusChangedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z"),
                PlanCode: OrganizationPlanCodeNames.Starter,
                SubscriptionStatus: SubscriptionStatusNames.Trial,
                Limits: new OrganizationLimitsDto(1, 20, 30, 5),
                Branches: [
                    new OrganizationBranchDto(
                        BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
                        Slug: "demo-branch",
                        Name: "Demo Branch",
                        City: "Dushanbe",
                        CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z"))
                ],
                CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z"),
                UpdatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z")),
            OrganizationOwnerInvite: new AFK4.Shared.Contracts.Identity.AccountActivation.OrganizationOwnerInviteDto(
                OrganizationOwnerInviteId: Guid.Parse("99999999-1111-2222-3333-444444444444"),
                OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
                BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
                Code: "demo-invite-abc123",
                Status: AFK4.Shared.Contracts.Identity.AccountActivation.OrganizationOwnerInviteStatusNames.Pending,
                OwnerUserName: "owner@demo-club.test",
                OwnerDisplayName: "Demo Owner",
                ExpiresAtUtc: DateTimeOffset.Parse("2026-05-30T08:00:00Z"),
                AcceptedAtUtc: null,
                RevokedAtUtc: null,
                RevokedReason: null,
                CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z")));

        var json = JsonSerializer.Serialize(response);
        var copy = JsonSerializer.Deserialize<AFK4.Shared.Contracts.Platform.Organizations.CreateOrganizationResponse>(json);

        Assert.NotNull(copy);
        Assert.Equal(response.Organization.OrganizationId, copy.Organization.OrganizationId);
        Assert.Equal(response.Organization.Branches[0].Slug, copy.Organization.Branches[0].Slug);
        Assert.Equal(response.OrganizationOwnerInvite.Code, copy.OrganizationOwnerInvite.Code);
        Assert.Equal(AFK4.Shared.Contracts.Identity.AccountActivation.OrganizationOwnerInviteStatusNames.Pending, copy.OrganizationOwnerInvite.Status);
    }
}
