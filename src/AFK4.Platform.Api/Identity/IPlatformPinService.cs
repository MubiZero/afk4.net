using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Сетевой PIN: короткий числовой пароль, которым человек сажает себя за игровой ПК в любом клубе
/// сети. Принадлежит личности, а не клубному счёту, поэтому и задаётся только самим человеком —
/// в приложении, где он уже вошёл.
/// </summary>
public interface IPlatformPinService
{
    Task<SetPinStatus> SetAsync(Guid platformPersonId, string? pin, CancellationToken cancellationToken);

    /// <summary>
    /// Проверяет PIN и впускает человека в названный клуб, открывая счёт, если его ещё нет.
    /// Любая неудача возвращается одним и тем же <see cref="PinSignInStatus.Refused"/>: причина
    /// отказа — это ответ на вопрос «есть ли у этого номера аккаунт», и его никто не получает.
    /// </summary>
    Task<PinSignInResult> SignInAsync(
        Guid organizationId,
        string? rawPhone,
        string? pin,
        Guid? branchId,
        CancellationToken cancellationToken);
}

public enum SetPinStatus
{
    Updated,

    /// <summary>Не 4–8 цифр. Единственная причина отказа, о которой человеку говорят прямо.</summary>
    InvalidPin,

    PersonNotFound,
}

public enum PinSignInStatus
{
    SignedIn,
    Refused,
}

public sealed record PinSignInResult(PinSignInStatus Status, PlatformPersonSessionResponse? Session)
{
    public static readonly PinSignInResult Refused = new(PinSignInStatus.Refused, null);

    public static PinSignInResult SignedIn(PlatformPersonSessionResponse session) =>
        new(PinSignInStatus.SignedIn, session);
}
