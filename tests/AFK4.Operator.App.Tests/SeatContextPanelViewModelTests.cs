using AFK4.Operator.App.FloorMap;
using AFK4.Operator.App.Mvvm;
using AFK4.Operator.App.Sessions;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Operator.App.Tests;

public sealed class SeatContextPanelViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid ActiveSessionId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid TargetSeatId = Guid.Parse("55555555-5555-4555-8555-555555555555");

    [Fact]
    public async Task StartGuestSessionAsync_SendsBackendCommandAndMarksPending()
    {
        var apiClient = new RecordingSessionApiClient();
        var keyFactory = new FixedIdempotencyKeyFactory("session-start-001");
        var selectedSeat = FloorMapSeatViewModel.FromDto(Seat("PC-001", state: "Free"));
        var panel = new SeatContextPanelViewModel(apiClient, keyFactory);
        panel.ApplyContext(OrganizationId, BranchId);
        panel.SelectSeat(selectedSeat);
        panel.DurationMinutes = 60;
        panel.BillingMode = BillingModeNames.PostpaidDebt;

        await panel.StartGuestSessionAsync(CancellationToken.None);

        Assert.Equal(selectedSeat.SeatId, apiClient.LastStartRequest?.SeatId);
        Assert.Equal("session-start-001", apiClient.LastStartRequest?.IdempotencyKey);
        Assert.Equal(BillingModeNames.PostpaidDebt, apiClient.LastStartRequest?.BillingMode);
        Assert.Equal(OrganizationId, apiClient.LastStartRequest?.OrganizationId);
        Assert.Equal(BranchId, apiClient.LastBranchId);
        Assert.Equal("Waiting for backend confirmation", panel.PendingOperation);
        Assert.Equal("Session command accepted.", panel.StatusMessage);
        Assert.Null(panel.ErrorMessage);
    }

    [Fact]
    public async Task ExtendSessionAsync_SendsAdditionalMinutesForActiveSeat()
    {
        var apiClient = new RecordingSessionApiClient();
        var panel = new SeatContextPanelViewModel(apiClient, new FixedIdempotencyKeyFactory("session-extend-001"));
        panel.SelectSeat(FloorMapSeatViewModel.FromDto(Seat("PC-002", state: "Active")));
        panel.AdditionalMinutes = 30;
        panel.BillingMode = BillingModeNames.PrepaidWallet;

        await panel.ExtendSessionAsync(CancellationToken.None);

        Assert.Equal(ActiveSessionId, apiClient.LastExtendSessionId);
        Assert.Equal(30, apiClient.LastExtendRequest?.AdditionalMinutes);
        Assert.Equal("session-extend-001", apiClient.LastExtendRequest?.IdempotencyKey);
        Assert.Equal("Session command accepted.", panel.StatusMessage);
        Assert.Null(panel.ErrorMessage);
    }

    [Fact]
    public async Task TransferSessionAsync_SendsTargetSeatForActiveSeat()
    {
        var apiClient = new RecordingSessionApiClient();
        var panel = new SeatContextPanelViewModel(apiClient, new FixedIdempotencyKeyFactory("session-transfer-001"));
        panel.SelectSeat(FloorMapSeatViewModel.FromDto(Seat("PC-003", state: "Active")));
        panel.TargetSeatIdText = TargetSeatId.ToString("D");

        await panel.TransferSessionAsync(CancellationToken.None);

        Assert.Equal(ActiveSessionId, apiClient.LastTransferSessionId);
        Assert.Equal(TargetSeatId, apiClient.LastTransferRequest?.TargetSeatId);
        Assert.Equal("session-transfer-001", apiClient.LastTransferRequest?.IdempotencyKey);
        Assert.Equal("Session command accepted.", panel.StatusMessage);
        Assert.Null(panel.ErrorMessage);
    }

    [Fact]
    public async Task EndSessionAsync_RequiresActiveSession()
    {
        var panel = new SeatContextPanelViewModel(new RecordingSessionApiClient(), new FixedIdempotencyKeyFactory("end-001"));
        panel.SelectSeat(FloorMapSeatViewModel.FromDto(Seat("PC-004", state: "Free")));

        await panel.EndSessionAsync(CancellationToken.None);

        Assert.Equal("Selected seat has no active session.", panel.ErrorMessage);
        Assert.Null(panel.PendingOperation);
    }

    [Fact]
    public async Task EndSessionAsync_SendsReasonForActiveSeat()
    {
        var apiClient = new RecordingSessionApiClient();
        var panel = new SeatContextPanelViewModel(apiClient, new FixedIdempotencyKeyFactory("session-end-001"));
        panel.SelectSeat(FloorMapSeatViewModel.FromDto(Seat("PC-005", state: "Active")));
        panel.Reason = "operator close";

        await panel.EndSessionAsync(CancellationToken.None);

        Assert.Equal(ActiveSessionId, apiClient.LastEndSessionId);
        Assert.Equal("operator close", apiClient.LastEndRequest?.Reason);
        Assert.Equal("session-end-001", apiClient.LastEndRequest?.IdempotencyKey);
        Assert.Equal("Session command accepted.", panel.StatusMessage);
        Assert.Null(panel.ErrorMessage);
    }

    [Fact]
    public async Task Commands_AreDisabledWhileBusy()
    {
        var apiClient = new RecordingSessionApiClient
        {
            HoldRequest = new TaskCompletionSource<SessionCommandResponse>()
        };
        var panel = new SeatContextPanelViewModel(apiClient, new FixedIdempotencyKeyFactory("session-start-001"));
        panel.ApplyContext(OrganizationId, BranchId);
        panel.SelectSeat(FloorMapSeatViewModel.FromDto(Seat("PC-006", state: "Free")));

        var task = panel.StartGuestSessionAsync(CancellationToken.None);

        Assert.True(panel.IsBusy);
        Assert.False(panel.StartGuestSessionCommand.CanExecute(null));

        apiClient.HoldRequest.SetResult(CreateResponse(panel.SelectedSeat?.SeatId ?? Guid.Empty));
        await task;
    }

    private static SeatStatusDto Seat(string name, string state)
    {
        return new SeatStatusDto(
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            SeatName: name,
            ZoneId: Guid.Parse("22222222-2222-4222-8222-222222222222"),
            ZoneName: "Main Hall",
            SortOrder: 1,
            State: state,
            DeviceId: Guid.Parse("33333333-3333-4333-8333-333333333333"),
            DeviceName: name,
            IsDeviceOnline: true,
            IsDeviceLocked: state == "Locked",
            LastHeartbeatAtUtc: DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
            AgentVersion: "0.1.1",
            ShellVersion: "0.1.2",
            ActiveSessionId: state == "Active" ? ActiveSessionId : null,
            RemainingSeconds: state == "Active" ? 1800 : null);
    }

    private static SessionCommandResponse CreateResponse(Guid seatId)
    {
        return new SessionCommandResponse(
            "response-key",
            new SessionDto(
                ActiveSessionId,
                OrganizationId,
                BranchId,
                seatId,
                Guid.Parse("33333333-3333-4333-8333-333333333333"),
                SessionStateNames.Active,
                "default",
                DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
                DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
                null,
                3600,
                null),
            [
                new DeviceCommandDto(
                    Guid.Parse("66666666-6666-4666-8666-666666666666"),
                    "unlock",
                    DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
                    new Dictionary<string, string>())
            ]);
    }

    private sealed class FixedIdempotencyKeyFactory(string key) : IIdempotencyKeyFactory
    {
        public string Create(string operationName)
        {
            return key;
        }
    }

    private sealed class RecordingSessionApiClient : IOperatorSessionApiClient
    {
        public Guid LastBranchId { get; private set; }

        public StartGuestSessionRequest? LastStartRequest { get; private set; }

        public Guid LastExtendSessionId { get; private set; }

        public ExtendSessionRequest? LastExtendRequest { get; private set; }

        public Guid LastTransferSessionId { get; private set; }

        public TransferSessionRequest? LastTransferRequest { get; private set; }

        public Guid LastEndSessionId { get; private set; }

        public EndSessionRequest? LastEndRequest { get; private set; }

        public TaskCompletionSource<SessionCommandResponse>? HoldRequest { get; init; }

        public Task<SessionCommandResponse> StartGuestSessionAsync(
            Guid branchId,
            StartGuestSessionRequest request,
            CancellationToken cancellationToken)
        {
            LastBranchId = branchId;
            LastStartRequest = request;
            return CompleteAsync(request.SeatId);
        }

        public Task<SessionCommandResponse> ExtendSessionAsync(
            Guid sessionId,
            ExtendSessionRequest request,
            CancellationToken cancellationToken)
        {
            LastExtendSessionId = sessionId;
            LastExtendRequest = request;
            return CompleteAsync(Guid.Parse("11111111-1111-4111-8111-111111111111"));
        }

        public Task<SessionCommandResponse> TransferSessionAsync(
            Guid sessionId,
            TransferSessionRequest request,
            CancellationToken cancellationToken)
        {
            LastTransferSessionId = sessionId;
            LastTransferRequest = request;
            return CompleteAsync(request.TargetSeatId);
        }

        public Task<SessionCommandResponse> EndSessionAsync(
            Guid sessionId,
            EndSessionRequest request,
            CancellationToken cancellationToken)
        {
            LastEndSessionId = sessionId;
            LastEndRequest = request;
            return CompleteAsync(Guid.Parse("11111111-1111-4111-8111-111111111111"));
        }

        private Task<SessionCommandResponse> CompleteAsync(Guid seatId)
        {
            return HoldRequest?.Task ?? Task.FromResult(CreateResponse(seatId));
        }
    }
}
