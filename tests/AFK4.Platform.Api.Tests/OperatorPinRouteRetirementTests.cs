using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Клубный админ больше не задаёт PIN. Раньше это был клубный пароль, теперь — сетевой: разрешить
/// админу одного клуба назначить его значило бы выдать ему вход от чужого имени во все остальные
/// клубы сети. Маршрут отвечает отказом всегда — не «если PIN уже задан», а всегда, — и каждую
/// попытку записывает в аудит: установленные в поле версии Organization Admin кнопку сохранят.
/// </summary>
public sealed class OperatorPinRouteRetirementTests
{
    [Fact]
    public async Task SetPin_IsRefusedAlways_WithADeprecationHeader()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var playerAccountId = await SeedPlayerAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/players/{playerAccountId:D}/pin",
            new SetPlayerPinRequest("1234"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("pin_owned_by_player", await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.Contains("Deprecation"));
    }

    [Fact]
    public async Task SetPin_StoresNoCredential_AndWritesAnAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var playerAccountId = await SeedPlayerAsync(factory);

        await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/players/{playerAccountId:D}/pin",
            new SetPlayerPinRequest("1234"));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await db.PlayerCredentials.ToListAsync());

        var audit = await db.AuditRecords.SingleAsync(record => record.Action == AuditActionNames.SetPlayerPin);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
        Assert.Equal(playerAccountId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task SetPin_WithoutStaffToken_IsStillUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/players/{Guid.NewGuid():D}/pin",
            new SetPlayerPinRequest("1234"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<Guid> SeedPlayerAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var playerAccountId = Guid.NewGuid();
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Фаррух",
            PhoneNumber = "+992900000701",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.Parse("2026-08-19T09:00:00Z")
        });
        await db.SaveChangesAsync();
        return playerAccountId;
    }
}
