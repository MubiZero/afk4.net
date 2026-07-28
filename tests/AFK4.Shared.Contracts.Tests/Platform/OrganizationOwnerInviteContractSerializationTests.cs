using System.Text.Json;
using AFK4.Shared.Contracts.Identity.AccountActivation;

namespace AFK4.Shared.Contracts.Tests.Platform;

public sealed class OrganizationOwnerInviteContractSerializationTests
{
    [Fact]
    public void OrganizationOwnerInviteDto_RoundTripsLifecycleTimestamps()
    {
        var invite = new OrganizationOwnerInviteDto(
            OrganizationOwnerInviteId: Guid.Parse("99999999-1111-2222-3333-444444444444"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            Code: "demo-invite-abc123",
            Status: OrganizationOwnerInviteStatusNames.Pending,
            OwnerUserName: "owner@demo.test",
            OwnerDisplayName: "Demo Owner",
            ExpiresAtUtc: DateTimeOffset.Parse("2026-05-30T08:00:00Z"),
            AcceptedAtUtc: null,
            RevokedAtUtc: null,
            RevokedReason: null,
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z"));

        var json = JsonSerializer.Serialize(invite);
        var copy = JsonSerializer.Deserialize<OrganizationOwnerInviteDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(invite.OrganizationOwnerInviteId, copy.OrganizationOwnerInviteId);
        Assert.Equal(invite.OrganizationId, copy.OrganizationId);
        Assert.Equal(invite.BranchId, copy.BranchId);
        Assert.Equal(invite.Code, copy.Code);
        Assert.Equal(invite.Status, copy.Status);
        Assert.Equal(invite.OwnerUserName, copy.OwnerUserName);
        Assert.Equal(invite.ExpiresAtUtc, copy.ExpiresAtUtc);
        Assert.Null(copy.AcceptedAtUtc);
        Assert.Null(copy.RevokedAtUtc);
    }

    [Fact]
    public void CreateOrganizationOwnerInviteRequest_RoundTripsOptionalFields()
    {
        var request = new CreateOrganizationOwnerInviteRequest(
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            OwnerUserName: "owner@demo.test",
            OwnerDisplayName: "Demo Owner",
            Lifetime: TimeSpan.FromDays(14));

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<CreateOrganizationOwnerInviteRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.BranchId, copy.BranchId);
        Assert.Equal(request.OwnerUserName, copy.OwnerUserName);
        Assert.Equal(request.Lifetime, copy.Lifetime);
    }

    [Fact]
    public void RevokeOrganizationOwnerInviteRequest_RoundTripsThroughJson()
    {
        var request = new RevokeOrganizationOwnerInviteRequest("Owner left the company");

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<RevokeOrganizationOwnerInviteRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.Reason, copy.Reason);
    }

    [Fact]
    public void OrganizationOwnerInviteSummaryDto_RoundTripsWithCodeSuffix()
    {
        var summary = new OrganizationOwnerInviteSummaryDto(
            OrganizationOwnerInviteId: Guid.Parse("99999999-1111-2222-3333-444444444444"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            CodeSuffix: "c123",
            Status: OrganizationOwnerInviteStatusNames.Pending,
            OwnerUserName: "owner@demo.test",
            OwnerDisplayName: "Demo Owner",
            ExpiresAtUtc: DateTimeOffset.Parse("2026-05-30T08:00:00Z"),
            AcceptedAtUtc: null,
            RevokedAtUtc: null,
            RevokedReason: null,
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z"));

        var json = JsonSerializer.Serialize(summary);
        var copy = JsonSerializer.Deserialize<OrganizationOwnerInviteSummaryDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(summary.OrganizationOwnerInviteId, copy.OrganizationOwnerInviteId);
        Assert.Equal("c123", copy.CodeSuffix);
        Assert.DoesNotContain("demo-invite", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptOrganizationOwnerInviteRequest_RoundTripsThroughJson()
    {
        var request = new AcceptOrganizationOwnerInviteRequest(
            Code: "demo-invite-abc123",
            UserName: "demo.owner",
            DisplayName: "Demo Owner",
            Password: "Passw0rd!Real");

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<AcceptOrganizationOwnerInviteRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.Code, copy.Code);
        Assert.Equal(request.UserName, copy.UserName);
        Assert.Equal(request.DisplayName, copy.DisplayName);
        Assert.Equal(request.Password, copy.Password);
    }
}
