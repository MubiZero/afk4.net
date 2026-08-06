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

    // Запись под режимом поддержки разрешена ровно в этих областях. Список намеренно
    // дублирует спеку: если кто-то пометит денежный эндпоинт «за компанию», тест назовёт его
    // поимённо, а не промолчит.
    private static readonly HashSet<string> WritableRoutePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/settings",
        "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/floor-map",
        "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/profile",
        "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/layout",
        "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/staff",
        "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/devices",
        "/api/organizations/{organizationId:guid}/devices",
        "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/device-enrollment-codes"
    };

    [Fact]
    public void PlatformSupportAllowlist_AllowsWritesOnlyInDeclaredAreas()
    {
        using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();

        var offenders = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<PlatformSupportAccessMetadata>() is not null)
            // Только эндпоинты клуба: управление самой сессией поддержки живёт вне домена
            // организации и белым списком областей клуба не описывается.
            .Where(endpoint => endpoint.Metadata.GetMetadata<AuthenticationDomainMetadata>()?.Domain
                == AuthenticationDomain.Organization)
            .Where(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>() is { } methods
                && !methods.HttpMethods.Contains(HttpMethods.Get))
            .Where(endpoint => !WritableRoutePrefixes.Any(prefix =>
                endpoint.RoutePattern.RawText!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Запись под режимом поддержки разрешена только в объявленных областях, "
                + $"а помечены ещё и эти: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void PlatformSupportAllowlist_NeverCoversMoneyEndpoints()
    {
        using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();

        string[] forbidden =
        [
            "/pos/", "/wallet/", "/money-actions", "/shifts", "/packages",
            "/dc-topups", "/payments/eskhata", "/shop/orders", "/loyalty",
            "/sessions", "/reservations", "/tariffs"
        ];

        var offenders = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<PlatformSupportAccessMetadata>() is not null)
            .Where(endpoint => endpoint.Metadata.GetMetadata<AuthenticationDomainMetadata>()?.Domain
                == AuthenticationDomain.Organization)
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .Where(route => forbidden.Any(fragment => route.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"Денежные эндпоинты не открываются поддержке ни на чтение, ни на запись: {string.Join(", ", offenders)}");
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
