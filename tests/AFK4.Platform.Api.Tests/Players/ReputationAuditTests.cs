using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Players;

/// <summary>
/// Аудит репутации пишется на сам факт чтения, а не на изменение — утечка здесь происходит через
/// чтение. Именно этим потом доказывают, что клуб не изучал чужую клиентуру.
/// </summary>
public sealed class ReputationAuditTests
{
    [Fact]
    public async Task SuccessfulRequest_IsRecordedWithWhoAskedAboutWhom()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var personId = await ReputationTestData.AddPersonAsync(factory, "+992900000201");
        await ReputationTestData.AddAccountAsync(factory, TestIds.OrganizationId, TestIds.BranchId, personId);

        var response = await client.GetAsync(ReputationTestData.ReputationRoute(personId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var record = Assert.Single(await ReadAuditAsync(factory));
        Assert.Equal(AuditActionNames.ViewPlayerReputation, record.Action);
        Assert.Equal("platform_person", record.TargetType);
        Assert.Equal(personId.ToString("D"), record.TargetId);
        Assert.Equal(AuditOutcome.Succeeded, record.Outcome);
        Assert.Equal(TestIds.OrganizationId, record.OrganizationId);
        Assert.Equal(TestIds.BranchId, record.BranchId);
        Assert.Equal(TestIds.TechnicianStaffUserId, record.ActorStaffUserId);
    }

    [Fact]
    public async Task RefusedRequest_IsRecordedToo()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var response = await client.GetAsync(ReputationTestData.ReputationRoute(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var record = Assert.Single(await ReadAuditAsync(factory));
        Assert.Equal(AuditActionNames.ViewPlayerReputation, record.Action);
        Assert.Equal(AuditOutcome.Denied, record.Outcome);
    }

    [Fact]
    public async Task RefusedByPermission_IsRecordedToo()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        var response = await client.GetAsync(ReputationTestData.ReputationRoute(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var record = Assert.Single(await ReadAuditAsync(factory));
        Assert.Equal(AuditOutcome.Denied, record.Outcome);
    }

    /// <summary>
    /// Свой аудит клуб читает сам. Значит запись о спросе по номеру не имеет права выдавать то,
    /// что скрыл ответ: если у знакомого сети номера в аудите стоит идентификатор личности, а у
    /// незнакомого пусто, клуб перебирает номера по собственному журналу.
    /// </summary>
    [Fact]
    public async Task LookupAudit_LooksTheSameForKnownAndUnknownNumbers()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var personId = await ReputationTestData.AddPersonAsync(factory, "+992900000202");
        await ReputationTestData.AddSnapshotAsync(factory, personId, visits: 11, noShows: 3);

        await LookupAsync(client, "+992900000202");
        await LookupAsync(client, "+992900000909");

        var records = await ReadAuditAsync(factory);
        Assert.Equal(2, records.Count);
        Assert.All(records, record =>
        {
            Assert.Equal(AuditActionNames.ViewPlayerReputation, record.Action);
            Assert.Equal("platform_person", record.TargetType);
            Assert.Equal(AuditOutcome.Succeeded, record.Outcome);
            // Идентификатор личности в записи о спросе по номеру не хранится намеренно: он и есть
            // та разница, по которой знакомый номер отличался бы от незнакомого.
            Assert.Null(record.TargetId);
        });

        var shapes = records
            .Select(record => JsonShape(record.DetailsJson))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.Single(shapes);
    }

    /// <summary>Спрос по номеру записывает сам номер: без него нельзя доказать, что перебора не было.</summary>
    [Fact]
    public async Task LookupAudit_KeepsTheNumberThatWasAsked()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        await LookupAsync(client, "+992 90 000-02-03");

        var record = Assert.Single(await ReadAuditAsync(factory));
        using var details = System.Text.Json.JsonDocument.Parse(record.DetailsJson);
        Assert.Equal("+992900000203", details.RootElement.GetProperty("Phone").GetString());
    }

    private static Task<HttpResponseMessage> LookupAsync(HttpClient client, string phone) =>
        client.PostAsJsonAsync(ReputationTestData.LookupRoute(), new PlayerReputationLookupRequest(phone));

    private static async Task<List<AuditRecordEntity>> ReadAuditAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.AuditRecords
            .AsNoTracking()
            .Where(record => record.Action == AuditActionNames.ViewPlayerReputation)
            .OrderBy(record => record.CreatedAtUtc)
            .ToListAsync();
    }

    /// <summary>Только имена полей: значения (сам номер) отличаться обязаны, форма — нет.</summary>
    private static string JsonShape(string detailsJson)
    {
        using var document = System.Text.Json.JsonDocument.Parse(detailsJson);
        return string.Join(
            ',',
            document.RootElement.EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal));
    }
}
