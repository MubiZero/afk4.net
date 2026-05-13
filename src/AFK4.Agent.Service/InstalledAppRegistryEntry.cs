namespace AFK4.Agent.Service;

public sealed record InstalledAppRegistryEntry(
    string? DisplayName,
    string? DisplayVersion,
    string? Publisher,
    string? InstallLocation,
    string? InstallDate);
