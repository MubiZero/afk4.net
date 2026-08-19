using System.Security.Cryptography;
using System.Text;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Непрозрачные токены личности. Форма, сроки и одноразовость refresh повторяют
/// <see cref="OpaquePlayerTokenService"/> дословно — токен остаётся строкой «идентификатор.секрет»,
/// в базе лежит только хеш.
/// </summary>
public sealed class OpaquePlatformPersonTokenService(PlatformDbContext dbContext, TimeProvider timeProvider)
    : IPlatformPersonTokenService
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<PlatformPersonSessionResponse> IssueAsync(
        PlatformPersonEntity person,
        PlayerAccountEntity? pinnedAccount,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var (accessTokenId, accessToken) = CreateToken();
        var (refreshTokenId, refreshToken) = CreateToken();
        var accessExpires = now.Add(AccessTokenLifetime);
        var refreshExpires = now.Add(RefreshTokenLifetime);

        dbContext.PlatformPersonAccessTokens.Add(new PlatformPersonAccessTokenEntity
        {
            PlatformPersonAccessTokenId = accessTokenId,
            PlatformPersonId = person.PlatformPersonId,
            PinnedOrganizationId = pinnedAccount?.OrganizationId,
            TokenHash = HashToken(accessToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = accessExpires
        });
        dbContext.PlatformPersonRefreshTokens.Add(new PlatformPersonRefreshTokenEntity
        {
            PlatformPersonRefreshTokenId = refreshTokenId,
            PlatformPersonId = person.PlatformPersonId,
            PinnedOrganizationId = pinnedAccount?.OrganizationId,
            TokenHash = HashToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpires
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        // Первые восемь полей стоят там же, где вчера: клуб, счёт и имя клиент читает как читал.
        // Клуба может не быть вовсе — так выглядит человек, зарегистрировавшийся дома.
        return new PlatformPersonSessionResponse(
            pinnedAccount?.PlayerAccountId,
            pinnedAccount?.OrganizationId,
            person.DisplayName,
            person.PhoneVerifiedAtUtc is not null,
            accessToken,
            accessExpires,
            refreshToken,
            refreshExpires,
            person.PlatformPersonId,
            person.PreferredLocale,
            !string.IsNullOrWhiteSpace(person.DisplayName));
    }

    public async Task<PlatformPersonSessionResponse?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (!TryReadTokenId(refreshToken, out var tokenId))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        // Без AsNoTracking: RevokedAtUtc ниже проставляется, refresh одноразовый.
        var stored = await dbContext.PlatformPersonRefreshTokens
            .SingleOrDefaultAsync(token => token.PlatformPersonRefreshTokenId == tokenId, cancellationToken);
        if (stored is null || stored.RevokedAtUtc is not null || stored.ExpiresAtUtc <= now)
        {
            return null;
        }

        if (!stored.TokenHash.SequenceEqual(HashToken(refreshToken!)))
        {
            return null;
        }

        var person = await dbContext.PlatformPersons
            .SingleOrDefaultAsync(candidate => candidate.PlatformPersonId == stored.PlatformPersonId, cancellationToken);
        if (person is null || !person.IsActive)
        {
            return null;
        }

        // Закреплённый клуб продлевается закреплённым, а незакреплённый так и остаётся
        // незакреплённым: подобрать клуб на продлении значило бы решить за человека то, чего он
        // не выбирал. Если закреплённая карточка закрыта клубом — продлевать нечего.
        PlayerAccountEntity? account = null;
        if (stored.PinnedOrganizationId is { } pinnedOrganizationId)
        {
            account = await FindPinnedAccountAsync(
                person.PlatformPersonId, pinnedOrganizationId, cancellationToken);
            if (account is null)
            {
                return null;
            }
        }

        stored.RevokedAtUtc = now;
        return await IssueAsync(person, account, cancellationToken);
    }

    public async Task<PlatformPersonContext?> ValidateAsync(string? bearerToken, CancellationToken cancellationToken)
    {
        if (!TryReadTokenId(bearerToken, out var tokenId))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var stored = await dbContext.PlatformPersonAccessTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.PlatformPersonAccessTokenId == tokenId, cancellationToken);
        if (stored is null || stored.RevokedAtUtc is not null || stored.ExpiresAtUtc <= now)
        {
            return null;
        }

        if (!stored.TokenHash.SequenceEqual(HashToken(bearerToken!)))
        {
            return null;
        }

        // Отзыв идёт по самой личности, а не по клубному счёту: клуб не вправе закрыть человеку
        // вход в чужие клубы.
        var person = await dbContext.PlatformPersons
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PlatformPersonId == stored.PlatformPersonId, cancellationToken);
        if (person is null || !person.IsActive)
        {
            return null;
        }

        return new PlatformPersonContext(
            person.PlatformPersonId,
            stored.PinnedOrganizationId,
            person.PhoneVerifiedAtUtc is not null);
    }

    private Task<PlayerAccountEntity?> FindPinnedAccountAsync(
        Guid platformPersonId,
        Guid pinnedOrganizationId,
        CancellationToken cancellationToken) =>
        dbContext.PlayerAccounts
            .Where(account => account.PlatformPersonId == platformPersonId
                && account.IsActive
                && account.OrganizationId == pinnedOrganizationId)
            .SingleOrDefaultAsync(cancellationToken);

    private static bool TryReadTokenId(string? token, out Guid tokenId)
    {
        tokenId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.');
        return parts.Length == 2 && Guid.TryParse(parts[0], out tokenId);
    }

    private static (Guid TokenId, string Token) CreateToken()
    {
        var tokenId = Guid.NewGuid();
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return (tokenId, $"{tokenId:N}.{secret}");
    }

    private static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
