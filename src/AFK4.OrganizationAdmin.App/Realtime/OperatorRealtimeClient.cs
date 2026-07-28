using System.Windows;
using AFK4.OrganizationAdmin.App.FloorMap;
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR.Client;

namespace AFK4.OrganizationAdmin.App.Realtime;

public interface IOperatorHubConnection : IAsyncDisposable
{
    void OnDeviceStatusChanged(Func<DeviceStatusChangedDto, Task> handler);

    Task StartAsync(CancellationToken cancellationToken);
}

public interface IUiDispatcher
{
    Task InvokeAsync(Action action);
}

public interface IOperatorRealtimeClient : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);
}

public sealed class OperatorRealtimeClient : IOperatorRealtimeClient
{
    private readonly FloorMapWorkspaceViewModel viewModel;
    private readonly IOperatorHubConnection connection;
    private readonly IUiDispatcher dispatcher;

    public OperatorRealtimeClient(FloorMapWorkspaceViewModel viewModel, Uri hubUrl)
        : this(viewModel, new SignalROperatorHubConnection(hubUrl), new WpfUiDispatcher())
    {
    }

    public OperatorRealtimeClient(
        FloorMapWorkspaceViewModel viewModel,
        IOperatorHubConnection connection,
        IUiDispatcher dispatcher)
    {
        this.viewModel = viewModel;
        this.connection = connection;
        this.dispatcher = dispatcher;

        connection.OnDeviceStatusChanged(ApplyDeviceStatusAsync);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return connection.StartAsync(cancellationToken);
    }

    private Task ApplyDeviceStatusAsync(DeviceStatusChangedDto status)
    {
        return dispatcher.InvokeAsync(() => viewModel.ApplyDeviceStatus(status));
    }

    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync();
    }
}

public sealed class SignalROperatorHubConnection : IOperatorHubConnection
{
    private readonly HubConnection connection;

    public SignalROperatorHubConnection(Uri hubUrl)
    {
        connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();
    }

    public void OnDeviceStatusChanged(Func<DeviceStatusChangedDto, Task> handler)
    {
        connection.On<DeviceStatusChangedDto>(DeviceRealtimeEvents.DeviceStatusChanged, handler);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return connection.StartAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync();
    }
}

public sealed class WpfUiDispatcher : IUiDispatcher
{
    public Task InvokeAsync(Action action)
    {
        var dispatcher = Application.Current.Dispatcher;
        return dispatcher.InvokeAsync(action).Task;
    }
}
