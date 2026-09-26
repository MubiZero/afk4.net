using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Devices;

/// <summary>Правила обслуживания ПК, общие для Панели, кнопки на самом ПК и автоснятия.</summary>
public static class DeviceMaintenance
{
    /// <summary>
    /// Сколько ПК может простоять открытым для техника. Дольше — значит, про него забыли, а открытый
    /// рабочий стол в зале — это ПК, за которым любой делает что хочет (спека оболочки, §6.5).
    /// </summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(8);

    public static void Clear(DeviceEntity device)
    {
        device.MaintenanceSinceUtc = null;
        device.MaintenanceByStaffUserId = null;
        device.MaintenanceByName = null;
    }
}
