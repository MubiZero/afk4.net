namespace AFK4.Shared.Contracts.Layout;

/// <summary>
/// Зал и его места. <see cref="HardwareSummary"/> — необязательный хвост: новое поле не должно
/// ломать позиционные вызовы, которых у этого контракта хватает и в Windows-проектах.
/// </summary>
public sealed record ZoneDto(
    Guid ZoneId,
    Guid OrganizationId,
    Guid BranchId,
    string Name,
    int SortOrder,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<SeatDto> Seats,
    string? HardwareSummary = null);
