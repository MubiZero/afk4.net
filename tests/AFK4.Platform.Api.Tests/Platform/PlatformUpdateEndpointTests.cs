using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformUpdateEndpointTests
{
    [Fact]
    public async Task ReleaseLifecycle_RequiresValidationAndPersistsPlatformActorAndTargets()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        await SeedOrganizationAsync(factory);
        var packageResponse = await client.PostAsJsonAsync(
            "/api/platform/updates/packages",
            PackageRequest());
        var package = await packageResponse.Content.ReadFromJsonAsync<PlatformUpdatePackageDto>();

        Assert.Equal(HttpStatusCode.OK, packageResponse.StatusCode);
        Assert.NotNull(package);
        Assert.Equal(UpdatePackageStateNames.Registered, package.State);
        Assert.Equal(admin.PlatformAdminId, package.CreatedByPlatformAdminUserId);

        var registeredRollout = await client.PostAsJsonAsync(
            "/api/platform/updates/rollouts",
            RolloutRequest(package.UpdatePackageId));
        Assert.Equal(HttpStatusCode.BadRequest, registeredRollout.StatusCode);

        var validationResponse = await client.PostAsJsonAsync(
            $"/api/platform/updates/packages/{package.UpdatePackageId:D}/state",
            new ChangePlatformUpdatePackageStateRequest(UpdatePackageStateNames.Validated, "Signature verified."));
        var validated = await validationResponse.Content.ReadFromJsonAsync<PlatformUpdatePackageDto>();
        Assert.Equal(HttpStatusCode.OK, validationResponse.StatusCode);
        Assert.NotNull(validated);
        Assert.Equal(admin.PlatformAdminId, validated.ValidatedByPlatformAdminUserId);
        Assert.NotNull(validated.ValidatedAtUtc);

        var rolloutResponse = await client.PostAsJsonAsync(
            "/api/platform/updates/rollouts",
            RolloutRequest(package.UpdatePackageId));
        var rollout = await rolloutResponse.Content.ReadFromJsonAsync<PlatformUpdateRolloutDto>();
        Assert.Equal(HttpStatusCode.OK, rolloutResponse.StatusCode);
        Assert.NotNull(rollout);
        Assert.Equal([TestIds.OrganizationId], rollout.OrganizationIds);
        Assert.Equal(10, rollout.BatchPercent);
        Assert.Equal(admin.PlatformAdminId, rollout.CreatedByPlatformAdminUserId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var target = await db.UpdateRolloutTargets.SingleAsync();
        Assert.Equal(TestIds.OrganizationId, target.OrganizationId);
        Assert.Null(target.BranchId);
        Assert.Null(target.DeviceId);
    }

    [Fact]
    public async Task PackageMutation_WithoutPlatformAuthentication_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/platform/updates/packages", PackageRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PackageMutation_WithPlatformSupportRole_ReturnsForbiddenAndDoesNotPersist()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(
            factory,
            client,
            roles: [PlatformAdminRoleNames.PlatformSupport]);

        var response = await client.PostAsJsonAsync("/api/platform/updates/packages", PackageRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await db.UpdatePackages.ToListAsync());
    }

    [Fact]
    public async Task ReleaseLists_ReturnCreatedPackageAndRollout()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        await SeedOrganizationAsync(factory);
        var packageResponse = await client.PostAsJsonAsync("/api/platform/updates/packages", PackageRequest());
        var package = Assert.IsType<PlatformUpdatePackageDto>(
            await packageResponse.Content.ReadFromJsonAsync<PlatformUpdatePackageDto>());
        await client.PostAsJsonAsync(
            $"/api/platform/updates/packages/{package.UpdatePackageId:D}/state",
            new ChangePlatformUpdatePackageStateRequest(UpdatePackageStateNames.Validated, "Signature verified."));
        var rolloutResponse = await client.PostAsJsonAsync(
            "/api/platform/updates/rollouts",
            RolloutRequest(package.UpdatePackageId));
        var rollout = Assert.IsType<PlatformUpdateRolloutDto>(
            await rolloutResponse.Content.ReadFromJsonAsync<PlatformUpdateRolloutDto>());

        var packages = await client.GetFromJsonAsync<IReadOnlyList<PlatformUpdatePackageDto>>(
            "/api/platform/updates/packages");
        var rollouts = await client.GetFromJsonAsync<IReadOnlyList<PlatformUpdateRolloutDto>>(
            "/api/platform/updates/rollouts");

        Assert.Equal(package.UpdatePackageId, Assert.Single(packages!).UpdatePackageId);
        Assert.Equal(rollout.UpdateRolloutId, Assert.Single(rollouts!).UpdateRolloutId);
    }

    private static CreatePlatformUpdatePackageRequest PackageRequest() => new(
        UpdateComponentNames.OrganizationAdmin,
        "1.4.0",
        UpdateChannelNames.Stable,
        "https://updates.afk4.net/organization-admin/stable/1.4.0/admin.msi",
        new string('a', 64),
        "signature",
        UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363,
        1_613_824,
        "Safe release.");

    private static async Task SeedOrganizationAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = TestIds.OrganizationId,
            Slug = "update-target",
            Name = "Update Target",
            CreatedAtUtc = DateTimeOffset.Parse("2026-07-29T10:00:00Z"),
            UpdatedAtUtc = DateTimeOffset.Parse("2026-07-29T10:00:00Z")
        });
        await db.SaveChangesAsync();
    }

    private static CreatePlatformUpdateRolloutRequest RolloutRequest(Guid packageId) => new(
        packageId,
        UpdateChannelNames.Stable,
        PlatformUpdateTargetKindNames.Organization,
        [TestIds.OrganizationId],
        [],
        [],
        10,
        DateTimeOffset.Parse("2026-07-29T12:00:00Z"),
        "Canary rollout.");
}
