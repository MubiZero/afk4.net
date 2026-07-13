using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class PlatformDbContextDesignTimeFactoryTests : IDisposable
{
    private readonly string? originalConnectionString;

    public PlatformDbContextDesignTimeFactoryTests()
    {
        originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PlatformDatabase");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__PlatformDatabase", originalConnectionString);
    }

    [Fact]
    public void CreateDbContext_UsesPlatformDatabaseEnvironmentConnectionStringWhenPresent()
    {
        const string connectionString = "Host=localhost;Port=55432;Database=afk4_restore_rehearsal;Username=postgres;SSL Mode=Disable";
        Environment.SetEnvironmentVariable("ConnectionStrings__PlatformDatabase", connectionString);

        using var context = new PlatformDbContextDesignTimeFactory().CreateDbContext([]);

        Assert.Equal(connectionString, context.Database.GetConnectionString());
    }

    [Fact]
    public void CreateDbContext_ConfiguresShopOrderPosSaleRelationshipAndLineCost()
    {
        using var context = new PlatformDbContextDesignTimeFactory().CreateDbContext([]);

        var shopOrderType = context.Model.FindEntityType(typeof(ShopOrderEntity));
        var posSaleId = shopOrderType!.FindProperty(nameof(ShopOrderEntity.PosSaleId));
        var index = shopOrderType.GetIndexes().Single(candidate => candidate.Properties.SequenceEqual([posSaleId!]));
        var foreignKey = shopOrderType.GetForeignKeys().Single(candidate => candidate.Properties.SequenceEqual([posSaleId!]));
        var unitCost = context.Model.FindEntityType(typeof(PosSaleLineEntity))!
            .FindProperty(nameof(PosSaleLineEntity.UnitCostMinorUnits));
        var tracksStock = context.Model.FindEntityType(typeof(PosSaleLineEntity))!
            .FindProperty(nameof(PosSaleLineEntity.TracksStock));

        Assert.True(index.IsUnique);
        Assert.Equal("\"PosSaleId\" IS NOT NULL", index.GetFilter());
        Assert.Equal(typeof(PosSaleEntity), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.False(unitCost!.IsNullable);
        Assert.False(tracksStock!.IsNullable);
    }
}
