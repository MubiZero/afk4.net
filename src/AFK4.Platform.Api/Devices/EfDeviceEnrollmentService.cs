using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

public sealed class EfDeviceEnrollmentService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IDeviceEnrollmentService, IDeviceCredentialValidator
{
    public async Task<DeviceEnrollmentCodeDto> CreateEnrollmentCodeAsync(
        Guid branchId,
        CreateDeviceEnrollmentCodeRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var code = await CreateUniqueEnrollmentCodeAsync(cancellationToken);
        var entity = new DeviceEnrollmentCodeEntity
        {
            OrganizationId = request.OrganizationId,
            BranchId = branchId,
            Code = code,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddSeconds(request.ExpiresInSeconds)
        };

        dbContext.DeviceEnrollmentCodes.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeviceEnrollmentCodeDto(
            OrganizationId: entity.OrganizationId,
            BranchId: entity.BranchId,
            Code: entity.Code,
            ExpiresAtUtc: entity.ExpiresAtUtc);
    }

    public async Task<DeviceEnrollmentResult> EnrollAsync(DeviceEnrollmentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EnrollmentCode))
        {
            return DeviceEnrollmentResult.Failure("Enrollment code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.MachineName))
        {
            return DeviceEnrollmentResult.Failure("Machine name is required.");
        }

        var normalizedCode = DeviceCredentialSecrets.NormalizeEnrollmentCode(request.EnrollmentCode);
        var now = timeProvider.GetUtcNow();
        var code = await dbContext.DeviceEnrollmentCodes.SingleOrDefaultAsync(
            candidate => candidate.Code == normalizedCode,
            cancellationToken);

        if (code is null)
        {
            return DeviceEnrollmentResult.Failure("Enrollment code is invalid.");
        }

        if (code.ConsumedAtUtc is not null)
        {
            return DeviceEnrollmentResult.Failure("Enrollment code has already been used.");
        }

        if (code.ExpiresAtUtc <= now)
        {
            return DeviceEnrollmentResult.Failure("Enrollment code has expired.");
        }

        if (code.OrganizationId != request.OrganizationId || code.BranchId != request.BranchId)
        {
            return DeviceEnrollmentResult.Failure("Enrollment code does not match the requested branch.");
        }

        var deviceId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var credentialSecret = DeviceCredentialSecrets.CreateCredentialSecret();

        dbContext.Devices.Add(new DeviceEntity
        {
            DeviceId = deviceId,
            OrganizationId = request.OrganizationId,
            BranchId = request.BranchId,
            MachineName = request.MachineName,
            DisplayName = request.MachineName,
            Role = DeviceRoleNames.GamingPc,
            EnrollmentState = DeviceEnrollmentStateNames.Approved,
            AgentVersion = request.AgentVersion,
            ShellVersion = request.ShellVersion,
            EnrolledAtUtc = now
        });

        dbContext.DeviceCredentials.Add(new DeviceCredentialEntity
        {
            CredentialId = credentialId,
            OrganizationId = request.OrganizationId,
            BranchId = request.BranchId,
            DeviceId = deviceId,
            SecretHash = DeviceCredentialSecrets.HashSecret(credentialSecret),
            CreatedAtUtc = now
        });

        code.ConsumedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return DeviceEnrollmentResult.Success(new DeviceEnrollmentResponse(
            OrganizationId: request.OrganizationId,
            BranchId: request.BranchId,
            DeviceId: deviceId,
            CredentialId: credentialId,
            CredentialSecret: credentialSecret,
            EnrolledAtUtc: now));
    }

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

        var credential = (
            from candidate in dbContext.DeviceCredentials.AsNoTracking()
            join device in dbContext.Devices.AsNoTracking()
                on candidate.DeviceId equals device.DeviceId
            where candidate.OrganizationId == organizationId &&
                candidate.BranchId == branchId &&
                candidate.DeviceId == deviceId &&
                candidate.RevokedAtUtc == null &&
                device.OrganizationId == organizationId &&
                device.BranchId == branchId &&
                (!requireApproved || device.EnrollmentState == DeviceEnrollmentStateNames.Approved)
            select candidate)
            .AsNoTracking()
            .SingleOrDefault();

        return credential is not null &&
            DeviceCredentialSecrets.SecretMatches(credential.SecretHash, credentialSecret);
    }

    private async Task<string> CreateUniqueEnrollmentCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = DeviceCredentialSecrets.CreateEnrollmentCode();
            var exists = await dbContext.DeviceEnrollmentCodes.AnyAsync(
                candidate => candidate.Code == code,
                cancellationToken);

            if (!exists)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Could not create a unique device enrollment code.");
    }
}
