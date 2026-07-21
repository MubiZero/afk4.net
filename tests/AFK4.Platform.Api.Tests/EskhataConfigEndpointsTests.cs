using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Payments;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class EskhataConfigEndpointsTests
{
    [Fact]
    public async Task PostThenGet_RoundTripsConfig_AndNeverReturnsHashKey()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var post = await owner.PostAsJsonAsync("/api/owner/eskhata-config",
            new UpdateEskhataMerchantConfigRequest("https://merchant-api.example.tld", "company-demo-001", 17, "example-secret"));
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        var body = await post.Content.ReadAsStringAsync();
        Assert.DoesNotContain("example-secret", body);

        var get = await owner.GetAsync("/api/owner/eskhata-config");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var dto = await get.Content.ReadFromJsonAsync<EskhataMerchantConfigDto>();
        Assert.Equal("https://merchant-api.example.tld", dto!.BaseUrl);
        Assert.Equal("company-demo-001", dto.CompanyId);
        Assert.Equal(17, dto.MerchantId);
        Assert.True(dto.HashKeySet);
        Assert.Equal("configured", dto.Status);
    }

    [Fact]
    public async Task Post_TrailingSlashTrimmed()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        await owner.PostAsJsonAsync("/api/owner/eskhata-config",
            new UpdateEskhataMerchantConfigRequest("https://merchant-api.example.tld/", "company-demo-001", 17, "example-secret"));

        var dto = await (await owner.GetAsync("/api/owner/eskhata-config")).Content.ReadFromJsonAsync<EskhataMerchantConfigDto>();
        Assert.Equal("https://merchant-api.example.tld", dto!.BaseUrl);
    }

    [Fact]
    public async Task Post_EmptyHashKeyOnUpdate_KeepsStoredSecret()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        await owner.PostAsJsonAsync("/api/owner/eskhata-config",
            new UpdateEskhataMerchantConfigRequest("https://merchant-api.example.tld", "company-demo-001", 17, "example-secret"));

        // Update other fields without re-sending the secret.
        var update = await owner.PostAsJsonAsync("/api/owner/eskhata-config",
            new UpdateEskhataMerchantConfigRequest("https://merchant-api.example.tld", "company-demo-002", 18, null));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var dto = await (await owner.GetAsync("/api/owner/eskhata-config")).Content.ReadFromJsonAsync<EskhataMerchantConfigDto>();
        Assert.Equal("company-demo-002", dto!.CompanyId);
        Assert.Equal(18, dto.MerchantId);
        Assert.True(dto.HashKeySet);
    }

    [Fact]
    public async Task Post_RejectsMissingHashKeyOnFirstSave()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var post = await owner.PostAsJsonAsync("/api/owner/eskhata-config",
            new UpdateEskhataMerchantConfigRequest("https://merchant-api.example.tld", "company-demo-001", 17, null));
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
    }

    [Fact]
    public async Task Post_RejectsInvalidBaseUrl()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var post = await owner.PostAsJsonAsync("/api/owner/eskhata-config",
            new UpdateEskhataMerchantConfigRequest("not-a-url", "company-demo-001", 17, "example-secret"));
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
    }

    [Fact]
    public async Task Post_RejectsNonPositiveMerchantId()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var post = await owner.PostAsJsonAsync("/api/owner/eskhata-config",
            new UpdateEskhataMerchantConfigRequest("https://merchant-api.example.tld", "company-demo-001", 0, "example-secret"));
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
    }

    [Fact]
    public async Task Post_ForbiddenForNonOwner()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var nonOwner = await OwnerTestAuth.SignInNonOwnerAsync(factory, client);

        var post = await nonOwner.PostAsJsonAsync("/api/owner/eskhata-config",
            new UpdateEskhataMerchantConfigRequest("https://merchant-api.example.tld", "company-demo-001", 17, "example-secret"));
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
    }

    [Fact]
    public async Task Get_DefaultsToInactiveWhenNoRow()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var get = await owner.GetAsync("/api/owner/eskhata-config");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var dto = await get.Content.ReadFromJsonAsync<EskhataMerchantConfigDto>();
        Assert.Equal(string.Empty, dto!.BaseUrl);
        Assert.Equal(0, dto.MerchantId);
        Assert.False(dto.HashKeySet);
        Assert.Equal("inactive", dto.Status);
    }
}
