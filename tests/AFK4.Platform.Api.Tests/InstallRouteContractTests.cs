using System.Net;
using AFK4.Shared.Contracts.Install;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Стык мастера установки и сервера. До 13.09.2026 его не проверял никто: мастер слал на
/// <c>/api/install/auth/*</c>, сервер отвечал только под <c>/api/organizations/{id}/install/auth/*</c>,
/// и обе стороны были зелёными — каждая проверяла себя. На живом API это было 404, то есть enroll
/// игрового ПК не работал вовсе.
/// </summary>
public sealed class InstallRouteContractTests
{
    private static readonly Guid OrganizationId = Guid.Parse("3f1d2a44-9c1e-4f7b-9a0d-2b6c5e8a1f30");

    public static TheoryData<string> InstallRoutePaths() =>
        new(
            InstallRoutes.AuthenticatedDiscover(OrganizationId),
            InstallRoutes.AuthenticatedSeats(OrganizationId),
            InstallRoutes.AuthenticatedEnroll(OrganizationId));

    [Theory]
    [MemberData(nameof(InstallRoutePaths))]
    public async Task InstallRoute_IsServedByTheApi(string path)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(path, content: null);

        // Важно ровно одно: маршрут существует. Без токена и без тела сервер отвечает отказом
        // в доступе или «нет тела» — но не «нет такого адреса».
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest });
    }

    // Мастер — не Organization Admin и его заголовков версии не шлёт. Пока проверка совместимости
    // висела на этих маршрутах, ответом был бы 426, а не работа.
    [Theory]
    [MemberData(nameof(InstallRoutePaths))]
    public async Task InstallRoute_DoesNotDemandOrganizationAdminHeaders(string path)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(path, content: null);

        Assert.NotEqual(HttpStatusCode.UpgradeRequired, response.StatusCode);
    }
}
