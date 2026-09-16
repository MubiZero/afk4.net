namespace AFK4.Shared.Contracts.Players;

/// <summary>Asks for a code to be sent to <paramref name="Phone"/> — the number the player claims.</summary>
public sealed record PlayerPhoneStartVerificationRequest(string Phone);

public sealed record PlayerPhoneVerificationStartedResponse(int ExpiresInSeconds, int ResendAfterSeconds);

public sealed record PlayerPhoneConfirmRequest(string Code);

public sealed record PlayerPhoneConfirmedResponse(string Phone);

/// <summary>
/// Ответ на просьбу прислать код: сколько он живёт и когда можно просить следующий. Общий для
/// входа-регистрации и для подтверждения номера в профиле.
/// </summary>
public sealed record PlayerCodeSignInStartedResponse(int ExpiresInSeconds, int ResendAfterSeconds);
