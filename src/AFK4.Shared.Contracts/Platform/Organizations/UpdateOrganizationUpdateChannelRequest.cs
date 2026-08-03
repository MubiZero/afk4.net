namespace AFK4.Shared.Contracts.Platform.Organizations;

public sealed record UpdateOrganizationUpdateChannelRequest(
    string Channel,
    string? PinnedClientVersion);
