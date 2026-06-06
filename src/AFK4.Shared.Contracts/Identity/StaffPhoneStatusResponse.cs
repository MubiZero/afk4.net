namespace AFK4.Shared.Contracts.Identity;

/// <summary>Current staff member's phone state (self-read). Both null until a phone is set/verified.</summary>
/// <remarks>Phone is in E.164 display form (e.g. "+992937380070").</remarks>
public sealed record StaffPhoneStatusResponse(string? Phone, DateTimeOffset? PhoneVerifiedAtUtc);
