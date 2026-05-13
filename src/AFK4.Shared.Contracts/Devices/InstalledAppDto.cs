namespace AFK4.Shared.Contracts.Devices;

public sealed record InstalledAppDto(
    string DisplayName,
    string? Version,
    string? Publisher,
    string? InstallLocation,
    DateTimeOffset? InstalledAtUtc);
