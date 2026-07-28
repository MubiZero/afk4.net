using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Configuration;
using AFK4.Platform.Api.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests;

public sealed class OrganizationAdminCompatibilityTests
{
    private static readonly Guid OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Theory]
    [InlineData(null, "organization-admin")]
    [InlineData("1", "organization-admin")]
    [InlineData("broken", "organization-admin")]
    [InlineData("2", "operator-app")]
    public async Task OrganizationRoute_WithIncompatibleIdentity_RequiresUpgrade(string? epoch, string product)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        ClearIdentity(client);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/organizations/{OrganizationId:D}/branches");
        request.Headers.TryAddWithoutValidation(OrganizationAdminCompatibilityMiddleware.ProductHeader, product);
        request.Headers.TryAddWithoutValidation(OrganizationAdminCompatibilityMiddleware.VersionHeader, "2.0.0");
        if (epoch is not null)
        {
            request.Headers.TryAddWithoutValidation(OrganizationAdminCompatibilityMiddleware.EpochHeader, epoch);
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UpgradeResponse>();
        Assert.Equal("organization_admin_upgrade_required", body?.Code);
        Assert.Equal(2, body?.RequiredEpoch);
    }

    [Fact]
    public async Task OrganizationRoute_WithCurrentIdentity_ReachesNormalAuthorization()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        ClearIdentity(client);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/organizations/{OrganizationId:D}/branches");
        AddCurrentIdentity(request);

        var response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.UpgradeRequired, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(AuthenticationDomain.Platform)]
    [InlineData(null)]
    public async Task NonOrganizationEndpoint_DoesNotRequireClientIdentity(AuthenticationDomain? domain)
    {
        var called = false;
        var middleware = new OrganizationAdminCompatibilityMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        var metadata = domain is null
            ? new EndpointMetadataCollection()
            : new EndpointMetadataCollection(new AuthenticationDomainMetadata(domain.Value));
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "unrelated"));

        await middleware.InvokeAsync(context, Options.Create(new OrganizationAdminCompatibilityOptions()));

        Assert.True(called);
    }

    private static void AddCurrentIdentity(HttpRequestMessage request)
    {
        request.Headers.Add(OrganizationAdminCompatibilityMiddleware.ProductHeader, "organization-admin");
        request.Headers.Add(OrganizationAdminCompatibilityMiddleware.EpochHeader, "2");
        request.Headers.Add(OrganizationAdminCompatibilityMiddleware.VersionHeader, "2.0.0");
    }

    private static void ClearIdentity(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove(OrganizationAdminCompatibilityMiddleware.ProductHeader);
        client.DefaultRequestHeaders.Remove(OrganizationAdminCompatibilityMiddleware.EpochHeader);
        client.DefaultRequestHeaders.Remove(OrganizationAdminCompatibilityMiddleware.VersionHeader);
    }

    private sealed record UpgradeResponse(string Code, int RequiredEpoch, string DownloadUrl);
}
