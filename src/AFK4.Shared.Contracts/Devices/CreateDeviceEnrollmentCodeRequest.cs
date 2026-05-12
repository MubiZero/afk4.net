namespace AFK4.Shared.Contracts.Devices;

public sealed record CreateDeviceEnrollmentCodeRequest(
    Guid OrganizationId,
    int ExpiresInSeconds);
