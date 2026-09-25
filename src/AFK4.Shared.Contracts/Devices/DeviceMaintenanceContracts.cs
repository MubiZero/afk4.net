namespace AFK4.Shared.Contracts.Devices;

/// <summary>
/// «Вернуть в зал» с самого ПК (спека оболочки, §6.5): техник закончил и нажал кнопку на полосе.
/// Агент зовёт сервер ключом устройства, а не ждёт Панель — иначе ПК стоял бы открытым, пока
/// кто-нибудь не дойдёт до стойки.
/// </summary>
public sealed record DeviceMaintenanceReturnRequest(Guid OrganizationId, Guid BranchId, Guid DeviceId);

/// <summary>Пути обслуживания для агента — одна строка в двух местах однажды уже разводила стороны.</summary>
public static class DeviceMaintenanceRoutes
{
    public static string Return(Guid deviceId) => $"/api/devices/{deviceId:D}/maintenance/return";
}
