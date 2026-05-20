using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.PilotSetup;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Tariffs;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorPilotSetupApiClientTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid StaffUserId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ZoneId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid SeatId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid TariffId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid TariffVersionId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid CategoryId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid ProductId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    private static readonly Guid DeviceId = Guid.Parse("88888888-8888-4888-8888-888888888888");
    private static readonly Guid DeviceSeatAssignmentId = Guid.Parse("99999999-9999-4999-8999-999999999999");

    [Fact]
    public async Task GetStaffUsersAsync_GetsBearerAuthenticatedBranchStaff()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse<IReadOnlyList<StaffUserDto>>([CreateStaffUser()]));
        var client = CreateClient(handler);

        var staffUsers = await client.GetStaffUsersAsync(BranchId, CancellationToken.None);

        Assert.Single(staffUsers);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/staff", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);
    }

    [Fact]
    public async Task CreateStaffUserAsync_PostsStaffUserToBranch()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(CreateStaffUser()));
        var client = CreateClient(handler);
        var request = new CreateStaffUserRequest(
            OrganizationId,
            "cashier.pilot@afk4.test",
            "Pilot Cashier",
            "TemporaryPassword123!",
            ["cashier"]);

        var staffUser = await client.CreateStaffUserAsync(BranchId, request, CancellationToken.None);

        Assert.Equal(StaffUserId, staffUser.StaffUserId);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/staff", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);

        var body = DeserializeRequest<CreateStaffUserRequest>(handler.LastRequestBody);
        Assert.Equal("cashier.pilot@afk4.test", body.UserName);
        Assert.Equal("cashier", body.RoleNames[0]);
    }

    [Fact]
    public async Task LayoutAsync_UsesBranchLayoutRoutesAndBodies()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonResponse<IReadOnlyList<ZoneDto>>([CreateZone()]),
            JsonResponse(CreateZone()),
            JsonResponse(CreateSeat())
        ]);
        var handler = new RecordingHttpMessageHandler(_ => responses.Dequeue());
        var client = CreateClient(handler);

        var zones = await client.GetLayoutZonesAsync(BranchId, CancellationToken.None);
        var zonesPath = handler.LastPathAndQuery;
        var zonesMethod = handler.LastMethod;
        var zonesAuthorization = handler.LastAuthorization;

        var zone = await client.CreateZoneAsync(
            BranchId,
            new CreateZoneRequest(OrganizationId, "Main Hall", 10),
            CancellationToken.None);
        var createZonePath = handler.LastPathAndQuery;
        var createZoneMethod = handler.LastMethod;
        var createZoneBody = DeserializeRequest<CreateZoneRequest>(handler.LastRequestBody);

        var seat = await client.CreateSeatAsync(
            BranchId,
            new CreateSeatRequest(OrganizationId, ZoneId, "PC-001", 1),
            CancellationToken.None);

        Assert.Single(zones);
        Assert.Equal(HttpMethod.Get, zonesMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/layout/zones", zonesPath);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), zonesAuthorization);
        Assert.Equal(ZoneId, zone.ZoneId);
        Assert.Equal(HttpMethod.Post, createZoneMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/layout/zones", createZonePath);
        Assert.Equal("Main Hall", createZoneBody.Name);
        Assert.Equal(SeatId, seat.SeatId);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/layout/seats", handler.LastPathAndQuery);

        var createSeatBody = DeserializeRequest<CreateSeatRequest>(handler.LastRequestBody);
        Assert.Equal(ZoneId, createSeatBody.ZoneId);
        Assert.Equal("PC-001", createSeatBody.Name);
    }

    [Fact]
    public async Task TariffAsync_PostsTariffAndVersionToBranchRoutes()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonResponse(CreateTariff()),
            JsonResponse(CreateTariffVersion())
        ]);
        var handler = new RecordingHttpMessageHandler(_ => responses.Dequeue());
        var client = CreateClient(handler);

        var tariff = await client.CreateTariffAsync(
            BranchId,
            new CreateTariffRequest(OrganizationId, "Standard", "tariff-create-001"),
            CancellationToken.None);
        var tariffPath = handler.LastPathAndQuery;
        var tariffMethod = handler.LastMethod;
        var tariffAuthorization = handler.LastAuthorization;
        var tariffBody = DeserializeRequest<CreateTariffRequest>(handler.LastRequestBody);

        var version = await client.CreateTariffVersionAsync(
            BranchId,
            TariffId,
            new CreateTariffVersionRequest(
                OrganizationId,
                TariffId,
                "USD",
                25,
                10,
                5,
                DateTimeOffset.Parse("2026-05-19T08:00:00Z"),
                "tariff-version-001"),
            CancellationToken.None);

        Assert.Equal(TariffId, tariff.TariffId);
        Assert.Equal(HttpMethod.Post, tariffMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/tariffs", tariffPath);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), tariffAuthorization);
        Assert.Equal("tariff-create-001", tariffBody.IdempotencyKey);
        Assert.Equal(TariffVersionId, version.TariffVersionId);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/tariffs/{TariffId:D}/versions", handler.LastPathAndQuery);

        var versionBody = DeserializeRequest<CreateTariffVersionRequest>(handler.LastRequestBody);
        Assert.Equal("USD", versionBody.CurrencyCode);
        Assert.Equal(25, versionBody.PricePerMinuteMinorUnits);
    }

    [Fact]
    public async Task PosCatalogSetupAsync_PostsCategoryAndProductToBranchRoutes()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonResponse(CreateCategory()),
            JsonResponse(CreateProduct())
        ]);
        var handler = new RecordingHttpMessageHandler(_ => responses.Dequeue());
        var client = CreateClient(handler);

        var category = await client.CreateProductCategoryAsync(
            BranchId,
            new CreateProductCategoryRequest(OrganizationId, "Drinks", "category-create-001"),
            CancellationToken.None);
        var categoryPath = handler.LastPathAndQuery;
        var categoryMethod = handler.LastMethod;
        var categoryAuthorization = handler.LastAuthorization;
        var categoryBody = DeserializeRequest<CreateProductCategoryRequest>(handler.LastRequestBody);

        var product = await client.CreateProductAsync(
            BranchId,
            new CreateProductRequest(
                OrganizationId,
                CategoryId,
                "Cola 0.5",
                "COLA-05",
                new MoneyDto("USD", 1200),
                trackStock: true,
                allowNegativeStock: false,
                "product-create-001"),
            CancellationToken.None);

        Assert.Equal(CategoryId, category.CategoryId);
        Assert.Equal(HttpMethod.Post, categoryMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/pos/categories", categoryPath);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), categoryAuthorization);
        Assert.Equal("category-create-001", categoryBody.IdempotencyKey);
        Assert.Equal(ProductId, product.ProductId);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/pos/products", handler.LastPathAndQuery);

        var productBody = DeserializeRequest<CreateProductRequest>(handler.LastRequestBody);
        Assert.Equal("COLA-05", productBody.Sku);
        Assert.Equal(1200, productBody.Price.MinorUnits);
    }

    [Fact]
    public async Task AssignDeviceSeatAsync_PostsAssignmentToBranchDeviceRoute()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(CreateAssignment()));
        var client = CreateClient(handler);

        var assignment = await client.AssignDeviceSeatAsync(
            BranchId,
            DeviceId,
            new AssignDeviceSeatRequest(OrganizationId, SeatId),
            CancellationToken.None);

        Assert.Equal(DeviceSeatAssignmentId, assignment.DeviceSeatAssignmentId);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/devices/{DeviceId:D}/seat-assignment", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);

        var body = DeserializeRequest<AssignDeviceSeatRequest>(handler.LastRequestBody);
        Assert.Equal(SeatId, body.SeatId);
    }

    [Fact]
    public async Task UnconfiguredClient_ThrowsConfiguredMessageForEveryMethod()
    {
        IOperatorPilotSetupApiClient client = new UnconfiguredOperatorPilotSetupApiClient();
        var staffRequest = new CreateStaffUserRequest(OrganizationId, "user", "User", "password", ["cashier"]);
        var zoneRequest = new CreateZoneRequest(OrganizationId, "Main Hall", 10);
        var seatRequest = new CreateSeatRequest(OrganizationId, ZoneId, "PC-001", 1);
        var tariffRequest = new CreateTariffRequest(OrganizationId, "Standard", "tariff-create-001");
        var versionRequest = new CreateTariffVersionRequest(
            OrganizationId,
            TariffId,
            "USD",
            25,
            10,
            5,
            DateTimeOffset.Parse("2026-05-19T08:00:00Z"),
            "tariff-version-001");
        var categoryRequest = new CreateProductCategoryRequest(OrganizationId, "Drinks", "category-create-001");
        var productRequest = new CreateProductRequest(
            OrganizationId,
            CategoryId,
            "Cola 0.5",
            "COLA-05",
            new MoneyDto("USD", 1200),
            trackStock: true,
            allowNegativeStock: false,
            "product-create-001");
        var assignmentRequest = new AssignDeviceSeatRequest(OrganizationId, SeatId);

        await AssertUnconfiguredAsync(() => client.GetStaffUsersAsync(BranchId, CancellationToken.None));
        await AssertUnconfiguredAsync(() => client.CreateStaffUserAsync(BranchId, staffRequest, CancellationToken.None));
        await AssertUnconfiguredAsync(() => client.GetLayoutZonesAsync(BranchId, CancellationToken.None));
        await AssertUnconfiguredAsync(() => client.CreateZoneAsync(BranchId, zoneRequest, CancellationToken.None));
        await AssertUnconfiguredAsync(() => client.CreateSeatAsync(BranchId, seatRequest, CancellationToken.None));
        await AssertUnconfiguredAsync(() => client.CreateTariffAsync(BranchId, tariffRequest, CancellationToken.None));
        await AssertUnconfiguredAsync(() => client.CreateTariffVersionAsync(BranchId, TariffId, versionRequest, CancellationToken.None));
        await AssertUnconfiguredAsync(() => client.CreateProductCategoryAsync(BranchId, categoryRequest, CancellationToken.None));
        await AssertUnconfiguredAsync(() => client.CreateProductAsync(BranchId, productRequest, CancellationToken.None));
        await AssertUnconfiguredAsync(() => client.AssignDeviceSeatAsync(BranchId, DeviceId, assignmentRequest, CancellationToken.None));
    }

    private static HttpOperatorPilotSetupApiClient CreateClient(RecordingHttpMessageHandler handler)
    {
        return new HttpOperatorPilotSetupApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5074")
        }, new StaticOperatorTokenStore());
    }

    private static StaffUserDto CreateStaffUser()
    {
        return new StaffUserDto(
            StaffUserId,
            OrganizationId,
            "cashier.pilot@afk4.test",
            "Pilot Cashier",
            IsActive: true,
            ["cashier"],
            DateTimeOffset.Parse("2026-05-19T07:00:00Z"));
    }

    private static ZoneDto CreateZone()
    {
        return new ZoneDto(
            ZoneId,
            OrganizationId,
            BranchId,
            "Main Hall",
            10,
            DateTimeOffset.Parse("2026-05-19T07:01:00Z"),
            [CreateSeat()]);
    }

    private static SeatDto CreateSeat()
    {
        return new SeatDto(
            SeatId,
            OrganizationId,
            BranchId,
            ZoneId,
            "PC-001",
            1,
            DateTimeOffset.Parse("2026-05-19T07:02:00Z"));
    }

    private static TariffDto CreateTariff()
    {
        return new TariffDto(
            TariffId,
            OrganizationId,
            BranchId,
            "Standard",
            IsActive: true,
            DateTimeOffset.Parse("2026-05-19T07:03:00Z"));
    }

    private static TariffVersionDto CreateTariffVersion()
    {
        return new TariffVersionDto(
            TariffVersionId,
            TariffId,
            1,
            "USD",
            25,
            10,
            5,
            DateTimeOffset.Parse("2026-05-19T08:00:00Z"),
            RetiredAtUtc: null,
            DateTimeOffset.Parse("2026-05-19T07:04:00Z"));
    }

    private static PosProductCategoryDto CreateCategory()
    {
        return new PosProductCategoryDto(
            CategoryId,
            OrganizationId,
            BranchId,
            "Drinks",
            IsActive: true,
            DateTimeOffset.Parse("2026-05-19T07:05:00Z"));
    }

    private static PosProductDto CreateProduct()
    {
        return new PosProductDto(
            ProductId,
            OrganizationId,
            BranchId,
            CategoryId,
            "Cola 0.5",
            "COLA-05",
            new MoneyDto("USD", 1200),
            TrackStock: true,
            AllowNegativeStock: false,
            IsActive: true,
            StockOnHand: 0,
            DateTimeOffset.Parse("2026-05-19T07:06:00Z"));
    }

    private static DeviceSeatAssignmentDto CreateAssignment()
    {
        return new DeviceSeatAssignmentDto(
            DeviceSeatAssignmentId,
            OrganizationId,
            BranchId,
            SeatId,
            DeviceId,
            DateTimeOffset.Parse("2026-05-19T07:07:00Z"),
            DetachedAtUtc: null);
    }

    private static async Task AssertUnconfiguredAsync(Func<Task> action)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Equal("Operator pilot setup API client is not configured.", exception.Message);
    }

    private static T DeserializeRequest<T>(string? json)
    {
        Assert.False(string.IsNullOrWhiteSpace(json));
        var result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(result);
        return result;
    }

    private static HttpResponseMessage JsonResponse<T>(T body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(body)
        };
    }

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }

        public string? LastPathAndQuery { get; private set; }

        public string? LastRequestBody { get; private set; }

        public AuthenticationHeaderValue? LastAuthorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastPathAndQuery = request.RequestUri?.PathAndQuery;
            LastAuthorization = request.Headers.Authorization;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responder(request);
        }
    }

    private sealed class StaticOperatorTokenStore : IOperatorTokenStore
    {
        public Task SaveAsync(OperatorTokenSnapshot snapshot, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<OperatorTokenSnapshot?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<OperatorTokenSnapshot?>(new OperatorTokenSnapshot(
                StaffUserId,
                OrganizationId,
                "Pilot Manager",
                "staff-access-token",
                DateTimeOffset.Parse("2026-05-19T08:00:00Z"),
                "refresh-token",
                DateTimeOffset.Parse("2026-05-20T08:00:00Z")));
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
