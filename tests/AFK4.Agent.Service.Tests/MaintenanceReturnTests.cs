using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Agent.Service.Tests;

/// <summary>«Вернуть в зал» с полосы на самом ПК (спека оболочки, §6.5).</summary>
public sealed class MaintenanceReturnTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-25T10:00:00Z");

    [Fact]
    public async Task TheButton_TellsTheServer_ThenClosesTheTechniciansDesktop()
    {
        var fixture = new Fixture();

        var reply = await fixture.ReturnAsync();

        Assert.True(reply.Ok);
        Assert.Equal(1, fixture.Client.Calls);
        Assert.Equal(PlayerShellStateNames.Locked, fixture.RuntimeState.Current.State);
        Assert.Equal(1, fixture.Desktop.Closed);
    }

    /// <summary>
    /// Без связи ПК не закрывается сам: сервер через десять секунд открыл бы его обратно, а
    /// человек у ПК решил бы, что кнопка сломана. Отказ честный — вернуть можно из Панели.
    /// </summary>
    [Fact]
    public async Task WithoutTheServer_ThePcStaysUnderMaintenance_AndSaysWhy()
    {
        var fixture = new Fixture();
        fixture.Client.Fails = true;

        var reply = await fixture.ReturnAsync();

        Assert.False(reply.Ok);
        Assert.Equal(ShellPipeErrorCodeNames.PlatformUnreachable, reply.ErrorCode);
        Assert.Equal(PlayerShellStateNames.Maintenance, fixture.RuntimeState.Current.State);
        Assert.Equal(0, fixture.Desktop.Closed);
    }

    [Fact]
    public async Task OnAPcAlreadyOnTheFloor_ThereIsNothingToDo()
    {
        var fixture = new Fixture();
        fixture.RuntimeState.Save(AgentRuntimeState.Locked(Now));

        var reply = await fixture.ReturnAsync();

        Assert.True(reply.Ok);
        Assert.Equal(0, fixture.Client.Calls);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            var maintenance = new MaintenanceMode(RuntimeState, Lock, Desktop, TimeProvider.System, NullLogger<MaintenanceMode>.Instance);
            Return = new MaintenanceReturn(Client, maintenance, RuntimeState, NullLogger<MaintenanceReturn>.Instance);
        }

        public PlayerShellStateBuilderTests.MemoryRuntimeStateStore RuntimeState { get; } = new(AgentRuntimeState.Maintenance(Now, desktopOpened: true));

        public MachineCommandHandlerTests.RecordingLock Lock { get; } = new();

        public MachineCommandHandlerTests.RecordingDesktop Desktop { get; } = new();

        public RecordingClient Client { get; } = new();

        public MaintenanceReturn Return { get; }

        public Task<ShellPipeReplyDto> ReturnAsync() =>
            Return.ReturnAsync(
                new ShellPipeRequestDto(Guid.NewGuid(), ShellPipeRequestTypeNames.MaintenanceReturn, new Dictionary<string, string>()),
                CancellationToken.None);
    }

    private sealed class RecordingClient : IMaintenanceReturnClient
    {
        public bool Fails { get; set; }

        public int Calls { get; private set; }

        public Task ReturnAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Fails ? throw new HttpRequestException("offline") : Task.CompletedTask;
        }
    }
}
