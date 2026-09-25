using System.Globalization;

namespace AFK4.Shared.Contracts.Install;

/// <summary>
/// Пути установочного потока, общие для мастера и сервера. Один источник правды намеренно: пока
/// путь жил строкой в двух местах, мастер слал на <c>/api/install/auth/*</c>, сервер отвечал только
/// под <c>/api/organizations/{id}/install/auth/*</c>, и обе стороны были покрыты зелёными тестами —
/// каждая проверяла себя, а не стык. На живом API это было 404.
///
/// Организацию эндпоинты берут из токена, но в пути она обязательна: в этом проекте у всех
/// организационных маршрутов канонический префикс, и он стережётся архитектурным тестом.
/// </summary>
public static class InstallRoutes
{
    public static string AuthenticatedDiscover(Guid organizationId) => Build(organizationId, "discover");

    public static string AuthenticatedSeats(Guid organizationId) => Build(organizationId, "seats");

    public static string AuthenticatedEnroll(Guid organizationId) => Build(organizationId, "enroll");

    /// <summary>Тихая установка по коду: организации в пути нет — её называет сам код.</summary>
    public const string CodeEnroll = "/api/install/code/enroll";

    private static string Build(Guid organizationId, string action) => string.Create(
        CultureInfo.InvariantCulture,
        $"/api/organizations/{organizationId:D}/install/auth/{action}");
}
