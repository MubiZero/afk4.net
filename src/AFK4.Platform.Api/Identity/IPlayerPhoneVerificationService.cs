namespace AFK4.Platform.Api.Identity;

/// <summary>Where the player's phone stands right now: the number and whether it is confirmed.</summary>
public sealed record PlayerPhoneStatus(string? Phone, DateTimeOffset? PhoneVerifiedAtUtc);
