using System.Net;
using AFK4.Shared.Contracts.Branding;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Media;
using AFK4.Shared.Contracts.Tariffs;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Стык мастера установки и сервера. До 13.09.2026 его не проверял никто: мастер слал на
/// <c>/api/install/auth/*</c>, сервер отвечал только под <c>/api/organizations/{id}/install/auth/*</c>,
/// и обе стороны были зелёными — каждая проверяла себя. На живом API это было 404, то есть enroll
/// игрового ПК не работал вовсе.
///
/// Ровно то же самое обнаружилось у входа и не было починено тогда же: маршруты
/// <c>auth/staff/sign-in*</c> тоже жили строкой в двух местах, и мастер падал на первом экране —
/// то есть не работал ни enroll, ни всё, что за ним. Поэтому список ниже покрывает ВЕСЬ путь
/// мастера, а не только установочные вызовы: новый адрес мастера обязан попадать сюда сразу.
/// </summary>
public sealed class WizardRouteContractTests
{
    private static readonly Guid OrganizationId = Guid.Parse("3f1d2a44-9c1e-4f7b-9a0d-2b6c5e8a1f30");

    private static readonly Guid BranchId = Guid.Parse("6c2f9b18-70a4-4d53-8f0e-1d9b4c7a2e55");

    private static readonly Guid TariffId = Guid.Parse("b4e0a1c7-5d32-4e88-9f11-73a6c0d2e945");

    /// <summary>
    /// Всё, куда ходит мастер установки. Список общий для обеих проверок намеренно: маршрут,
    /// добавленный мастеру, обязан сразу попасть под оба условия — существовать и не требовать
    /// заголовков панели управляющего.
    /// </summary>
    public static TheoryData<string> InstallRoutePaths() =>
        new(
            // Вход. Ровно та же ошибка повторилась здесь и жила до 13.09.2026: мастер слал на
            // корневые адреса, сервер отвечал только под /api/organizations/{id}/..., и мастер
            // получал 404 на ПЕРВОМ экране — то есть ни один клуб нельзя было поставить. Тест
            // мастера при этом был зелёным: он проверял, что клиент шлёт на строку, которую сам
            // же и объявил.
            StaffAuthRoutes.SignIn,
            StaffAuthRoutes.SignInByLogin,
            StaffAuthRoutes.SignInByPhone,
            InstallRoutes.AuthenticatedDiscover(OrganizationId),
            InstallRoutes.AuthenticatedSeats(OrganizationId),
            InstallRoutes.AuthenticatedEnroll(OrganizationId),
            BrandingRoutes.Organization(OrganizationId),
            StaffRoutes.Invites(OrganizationId, BranchId),
            // Мастер зовёт их следом за созданием тарифа и загрузкой логотипа: маркер у обоих
            // стоит, но без этой строки регрессию никто не сторожил.
            TariffRoutes.Versions(OrganizationId, BranchId, TariffId),
            MediaRoutes.BranchMedia(OrganizationId, BranchId),
            TariffRoutes.Tariffs(OrganizationId, BranchId));

    [Theory]
    [MemberData(nameof(InstallRoutePaths))]
    public async Task InstallRoute_IsServedByTheApi(string path)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await SendAsync(client, path);

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

        var response = await SendAsync(client, path);

        Assert.NotEqual(HttpStatusCode.UpgradeRequired, response.StatusCode);
    }

    // Оформление правится PATCH-ом, остальное создаётся POST-ом: проверяем тем методом, которым
    // маршрут и вызывают, иначе «маршрут есть» доказывался бы на 405.
    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string path) =>
        path.EndsWith("/branding", StringComparison.Ordinal)
            ? client.PatchAsync(path, content: null)
            : client.PostAsync(path, content: null);
}
