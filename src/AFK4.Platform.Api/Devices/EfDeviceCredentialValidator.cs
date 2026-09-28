using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Install;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

public sealed class EfDeviceCredentialValidator(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IDeviceCredentialValidator
{
    public bool Validate(Guid organizationId, Guid branchId, Guid deviceId, string? credentialSecret)
    {
        return ValidateCore(organizationId, branchId, deviceId, credentialSecret, requireApproved: false);
    }

    public bool ValidateApproved(Guid organizationId, Guid branchId, Guid deviceId, string? credentialSecret)
    {
        return ValidateCore(organizationId, branchId, deviceId, credentialSecret, requireApproved: true);
    }

    private bool ValidateCore(
        Guid organizationId,
        Guid branchId,
        Guid deviceId,
        string? credentialSecret,
        bool requireApproved)
    {
        if (string.IsNullOrWhiteSpace(credentialSecret))
        {
            return false;
        }

        // Живых ключей у машины бывает два, а не один: после перевыпуска старый ещё некоторое
        // время принимается, чтобы ПК, выключившийся в момент замены, не остался без входа
        // (см. DeviceCredentialEntity.ExpiresAtUtc). Поэтому берём все подходящие и сверяем
        // предъявленный с каждым, а не требуем ровно одного.
        var now = timeProvider.GetUtcNow();
        var credentials = (
            from candidate in dbContext.DeviceCredentials.AsNoTracking()
            join device in dbContext.Devices.AsNoTracking()
                on candidate.DeviceId equals device.DeviceId
            where candidate.OrganizationId == organizationId &&
                candidate.BranchId == branchId &&
                candidate.DeviceId == deviceId &&
                candidate.RevokedAtUtc == null &&
                (candidate.ExpiresAtUtc == null || candidate.ExpiresAtUtc > now) &&
                device.OrganizationId == organizationId &&
                device.BranchId == branchId &&
                (!requireApproved || device.EnrollmentState == DeviceEnrollmentStateNames.Approved)
            select candidate)
            .AsNoTracking()
            .ToList();

        return credentials.Any(candidate =>
            DeviceCredentialSecrets.SecretMatches(candidate.SecretHash, credentialSecret));
    }
}
