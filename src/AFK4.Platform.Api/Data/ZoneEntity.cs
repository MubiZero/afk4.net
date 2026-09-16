namespace AFK4.Platform.Api.Data;

public sealed class ZoneEntity
{
    public Guid ZoneId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>
    /// Чем зал оснащён, словами владельца: «RTX 4060 · 27\" 165 Гц · кресла DXRacer».
    /// Показывается игроку в подробностях клуба — по железу клубы и сравнивают.
    /// </summary>
    public string? HardwareSummary { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
