namespace AFK4.GamingPc.Setup.Core;

using AFK4.Shared.Contracts.Devices;

public sealed class GamingPcSetupOrchestrator(
    ISetupApiClient apiClient,
    IGamingPcMsiInstaller msiInstaller,
    IAgentMachineConfigurationWriter configurationWriter,
    IWindowsServiceController serviceController,
    TimeProvider timeProvider,
    TimeSpan? heartbeatTimeout = null,
    TimeSpan? heartbeatPollInterval = null)
{
    private readonly TimeSpan heartbeatTimeout = heartbeatTimeout ?? TimeSpan.FromSeconds(45);
    private readonly TimeSpan heartbeatPollInterval = heartbeatPollInterval ?? TimeSpan.FromSeconds(3);

    public async Task<SetupInstallationResult> InstallAsync(
        SetupRequest request,
        IProgress<SetupStepResult>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MachineName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LeaseSigningPublicKeyPem);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UpdatePackageSigningPublicKeyPem);

        var steps = new List<SetupStepResult>();

        async Task<bool> RunStepAsync(string step, string runningMessage, Func<Task<SetupStepResult>> action)
        {
            Add(SetupStepResult.Running(step, runningMessage));

            try
            {
                Add(await action());
                return steps[^1].Status is SetupStepStatus.Succeeded or SetupStepStatus.Partial;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Add(SetupStepResult.Failed(step, $"{step} failed.", ex.Message));
                return false;
            }
        }

        if (!await RunStepAsync("Health", "Checking staging API health.", async () =>
        {
            await apiClient.CheckHealthAsync(cancellationToken);
            return SetupStepResult.Succeeded("Health", "Staging API is reachable.");
        }))
        {
            return Failed();
        }

        string accessToken = string.Empty;
        if (!await RunStepAsync("SignIn", "Signing in staff user.", async () =>
        {
            var session = await apiClient.SignInAsync(request.UserName, request.Password, cancellationToken);
            accessToken = session.AccessToken;
            return SetupStepResult.Succeeded("SignIn", $"Signed in as {session.DisplayName}.");
        }))
        {
            return Failed();
        }

        string enrollmentCode = string.Empty;
        if (!await RunStepAsync("EnrollmentCode", "Creating short-lived enrollment code.", async () =>
        {
            var code = await apiClient.CreateEnrollmentCodeAsync(accessToken, cancellationToken);
            enrollmentCode = code.Code;
            return SetupStepResult.Succeeded("EnrollmentCode", "Enrollment code created.");
        }))
        {
            return Failed();
        }

        DeviceEnrollmentResponse? enrollment = null;
        if (!await RunStepAsync("Enroll", "Enrolling this machine.", async () =>
        {
            enrollment = await apiClient.EnrollDeviceAsync(enrollmentCode, request.MachineName, cancellationToken);
            return SetupStepResult.Succeeded("Enroll", "Device enrolled.", enrollment.DeviceId.ToString("D"));
        }))
        {
            return Failed();
        }

        if (!await RunStepAsync("AssignSeat", "Assigning device to staging smoke seat.", async () =>
        {
            var assignment = await apiClient.AssignDeviceToSmokeSeatAsync(accessToken, enrollment!.DeviceId, cancellationToken);
            return SetupStepResult.Succeeded("AssignSeat", "Device assigned to staging smoke seat.", assignment.SeatId.ToString("D"));
        }))
        {
            return Failed(enrollment?.DeviceId);
        }

        if (!await RunStepAsync("InstallMsi", "Installing Gaming PC client.", async () =>
        {
            serviceController.StopIfExists(StagingSetupDefaults.AgentServiceName);
            await msiInstaller.InstallAsync(cancellationToken);
            serviceController.StopIfExists(StagingSetupDefaults.AgentServiceName);
            return SetupStepResult.Succeeded("InstallMsi", "Gaming PC client installed.");
        }))
        {
            return Failed(enrollment?.DeviceId);
        }

        if (!await RunStepAsync("ConfigureAgent", "Writing Agent machine configuration.", () =>
        {
            configurationWriter.Write(request, enrollment!);
            return Task.FromResult(SetupStepResult.Succeeded("ConfigureAgent", "Agent configuration written."));
        }))
        {
            return Failed(enrollment?.DeviceId);
        }

        if (!await RunStepAsync("StartService", "Starting AFK4.NET Agent Service.", () =>
        {
            serviceController.Start(StagingSetupDefaults.AgentServiceName);
            var status = serviceController.QueryStatus(StagingSetupDefaults.AgentServiceName);
            return Task.FromResult(SetupStepResult.Succeeded("StartService", $"Service status: {status}."));
        }))
        {
            return Failed(enrollment?.DeviceId);
        }

        if (!await RunStepAsync("Heartbeat", "Waiting for backend heartbeat evidence.", async () =>
        {
            var deadline = timeProvider.GetUtcNow().Add(this.heartbeatTimeout);
            while (timeProvider.GetUtcNow() < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var detail = await apiClient.GetDeviceDetailAsync(accessToken, enrollment!.DeviceId, cancellationToken);
                if (detail.IsOnline && detail.LastHeartbeatAtUtc is not null)
                {
                    return SetupStepResult.Succeeded("Heartbeat", "Backend reports the device online.", detail.LastHeartbeatAtUtc.Value.ToString("O"));
                }

                await Task.Delay(this.heartbeatPollInterval, timeProvider, cancellationToken);
            }

            return SetupStepResult.Partial("Heartbeat", "Service started, but backend heartbeat was not observed before timeout.");
        }))
        {
            return Failed(enrollment?.DeviceId);
        }

        var finalStatus = steps.Any(step => step.Status == SetupStepStatus.Partial)
            ? SetupStepStatus.Partial
            : SetupStepStatus.Succeeded;

        return new SetupInstallationResult(finalStatus, enrollment?.DeviceId, steps);

        void Add(SetupStepResult result)
        {
            steps.Add(result);
            progress?.Report(result);
        }

        SetupInstallationResult Failed(Guid? deviceId = null) =>
            new(SetupStepStatus.Failed, deviceId, steps);
    }
}
