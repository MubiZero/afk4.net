using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Токены выдаются человеку, а клуб выбирается запросом. Первые восемь полей ответа — дословно те
/// же, что в <see cref="PlayerSignInResponse"/>: старое приложение и веб-версия игрока не должны
/// заметить, что за токеном теперь стоит личность.
/// </summary>
public interface IPlatformPersonTokenService
{
    /// <summary>
    /// Выдаёт пару токенов личности. <paramref name="pinnedAccount"/> — клуб, который клиент
    /// назвал при входе: он закрепляется в токене, чтобы клиент, ещё не умеющий выбирать клуб
    /// заголовком, продолжал попадать туда же, куда и вчера. <c>null</c> — человек, у которого
    /// клуба пока нет вовсе или их несколько: он зарегистрировался дома и выберет клуб сам.
    /// </summary>
    Task<PlatformPersonSessionResponse> IssueAsync(
        PlatformPersonEntity person,
        PlayerAccountEntity? pinnedAccount,
        CancellationToken cancellationToken);

    Task<PlatformPersonSessionResponse?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);

    Task<PlatformPersonContext?> ValidateAsync(string? bearerToken, CancellationToken cancellationToken);
}
