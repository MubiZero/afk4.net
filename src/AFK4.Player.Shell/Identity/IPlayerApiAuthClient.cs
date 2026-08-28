namespace AFK4.Player.Shell.Identity;

public readonly record struct AuthSnapshot(bool Authenticated, string? DisplayName, bool PhoneVerified);

public interface IPlayerApiAuthClient
{
    AuthSnapshot Current { get; }
    string? CurrentAccessToken { get; }
    /// <param name="branchId">
    /// Зал, у ПК которого стоит человек. Нужен ровно в одном случае: сеть из нескольких залов, а
    /// счёта в клубе у человека ещё нет — тогда без зала сервер счёт не откроет и вход кончится
    /// отказом. Оболочка свой зал знает всегда, так что гадать не приходится; null — только там,
    /// где состояние зала ещё не приехало.
    /// </param>
    Task<AuthSnapshot> SignInAsync(
        Guid organizationId, string phoneNumber, string password, Guid? branchId, CancellationToken ct);
    Task EnsureFreshTokenAsync(CancellationToken ct);
    void SignOut();
}
