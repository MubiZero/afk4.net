using System.Security.Cryptography;
using System.Text;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Devices;

public sealed class InMemoryDeviceEnrollmentService(TimeProvider timeProvider) : IDeviceEnrollmentService, IDeviceCredentialValidator
{
    private readonly object gate = new();
    private readonly Dictionary<string, EnrollmentCodeRecord> enrollmentCodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, DeviceCredentialRecord> credentialsByDeviceId = new();

    public Task<DeviceEnrollmentCodeDto> CreateEnrollmentCodeAsync(
        Guid branchId,
        CreateDeviceEnrollmentCodeRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var code = CreateEnrollmentCode();
        var record = new EnrollmentCodeRecord(
            OrganizationId: request.OrganizationId,
            BranchId: branchId,
            Code: code,
            ExpiresAtUtc: now.AddSeconds(request.ExpiresInSeconds),
            Consumed: false);

        lock (gate)
        {
            enrollmentCodes[code] = record;
        }

        return Task.FromResult(new DeviceEnrollmentCodeDto(
            OrganizationId: record.OrganizationId,
            BranchId: record.BranchId,
            Code: record.Code,
            ExpiresAtUtc: record.ExpiresAtUtc));
    }

    public Task<DeviceEnrollmentResult> EnrollAsync(DeviceEnrollmentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EnrollmentCode))
        {
            return Task.FromResult(DeviceEnrollmentResult.Failure("Enrollment code is required."));
        }

        if (string.IsNullOrWhiteSpace(request.MachineName))
        {
            return Task.FromResult(DeviceEnrollmentResult.Failure("Machine name is required."));
        }

        var now = timeProvider.GetUtcNow();
        lock (gate)
        {
            if (!enrollmentCodes.TryGetValue(request.EnrollmentCode, out var code))
            {
                return Task.FromResult(DeviceEnrollmentResult.Failure("Enrollment code is invalid."));
            }

            if (code.Consumed)
            {
                return Task.FromResult(DeviceEnrollmentResult.Failure("Enrollment code has already been used."));
            }

            if (code.ExpiresAtUtc <= now)
            {
                return Task.FromResult(DeviceEnrollmentResult.Failure("Enrollment code has expired."));
            }

            if (code.OrganizationId != request.OrganizationId || code.BranchId != request.BranchId)
            {
                return Task.FromResult(DeviceEnrollmentResult.Failure("Enrollment code does not match the requested branch."));
            }

            var deviceId = Guid.NewGuid();
            var credentialId = Guid.NewGuid();
            var credentialSecret = CreateCredentialSecret();
            credentialsByDeviceId[deviceId] = new DeviceCredentialRecord(
                OrganizationId: request.OrganizationId,
                BranchId: request.BranchId,
                DeviceId: deviceId,
                CredentialId: credentialId,
                SecretHash: HashSecret(credentialSecret),
                Revoked: false);

            enrollmentCodes[request.EnrollmentCode] = code with { Consumed = true };

            var response = new DeviceEnrollmentResponse(
                OrganizationId: request.OrganizationId,
                BranchId: request.BranchId,
                DeviceId: deviceId,
                CredentialId: credentialId,
                CredentialSecret: credentialSecret,
                EnrolledAtUtc: now);

            return Task.FromResult(DeviceEnrollmentResult.Success(response));
        }
    }

    public bool Validate(Guid organizationId, Guid branchId, Guid deviceId, string? credentialSecret)
    {
        return ValidateApproved(organizationId, branchId, deviceId, credentialSecret);
    }

    public bool ValidateApproved(Guid organizationId, Guid branchId, Guid deviceId, string? credentialSecret)
    {
        if (string.IsNullOrWhiteSpace(credentialSecret))
        {
            return false;
        }

        DeviceCredentialRecord credential;
        lock (gate)
        {
            if (!credentialsByDeviceId.TryGetValue(deviceId, out var candidate))
            {
                return false;
            }

            credential = candidate;
        }

        if (credential.Revoked ||
            credential.OrganizationId != organizationId ||
            credential.BranchId != branchId)
        {
            return false;
        }

        var presentedHash = HashSecret(credentialSecret);
        return CryptographicOperations.FixedTimeEquals(credential.SecretHash, presentedHash);
    }

    private static string CreateEnrollmentCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        var value = Convert.ToHexString(bytes);
        return $"AFK4-{value[..4]}-{value[4..]}";
    }

    private static string CreateCredentialSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var value = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return $"afk4_{value}";
    }

    private static byte[] HashSecret(string secret)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    private sealed record EnrollmentCodeRecord(
        Guid OrganizationId,
        Guid BranchId,
        string Code,
        DateTimeOffset ExpiresAtUtc,
        bool Consumed);

    private sealed record DeviceCredentialRecord(
        Guid OrganizationId,
        Guid BranchId,
        Guid DeviceId,
        Guid CredentialId,
        byte[] SecretHash,
        bool Revoked);
}
