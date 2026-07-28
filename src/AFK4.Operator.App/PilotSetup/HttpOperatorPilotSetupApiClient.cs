using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Tariffs;

namespace AFK4.Operator.App.PilotSetup;

public sealed class HttpOperatorPilotSetupApiClient(HttpClient httpClient, IOperatorTokenStore tokenStore)
    : IOperatorPilotSetupApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<StaffUserDto>> GetStaffUsersAsync(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        return GetAsync<IReadOnlyList<StaffUserDto>>(
            $"branches/{branchId:D}/staff",
            cancellationToken);
    }

    public Task<StaffInviteDto> CreateStaffInviteAsync(
        Guid branchId,
        CreateStaffInviteRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<StaffInviteDto, CreateStaffInviteRequest>(
            $"branches/{branchId:D}/staff/invites",
            request,
            cancellationToken);
    }

    public Task<IReadOnlyList<ZoneDto>> GetLayoutZonesAsync(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        return GetAsync<IReadOnlyList<ZoneDto>>(
            $"branches/{branchId:D}/layout/zones",
            cancellationToken);
    }

    public Task<ZoneDto> CreateZoneAsync(
        Guid branchId,
        CreateZoneRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<ZoneDto, CreateZoneRequest>(
            $"branches/{branchId:D}/layout/zones",
            request,
            cancellationToken);
    }

    public Task<SeatDto> CreateSeatAsync(
        Guid branchId,
        CreateSeatRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<SeatDto, CreateSeatRequest>(
            $"branches/{branchId:D}/layout/seats",
            request,
            cancellationToken);
    }

    public Task<TariffDto> CreateTariffAsync(
        Guid branchId,
        CreateTariffRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<TariffDto, CreateTariffRequest>(
            $"branches/{branchId:D}/tariffs",
            request,
            cancellationToken);
    }

    public Task<TariffVersionDto> CreateTariffVersionAsync(
        Guid branchId,
        Guid tariffId,
        CreateTariffVersionRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<TariffVersionDto, CreateTariffVersionRequest>(
            $"branches/{branchId:D}/tariffs/{tariffId:D}/versions",
            request,
            cancellationToken);
    }

    public Task<PosProductCategoryDto> CreateProductCategoryAsync(
        Guid branchId,
        CreateProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<PosProductCategoryDto, CreateProductCategoryRequest>(
            $"branches/{branchId:D}/pos/categories",
            request,
            cancellationToken);
    }

    public Task<PosProductDto> CreateProductAsync(
        Guid branchId,
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<PosProductDto, CreateProductRequest>(
            $"branches/{branchId:D}/pos/products",
            request,
            cancellationToken);
    }

    public Task<DeviceSeatAssignmentDto> AssignDeviceSeatAsync(
        Guid branchId,
        Guid deviceId,
        AssignDeviceSeatRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<DeviceSeatAssignmentDto, AssignDeviceSeatRequest>(
            $"branches/{branchId:D}/devices/{deviceId:D}/seat-assignment",
            request,
            cancellationToken);
    }

    private async Task<TResponse> GetAsync<TResponse>(
        string requestUri,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, requestUri, cancellationToken);
        return await SendAndReadAsync<TResponse>(request, cancellationToken);
    }

    private async Task<TResponse> PostAsync<TResponse, TRequest>(
        string requestUri,
        TRequest body,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Post, requestUri, cancellationToken);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        return await SendAndReadAsync<TResponse>(request, cancellationToken);
    }

    private async Task<TResponse> SendAndReadAsync<TResponse>(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Platform API returned {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}",
                inner: null,
                response.StatusCode);
        }

        var body = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
        return body ?? throw new InvalidOperationException("Platform API returned an empty pilot setup response.");
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string requestUri,
        CancellationToken cancellationToken)
    {
        var snapshot = await tokenStore.LoadAsync(cancellationToken);
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.AccessToken))
        {
            throw new InvalidOperationException("Operator access token is missing.");
        }

        var request = new HttpRequestMessage(method, OrganizationApiRoute.Build(snapshot, requestUri));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", snapshot.AccessToken);
        return request;
    }
}
