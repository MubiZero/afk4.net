using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Data;

public sealed class BranchDailySnapshotSchemaTests
{
    [Fact]
    public void Snapshot_HasUniqueIndexOnBranchAndDate()
    {
        using var factory = new PlatformApiFactory();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var entityType = dbContext.Model.FindEntityType(typeof(BranchDailySnapshotEntity));
        Assert.NotNull(entityType);
        Assert.Equal("branch_daily_snapshots", entityType!.GetTableName());

        var unique = entityType.GetIndexes().Single(index => index.IsUnique);
        Assert.Equal(
            new[] { nameof(BranchDailySnapshotEntity.BranchId), nameof(BranchDailySnapshotEntity.SnapshotDate) },
            unique.Properties.Select(property => property.Name).ToArray());
    }

    [Fact]
    public void AgentAlive_IsNullable_SoBackfilledDaysCanSayUnknown()
    {
        using var factory = new PlatformApiFactory();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var property = dbContext.Model
            .FindEntityType(typeof(BranchDailySnapshotEntity))!
            .FindProperty(nameof(BranchDailySnapshotEntity.AgentAlive));

        Assert.NotNull(property);
        Assert.True(property!.IsNullable);
    }
}
