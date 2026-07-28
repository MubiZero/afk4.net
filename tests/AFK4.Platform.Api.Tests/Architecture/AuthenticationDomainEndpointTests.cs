using System.Reflection;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Architecture;

public sealed class AuthenticationDomainEndpointTests
{
    [Fact]
    public async Task EveryOrganizationEndpoint_UsesCanonicalOrganizationPrefix()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<AuthenticationDomainMetadata>()?.Domain
                == AuthenticationDomain.Organization)
            .ToArray();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint =>
            Assert.StartsWith(
                "/api/organizations/{organizationId:guid}",
                endpoint.RoutePattern.RawText,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlatformSupportAllowlist_ContainsOnlyReadOnlyOrganizationEndpoints()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<PlatformSupportAccessMetadata>() is not null)
            .ToArray();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint =>
        {
            Assert.Equal(AuthenticationDomain.Organization,
                endpoint.Metadata.GetMetadata<AuthenticationDomainMetadata>()?.Domain);
            Assert.Contains("GET", endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
            Assert.StartsWith("/api/", endpoint.RoutePattern.RawText);
        });
    }

    [Fact]
    public void ApiAssembly_ExposesExplicitAuthenticationDomainContract()
    {
        var apiAssembly = typeof(Program).Assembly;

        var domainType = apiAssembly.GetType("AFK4.Platform.Api.Identity.AuthenticationDomain");
        var metadataType = apiAssembly.GetType("AFK4.Platform.Api.Identity.AuthenticationDomainMetadata");

        Assert.NotNull(domainType);
        Assert.True(domainType.IsEnum);
        Assert.Equal(["Platform", "Organization"], Enum.GetNames(domainType));
        Assert.NotNull(metadataType);
        Assert.NotNull(metadataType.GetProperty("Domain", BindingFlags.Public | BindingFlags.Instance));
    }

    [Theory]
    [InlineData(AuthenticationDomain.Organization)]
    [InlineData(AuthenticationDomain.Platform)]
    public async Task DomainEnforcement_RejectsTokenFromOppositeDomain(AuthenticationDomain endpointDomain)
    {
        var nextWasCalled = false;
        var middleware = new AuthenticationDomainEnforcementMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthenticationDomainMetadata(endpointDomain)),
            "secured"));
        var staff = new StaffContextAccessor();
        var platform = new PlatformAdminContextAccessor();

        if (endpointDomain == AuthenticationDomain.Organization)
        {
            platform.Current = new PlatformAdminContext(
                Guid.NewGuid(), "platform", "Platform", EmptyStrings(), EmptyStrings());
        }
        else
        {
            staff.Current = new StaffContext(
                Guid.NewGuid(), Guid.NewGuid(), "Organization", EmptyGuids(), EmptyStrings());
        }

        await middleware.InvokeAsync(httpContext, staff, platform);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.False(nextWasCalled);
    }

    [Theory]
    [InlineData(AuthenticationDomain.Organization)]
    [InlineData(AuthenticationDomain.Platform)]
    public async Task DomainEnforcement_AllowsMatchingTokenDomain(AuthenticationDomain endpointDomain)
    {
        var nextWasCalled = false;
        var middleware = new AuthenticationDomainEnforcementMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthenticationDomainMetadata(endpointDomain)),
            "secured"));
        var staff = new StaffContextAccessor();
        var platform = new PlatformAdminContextAccessor();

        if (endpointDomain == AuthenticationDomain.Organization)
        {
            staff.Current = new StaffContext(
                Guid.NewGuid(), Guid.NewGuid(), "Organization", EmptyGuids(), EmptyStrings());
        }
        else
        {
            platform.Current = new PlatformAdminContext(
                Guid.NewGuid(), "platform", "Platform", EmptyStrings(), EmptyStrings());
        }

        await middleware.InvokeAsync(httpContext, staff, platform);

        Assert.True(nextWasCalled);
    }

    [Fact]
    public async Task DomainEnforcement_RejectsStaffTokenForAnotherRouteOrganization()
    {
        var authenticatedOrganizationId = Guid.NewGuid();
        var nextWasCalled = false;
        var middleware = new AuthenticationDomainEnforcementMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["organizationId"] = Guid.NewGuid().ToString("D");
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new AuthenticationDomainMetadata(AuthenticationDomain.Organization)),
            "organization secured"));
        var staff = new StaffContextAccessor
        {
            Current = new StaffContext(
                Guid.NewGuid(),
                authenticatedOrganizationId,
                "Organization",
                EmptyGuids(),
                EmptyStrings())
        };

        await middleware.InvokeAsync(httpContext, staff, new PlatformAdminContextAccessor());

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.False(nextWasCalled);
    }

    private static IReadOnlySet<Guid> EmptyGuids() => new HashSet<Guid>();

    private static IReadOnlySet<string> EmptyStrings() => new HashSet<string>();
}
