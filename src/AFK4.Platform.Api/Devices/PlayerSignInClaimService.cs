using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

/// <summary>Итог погашения заявки: токены ПК либо код отказа (PlayerSignInClaimErrorCodeNames).</summary>
public sealed record PlayerSignInClaimRedeemResult(PlatformPersonSessionResponse? Session, string? Error = null);

/// <summary>
/// Вход на ПК с телефона по QR (спека оболочки, §5.4). Телефон заводит заявку по коду с
/// монитора, ПК забирает её ключом устройства и получает токены, привязанные к себе, — номер и
/// ПИН-код у машины не набираются вовсе.
/// </summary>
public sealed class PlayerSignInClaimService(
    PlatformDbContext dbContext,
    IPlatformPersonTokenService tokenService,
    IDeviceBoundPlayerTokens deviceTokens,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Сколько ПК ждёт, чтобы забрать заявку. Сигнал приходит по SignalR за доли секунды, а на
    /// случай обрыва — сердцебиением через 3–10 с. Минута — с запасом на медленную сеть, но не
    /// столько, чтобы человек успел отойти от машины.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    public Task<PlayerSignInClaimEntity?> FindRepeatAsync(
        Guid platformPersonId, Guid organizationId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var keyHash = SessionCommandIdempotencyKeyHasher.Hash(idempotencyKey);
        return dbContext.PlayerSignInClaims
            .AsNoTracking()
            .FirstOrDefaultAsync(
                claim => claim.PlatformPersonId == platformPersonId
                    && claim.OrganizationId == organizationId
                    && claim.IdempotencyKeyHash == keyHash,
                cancellationToken);
    }

    public async Task<PlayerSignInClaimEntity> CreateAsync(
        Guid organizationId,
        Guid branchId,
        Guid deviceId,
        Guid platformPersonId,
        Guid playerAccountId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var claim = new PlayerSignInClaimEntity
        {
            ClaimId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            DeviceId = deviceId,
            PlatformPersonId = platformPersonId,
            PlayerAccountId = playerAccountId,
            IdempotencyKeyHash = SessionCommandIdempotencyKeyHasher.Hash(idempotencyKey),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(Lifetime)
        };
        dbContext.PlayerSignInClaims.Add(claim);
        await dbContext.SaveChangesAsync(cancellationToken);
        return claim;
    }

    public async Task<PlayerSignInClaimDto?> DescribeAsync(
        Guid claimId, Guid platformPersonId, CancellationToken cancellationToken)
    {
        var claim = await dbContext.PlayerSignInClaims
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.ClaimId == claimId && candidate.PlatformPersonId == platformPersonId,
                cancellationToken);
        return claim is null ? null : await ToDtoAsync(claim, cancellationToken);
    }

    public async Task<PlayerSignInClaimDto> ToDtoAsync(PlayerSignInClaimEntity claim, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var status = claim.RedeemedAtUtc is not null
            ? PlayerSignInClaimStatusNames.Redeemed
            : claim.ExpiresAtUtc <= now
                ? PlayerSignInClaimStatusNames.Expired
                : PlayerSignInClaimStatusNames.Pending;

        var seatLabel = await (
            from assignment in dbContext.DeviceSeatAssignments.AsNoTracking()
            join seat in dbContext.Seats.AsNoTracking() on assignment.SeatId equals seat.SeatId
            where assignment.DeviceId == claim.DeviceId && assignment.DetachedAtUtc == null
            orderby assignment.AttachedAtUtc descending
            select seat.Name).FirstOrDefaultAsync(cancellationToken);

        return new PlayerSignInClaimDto(claim.ClaimId, status, claim.ExpiresAtUtc, seatLabel);
    }

    /// <summary>Заявка, которую ПК ещё не забрал, — для сердцебиения, если сигнал потерялся.</summary>
    public async Task<PlayerSignInClaimedDto?> PendingForDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return await dbContext.PlayerSignInClaims
            .AsNoTracking()
            .Where(claim => claim.DeviceId == deviceId && claim.RedeemedAtUtc == null && claim.ExpiresAtUtc > now)
            .OrderByDescending(claim => claim.CreatedAtUtc)
            .Select(claim => new PlayerSignInClaimedDto(claim.ClaimId, claim.ExpiresAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PlayerSignInClaimRedeemResult> RedeemAsync(
        DeviceEntity device, Guid claimId, CancellationToken cancellationToken)
    {
        var claim = await dbContext.PlayerSignInClaims.FirstOrDefaultAsync(
            candidate => candidate.ClaimId == claimId && candidate.DeviceId == device.DeviceId,
            cancellationToken);
        if (claim is null)
        {
            return new PlayerSignInClaimRedeemResult(null, PlayerSignInClaimErrorCodeNames.NotFound);
        }

        if (claim.RedeemedAtUtc is not null)
        {
            return new PlayerSignInClaimRedeemResult(null, PlayerSignInClaimErrorCodeNames.AlreadyRedeemed);
        }

        var now = timeProvider.GetUtcNow();
        if (claim.ExpiresAtUtc <= now)
        {
            return new PlayerSignInClaimRedeemResult(null, PlayerSignInClaimErrorCodeNames.Expired);
        }

        var person = await dbContext.PlatformPersons.SingleOrDefaultAsync(
            candidate => candidate.PlatformPersonId == claim.PlatformPersonId, cancellationToken);
        var account = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
            candidate => candidate.PlayerAccountId == claim.PlayerAccountId, cancellationToken);
        if (person is null || !person.IsActive || account is null || !account.IsActive)
        {
            // Человека отключили или клуб закрыл счёт за ту минуту, что заявка ждала ПК.
            return new PlayerSignInClaimRedeemResult(null, PlayerSignInClaimErrorCodeNames.NotFound);
        }

        // За минуту ожидания за машину мог сесть другой — открывать ему вход заявителя нельзя.
        var liveSessionOwner = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.DeviceId == device.DeviceId
                && (session.State == SessionStateNames.Active
                    || session.State == SessionStateNames.Paused
                    || session.State == SessionStateNames.Ending))
            .Select(session => new { session.PlayerAccountId })
            .FirstOrDefaultAsync(cancellationToken);
        if (liveSessionOwner is not null && liveSessionOwner.PlayerAccountId != account.PlayerAccountId)
        {
            return new PlayerSignInClaimRedeemResult(null, DevicePlayerSignInErrorCodeNames.SessionNotYours);
        }

        claim.RedeemedAtUtc = now;
        await deviceTokens.RevokeForDeviceAsync(device.DeviceId, cancellationToken);
        try
        {
            // Заявка гасится тем же сохранением, что выдаёт токены: либо вошли и заявка забрана,
            // либо ни того ни другого.
            var session = await tokenService.IssueOnDeviceAsync(person, account, device.DeviceId, cancellationToken);
            return new PlayerSignInClaimRedeemResult(session);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new PlayerSignInClaimRedeemResult(null, PlayerSignInClaimErrorCodeNames.AlreadyRedeemed);
        }
    }
}
