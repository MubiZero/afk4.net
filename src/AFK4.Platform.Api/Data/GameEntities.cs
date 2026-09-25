namespace AFK4.Platform.Api.Data;

/// <summary>Игра в каталоге платформы. Не удаляется — снимается с публикации: на неё ссылаются клубы.</summary>
public sealed class CatalogGameEntity
{
    public Guid CatalogGameId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Genre { get; set; }

    public int? MinAge { get; set; }

    public string LaunchKind { get; set; } = string.Empty;

    public string? LaunchTarget { get; set; }

    public string? CoverUrl { get; set; }

    public bool IsPublished { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid? UpdatedByPlatformAdminUserId { get; set; }
}

/// <summary>Игра в библиотеке филиала.</summary>
public sealed class BranchGameEntity
{
    public Guid BranchGameId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid? CatalogGameId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Genre { get; set; }

    public int? MinAge { get; set; }

    public string? CoverUrl { get; set; }

    public string LaunchKind { get; set; } = string.Empty;

    public string? LaunchTarget { get; set; }

    public string? ExecutablePath { get; set; }

    public string? Arguments { get; set; }

    public bool AvailableWithoutSession { get; set; }

    public bool IsEnabled { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>
/// Версия библиотеки филиала: растёт с каждой правкой и едет в сердцебиении — по её смене агент
/// перечитывает список. Строки нет — библиотеку не трогали, версия 0.
/// </summary>
public sealed class BranchGameLibraryEntity
{
    public Guid BranchId { get; set; }

    public Guid OrganizationId { get; set; }

    public int Version { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
