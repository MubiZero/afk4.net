using System.Text.Json.Serialization;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Enforcement;

public sealed record AgentRuntimeState(
    string State,
    bool IsLocked,
    Guid? ActiveSessionId,
    DateTimeOffset? LeaseExpiresAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static AgentRuntimeState Locked(DateTimeOffset updatedAtUtc)
    {
        return new AgentRuntimeState(
            PlayerShellStateNames.Locked,
            IsLocked: true,
            ActiveSessionId: null,
            LeaseExpiresAtUtc: null,
            updatedAtUtc);
    }

    /// <summary>
    /// Связь с платформой потеряна, но гость продолжает играть по уже подписанной аренде.
    ///
    /// Это состояние было заведено в контракте, оболочка умеет его показывать («связь потеряна,
    /// сессия продолжается»), а ставить его было некому: агент знал только «заперто» и
    /// «играем». Игрок узнавал об обрыве по внезапно погасшему экрану в конце льготного окна.
    /// </summary>
    public static AgentRuntimeState Grace(
        Guid sessionId,
        DateTimeOffset? leaseExpiresAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        return new AgentRuntimeState(
            PlayerShellStateNames.Grace,
            IsLocked: false,
            sessionId,
            leaseExpiresAtUtc,
            updatedAtUtc);
    }

    /// <summary>
    /// ПК на обслуживании: заперт, игрокам вход закрыт. Состояние живёт файлом, как и остальные, —
    /// перезапуск службы не возвращает машину в зал.
    /// </summary>
    public static AgentRuntimeState Maintenance(DateTimeOffset updatedAtUtc)
    {
        return new AgentRuntimeState(
            PlayerShellStateNames.Maintenance,
            IsLocked: true,
            ActiveSessionId: null,
            LeaseExpiresAtUtc: null,
            updatedAtUtc);
    }

    /// <summary>За ПК играют: по аренде или в льготном окне после обрыва связи.</summary>
    [JsonIgnore]
    public bool SessionRuns => State is PlayerShellStateNames.Active or PlayerShellStateNames.Grace;

    public static AgentRuntimeState Active(SessionLeaseDto lease, DateTimeOffset updatedAtUtc)
    {
        return new AgentRuntimeState(
            PlayerShellStateNames.Active,
            IsLocked: false,
            lease.SessionId,
            lease.ExpiresAtUtc,
            updatedAtUtc);
    }
}
