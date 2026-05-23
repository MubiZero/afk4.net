using System.Text.Json;
using AFK4.Shared.Contracts.Platform.Invites;

namespace AFK4.Shared.Contracts.Tests.Platform;

public sealed class OwnerInviteContractSerializationTests
{
    [Fact]
    public void OwnerInviteDto_RoundTripsLifecycleTimestamps()
    {
        var invite = new OwnerInviteDto(
            OwnerInviteId: Guid.Parse("99999999-1111-2222-3333-444444444444"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            Code: "demo-invite-abc123",
            Status: OwnerInviteStatusNames.Pending,
            OwnerUserName: "owner@demo.test",
            OwnerDisplayName: "Demo Owner",
            ExpiresAtUtc: DateTimeOffset.Parse("2026-05-30T08:00:00Z"),
            AcceptedAtUtc: null,
            RevokedAtUtc: null,
            RevokedReason: null,
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T08:00:00Z"));

        var json = JsonSerializer.Serialize(invite);
        var copy = JsonSerializer.Deserialize<OwnerInviteDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(invite.OwnerInviteId, copy.OwnerInviteId);
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
    public void CreateOwnerInviteRequest_RoundTripsOptionalFields()
    {
        var request = new CreateOwnerInviteRequest(
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            OwnerUserName: "owner@demo.test",
            OwnerDisplayName: "Demo Owner",
            Lifetime: TimeSpan.FromDays(14));

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<CreateOwnerInviteRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.BranchId, copy.BranchId);
        Assert.Equal(request.OwnerUserName, copy.OwnerUserName);
        Assert.Equal(request.Lifetime, copy.Lifetime);
    }

    [Fact]
    public void RevokeOwnerInviteRequest_RoundTripsThroughJson()
    {
        var request = new RevokeOwnerInviteRequest("Owner left the company");

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<RevokeOwnerInviteRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.Reason, copy.Reason);
    }
}
