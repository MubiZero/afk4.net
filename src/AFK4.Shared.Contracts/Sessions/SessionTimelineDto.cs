namespace AFK4.Shared.Contracts.Sessions;

/// <summary>
/// A started game session projected onto the booking timeline: when it began, its scheduled end
/// (fixed/prepaid) and its actual end (null while still running). The operator UI derives the bar —
/// open tab (no scheduled, no actual end) renders open-ended; otherwise bounded.
/// </summary>
public sealed record SessionTimelineItemDto(
    Guid SessionId,
    Guid SeatId,
    string SeatName,
    Guid ZoneId,
    string ZoneName,
    string State,
    string? PlayerDisplayName,
    string? TariffName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndsAtUtc,
    DateTimeOffset? EndedAtUtc);

public sealed record SessionTimelineResult(IReadOnlyList<SessionTimelineItemDto> Sessions);
