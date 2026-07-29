using System.Text.Json;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Updates;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Shared.Contracts.Tests.Platform;

public sealed class PlatformUpdateContractSerializationTests
{
    [Fact]
    public void CreatePlatformUpdatePackageRequest_RoundTripsSignedImmutableMetadata()
    {
        var request = new CreatePlatformUpdatePackageRequest(
            UpdateComponentNames.OrganizationAdmin,
            "1.4.0",
            UpdateChannelNames.Stable,
            "https://updates.afk4.net/organization-admin/stable/1.4.0/admin.msi",
            new string('a', 64),
            "base64-signature",
            UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363,
            1_613_824,
            "Safe update lifecycle.");

        var copy = JsonSerializer.Deserialize<CreatePlatformUpdatePackageRequest>(
            JsonSerializer.Serialize(request));

        Assert.Equal(request, copy);
    }

    [Fact]
    public void CreatePlatformUpdateRolloutRequest_RoundTripsEveryTargetScope()
    {
        var organizationId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var branchId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var deviceId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var request = new CreatePlatformUpdateRolloutRequest(
            UpdatePackageId: Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            Channel: UpdateChannelNames.Stable,
            TargetKind: PlatformUpdateTargetKindNames.Device,
            OrganizationIds: [organizationId],
            BranchIds: [branchId],
            DeviceIds: [deviceId],
            BatchPercent: 10,
            StartsAtUtc: DateTimeOffset.Parse("2026-07-29T12:00:00Z"),
            Reason: "Canary rollout.");

        var copy = JsonSerializer.Deserialize<CreatePlatformUpdateRolloutRequest>(
            JsonSerializer.Serialize(request));

        Assert.NotNull(copy);
        Assert.Equal(request.UpdatePackageId, copy.UpdatePackageId);
        Assert.Equal(organizationId, Assert.Single(copy.OrganizationIds));
        Assert.Equal(branchId, Assert.Single(copy.BranchIds));
        Assert.Equal(deviceId, Assert.Single(copy.DeviceIds));
        Assert.Equal(10, copy.BatchPercent);
    }

    [Fact]
    public void PlatformUpdateDtos_RoundTripPlatformActorsAndLifecycleTimes()
    {
        var actorId = Guid.Parse("44444444-4444-4444-8444-444444444444");
        var package = new PlatformUpdatePackageDto(
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            UpdateComponentNames.AgentService,
            "1.4.0",
            UpdateChannelNames.Beta,
            "https://updates.afk4.net/agent-service/beta/1.4.0/agent.msi",
            new string('b', 64),
            "signature",
            UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363,
            42,
            UpdatePackageStateNames.Validated,
            "Beta candidate.",
            actorId,
            DateTimeOffset.Parse("2026-07-29T10:00:00Z"),
            actorId,
            DateTimeOffset.Parse("2026-07-29T10:05:00Z"),
            null);

        var copy = JsonSerializer.Deserialize<PlatformUpdatePackageDto>(JsonSerializer.Serialize(package));

        Assert.Equal(package, copy);
        Assert.Equal(actorId, copy!.CreatedByPlatformAdminUserId);
        Assert.Equal(actorId, copy.ValidatedByPlatformAdminUserId);
        Assert.Null(copy.RetiredAtUtc);
    }

    [Fact]
    public void PlatformUpdatePermissions_BelongToPlatformDomain()
    {
        Assert.Equal("platform.updates.view", PlatformAdminPermissionNames.ViewUpdates);
        Assert.Equal("platform.updates.packages.manage", PlatformAdminPermissionNames.ManageUpdatePackages);
        Assert.Equal("platform.updates.rollouts.manage", PlatformAdminPermissionNames.ManageUpdateRollouts);
    }
}
