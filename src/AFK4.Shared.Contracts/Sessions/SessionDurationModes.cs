namespace AFK4.Shared.Contracts.Sessions;

/// <summary>
/// How a session's billable time is bounded. <see cref="Open"/> is an open tab
/// (postpaid's natural mode) with no end boundary; <see cref="Fixed"/> reserves a
/// known duration up front and ends at <c>StartedAtUtc + DurationMinutes</c>.
/// </summary>
public static class SessionDurationModes
{
    public const string Open = "open";
    public const string Fixed = "fixed";

    public static bool IsValid(string? value) =>
        value is Open or Fixed;
}
