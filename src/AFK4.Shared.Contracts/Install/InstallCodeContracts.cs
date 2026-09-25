using System.Globalization;

namespace AFK4.Shared.Contracts.Install;

/// <summary>
/// Код установки: техник ставит AFK4 на ПК зала без мастера —
/// <c>afk4-client.exe /quiet AFK4_INSTALL_CODE=…</c>. Код многоразовый, но ограничен сроком и
/// числом новых ПК; сервер хранит его хешем, открытым он виден один раз — при выдаче.
/// </summary>
public sealed record CreateInstallCodeRequest(int LifetimeHours, int MaxDevices);

/// <summary>Действующий код установки филиала.</summary>
/// <param name="Code">Сам код — только в ответе на выдачу; в списке его нет.</param>
/// <param name="UsedDevices">Сколько новых ПК уже встало по коду. Переустановка того же ПК код не тратит.</param>
public sealed record InstallCodeDto(
    Guid InstallCodeId,
    Guid BranchId,
    string? Code,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    int MaxDevices,
    int UsedDevices);

/// <summary>
/// Тихая регистрация ПК по коду.
/// </summary>
/// <param name="SeatName">
/// Место по имени. Не названо — ищется место с именем компьютера. Не нашлось или занято другим
/// ПК — ПК встаёт без места, и его привязывают в Панели: отказ из-за опечатки в имени оставил бы
/// ПК вовсе не зарегистрированным, а узнал бы о нём техник только обходом зала.
/// </param>
public sealed record InstallCodeEnrollRequest(
    string Code,
    string? SeatName,
    string? DisplayName,
    string MachineName,
    string DevicePublicKey);

public static class InstallCodeLimits
{
    public const int MinLifetimeHours = 1;

    /// <summary>Неделя: столько живёт образ диска, который раскатывают на зал.</summary>
    public const int MaxLifetimeHours = 24 * 7;

    public const int MinDevices = 1;

    public const int MaxDevices = 200;
}

public static class InstallCodeRoutes
{
    public static string Branch(Guid organizationId, Guid branchId) => string.Create(
        CultureInfo.InvariantCulture,
        $"/api/organizations/{organizationId:D}/branches/{branchId:D}/install-codes");

    public static string Single(Guid organizationId, Guid branchId, Guid installCodeId) => string.Create(
        CultureInfo.InvariantCulture,
        $"/api/organizations/{organizationId:D}/branches/{branchId:D}/install-codes/{installCodeId:D}");
}
