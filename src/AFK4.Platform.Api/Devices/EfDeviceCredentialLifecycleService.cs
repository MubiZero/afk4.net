using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

public sealed class EfDeviceCredentialLifecycleService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    IDeviceBoundPlayerTokens? deviceTokens = null) : IDeviceCredentialLifecycleService
{
    /// <summary>
    /// Сколько ещё принимается старый ключ после самоперевыпуска. Пятнадцать минут — это запас
    /// на выключенный в неудачный момент ПК, а не срок жизни: как только агент предъявит новый,
    /// старым всё равно никто не пользуется.
    /// </summary>
    private static readonly TimeSpan OverlapWindow = TimeSpan.FromMinutes(15);

    public async Task<RotateDeviceCredentialResponse?> RotateAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices.SingleOrDefaultAsync(
            candidate => candidate.DeviceId == deviceId,
            cancellationToken);

        if (device is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var activeCredentials = await dbContext.DeviceCredentials
            .Where(candidate => candidate.DeviceId == deviceId && candidate.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var credential in activeCredentials)
        {
            credential.RevokedAtUtc = now;
        }

        // Принудительная смена ключа — это «машине больше не верим как прежде». Вход игрока,
        // выданный под старым ключом, гаснет вместе с ним.
        if (deviceTokens is not null)
        {
            await deviceTokens.RevokeForDeviceAsync(deviceId, cancellationToken);
        }

        var credentialId = Guid.NewGuid();
        var credentialSecret = DeviceCredentialSecrets.CreateCredentialSecret();
        dbContext.DeviceCredentials.Add(new DeviceCredentialEntity
        {
            CredentialId = credentialId,
            OrganizationId = device.OrganizationId,
            BranchId = device.BranchId,
            DeviceId = device.DeviceId,
            SecretHash = DeviceCredentialSecrets.HashSecret(credentialSecret),
            CreatedAtUtc = now
        });

        // Ключ уже сменили жёстко — просить машину сменить его самой больше незачем.
        device.CredentialRotationRequestedAtUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RotateDeviceCredentialResponse(
            OrganizationId: device.OrganizationId,
            BranchId: device.BranchId,
            DeviceId: device.DeviceId,
            CredentialId: credentialId,
            CredentialSecret: credentialSecret,
            RotatedAtUtc: now);
    }

    public async Task<bool> RequestRotationAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices.SingleOrDefaultAsync(
            candidate => candidate.DeviceId == deviceId,
            cancellationToken);
        if (device is null)
        {
            return false;
        }

        // Ключи не трогаем: машина работает как работала, пока сама не сменит ключ. Просьба
        // ставится один раз — повтор ничего не портит и ничего не двигает.
        device.CredentialRotationRequestedAtUtc ??= timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<RotateDeviceCredentialResponse?> RotateForAgentAsync(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices.SingleOrDefaultAsync(
            candidate => candidate.DeviceId == deviceId,
            cancellationToken);
        if (device is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var activeCredentials = await dbContext.DeviceCredentials
            .Where(candidate => candidate.DeviceId == deviceId
                && candidate.RevokedAtUtc == null
                && (candidate.ExpiresAtUtc == null || candidate.ExpiresAtUtc > now))
            .ToListAsync(cancellationToken);

        foreach (var credential in activeCredentials)
        {
            // Не отзываем, а даём дожить: между этим ответом и записью нового ключа на диск ПК
            // может выключиться, и тогда единственный оставшийся у него ключ — старый.
            credential.ExpiresAtUtc = Earliest(credential.ExpiresAtUtc, now + OverlapWindow);
        }

        var credentialId = Guid.NewGuid();
        var credentialSecret = DeviceCredentialSecrets.CreateCredentialSecret();
        dbContext.DeviceCredentials.Add(new DeviceCredentialEntity
        {
            CredentialId = credentialId,
            OrganizationId = device.OrganizationId,
            BranchId = device.BranchId,
            DeviceId = device.DeviceId,
            SecretHash = DeviceCredentialSecrets.HashSecret(credentialSecret),
            CreatedAtUtc = now
        });

        // Просьба выполнена — иначе агент менял бы ключ на каждом сердцебиении.
        device.CredentialRotationRequestedAtUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RotateDeviceCredentialResponse(
            OrganizationId: device.OrganizationId,
            BranchId: device.BranchId,
            DeviceId: device.DeviceId,
            CredentialId: credentialId,
            CredentialSecret: credentialSecret,
            RotatedAtUtc: now);
    }

    private static DateTimeOffset Earliest(DateTimeOffset? current, DateTimeOffset candidate) =>
        current is null || candidate < current.Value ? candidate : current.Value;

    public async Task<RevokeDeviceCredentialResponse?> RevokeAsync(
        Guid deviceId,
        Guid credentialId,
        CancellationToken cancellationToken)
    {
        var credential = await dbContext.DeviceCredentials.SingleOrDefaultAsync(
            candidate => candidate.DeviceId == deviceId && candidate.CredentialId == credentialId,
            cancellationToken);

        if (credential is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        credential.RevokedAtUtc ??= now;
        if (deviceTokens is not null)
        {
            await deviceTokens.RevokeForDeviceAsync(deviceId, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RevokeDeviceCredentialResponse(
            OrganizationId: credential.OrganizationId,
            BranchId: credential.BranchId,
            DeviceId: credential.DeviceId,
            CredentialId: credential.CredentialId,
            RevokedAtUtc: credential.RevokedAtUtc.Value);
    }
}
