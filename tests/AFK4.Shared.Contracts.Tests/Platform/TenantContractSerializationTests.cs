using System.Text.Json;
using AFK4.Shared.Contracts.Platform.Tenants;

namespace AFK4.Shared.Contracts.Tests.Platform;

public sealed class TenantContractSerializationTests
{
    [Fact]
    public void TenantSummaryDto_RoundTripsThroughJson()
    {
        var summary = new TenantSummaryDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            Slug: "demo-org",
            Name: "Demo Org",
            Status: TenantStatusNames.Active,
            PlanCode: TenantPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Trial,
            BranchCount: 1,
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:10:00Z"));

        var json = JsonSerializer.Serialize(summary);
        var copy = JsonSerializer.Deserialize<TenantSummaryDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(summary.OrganizationId, copy.OrganizationId);
        Assert.Equal(summary.Slug, copy.Slug);
        Assert.Equal(summary.Status, copy.Status);
        Assert.Equal(summary.PlanCode, copy.PlanCode);
        Assert.Equal(summary.SubscriptionStatus, copy.SubscriptionStatus);
        Assert.Equal(summary.BranchCount, copy.BranchCount);
    }

    [Fact]
    public void TenantDetailDto_RoundTripsBranchesAndLimits()
    {
        var detail = new TenantDetailDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            Slug: "demo-org",
            Name: "Demo Org",
            Status: TenantStatusNames.Suspended,
            StatusReason: "Outstanding invoice",
            StatusChangedAtUtc: DateTimeOffset.Parse("2026-05-23T08:30:00Z"),
            PlanCode: TenantPlanCodeNames.Growth,
            SubscriptionStatus: SubscriptionStatusNames.PastDue,
            Limits: new TenantLimitsDto(
                MaxBranches: 3,
                MaxDevicesPerBranch: 60,
                MaxConcurrentSessions: 80,
                MaxStaffUsersPerBranch: 20),
            Branches: [
                new TenantBranchDto(
                    BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
                    Slug: "demo-branch",
                    Name: "Demo Branch",
                    City: "Dushanbe",
                    CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z"))
            ],
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:30:00Z"));

        var json = JsonSerializer.Serialize(detail);
        var copy = JsonSerializer.Deserialize<TenantDetailDto>(json);

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
    public void CreateTenantRequest_RoundTripsOptionalOwnerInviteFields()
    {
        var request = new CreateTenantRequest(
            OrganizationSlug: "new-club",
            OrganizationName: "New Club",
            BranchSlug: "main",
            BranchName: "Main Branch",
            BranchCity: "Khujand",
            PlanCode: TenantPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Trial,
            Limits: new TenantLimitsDto(2, 40, 60, 12),
            OwnerUserName: "owner@new-club.test",
            OwnerDisplayName: "Owner",
            OwnerInviteLifetime: TimeSpan.FromDays(7));

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<CreateTenantRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.OrganizationSlug, copy.OrganizationSlug);
        Assert.Equal(request.BranchSlug, copy.BranchSlug);
        Assert.Equal(request.OwnerUserName, copy.OwnerUserName);
        Assert.Equal(request.OwnerInviteLifetime, copy.OwnerInviteLifetime);
        Assert.NotNull(copy.Limits);
        Assert.Equal(2, copy.Limits!.MaxBranches);
    }

    [Fact]
    public void UpdateTenantStatusRequest_RoundTripsThroughJson()
    {
        var request = new UpdateTenantStatusRequest(
            Status: TenantStatusNames.Suspended,
            Reason: "Payment overdue");

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<UpdateTenantStatusRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.Status, copy.Status);
        Assert.Equal(request.Reason, copy.Reason);
    }

    [Fact]
    public void UpdateTenantLimitsRequest_RoundTripsNullableFields()
    {
        var request = new UpdateTenantLimitsRequest(
            new TenantLimitsDto(
                MaxBranches: null,
                MaxDevicesPerBranch: 100,
                MaxConcurrentSessions: null,
                MaxStaffUsersPerBranch: 50));

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<UpdateTenantLimitsRequest>(json);

        Assert.NotNull(copy);
        Assert.Null(copy.Limits.MaxBranches);
        Assert.Equal(100, copy.Limits.MaxDevicesPerBranch);
        Assert.Null(copy.Limits.MaxConcurrentSessions);
        Assert.Equal(50, copy.Limits.MaxStaffUsersPerBranch);
    }

    [Fact]
    public void CreateTenantResponse_RoundTripsTenantAndInvite()
    {
        var response = new AFK4.Shared.Contracts.Platform.Tenants.CreateTenantResponse(
            Tenant: new TenantDetailDto(
                OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
                Slug: "demo-club",
                Name: "Demo Club",
                Status: TenantStatusNames.Active,
                StatusReason: null,
                StatusChangedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z"),
                PlanCode: TenantPlanCodeNames.Starter,
                SubscriptionStatus: SubscriptionStatusNames.Trial,
                Limits: new TenantLimitsDto(1, 20, 30, 5),
                Branches: [
                    new TenantBranchDto(
                        BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
                        Slug: "demo-branch",
                        Name: "Demo Branch",
                        City: "Dushanbe",
                        CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z"))
                ],
                CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z"),
                UpdatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z")),
            OwnerInvite: new AFK4.Shared.Contracts.Platform.Invites.OwnerInviteDto(
                OwnerInviteId: Guid.Parse("99999999-1111-2222-3333-444444444444"),
                OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
                BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
                Code: "demo-invite-abc123",
                Status: AFK4.Shared.Contracts.Platform.Invites.OwnerInviteStatusNames.Pending,
                OwnerUserName: "owner@demo-club.test",
                OwnerDisplayName: "Demo Owner",
                ExpiresAtUtc: DateTimeOffset.Parse("2026-05-30T08:00:00Z"),
                AcceptedAtUtc: null,
                RevokedAtUtc: null,
                RevokedReason: null,
                CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z")));

        var json = JsonSerializer.Serialize(response);
        var copy = JsonSerializer.Deserialize<AFK4.Shared.Contracts.Platform.Tenants.CreateTenantResponse>(json);

        Assert.NotNull(copy);
        Assert.Equal(response.Tenant.OrganizationId, copy.Tenant.OrganizationId);
        Assert.Equal(response.Tenant.Branches[0].Slug, copy.Tenant.Branches[0].Slug);
        Assert.Equal(response.OwnerInvite.Code, copy.OwnerInvite.Code);
        Assert.Equal(AFK4.Shared.Contracts.Platform.Invites.OwnerInviteStatusNames.Pending, copy.OwnerInvite.Status);
    }
}
