namespace AFK4.Shared.Contracts.Players;

/// <summary>Asks for a code to be sent to <paramref name="Phone"/> — the number the player claims.</summary>
public sealed record PlayerPhoneStartVerificationRequest(string Phone);

public sealed record PlayerPhoneVerificationStartedResponse(int ExpiresInSeconds, int ResendAfterSeconds);

public sealed record PlayerPhoneConfirmRequest(string Code);

public sealed record PlayerPhoneConfirmedResponse(string Phone);

public sealed record PlayerPhoneStatusResponse(string? Phone, DateTimeOffset? PhoneVerifiedAtUtc);

/// <summary>Просьба прислать код для входа. Ответ одинаков независимо от того, есть ли такой игрок.</summary>
public sealed record PlayerCodeSignInStartRequest(Guid OrganizationId, string PhoneNumber);

public sealed record PlayerCodeSignInStartedResponse(int ExpiresInSeconds, int ResendAfterSeconds);

public sealed record PlayerCodeSignInRequest(Guid OrganizationId, string PhoneNumber, string Code);
