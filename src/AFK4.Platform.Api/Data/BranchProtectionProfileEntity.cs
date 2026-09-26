namespace AFK4.Platform.Api.Data;

/// <summary>
/// Профиль защиты ПК филиала (спека оболочки, §6.3). Строки нет — клуб профиль не настраивал, и
/// на ПК действует только постоянная база киоска (Ctrl+Alt+Del без блокировки, выхода и смены
/// пользователя). Версия — сторож от одновременной правки и сигнал агентам перечитать профиль.
/// </summary>
public sealed class BranchProtectionProfileEntity
{
    public Guid BranchId { get; set; }

    public Guid OrganizationId { get; set; }

    public int Version { get; set; }

    public bool BlockRemovableStorage { get; set; }

    public bool BlockBrowserDownloads { get; set; }

    public bool BlockBrowserIncognito { get; set; }

    public bool DisableRunDialog { get; set; }

    /// <summary>Буквы скрытых дисков подряд, по алфавиту: «DE».</summary>
    public string HiddenDrives { get; set; } = string.Empty;

    public string UrlBlocklistJson { get; set; } = "[]";

    public string BlockedWindowsJson { get; set; } = "[]";

    /// <summary>Что стирать после сессии, JSON-список из SessionTraceNames. По умолчанию — всё.</summary>
    public string ClearAfterSessionJson { get; set; } = DefaultClearAfterSessionJson;

    /// <summary>Выключать свободный ПК через столько минут простоя; null — не выключать.</summary>
    public int? IdleShutdownMinutes { get; set; }

    /// <summary>Правила клуба на экране ПК.</summary>
    public string? ClubRules { get; set; }

    public const string DefaultClearAfterSessionJson = "[\"steam\",\"browsers\",\"launchers\",\"messengers\"]";

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid UpdatedByStaffUserId { get; set; }
}
