using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

/// <summary>
/// Кто ставит сетевой запрет и чем это отличается от клубного решения.
///
/// Запрет по всей сети — рычаг платформы, а не клуба: клуб закрывает человеку свою карточку и
/// этим его решение и заканчивается. Обратное — клуб, закрывающий человеку вход к конкурентам —
/// и было бы той самой утечкой контроля, ради которой личность отделена от клубного счёта.
/// </summary>
public sealed class NetworkBanAdminTests
{
    private const string Phone = "+992900000801";

    [Fact]
    public async Task Ban_ClosesTheNetworkDoorAndSaysWhy()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var personId = await AddPersonAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/people/{personId}/network-ban",
            new SetNetworkBanRequest("Подобрал чужой кошелёк"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var person = await ReadPersonAsync(factory, personId);
        Assert.NotNull(person.NetworkBanAtUtc);
        Assert.Equal("Подобрал чужой кошелёк", person.NetworkBanReason);
    }

    /// <summary>
    /// Запрет без причины — это запрет, о котором через месяц никто не скажет, за что он.
    /// Снимать такой некому и не на каком основании.
    /// </summary>
    [Fact]
    public async Task Ban_WithoutAReason_IsRefused()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var personId = await AddPersonAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/people/{personId}/network-ban", new SetNetworkBanRequest("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var person = await ReadPersonAsync(factory, personId);
        Assert.Null(person.NetworkBanAtUtc);
    }

    [Fact]
    public async Task Lift_OpensTheDoorBackAndForgetsTheReason()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var personId = await AddPersonAsync(factory);
        await client.PostAsJsonAsync(
            $"/api/platform/people/{personId}/network-ban", new SetNetworkBanRequest("Ошибка"));

        var response = await client.DeleteAsync($"/api/platform/people/{personId}/network-ban");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var person = await ReadPersonAsync(factory, personId);
        Assert.Null(person.NetworkBanAtUtc);
        Assert.Null(person.NetworkBanReason);
    }

    /// <summary>
    /// Повторный запрет не переписывает дату: «под запретом с 20 августа» — это факт, а не
    /// последнее нажатие кнопки.
    /// </summary>
    [Fact]
    public async Task BanningTwice_KeepsTheFirstDate()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var personId = await AddPersonAsync(factory);

        await client.PostAsJsonAsync(
            $"/api/platform/people/{personId}/network-ban", new SetNetworkBanRequest("Первая причина"));
        var first = (await ReadPersonAsync(factory, personId)).NetworkBanAtUtc;
        await client.PostAsJsonAsync(
            $"/api/platform/people/{personId}/network-ban", new SetNetworkBanRequest("Вторая причина"));

        var person = await ReadPersonAsync(factory, personId);
        Assert.Equal(first, person.NetworkBanAtUtc);
        Assert.Equal("Первая причина", person.NetworkBanReason);
    }

    /// <summary>
    /// Поддержка не закрывает людям вход в сеть — по той же причине, по которой она не видит
    /// репутацию: чужая клиентура не предмет обращения в поддержку.
    /// </summary>
    [Fact]
    public async Task Support_DoesNotBan()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(
            factory, client, userName: "support@platform.test",
            roles: [PlatformAdminRoleNames.PlatformSupport]);
        var personId = await AddPersonAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/people/{personId}/network-ban", new SetNetworkBanRequest("Причина"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null((await ReadPersonAsync(factory, personId)).NetworkBanAtUtc);
    }

    [Fact]
    public async Task Lookup_ByExactPhone_FindsThePersonAndHisBan()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var personId = await AddPersonAsync(factory);
        await client.PostAsJsonAsync(
            $"/api/platform/people/{personId}/network-ban", new SetNetworkBanRequest("Подобрал чужой кошелёк"));

        var response = await client.PostAsJsonAsync(
            "/api/platform/people/lookup", new NetworkPersonLookupRequest("+992 90 000-08-01"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var person = await response.Content.ReadFromJsonAsync<NetworkPersonDto>();
        Assert.Equal(personId, person!.PlatformPersonId);
        Assert.Equal(Phone, person.PhoneNumber);
        Assert.NotNull(person.NetworkBanAtUtc);
        Assert.Equal("Подобрал чужой кошелёк", person.NetworkBanReason);
    }

    /// <summary>
    /// Обрывок номера — не поиск. Иначе панель платформы становится способом листать людей
    /// сети, а список людей — это то, чего в ней быть не должно.
    /// </summary>
    [Fact]
    public async Task Lookup_ByPartOfANumber_IsNotASearch()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        await AddPersonAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/platform/people/lookup", new NetworkPersonLookupRequest("9000008"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Lookup_OfAnUnknownNumber_FindsNothing()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            "/api/platform/people/lookup", new NetworkPersonLookupRequest("+992900000999"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BanAndLift_AreWrittenIntoTheAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var personId = await AddPersonAsync(factory);

        await client.PostAsJsonAsync(
            $"/api/platform/people/{personId}/network-ban", new SetNetworkBanRequest("Подобрал чужой кошелёк"));
        await client.DeleteAsync($"/api/platform/people/{personId}/network-ban");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var actions = await db.AuditRecords
            .Where(record => record.TargetId == personId.ToString("D"))
            .Select(record => record.Action)
            .ToListAsync();

        Assert.Contains(AuditActionNames.SetNetworkBan, actions);
        Assert.Contains(AuditActionNames.LiftNetworkBan, actions);
    }

    private static async Task<Guid> AddPersonAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var now = DateTimeOffset.Parse("2026-08-20T09:00:00Z");
        var person = new PlatformPersonEntity
        {
            PlatformPersonId = Guid.NewGuid(),
            PhoneNumber = Phone,
            DisplayName = "Фаррух",
            PhoneVerifiedAtUtc = now,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.PlatformPersons.Add(person);
        await db.SaveChangesAsync();
        return person.PlatformPersonId;
    }

    private static async Task<PlatformPersonEntity> ReadPersonAsync(PlatformApiFactory factory, Guid personId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.PlatformPersons.SingleAsync(person => person.PlatformPersonId == personId);
    }
}
