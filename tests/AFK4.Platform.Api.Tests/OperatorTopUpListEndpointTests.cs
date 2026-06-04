using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class OperatorTopUpListEndpointTests
{
    [Fact]
    public async Task PendingList_ReturnsPendingIntentsWithPlayerName_ForBranch()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, "cashier_operator");

        var playerId = Guid.NewGuid();
        var intentId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.PlayerAccounts.Add(new PlayerAccountEntity
            {
                PlayerAccountId = playerId,
                OrganizationId = TestIds.OrganizationId,
                HomeBranchId = TestIds.BranchId,
                DisplayName = "Alisher",
                PhoneNumber = "+992900000123",
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            db.PaymentIntents.Add(new PaymentIntentEntity
            {
                PaymentIntentId = intentId,
                PlayerAccountId = playerId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                AmountMinorUnits = 5_000,
                CurrencyCode = "TJS",
                Purpose = "wallet_topup",
                State = "pending",
                Method = "counter",
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync(
            $"/api/branches/{TestIds.BranchId}/wallet/top-up-intents?status=pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<OperatorTopUpIntentDto>>();
        Assert.NotNull(list);
        var item = Assert.Single(list!);
        Assert.Equal(intentId, item.PaymentIntentId);
        Assert.Equal("Alisher", item.DisplayName);
        Assert.Equal(5_000, item.AmountMinorUnits);
        Assert.Equal("pending", item.State);
    }
}
