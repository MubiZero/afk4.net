namespace AFK4.Platform.Api.Devices;

public interface IDeviceCredentialValidator
{
    bool Validate(Guid organizationId, Guid branchId, Guid deviceId, string? credentialSecret);
}
