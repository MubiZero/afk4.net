using System.Security.Cryptography;
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

/// <summary>Код посадки, выданный ПК и предъявленный человеком.</summary>
public sealed record IssuedSeatingCode(string Code, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Выдаёт простаивающему ПК код для монитора и находит по коду ту машину, перед которой стоит
/// человек.
///
/// Пока код жив, ПК получает один и тот же: перерисовывать монитор на каждый вопрос значит
/// заставить человека набирать движущуюся мишень.
/// </summary>
public sealed class EfSeatingCodeService(PlatformDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<IssuedSeatingCode?> IssueAsync(
        Guid organizationId, Guid deviceId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var existing = await dbContext.DeviceSeatingCodes
            .FirstOrDefaultAsync(row => row.DeviceId == deviceId, cancellationToken);

        if (existing is not null && existing.ExpiresAtUtc > now)
        {
            return new IssuedSeatingCode(existing.Code, existing.ExpiresAtUtc);
        }

        var code = await NextFreeCodeAsync(organizationId, now, cancellationToken);
        var expiresAtUtc = now.Add(SeatingCodePolicy.Lifetime);

        if (existing is null)
        {
            dbContext.DeviceSeatingCodes.Add(new DeviceSeatingCodeEntity
            {
                DeviceId = deviceId,
                OrganizationId = organizationId,
                Code = code,
                ExpiresAtUtc = expiresAtUtc,
                CreatedAtUtc = now
            });
        }
        else
        {
            existing.OrganizationId = organizationId;
            existing.Code = code;
            existing.ExpiresAtUtc = expiresAtUtc;
            existing.CreatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new IssuedSeatingCode(code, expiresAtUtc);
    }

    /// <summary>
    /// Машина, показывающая этот код прямо сейчас, или <c>null</c>. Клуб проверяется вместе с
    /// кодом: шестизначных кодов немного, и в сети из двадцати клубов совпадения — вопрос
    /// времени, а не удачи.
    /// </summary>
    public async Task<Guid?> RedeemAsync(
        Guid organizationId, string? typedCode, CancellationToken cancellationToken)
    {
        var code = SeatingCodePolicy.Normalize(typedCode);
        if (code.Length != SeatingCodePolicy.Digits)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var match = await dbContext.DeviceSeatingCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.OrganizationId == organizationId
                    && row.Code == code
                    && row.ExpiresAtUtc > now,
                cancellationToken);

        return match?.DeviceId;
    }

    /// <summary>
    /// Код, которого сейчас не показывает ни одна живая машина этого клуба. Два ПК с одним кодом
    /// значат, что человек сядет не за тот, — а это ровно то, от чего код и заводился.
    /// </summary>
    private async Task<string> NextFreeCodeAsync(
        Guid organizationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var taken = await dbContext.DeviceSeatingCodes
            .AsNoTracking()
            .Where(row => row.OrganizationId == organizationId && row.ExpiresAtUtc > now)
            .Select(row => row.Code)
            .ToListAsync(cancellationToken);

        var busy = taken.ToHashSet(StringComparer.Ordinal);
        for (var attempt = 0; attempt < 64; attempt++)
        {
            var candidate = RandomCode();
            if (!busy.Contains(candidate))
            {
                return candidate;
            }
        }

        // Столько занятых шестизначных кодов в одном клубе быть не может: это тысячи машин,
        // одновременно ждущих посадки. Молчать про такое нельзя.
        throw new InvalidOperationException("Could not find a free seating code for the club.");
    }

    private static string RandomCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString($"D{SeatingCodePolicy.Digits}");
}
