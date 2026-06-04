using System;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class BranchPaymentGatewayEntityTests
{
    [Fact]
    public async Task BranchPaymentGateway_PersistsAndReadsBack()
    {
        await using var factory = new PlatformApiFactory();
        var id = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.BranchPaymentGateways.Add(new BranchPaymentGatewayEntity
            {
                BranchPaymentGatewayId = id,
                OrganizationId = orgId,
                BranchId = branchId,
                DcgateProjectId = "proj_abc",
                ApiKeyEncrypted = "v1.aaa.bbb.ccc",
                WebhookSecretEncrypted = "v1.ddd.eee.fff",
                CardLast4 = "1953",
                Status = BranchPaymentGatewayStatus.PendingTelegram,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var stored = await db.BranchPaymentGateways.SingleAsync(g => g.BranchPaymentGatewayId == id);
            Assert.Equal(orgId, stored.OrganizationId);
            Assert.Equal(branchId, stored.BranchId);
            Assert.Equal("proj_abc", stored.DcgateProjectId);
            Assert.Equal("v1.aaa.bbb.ccc", stored.ApiKeyEncrypted);
            Assert.Equal("1953", stored.CardLast4);
            Assert.Equal("pending_telegram", stored.Status);
        }
    }

    [Fact]
    public async Task BranchPaymentGateway_AllowsNullBranchForOrgLevel()
    {
        await using var factory = new PlatformApiFactory();
        var id = Guid.NewGuid();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.BranchPaymentGateways.Add(new BranchPaymentGatewayEntity
        {
            BranchPaymentGatewayId = id,
            OrganizationId = Guid.NewGuid(),
            BranchId = null, // org-level
            DcgateProjectId = "proj_org",
            ApiKeyEncrypted = "v1.a.b.c",
            WebhookSecretEncrypted = "v1.d.e.f",
            CardLast4 = "0000",
            Status = BranchPaymentGatewayStatus.Active,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var stored = await db.BranchPaymentGateways.SingleAsync(g => g.BranchPaymentGatewayId == id);
        Assert.Null(stored.BranchId);
    }
}
