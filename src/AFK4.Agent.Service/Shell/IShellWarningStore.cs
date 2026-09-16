using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Shell;

/// <summary>Предупреждение, которое сервер прислал командой <c>warn</c>, и сессия, к которой оно относится.</summary>
public sealed record ShellWarning(Guid? SessionId, string Kind);

/// <summary>
/// Держит предупреждение сервера до конца сессии, к которой оно относится.
///
/// Оболочка сама вычисляет «время заканчивается» из остатка секунд, но про долг она знать не может:
/// у открытого счёта остатка нет вовсе. Сервер шлёт <c>warn</c> с причиной за минуту до блокировки —
/// и раньше агент отвечал «принято» и выбрасывал команду, так что игрок узнавал о долге по погасшему
/// экрану. Здесь она и живёт, пока сессия не сменилась.
/// </summary>
public interface IShellWarningStore
{
    ShellWarning? Current { get; }

    void Warn(Guid? sessionId, string kind);

    /// <summary>Сбрасывает предупреждение, если оно относилось к другой сессии (или сессии больше нет).</summary>
    void ForgetUnless(Guid? sessionId);
}

public sealed class ShellWarningStore : IShellWarningStore
{
    private readonly Lock gate = new();
    private ShellWarning? current;

    public ShellWarning? Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    public void Warn(Guid? sessionId, string kind)
    {
        if (string.IsNullOrWhiteSpace(kind) || string.Equals(kind, PlayerShellWarningKinds.None, StringComparison.Ordinal))
        {
            return;
        }

        lock (gate)
        {
            current = new ShellWarning(sessionId, kind);
        }
    }

    public void ForgetUnless(Guid? sessionId)
    {
        lock (gate)
        {
            if (current is null)
            {
                return;
            }

            if (sessionId is null || current.SessionId != sessionId)
            {
                current = null;
            }
        }
    }
}
