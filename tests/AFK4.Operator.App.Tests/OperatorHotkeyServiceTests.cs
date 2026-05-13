using System.Windows.Input;
using AFK4.Operator.App.Hotkeys;
using AFK4.Operator.App.Shell;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorHotkeyServiceTests
{
    [Fact]
    public void Resolve_ReturnsOnlyEnabledCommandForCurrentWorkspace()
    {
        var service = new OperatorHotkeyService();
        var command = new RecordingCommand(canExecute: true);
        service.Register(OperatorWorkspaceKind.FloorMap, "F2", command);

        var resolved = service.Resolve(OperatorWorkspaceKind.FloorMap, "F2");

        Assert.Same(command, resolved);
    }

    [Fact]
    public void Resolve_DoesNotReturnDisabledCommand()
    {
        var service = new OperatorHotkeyService();
        var command = new RecordingCommand(canExecute: false);
        service.Register(OperatorWorkspaceKind.FloorMap, "F2", command);

        var resolved = service.Resolve(OperatorWorkspaceKind.FloorMap, "F2");

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_DoesNotLeakWorkspaceCommand()
    {
        var service = new OperatorHotkeyService();
        var command = new RecordingCommand(canExecute: true);
        service.Register(OperatorWorkspaceKind.FloorMap, "F2", command);

        var resolved = service.Resolve(OperatorWorkspaceKind.Pos, "F2");

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_FallsBackToGlobalCommand()
    {
        var service = new OperatorHotkeyService();
        var command = new RecordingCommand(canExecute: true);
        service.RegisterGlobal("Ctrl+L", command);

        var resolved = service.Resolve(OperatorWorkspaceKind.Players, "ctrl + l");

        Assert.Same(command, resolved);
    }

    [Fact]
    public void TryExecute_ExecutesResolvedCommandOnce()
    {
        var service = new OperatorHotkeyService();
        var command = new RecordingCommand(canExecute: true);
        service.Register(OperatorWorkspaceKind.Shifts, "F8", command);

        var executed = service.TryExecute(OperatorWorkspaceKind.Shifts, "F8");

        Assert.True(executed);
        Assert.Equal(1, command.ExecuteCount);
    }

    private sealed class RecordingCommand(bool canExecute) : ICommand
    {
        public int ExecuteCount { get; private set; }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return canExecute;
        }

        public void Execute(object? parameter)
        {
            ExecuteCount++;
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
