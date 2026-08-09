namespace AFK4.Platform.Api.Data;

/// <summary>
/// Роль платформы — именованный набор прав. Названия самих прав живут в коде: право, которое
/// никто не проверяет, ничего не даёт.
/// </summary>
public sealed class PlatformRoleEntity
{
    public string RoleName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Встроенную роль можно редактировать, но нельзя удалить.</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Роль с полным доступом имеет все права из кода, включая те, что появятся завтра.
    /// Без этого флага каждое новое право после деплоя не принадлежало бы никому, и новый раздел
    /// был бы недоступен всем до ручной правки роли.
    /// </summary>
    public bool GrantsAllPermissions { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
