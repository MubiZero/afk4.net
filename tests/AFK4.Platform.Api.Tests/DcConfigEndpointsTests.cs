using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class DcConfigEndpointsTests
{
    [Fact]
    public async Task Post_ThenGet_StoresCardEncrypted_ReturnsLast4Only()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var post = await owner.PostAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/payments/dc-config",
            new UpdateDcPayLinkConfigRequest("1234567890123456", "AFK4-{ref}", true));
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var dto = await post.Content.ReadFromJsonAsync<DcPayLinkConfigDto>();
        Assert.True(dto!.CardSet);
        Assert.Equal("3456", dto.CardLast4);
        Assert.True(dto.IsActive);

        var get = await owner.GetFromJsonAsync<DcPayLinkConfigDto>($"/api/organizations/{TestIds.OrganizationId:D}/payments/dc-config");
        Assert.Equal("3456", get!.CardLast4);
        Assert.True(get.CardSet);

        // PAN зашифрован в БД, не хранится в открытом виде.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = await db.DcPayLinkConfigs.AsNoTracking().SingleAsync();
        Assert.DoesNotContain("1234567890123456", row.ReceivingCardEncrypted);
    }

    [Fact]
    public async Task Post_EmptyCardOnFirstSave_Fails()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var post = await owner.PostAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/payments/dc-config",
            new UpdateDcPayLinkConfigRequest(null, "AFK4-{ref}", true));
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
    }
}
