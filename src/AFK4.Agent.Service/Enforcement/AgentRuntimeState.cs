using System.Text.Json.Serialization;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Enforcement;

public sealed record AgentRuntimeState(
    string State,
    bool IsLocked,
    Guid? ActiveSessionId,
    DateTimeOffset? LeaseExpiresAtUtc,
    DateTimeOffset UpdatedAtUtc,
    // Проводник для техника запустил агент — значит, при возврате в зал его и закрывать. Если
    // проводник работал до обслуживания (ПК ещё не переведён в киоск), он чужой и остаётся.
    bool MaintenanceDesktopOpened = false)
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
    /// ПК на обслуживании: открыт для техника, игрокам вход закрыт (спека оболочки, §6.5).
    /// <c>IsLocked</c> остаётся true: для платформы и обновлений это «за ПК нет игрока», а не
    /// «экран заперт». Состояние живёт файлом — перезапуск службы не возвращает машину в зал.
    /// </summary>
    public static AgentRuntimeState Maintenance(DateTimeOffset updatedAtUtc, bool desktopOpened = false)
    {
        return new AgentRuntimeState(
            PlayerShellStateNames.Maintenance,
            IsLocked: true,
            ActiveSessionId: null,
            LeaseExpiresAtUtc: null,
            updatedAtUtc,
            desktopOpened);
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
