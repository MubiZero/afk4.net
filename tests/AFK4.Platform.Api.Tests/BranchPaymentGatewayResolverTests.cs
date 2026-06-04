using System;
using System.Threading;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Payments;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class BranchPaymentGatewayResolverTests
{
    private static BranchPaymentGatewayEntity Gateway(
        Guid orgId, Guid? branchId, string projectId, string status) =>
        new()
        {
            BranchPaymentGatewayId = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            DcgateProjectId = projectId,
            ApiKeyEncrypted = "v1.a.b.c",
            WebhookSecretEncrypted = "v1.d.e.f",
            CardLast4 = "0001",
            Status = status,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

    private static async Task SeedAsync(PlatformApiFactory factory, params BranchPaymentGatewayEntity[] rows)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.BranchPaymentGateways.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private static async Task<T> WithResolver<T>(
        PlatformApiFactory factory, Func<IBranchPaymentGatewayResolver, Task<T>> act)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IBranchPaymentGatewayResolver>();
        return await act(resolver);
    }

    [Fact]
    public async Task ResolveForBranch_PrefersBranchSpecificActiveGateway()
    {
        await using var factory = new PlatformApiFactory();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        await SeedAsync(factory,
            Gateway(org, null, "proj_org", BranchPaymentGatewayStatus.Active),
            Gateway(org, branch, "proj_branch", BranchPaymentGatewayStatus.Active));

        var result = await WithResolver(factory, r => r.ResolveForBranchAsync(org, branch, CancellationToken.None));

        Assert.NotNull(result);
        Assert.Equal("proj_branch", result!.DcgateProjectId);
    }

    [Fact]
    public async Task ResolveForBranch_FallsBackToOrgLevelGateway()
    {
        await using var factory = new PlatformApiFactory();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        await SeedAsync(factory, Gateway(org, null, "proj_org", BranchPaymentGatewayStatus.Active));

        var result = await WithResolver(factory, r => r.ResolveForBranchAsync(org, branch, CancellationToken.None));

        Assert.NotNull(result);
        Assert.Equal("proj_org", result!.DcgateProjectId);
    }

    [Fact]
    public async Task ResolveForBranch_IgnoresNonActiveAndForeignOrg()
    {
        await using var factory = new PlatformApiFactory();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        await SeedAsync(factory,
            Gateway(org, branch, "proj_disabled", BranchPaymentGatewayStatus.Disabled),
            Gateway(Guid.NewGuid(), null, "proj_other_org", BranchPaymentGatewayStatus.Active));

        var result = await WithResolver(factory, r => r.ResolveForBranchAsync(org, branch, CancellationToken.None));

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveByProjectId_ReturnsRowRegardlessOfStatus()
    {
        await using var factory = new PlatformApiFactory();
        var org = Guid.NewGuid();
        await SeedAsync(factory, Gateway(org, null, "proj_late", BranchPaymentGatewayStatus.Disabled));

        var result = await WithResolver(factory, r => r.ResolveByProjectIdAsync("proj_late", CancellationToken.None));

        Assert.NotNull(result);
        Assert.Equal(org, result!.OrganizationId);
    }

    [Fact]
    public async Task ResolveByProjectId_UnknownReturnsNull()
    {
        await using var factory = new PlatformApiFactory();

        var result = await WithResolver(factory, r => r.ResolveByProjectIdAsync("nope", CancellationToken.None));

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveForBranch_IgnoresDisabledOrgLevelGateway()
    {
        await using var factory = new PlatformApiFactory();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        await SeedAsync(factory, Gateway(org, null, "proj_org_disabled", BranchPaymentGatewayStatus.Disabled));

        var result = await WithResolver(factory, r => r.ResolveForBranchAsync(org, branch, CancellationToken.None));

        Assert.Null(result);
    }
}
