using System.Text.Json;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Shared.Contracts.Tests;

public sealed class DeviceEnrollmentContractSerializationTests
{
    [Fact]
    public void DeviceEnrollmentCodeDto_RoundTripsThroughJson()
    {
        var code = new DeviceEnrollmentCodeDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            Code: "AFK4-ABCD-EFGH",
            ExpiresAtUtc: DateTimeOffset.Parse("2026-05-12T00:05:00Z"));

        var json = JsonSerializer.Serialize(code);
        var copy = JsonSerializer.Deserialize<DeviceEnrollmentCodeDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(code.BranchId, copy.BranchId);
        Assert.Equal("AFK4-ABCD-EFGH", copy.Code);
    }

    [Fact]
    public void DeviceEnrollmentRequest_RoundTripsThroughJson()
    {
        var request = new DeviceEnrollmentRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            EnrollmentCode: "AFK4-ABCD-EFGH",
            MachineName: "PC-001",
            AgentVersion: "0.1.0",
            ShellVersion: "0.1.0",
            RequestedAtUtc: DateTimeOffset.Parse("2026-05-12T00:01:00Z"));

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<DeviceEnrollmentRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.OrganizationId, copy.OrganizationId);
        Assert.Equal("PC-001", copy.MachineName);
        Assert.Equal("AFK4-ABCD-EFGH", copy.EnrollmentCode);
    }

    [Fact]
    public void DeviceEnrollmentResponse_RoundTripsThroughJson()
    {
        var response = new DeviceEnrollmentResponse(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            CredentialId: Guid.Parse("4e083a8a-2669-48be-86b5-e44285012f8c"),
            CredentialSecret: "device-secret",
            EnrolledAtUtc: DateTimeOffset.Parse("2026-05-12T00:01:05Z"));

        var json = JsonSerializer.Serialize(response);
        var copy = JsonSerializer.Deserialize<DeviceEnrollmentResponse>(json);

        Assert.NotNull(copy);
        Assert.Equal(response.DeviceId, copy.DeviceId);
        Assert.Equal(response.CredentialId, copy.CredentialId);
        Assert.Equal("device-secret", copy.CredentialSecret);
    }

    [Fact]
    public void DeviceCommandStatusDto_RoundTripsThroughJson()
    {
        var status = new DeviceCommandStatusDto(
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            CommandId: Guid.Parse("63d6536d-f2c5-4379-a8b3-cd487f0c1e94"),
            Type: "lock",
            Status: "Accepted",
            Message: "Command accepted by Agent skeleton.",
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-12T00:01:00Z"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-05-12T00:01:05Z"));

        var json = JsonSerializer.Serialize(status);
        var copy = JsonSerializer.Deserialize<DeviceCommandStatusDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(status.CommandId, copy.CommandId);
        Assert.Equal("Accepted", copy.Status);
        Assert.Equal("Command accepted by Agent skeleton.", copy.Message);
    }

    [Fact]
    public void DeviceCredentialHeaders_AreStable()
    {
        Assert.Equal("X-AFK4-Device-Credential", DeviceCredentialHeaders.CredentialSecret);
    }
}
