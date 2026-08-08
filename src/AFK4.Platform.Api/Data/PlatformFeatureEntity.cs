namespace AFK4.Platform.Api.Data;

/// <summary>
/// Строка каталога фич. Заводится при старте из объявлений в коде (<c>FeatureCatalog</c>),
/// дальше редактируется в панели: строка без кода и код без строки невозможны.
/// </summary>
public sealed class PlatformFeatureEntity
{
    public string FeatureKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Последняя ступень лестницы: значение, когда ни клуб, ни тариф не высказались.</summary>
    public bool EnabledByDefault { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
