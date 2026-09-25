namespace AFK4.Platform.Api.Data;

/// <summary>
/// Код установки филиала (тихая установка по коду, P5f). Хранится хешем: открытый код видел только
/// тот, кто его выдал. Отзыв — удаление строки.
/// </summary>
public sealed class InstallCodeEntity
{
    public Guid InstallCodeId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid CreatedByStaffUserId { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public int MaxDevices { get; set; }

    /// <summary>Сколько новых ПК встало по коду. Повторная установка той же машины код не тратит.</summary>
    public int UsedDevices { get; set; }
}
