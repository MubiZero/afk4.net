using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Токены выдаются человеку, а клуб выбирается запросом. Ответ при этом остаётся дословно тем же
/// <see cref="PlayerSignInResponse"/>, что и раньше: старое приложение и веб-версия игрока не
/// должны заметить, что за токеном теперь стоит личность.
/// </summary>
public interface IPlatformPersonTokenService
{
    /// <summary>
    /// Выдаёт пару токенов личности. <paramref name="pinnedAccount"/> — клуб, который клиент
    /// назвал при входе: он закрепляется в токене, чтобы клиент, ещё не умеющий выбирать клуб
    /// заголовком, продолжал попадать туда же, куда и вчера.
    /// </summary>
    Task<PlayerSignInResponse> IssueAsync(
        PlatformPersonEntity person,
        PlayerAccountEntity pinnedAccount,
        CancellationToken cancellationToken);

    Task<PlayerSignInResponse?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);

    Task<PlatformPersonContext?> ValidateAsync(string? bearerToken, CancellationToken cancellationToken);
}
