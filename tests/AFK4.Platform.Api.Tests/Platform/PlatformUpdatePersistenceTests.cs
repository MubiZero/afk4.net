using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformUpdatePersistenceTests
{
    [Fact]
    public void UpdatePackageModel_IsGlobalAndUniquelyIdentifiedByReleaseIdentity()
    {
        using var db = CreateDbContext();
        var entity = db.Model.FindEntityType(typeof(UpdatePackageEntity));

        Assert.NotNull(entity);
        Assert.Null(entity.FindProperty("OrganizationId"));
        Assert.Null(entity.FindProperty("BranchId"));
        Assert.Null(entity.FindProperty("CreatedByStaffUserId"));
        Assert.NotNull(entity.FindProperty("CreatedByPlatformAdminUserId"));
        Assert.NotNull(entity.FindProperty("ValidatedByPlatformAdminUserId"));
        Assert.NotNull(entity.FindProperty("ValidatedAtUtc"));
        Assert.NotNull(entity.FindProperty("RetiredAtUtc"));

        var uniqueReleaseIndex = Assert.Single(entity.GetIndexes(), index => index.IsUnique);
        Assert.Equal(
            ["Component", "Version", "Channel"],
            uniqueReleaseIndex.Properties.Select(property => property.Name));
    }

    [Fact]
    public void UpdateRolloutModel_UsesPlatformActorAndTargetRowsForScope()
    {
        using var db = CreateDbContext();
        var rollout = db.Model.FindEntityType(typeof(UpdateRolloutEntity));
        var target = db.Model.FindEntityType(typeof(UpdateRolloutTargetEntity));

        Assert.NotNull(rollout);
        Assert.Null(rollout.FindProperty("OrganizationId"));
        Assert.Null(rollout.FindProperty("BranchId"));
        Assert.Null(rollout.FindProperty("CreatedByStaffUserId"));
        Assert.NotNull(rollout.FindProperty("CreatedByPlatformAdminUserId"));

        Assert.NotNull(target);
        Assert.True(target.FindProperty("OrganizationId")!.IsNullable);
        Assert.True(target.FindProperty("BranchId")!.IsNullable);
        Assert.True(target.FindProperty("DeviceId")!.IsNullable);
        var uniqueTargetIndexes = target.GetIndexes().Where(index => index.IsUnique).ToList();
        Assert.Contains(uniqueTargetIndexes, index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["UpdateRolloutId", "OrganizationId"]) &&
            index.GetFilter() == "\"OrganizationId\" IS NOT NULL");
        Assert.Contains(uniqueTargetIndexes, index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["UpdateRolloutId", "BranchId"]) &&
            index.GetFilter() == "\"BranchId\" IS NOT NULL");
        Assert.Contains(uniqueTargetIndexes, index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["UpdateRolloutId", "DeviceId"]) &&
            index.GetFilter() == "\"DeviceId\" IS NOT NULL");
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }
}
