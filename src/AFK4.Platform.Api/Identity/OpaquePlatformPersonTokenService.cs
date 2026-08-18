using System.Security.Cryptography;
using System.Text;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;
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

    public async Task<PlayerSignInResponse> IssueAsync(
        PlatformPersonEntity person,
        PlayerAccountEntity pinnedAccount,
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
            PinnedOrganizationId = pinnedAccount.OrganizationId,
            TokenHash = HashToken(accessToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = accessExpires
        });
        dbContext.PlatformPersonRefreshTokens.Add(new PlatformPersonRefreshTokenEntity
        {
            PlatformPersonRefreshTokenId = refreshTokenId,
            PlatformPersonId = person.PlatformPersonId,
            PinnedOrganizationId = pinnedAccount.OrganizationId,
            TokenHash = HashToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpires
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        // Тело ответа не меняется ни на байт: клуб, счёт и имя там, где клиент их и ждёт.
        return new PlayerSignInResponse(
            pinnedAccount.PlayerAccountId,
            pinnedAccount.OrganizationId,
            person.DisplayName,
            person.PhoneVerifiedAtUtc is not null,
            accessToken,
            accessExpires,
            refreshToken,
            refreshExpires);
    }

    public async Task<PlayerSignInResponse?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
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

        var account = await FindPinnedAccountAsync(person.PlatformPersonId, stored.PinnedOrganizationId, cancellationToken);
        if (account is null)
        {
            return null;
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
        Guid? pinnedOrganizationId,
        CancellationToken cancellationToken)
    {
        var accounts = dbContext.PlayerAccounts
            .Where(account => account.PlatformPersonId == platformPersonId && account.IsActive);

        return pinnedOrganizationId is null
            ? accounts.OrderBy(account => account.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken)
            : accounts
                .Where(account => account.OrganizationId == pinnedOrganizationId)
                .SingleOrDefaultAsync(cancellationToken);
    }

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
