using System.Reflection;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Support;
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

    // Маршруты, которые меняют роли сотрудника или его пароль. Инвариант: под режимом поддержки
    // не должен быть помечен НИ ОДИН из них — назначение роли BranchManager (или сброс пароля
    // тому, у кого она уже есть) даёт постоянный доступ к деньгам клуба, который не отзывается
    // вместе с истечением гранта поддержки. Это должно оставаться так, даже если
    // PlatformSupportWritableAreas.Staff по-прежнему открывает профиль/состояние сотрудника —
    // роли и пароль внутри той же области намеренно исключены.
    [Fact]
    public void PlatformSupportAllowlist_NeverGrantsRoleOrPasswordChanges()
    {
        using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();

        var offenders = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<PlatformSupportAccessMetadata>() is not null)
            .Where(endpoint => endpoint.Metadata.GetMetadata<AuthenticationDomainMetadata>()?.Domain
                == AuthenticationDomain.Organization)
            .Where(endpoint =>
            {
                var route = endpoint.RoutePattern.RawText!;
                return route.EndsWith("/roles", StringComparison.OrdinalIgnoreCase)
                    || route.EndsWith("/password-reset", StringComparison.OrdinalIgnoreCase);
            })
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Режим поддержки не должен доставать до смены ролей сотрудника или сброса его пароля "
                + "(эскалация до денежных прав в обход срока гранта), а помечены ещё и эти "
                + $"маршруты: {string.Join(", ", offenders)}");
    }

    // Зеркало PlatformSupportWritableAreas.cs (сервер) на уровне маршрутов: каждая объявленная
    // область должна отвечать хотя бы одному помеченному не-GET маршруту, и наоборот — у каждого
    // помеченного не-GET маршрута клуба должна быть ровно одна область-владелец. Так рассинхрон
    // между списком областей и реальной разметкой эндпоинтов виден сразу, а не через комментарий
    // «грепни метки», который никто не обязан перечитывать. Клиентская карта областей
    // (supportWorkspaces.ts) не проверяется отсюда — сверка разных языков одним тестом невозможна,
    // но именно этот тест — источник истины, на который она ссылается.
    private static readonly Dictionary<string, string[]> AreaRoutePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        [PlatformSupportWritableAreas.BranchSettings] =
        [
            "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/settings"
        ],
        [PlatformSupportWritableAreas.Devices] =
        [
            "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/devices",
            "/api/organizations/{organizationId:guid}/devices",
            "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/device-enrollment-codes"
        ],
        [PlatformSupportWritableAreas.Staff] =
        [
            "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/staff"
        ],
        [PlatformSupportWritableAreas.FloorMap] =
        [
            "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/floor-map",
            "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/layout"
        ],
        [PlatformSupportWritableAreas.BranchProfile] =
        [
            "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/profile"
        ]
    };

    [Fact]
    public void PlatformSupportAllowlist_MatchesDeclaredAreas()
    {
        Assert.True(
            AreaRoutePrefixes.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(PlatformSupportWritableAreas.All),
            "AreaRoutePrefixes в тесте должен объявлять ровно те же области, что и "
                + "PlatformSupportWritableAreas.All — добавили/убрали область на сервере, "
                + "но забыли завести/снять для неё маршруты в этом тесте.");

        using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();

        var writableEndpoints = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<PlatformSupportAccessMetadata>() is not null)
            .Where(endpoint => endpoint.Metadata.GetMetadata<AuthenticationDomainMetadata>()?.Domain
                == AuthenticationDomain.Organization)
            .Where(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>() is { } methods
                && !methods.HttpMethods.Contains(HttpMethods.Get))
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .ToList();

        var unmatched = writableEndpoints
            .Where(route => !AreaRoutePrefixes.Values.Any(prefixes =>
                prefixes.Any(prefix => route.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        Assert.True(
            unmatched.Count == 0,
            "Каждый помеченный не-GET маршрут клуба должен относиться к одной из областей "
                + $"PlatformSupportWritableAreas — эти не относятся ни к одной: {string.Join(", ", unmatched)}");

        var emptyAreas = AreaRoutePrefixes
            .Where(area => !writableEndpoints.Any(route => area.Value.Any(prefix =>
                route.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
            .Select(area => area.Key)
            .ToList();

        Assert.True(
            emptyAreas.Count == 0,
            "Каждая область из PlatformSupportWritableAreas.All должна отвечать хотя бы одному "
                + "помеченному не-GET маршруту — для этих областей такого маршрута не нашлось "
                + $"(область объявлена, но ничего не помечено): {string.Join(", ", emptyAreas)}");
    }

    // Денежные файлы эндпоинтов — поимённо, ровно из спеки. Проверяем принадлежность обработчика
    // классу, а не подстроку в пути: путь может случайно содержать слово из имени денежного файла
    // (например "отчёт по сменам" reports/shifts vs ShiftEndpoints), а класс — нет.
    private static readonly string[] MoneyEndpointClassNames =
    [
        "PosEndpoints", "WalletEndpoints", "MoneyActionEndpoints", "ShiftEndpoints",
        "PackageEndpoints", "DcTopUpEndpoints", "EskhataPaymentEndpoints", "ShopOrderEndpoints",
        "PlayerLoyaltyEndpoints", "SessionEndpoints", "ReservationEndpoints", "TariffEndpoints"
    ];

    [Fact]
    public void PlatformSupportAllowlist_NeverCoversMoneyEndpoints()
    {
        using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();

        var moneyClassPrefixes = MoneyEndpointClassNames
            .Select(className => $"AFK4.Platform.Api.Endpoints.{className}")
            .ToArray();

        var offenders = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<PlatformSupportAccessMetadata>() is not null)
            .Where(endpoint => endpoint.Metadata.GetMetadata<AuthenticationDomainMetadata>()?.Domain
                == AuthenticationDomain.Organization)
            .Where(endpoint =>
            {
                var declaringTypeName = endpoint.Metadata.GetMetadata<MethodInfo>()?.DeclaringType?.FullName;
                return declaringTypeName is not null
                    && moneyClassPrefixes.Any(prefix =>
                        declaringTypeName.StartsWith(prefix, StringComparison.Ordinal));
            })
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"Денежные эндпоинты не открываются поддержке ни на чтение, ни на запись: {string.Join(", ", offenders)}");
    }

    // Маршруты вне домена Organization, которым намеренно разрешено носить
    // PlatformSupportAccessMetadata. Сюда попадают только эндпоинты управления самой сессией
    // поддержки (она живёт вне домена организации). Пополнение этого списка — осознанное решение
    // по конкретному маршруту, а не способ погасить упавший тест.
    private static readonly HashSet<string> ExpectedNonOrganizationSupportRoutes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Управление самой сессией поддержки: ей ещё не/уже не назначен клуб, поэтому эти
            // маршруты не могут жить под /api/organizations/{organizationId:guid}.
            "/api/support-access/session"
        };

    [Fact]
    public void PlatformSupportAccessMetadata_OnlyOnOrganizationDomainOrExplicitException()
    {
        using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();

        var offenders = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<PlatformSupportAccessMetadata>() is not null)
            .Where(endpoint => endpoint.Metadata.GetMetadata<AuthenticationDomainMetadata>()?.Domain
                != AuthenticationDomain.Organization)
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .Where(route => !ExpectedNonOrganizationSupportRoutes.Contains(route))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "PlatformSupportAccessMetadata разрешена только на эндпоинтах домена Organization "
                + "или в явном списке исключений (ExpectedNonOrganizationSupportRoutes), а "
                + $"помечены ещё и эти вне домена: {string.Join(", ", offenders)}");
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
