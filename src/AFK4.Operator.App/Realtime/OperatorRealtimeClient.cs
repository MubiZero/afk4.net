using AFK4.Operator.App.FloorMap;
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR.Client;

namespace AFK4.Operator.App.Realtime;

public sealed class OperatorRealtimeClient : IAsyncDisposable
{
    private readonly MainWindowViewModel viewModel;
    private readonly HubConnection connection;

    public OperatorRealtimeClient(MainWindowViewModel viewModel, Uri hubUrl)
    {
        this.viewModel = viewModel;
        connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        connection.On<DeviceStatusChangedDto>(DeviceRealtimeEvents.DeviceStatusChanged, ApplyDeviceStatus);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return connection.StartAsync(cancellationToken);
    }

    private void ApplyDeviceStatus(DeviceStatusChangedDto status)
    {
        viewModel.ApplyDeviceStatus(status);
    }

    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync();
    }
}
