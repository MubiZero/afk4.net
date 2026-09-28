using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Identity;

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

public sealed record PinAuthenticationResult(
    PinSignInStatus Status,
    PlatformPersonEntity? Person,
    PlayerAccountEntity? Account)
{
    public static readonly PinAuthenticationResult Refused = new(PinSignInStatus.Refused, null, null);

    public static PinAuthenticationResult Authenticated(PlatformPersonEntity person, PlayerAccountEntity account) =>
        new(PinSignInStatus.SignedIn, person, account);
}
