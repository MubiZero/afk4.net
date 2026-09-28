using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Identity;

public enum PlatformRegistrationConfirmStatus
{
    SignedIn,
    InvalidCode,
    Expired,
    NoActiveCode,
    TooManyAttempts,

    /// <summary>
    /// Личность закрыта платформой. Об этом узнаёт только тот, кто уже доказал владение номером
    /// кодом из SMS, — до этого момента ответ неотличим от ответа незнакомому номеру.
    /// </summary>
    PersonDeactivated,
}

public sealed record PlatformRegistrationConfirmResult(
    PlatformRegistrationConfirmStatus Status,
    PlatformPersonSessionResponse? Session,
    int RemainingAttempts);
