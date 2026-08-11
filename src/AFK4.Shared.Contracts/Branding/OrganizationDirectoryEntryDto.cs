namespace AFK4.Shared.Contracts.Branding;

/// A club as it appears in the public picker: enough to recognise it, nothing about how the
/// business is doing. The mobile app has no hostname to derive a club from, so the player picks
/// one from this list before signing in.
public sealed record OrganizationDirectoryEntryDto(
    Guid OrganizationId,
    string Slug,
    string Name,
    string? LogoUrl,
    string? AccentColor);
