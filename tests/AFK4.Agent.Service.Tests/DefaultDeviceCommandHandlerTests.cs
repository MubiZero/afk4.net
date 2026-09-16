using AFK4.Agent.Service;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class DefaultDeviceCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_AcknowledgesCommandForConfiguredDevice()
    {
        var options = Options.Create(new AgentOptions
        {
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001"
        });

        var handler = new DefaultDeviceCommandHandler(
            options,
            new AcceptingSessionEnforcementCoordinator(),
            new ShellWarningStore(),
            NullLogger<DefaultDeviceCommandHandler>.Instance);
        var command = new DeviceCommandDto(
            CommandId: Guid.Parse("63d6536d-f2c5-4379-a8b3-cd487f0c1e94"),
            Type: "lock",
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
            Payload: new Dictionary<string, string>
            {
                ["reason"] = "operator-request"
            });

        var before = DateTimeOffset.UtcNow;
        var result = await handler.HandleAsync(command, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(options.Value.OrganizationId, result.OrganizationId);
        Assert.Equal(options.Value.BranchId, result.BranchId);
        Assert.Equal(options.Value.DeviceId, result.DeviceId);
        Assert.Equal(command.CommandId, result.CommandId);
        Assert.Equal("Accepted", result.Status);
        Assert.Equal("Workstation lock requested.", result.Message);
        Assert.InRange(result.ObservedAtUtc, before, after);
    }

    private sealed class AcceptingSessionEnforcementCoordinator : ISessionEnforcementCoordinator
    {
        public Task<SessionEnforcementResult> UnlockAsync(
            SessionLeaseDto lease,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(SessionEnforcementResult.Accepted("Session lease accepted."));
        }

        public Task<SessionEnforcementResult> RefreshLeaseAsync(
            SessionLeaseDto lease,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(SessionEnforcementResult.Accepted("Session lease refreshed."));
        }

        public Task<SessionEnforcementResult> LockAsync(
            Guid? sessionId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(SessionEnforcementResult.Accepted("Workstation lock requested."));
        }
    }

    // Отвечать «принято» на команду, которую агент не умеет исполнять, — врать серверу: в журнале
    // она значилась бы выполненной. Так полгода отвечали reboot, shutdown и wake-on-LAN.
    [Fact]
    public async Task HandleAsync_RejectsACommandTypeItCannotPerform()
    {
        var handler = CreateHandler(out _);

        var result = await handler.HandleAsync(CreateCommand("reboot", reason: "operator-request"), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Contains("reboot", result.Message, StringComparison.Ordinal);
    }

    // Предупреждение сервера доезжает до экрана: у открытого счёта остатка секунд нет, и сама
    // оболочка про долг знать не может.
    [Fact]
    public async Task HandleAsync_PutsTheServerWarningOnThePlayerScreen()
    {
        var handler = CreateHandler(out var warnings);
        var sessionId = Guid.Parse("992624cf-77d1-413b-8e51-6f88872183eb");

        var result = await handler.HandleAsync(
            CreateCommand("warn", reason: "credit-limit", sessionId: sessionId),
            CancellationToken.None);

        Assert.Equal("Accepted", result.Status);
        Assert.Equal(PlayerShellWarningKinds.CreditLimit, warnings.Current?.Kind);
        Assert.Equal(sessionId, warnings.Current?.SessionId);
    }

    // Причина, которой агент не знает, не превращается в случайную картинку на экране игрока.
    [Fact]
    public async Task HandleAsync_RejectsAWarningWithAnUnknownReason()
    {
        var handler = CreateHandler(out var warnings);

        var result = await handler.HandleAsync(CreateCommand("warn", reason: "moon-phase"), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Contains("moon-phase", result.Message, StringComparison.Ordinal);
        Assert.Null(warnings.Current);
    }

    private static DefaultDeviceCommandHandler CreateHandler(out ShellWarningStore warnings)
    {
        warnings = new ShellWarningStore();
        return new DefaultDeviceCommandHandler(
            Options.Create(new AgentOptions
            {
                OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
                BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
                DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
                MachineName = "PC-001"
            }),
            new AcceptingSessionEnforcementCoordinator(),
            warnings,
            NullLogger<DefaultDeviceCommandHandler>.Instance);
    }

    private static DeviceCommandDto CreateCommand(string type, string reason, Guid? sessionId = null)
    {
        var payload = new Dictionary<string, string> { ["reason"] = reason };
        if (sessionId is { } id)
        {
            payload["sessionId"] = id.ToString("D");
        }

        return new DeviceCommandDto(
            CommandId: Guid.NewGuid(),
            Type: type,
            CreatedAtUtc: DateTimeOffset.Parse("2026-09-16T00:00:00Z"),
            Payload: payload);
    }
}
