using System.Text.Json;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Shared.Contracts.Tests;

public sealed class UpdateContractSerializationTests
{
    [Fact]
    public void UpdatePackage_RoundTripsSignedPackageMetadata()
    {
        var request = new CreateUpdatePackageRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            Component: UpdateComponentNames.AgentService,
            Version: "1.2.3",
            Channel: UpdateChannelNames.Stable,
            ArtifactUri: "https://updates.afk4.test/agent/1.2.3/agent.msi",
            Sha256: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            Signature: "base64-signature",
            SignatureAlgorithm: "ed25519",
            SizeBytes: 42_000_000,
            ReleaseNotes: "Signed Agent Service update.");
        var package = new UpdatePackageDto(
            UpdatePackageId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: request.OrganizationId,
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            Component: request.Component,
            Version: request.Version,
            Channel: request.Channel,
            ArtifactUri: request.ArtifactUri,
            Sha256: request.Sha256,
            Signature: request.Signature,
            SignatureAlgorithm: request.SignatureAlgorithm,
            SizeBytes: request.SizeBytes,
            State: UpdatePackageStateNames.Registered,
            ReleaseNotes: request.ReleaseNotes,
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-14T12:00:00Z"));

        var requestCopy = JsonSerializer.Deserialize<CreateUpdatePackageRequest>(JsonSerializer.Serialize(request));
        var packageCopy = JsonSerializer.Deserialize<UpdatePackageDto>(JsonSerializer.Serialize(package));

        Assert.NotNull(requestCopy);
        Assert.NotNull(packageCopy);
        Assert.Equal(UpdateComponentNames.AgentService, requestCopy.Component);
        Assert.Equal(UpdateChannelNames.Stable, packageCopy.Channel);
        Assert.Equal(UpdatePackageStateNames.Registered, packageCopy.State);
        Assert.Equal("ed25519", packageCopy.SignatureAlgorithm);
    }

    [Fact]
    public void UpdateRollout_RoundTripsBranchAndDeviceTargeting()
    {
        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
        var request = new CreateUpdateRolloutRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            UpdatePackageId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            Channel: UpdateChannelNames.Beta,
            TargetKind: UpdateTargetKindNames.Device,
            TargetDeviceIds: [deviceId],
            BatchPercent: 25,
            StartsAtUtc: DateTimeOffset.Parse("2026-05-14T12:30:00Z"),
            Reason: "Beta validation on one row.");
        var rollout = new UpdateRolloutDto(
            UpdateRolloutId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            OrganizationId: request.OrganizationId,
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            UpdatePackageId: request.UpdatePackageId,
            Component: UpdateComponentNames.AgentService,
            Version: "1.2.3",
            Channel: request.Channel,
            State: UpdateRolloutStateNames.Active,
            TargetKind: request.TargetKind,
            TargetDeviceIds: request.TargetDeviceIds,
            BatchPercent: request.BatchPercent,
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-14T12:05:00Z"),
            StartsAtUtc: request.StartsAtUtc,
            CompletedAtUtc: null);

        var requestCopy = JsonSerializer.Deserialize<CreateUpdateRolloutRequest>(JsonSerializer.Serialize(request));
        var rolloutCopy = JsonSerializer.Deserialize<UpdateRolloutDto>(JsonSerializer.Serialize(rollout));

        Assert.NotNull(requestCopy);
        Assert.NotNull(rolloutCopy);
        Assert.Equal(UpdateTargetKindNames.Device, requestCopy.TargetKind);
        Assert.Equal(deviceId, requestCopy.TargetDeviceIds.Single());
        Assert.Equal(UpdateRolloutStateNames.Active, rolloutCopy.State);
        Assert.Equal(25, rolloutCopy.BatchPercent);
    }

    [Fact]
    public void DeviceUpdateCheck_RoundTripsAvailableUpdateInstruction()
    {
        var check = new DeviceUpdateCheckRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            Channel: UpdateChannelNames.Internal,
            CheckedAtUtc: DateTimeOffset.Parse("2026-05-14T12:10:00Z"),
            InstalledComponents:
            [
                new DeviceComponentVersionDto(UpdateComponentNames.AgentService, "1.2.2"),
                new DeviceComponentVersionDto(UpdateComponentNames.PlayerShell, "1.2.2")
            ]);
        var response = new DeviceUpdateCheckResponse(
            ServerTimeUtc: DateTimeOffset.Parse("2026-05-14T12:10:01Z"),
            Updates:
            [
                new ComponentUpdateInstructionDto(
                    UpdateRolloutId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
                    UpdatePackageId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
                    Component: UpdateComponentNames.AgentService,
                    Version: "1.2.3",
                    Channel: UpdateChannelNames.Internal,
                    ArtifactUri: "https://updates.afk4.test/agent/1.2.3/agent.msi",
                    Sha256: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                    Signature: "base64-signature",
                    SignatureAlgorithm: "ed25519",
                    SizeBytes: 42_000_000,
                    ReleaseNotes: "Internal rollout.")
            ]);

        var checkCopy = JsonSerializer.Deserialize<DeviceUpdateCheckRequest>(JsonSerializer.Serialize(check));
        var responseCopy = JsonSerializer.Deserialize<DeviceUpdateCheckResponse>(JsonSerializer.Serialize(response));

        Assert.NotNull(checkCopy);
        Assert.NotNull(responseCopy);
        Assert.Equal(UpdateChannelNames.Internal, checkCopy.Channel);
        Assert.Equal("1.2.2", checkCopy.InstalledComponents[0].Version);
        Assert.Single(responseCopy.Updates);
        Assert.Equal(UpdateComponentNames.AgentService, responseCopy.Updates[0].Component);
        Assert.Equal("1.2.3", responseCopy.Updates[0].Version);
    }

    [Fact]
    public void DeviceUpdateStatus_RoundTripsReportAndResult()
    {
        var report = new DeviceUpdateStatusReportRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            UpdateRolloutId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            UpdatePackageId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            Component: UpdateComponentNames.AgentService,
            InstalledVersion: "1.2.2",
            TargetVersion: "1.2.3",
            Status: UpdateStatusNames.Downloading,
            Message: "Download started.",
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-14T12:11:00Z"));
        var result = new DeviceUpdateStatusResultDto(
            DeviceId: report.DeviceId,
            UpdateRolloutId: report.UpdateRolloutId,
            UpdatePackageId: report.UpdatePackageId,
            Component: report.Component,
            Status: report.Status,
            Message: report.Message,
            UpdatedAtUtc: report.ObservedAtUtc);

        var reportCopy = JsonSerializer.Deserialize<DeviceUpdateStatusReportRequest>(JsonSerializer.Serialize(report));
        var resultCopy = JsonSerializer.Deserialize<DeviceUpdateStatusResultDto>(JsonSerializer.Serialize(result));

        Assert.NotNull(reportCopy);
        Assert.NotNull(resultCopy);
        Assert.Equal(UpdateStatusNames.Downloading, reportCopy.Status);
        Assert.Equal(report.DeviceId, resultCopy.DeviceId);
        Assert.Equal("Download started.", resultCopy.Message);
    }

    [Fact]
    public void UpdateLifecycleRequests_RoundTripRequestedStateAndReason()
    {
        var packageRequest = new UpdatePackageStateChangeRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            State: UpdatePackageStateNames.Validated,
            Reason: "Signature and hash verified.");
        var rolloutRequest = new UpdateRolloutStateChangeRequest(
            OrganizationId: packageRequest.OrganizationId,
            State: UpdateRolloutStateNames.RollbackRequested,
            Reason: "Install failure rate exceeded threshold.");

        var packageCopy = JsonSerializer.Deserialize<UpdatePackageStateChangeRequest>(
            JsonSerializer.Serialize(packageRequest));
        var rolloutCopy = JsonSerializer.Deserialize<UpdateRolloutStateChangeRequest>(
            JsonSerializer.Serialize(rolloutRequest));

        Assert.NotNull(packageCopy);
        Assert.NotNull(rolloutCopy);
        Assert.Equal(UpdatePackageStateNames.Validated, packageCopy.State);
        Assert.Equal("Signature and hash verified.", packageCopy.Reason);
        Assert.Equal(UpdateRolloutStateNames.RollbackRequested, rolloutCopy.State);
        Assert.Equal("Install failure rate exceeded threshold.", rolloutCopy.Reason);
    }
}
