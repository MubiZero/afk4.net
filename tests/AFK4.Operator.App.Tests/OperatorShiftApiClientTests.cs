using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Shifts;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorShiftApiClientTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid ShiftId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid StaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");

    [Fact]
    public async Task OpenShiftAsync_PostsBearerAuthenticatedOpenRequest()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(CreateShift(ShiftStateNames.Open)));
        var client = CreateClient(handler);
        var request = new OpenShiftRequest(
            OrganizationId,
            new MoneyDto("USD", 50000),
            "front register",
            "shift-open-001");

        var shift = await client.OpenShiftAsync(BranchId, request, CancellationToken.None);

        Assert.Equal(ShiftId, shift.ShiftId);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/shifts/open", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);

        var body = DeserializeRequest<OpenShiftRequest>(handler.LastRequestBody);
        Assert.Equal("shift-open-001", body.IdempotencyKey);
        Assert.Equal(50000, body.StartingCash.MinorUnits);
    }

    [Fact]
    public async Task GetCurrentShiftAsync_GetsCurrentShiftOrNullWhenMissing()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonContentResponse(CreateShift(ShiftStateNames.Open)),
            new HttpResponseMessage(HttpStatusCode.NotFound)
        ]);
        var handler = new RecordingHttpMessageHandler(_ => responses.Dequeue());
        var client = CreateClient(handler);

        var shift = await client.GetCurrentShiftAsync(BranchId, CancellationToken.None);
        var firstPath = handler.LastPathAndQuery;
        var missing = await client.GetCurrentShiftAsync(BranchId, CancellationToken.None);

        Assert.Equal(ShiftId, shift?.ShiftId);
        Assert.Equal($"/api/branches/{BranchId:D}/shifts/current", firstPath);
        Assert.Null(missing);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
    }

    [Fact]
    public async Task RecordCashMovementAsync_PostsMovementToShift()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(new CashMovementDto(
            Guid.Parse("66666666-6666-4666-8666-666666666666"),
            OrganizationId,
            BranchId,
            ShiftId,
            StaffUserId,
            CashMovementTypeNames.CashIn,
            new MoneyDto("USD", 2500),
            "cash drawer correction",
            DateTimeOffset.Parse("2026-05-14T10:00:00Z"))));
        var client = CreateClient(handler);
        var request = new RecordCashMovementRequest(
            OrganizationId,
            CashMovementTypeNames.CashIn,
            new MoneyDto("USD", 2500),
            "cash drawer correction",
            "cash-in-001");

        var movement = await client.RecordCashMovementAsync(ShiftId, request, CancellationToken.None);

        Assert.Equal(CashMovementTypeNames.CashIn, movement.MovementType);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/shifts/{ShiftId:D}/cash-movements", handler.LastPathAndQuery);

        var body = DeserializeRequest<RecordCashMovementRequest>(handler.LastRequestBody);
        Assert.Equal("cash-in-001", body.IdempotencyKey);
        Assert.Equal(2500, body.Amount.MinorUnits);
    }

    [Fact]
    public async Task CloseShiftAsync_PostsCloseRequest()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(CreateShift(ShiftStateNames.Closed)));
        var client = CreateClient(handler);
        var request = new CloseShiftRequest(
            OrganizationId,
            new MoneyDto("USD", 51500),
            "balanced",
            "shift-close-001");

        var closed = await client.CloseShiftAsync(ShiftId, request, CancellationToken.None);

        Assert.Equal(ShiftStateNames.Closed, closed.State);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/shifts/{ShiftId:D}/close", handler.LastPathAndQuery);

        var body = DeserializeRequest<CloseShiftRequest>(handler.LastRequestBody);
        Assert.Equal("shift-close-001", body.IdempotencyKey);
        Assert.Equal(51500, body.CountedCash.MinorUnits);
    }

    [Fact]
    public async Task GetShiftReportAsync_GetsBearerAuthenticatedReportWithFilters()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(new ShiftReportResultDto([], Limit: 25)));
        var client = CreateClient(handler);

        var report = await client.GetShiftReportAsync(
            BranchId,
            DateTimeOffset.Parse("2026-05-14T00:00:00Z"),
            DateTimeOffset.Parse("2026-05-15T00:00:00Z"),
            25,
            CancellationToken.None);

        Assert.Equal(25, report.Limit);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.NotNull(handler.LastPathAndQuery);
        Assert.StartsWith($"/api/branches/{BranchId:D}/reports/shifts?", handler.LastPathAndQuery);
        Assert.Contains("fromUtc=", handler.LastPathAndQuery);
        Assert.Contains("toUtc=", handler.LastPathAndQuery);
        Assert.Contains("limit=25", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);
    }

    [Fact]
    public async Task GetSalesReportAsync_GetsBearerAuthenticatedReport()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(new SalesReportResultDto(
            [],
            Limit: 50,
            GrossSalesTotal: new MoneyDto("USD", 0),
            RefundsTotal: new MoneyDto("USD", 0),
            NetSalesTotal: new MoneyDto("USD", 0))));
        var client = CreateClient(handler);

        var report = await client.GetSalesReportAsync(BranchId, null, null, null, CancellationToken.None);

        Assert.Equal(50, report.Limit);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/reports/sales", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);
    }

    [Theory]
    [InlineData("gameplay-time")]
    [InlineData("cash-operations")]
    [InlineData("operator-actions")]
    public async Task GetOperationalReportAsync_GetsBearerAuthenticatedReport(string reportName)
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(CreateEmptyOperationalReport(reportName)));
        var client = CreateClient(handler);

        switch (reportName)
        {
            case "gameplay-time":
                await client.GetGameplayTimeReportAsync(BranchId, null, null, null, CancellationToken.None);
                break;
            case "cash-operations":
                await client.GetCashOperationReportAsync(BranchId, null, null, null, CancellationToken.None);
                break;
            case "operator-actions":
                await client.GetOperatorActionReportAsync(BranchId, null, null, null, CancellationToken.None);
                break;
        }

        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/reports/{reportName}", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);
    }

    [Theory]
    [InlineData("shifts")]
    [InlineData("sales")]
    [InlineData("gameplay-time")]
    [InlineData("cash-operations")]
    [InlineData("operator-actions")]
    public async Task ExportReportCsvAsync_GetsBearerAuthenticatedCsvWithFilters(string reportName)
    {
        var handler = new RecordingHttpMessageHandler(_ => CsvContentResponse("id,state\r\n1,open\r\n"));
        var client = CreateClient(handler);

        var csv = reportName switch
        {
            "shifts" => await client.ExportShiftReportCsvAsync(
                BranchId,
                DateTimeOffset.Parse("2026-05-14T00:00:00Z"),
                null,
                25,
                CancellationToken.None),
            "sales" => await client.ExportSalesReportCsvAsync(BranchId, null, null, null, CancellationToken.None),
            "gameplay-time" => await client.ExportGameplayTimeReportCsvAsync(BranchId, null, null, null, CancellationToken.None),
            "cash-operations" => await client.ExportCashOperationReportCsvAsync(BranchId, null, null, null, CancellationToken.None),
            "operator-actions" => await client.ExportOperatorActionReportCsvAsync(BranchId, null, null, null, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(reportName), reportName, null)
        };

        Assert.Equal("id,state\r\n1,open\r\n", csv);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.NotNull(handler.LastPathAndQuery);
        Assert.StartsWith($"/api/branches/{BranchId:D}/reports/{reportName}/export.csv", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);
        if (reportName == "shifts")
        {
            Assert.Contains("fromUtc=", handler.LastPathAndQuery);
            Assert.Contains("limit=25", handler.LastPathAndQuery);
        }
    }

    private static HttpOperatorShiftApiClient CreateClient(RecordingHttpMessageHandler handler)
    {
        return new HttpOperatorShiftApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5074")
        }, new StaticOperatorTokenStore());
    }

    private static ShiftDto CreateShift(string state)
    {
        var isClosed = state == ShiftStateNames.Closed;

        return new ShiftDto(
            ShiftId,
            OrganizationId,
            BranchId,
            StaffUserId,
            isClosed ? StaffUserId : null,
            state,
            new MoneyDto("USD", 50000),
            isClosed ? new MoneyDto("USD", 51500) : null,
            isClosed ? new MoneyDto("USD", 51500) : null,
            isClosed ? new MoneyDto("USD", 0) : null,
            "front register",
            isClosed ? "balanced" : string.Empty,
            DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
            isClosed ? DateTimeOffset.Parse("2026-05-14T18:00:00Z") : null);
    }

    private static object CreateEmptyOperationalReport(string reportName)
    {
        return reportName switch
        {
            "gameplay-time" => new GameplayTimeReportResultDto(
                [],
                Limit: 50,
                TotalDurationSeconds: 0,
                TotalPackageSeconds: 0,
                TotalBonusSeconds: 0,
                GameplayRevenueTotal: new MoneyDto("USD", 0)),
            "cash-operations" => new CashOperationReportResultDto(
                [],
                Limit: 50,
                CashInTotal: new MoneyDto("USD", 0),
                CashOutTotal: new MoneyDto("USD", 0),
                NetCashTotal: new MoneyDto("USD", 0)),
            "operator-actions" => new OperatorActionReportResultDto([], Limit: 50, TotalActionCount: 0),
            _ => throw new ArgumentOutOfRangeException(nameof(reportName), reportName, null)
        };
    }

    private static T DeserializeRequest<T>(string? json)
    {
        Assert.False(string.IsNullOrWhiteSpace(json));
        var result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(result);
        return result;
    }

    private static HttpResponseMessage JsonContentResponse<T>(T body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(body)
        };
    }

    private static HttpResponseMessage CsvContentResponse(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/csv")
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
                "Cashier One",
                "staff-access-token",
                DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
                "refresh-token",
                DateTimeOffset.Parse("2026-05-15T10:00:00Z")));
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
