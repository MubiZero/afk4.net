using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service;

public static class DeviceConnectionRequestFactory
{
    public static DeviceConnectionRequest Create(AgentOptions options, DateTimeOffset connectedAtUtc)
    {
        return Create(options, connectedAtUtc, leaseStore: null);
    }

    /// <param name="credentialSecret">
    /// Действующий ключ машины. null — взять тот, что положил установщик: так зовут те, кто
    /// про смену ключа ничего не знает (тесты и первый запуск).
    /// </param>
    public static DeviceConnectionRequest Create(
        AgentOptions options,
        DateTimeOffset connectedAtUtc,
        ISessionLeaseStore? leaseStore,
        string? credentialSecret = null)
    {
        var lease = leaseStore?.Current;

        return new DeviceConnectionRequest(
            OrganizationId: options.OrganizationId,
            BranchId: options.BranchId,
            DeviceId: options.DeviceId,
            MachineName: options.MachineName,
            AgentVersion: options.AgentVersion,
            ShellVersion: options.ShellVersion,
            CredentialSecret: credentialSecret ?? options.DeviceCredentialSecret,
            ConnectedAtUtc: connectedAtUtc,
            ActiveSessionId: lease?.SessionId,
            ActiveSessionLeaseExpiresAtUtc: lease?.ExpiresAtUtc,
            ActiveSessionLeaseSequence: lease?.Sequence);
    }
}
