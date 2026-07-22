using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Security;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class DcTopUpEndpointsTests
{
    [Fact]
    public async Task Create_BuildsPayLink_AndPendingIntent()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var (branchId, playerId) = await DcTestSetup.SeedActiveConfigAndPlayerAsync(client, factory);

        var resp = await client.PostAsJsonAsync($"/api/branches/{branchId}/pos/dc-topups",
            new CreateDcTopUpRequest(playerId, 5000, "TJS"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<DcTopUpDto>();
        Assert.StartsWith("http://pay.dc.tj/?A=", dto!.PayUrl);
        Assert.Contains("&s=50.00&", dto.PayUrl);
        Assert.Equal(5000, dto.AmountMinorUnits);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent = await db.PaymentIntents.AsNoTracking().SingleAsync(i => i.PaymentIntentId == dto.IntentId);
        Assert.Equal("dc", intent.Method);
        Assert.Equal("pending", intent.State);
        Assert.Equal(dto.PayUrl, intent.GatewayPayUrl);
    }

    [Fact]
    public async Task Create_NoActiveConfig_Returns409()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var (branchId, playerId) = await DcTestSetup.SeedPlayerNoConfigAsync(client, factory);
        var resp = await client.PostAsJsonAsync($"/api/branches/{branchId}/pos/dc-topups",
            new CreateDcTopUpRequest(playerId, 5000, "TJS"));
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Confirm_ViaFulfil_CreditsWalletOnce()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var (branchId, playerId) = await DcTestSetup.SeedActiveConfigAndPlayerAsync(client, factory);
        var create = await (await client.PostAsJsonAsync($"/api/branches/{branchId}/pos/dc-topups",
            new CreateDcTopUpRequest(playerId, 5000, "TJS"))).Content.ReadFromJsonAsync<DcTopUpDto>();

        var f1 = await client.PostAsync($"/api/wallet/top-up-intents/{create!.IntentId}/fulfil", null);
        var f2 = await client.PostAsync($"/api/wallet/top-up-intents/{create.IntentId}/fulfil", null);
        Assert.Equal(HttpStatusCode.OK, f1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, f2.StatusCode); // идемпотентно

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var balance = await WalletTestHelper.GetBalanceMinorAsync(db, playerId);
        Assert.Equal(5000, balance); // зачислено ровно раз
    }

    [Fact]
    public async Task Cancel_PendingIntent_SetsCancelled()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var (branchId, playerId) = await DcTestSetup.SeedActiveConfigAndPlayerAsync(client, factory);
        var create = await (await client.PostAsJsonAsync($"/api/branches/{branchId}/pos/dc-topups",
            new CreateDcTopUpRequest(playerId, 5000, "TJS"))).Content.ReadFromJsonAsync<DcTopUpDto>();

        var cancel = await client.PostAsync($"/api/branches/{branchId}/pos/dc-topups/{create!.IntentId}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent = await db.PaymentIntents.AsNoTracking().SingleAsync(i => i.PaymentIntentId == create.IntentId);
        Assert.Equal("cancelled", intent.State);
    }
}

// Thin seeding helpers over the repo's real primitives (StaffAuthTestHelper/TestIds/ISecretProtector),
// same ones EskhataTopUpIntentTests and DcConfigEndpointsTests already use. TestIds.BranchId/OrganizationId
// come from StaffAuthTestHelper.AuthorizeAsAsync, which also creates the Organization/Branch rows.
internal static class DcTestSetup
{
    public static async Task<(Guid BranchId, Guid PlayerId)> SeedActiveConfigAndPlayerAsync(
        HttpClient client, PlatformApiFactory factory)
    {
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        var playerId = await SeedPlayerAsync(factory);
        await SeedConfigAsync(factory, isActive: true);
        // Fulfil credits through TopUpWalletAsync, which requires an open shift on the branch.
        await SeedOpenShiftAsync(factory);
        return (TestIds.BranchId, playerId);
    }

    public static async Task<(Guid BranchId, Guid PlayerId)> SeedPlayerNoConfigAsync(
        HttpClient client, PlatformApiFactory factory)
    {
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        var playerId = await SeedPlayerAsync(factory);
        return (TestIds.BranchId, playerId);
    }

    private static async Task<Guid> SeedPlayerAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var playerId = Guid.NewGuid();
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Test Player",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return playerId;
    }

    private static async Task SeedOpenShiftAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            OpenedByStaffUserId = TestIds.TechnicianStaffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50000,
            CountedCashMinorUnits = 0,
            ExpectedCashMinorUnits = 0,
            DifferenceMinorUnits = 0,
            OpeningNote = "test shift",
            ClosingNote = string.Empty,
            OpenedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedConfigAsync(PlatformApiFactory factory, bool isActive)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        db.DcPayLinkConfigs.Add(new DcPayLinkConfigEntity
        {
            DcPayLinkConfigId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = null,
            ReceivingCardEncrypted = protector.Protect("1234567890123456"),
            CardLast4 = "3456",
            CommentTemplate = "AFK4-{ref}",
            IsActive = isActive,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }
}

internal static class WalletTestHelper
{
    public static async Task<long> GetBalanceMinorAsync(PlatformDbContext db, Guid playerAccountId)
    {
        var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(db, playerAccountId, CancellationToken.None);
        return summary!.WalletBalance.MinorUnits;
    }
}
