using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Devices;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

public sealed class EfDeviceCredentialLifecycleService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IDeviceCredentialLifecycleService
{
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

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RotateDeviceCredentialResponse(
            OrganizationId: device.OrganizationId,
            BranchId: device.BranchId,
            DeviceId: device.DeviceId,
            CredentialId: credentialId,
            CredentialSecret: credentialSecret,
            RotatedAtUtc: now);
    }

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
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RevokeDeviceCredentialResponse(
            OrganizationId: credential.OrganizationId,
            BranchId: credential.BranchId,
            DeviceId: credential.DeviceId,
            CredentialId: credential.CredentialId,
            RevokedAtUtc: credential.RevokedAtUtc.Value);
    }
}
