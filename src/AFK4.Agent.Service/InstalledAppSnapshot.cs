namespace AFK4.Agent.Service;

public sealed record InstalledAppSnapshot(
    string DisplayName,
    string? Version,
    string? Publisher,
    string? InstallLocation,
    DateTimeOffset? InstalledAtUtc);
