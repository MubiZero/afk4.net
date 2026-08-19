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

/// <summary>
/// Человек заводит себя сам: дома, без клуба и без администратора. Тот же маршрут пускает и того,
/// кто уже есть, — «зарегистрироваться» и «войти» с точки зрения телефона одно и то же действие, и
/// разделить их значило бы сказать звонящему, знаком нам его номер или нет.
/// </summary>
public interface IPlatformRegistrationService
{
    /// <summary>
    /// Шлёт код на номер. Личность здесь не ищется вовсе — не «ищется осторожно», а не ищется:
    /// так неотличимость ответа держится устройством кода, а не аккуратностью следующего автора.
    /// </summary>
    Task<PhoneVerificationStartResult> StartAsync(string rawPhone, CancellationToken cancellationToken);

    Task<PlatformRegistrationConfirmResult> ConfirmAsync(
        string rawPhone, string code, CancellationToken cancellationToken);
}
