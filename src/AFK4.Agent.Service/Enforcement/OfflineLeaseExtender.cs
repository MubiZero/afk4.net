using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Agent.Service.Enforcement;

/// <summary>
/// Decides whether a paying customer's expired-but-signed lease may keep the PC unlocked through a
/// network outage (spec §6.1, D5). It NEVER mints a lease — it only honours an already-signed lease
/// for an <em>active</em> session, and only while the agent has been offline for less than the
/// effective grace window measured from the last successful backend contact. Past the window the
/// caller locks exactly as before.
/// </summary>
public interface IOfflineLeaseExtender
{
    bool ShouldExtend(SessionLeaseDto lease, DateTimeOffset nowUtc);

    /// <summary>
    /// Идёт ли ещё льготное окно для аренды с таким сроком. Отдельно от <see cref="ShouldExtend"/>:
    /// после перезапуска подписанной аренды на руках уже нет, а ответить на этот вопрос всё
    /// равно надо — срок известен из сохранённого состояния.
    /// </summary>
    bool WithinGraceWindow(DateTimeOffset nowUtc, DateTimeOffset leaseExpiresAtUtc);
}

public sealed class OfflineLeaseExtender(IOfflineGraceState graceState) : IOfflineLeaseExtender
{
    public bool ShouldExtend(SessionLeaseDto lease, DateTimeOffset nowUtc)
    {
        if (lease is null || !string.Equals(lease.State, SessionStateNames.Active, StringComparison.Ordinal))
        {
            return false;
        }

        return WithinGraceWindow(nowUtc, lease.ExpiresAtUtc);
    }

    public bool WithinGraceWindow(DateTimeOffset nowUtc, DateTimeOffset leaseExpiresAtUtc)
    {
        var lastContact = graceState.LastSuccessfulContactUtc;
        if (lastContact is null)
        {
            return false;
        }

        // Льгота — это про обрыв связи, а не про любую истёкшую аренду. Если агент разговаривал
        // с платформой уже после того, как аренда кончилась, и новой она не прислала, значит
        // сессия правда закончилась: держать машину открытой больше не за что. Без этой проверки
        // на живой связи «последний контакт» обновлялся каждым сердцебиением, окно не кончалось
        // никогда, и запасной запрет по сроку аренды не срабатывал вовсе.
        if (lastContact.Value >= leaseExpiresAtUtc)
        {
            return false;
        }

        var graceWindow = TimeSpan.FromMinutes(graceState.EffectiveGraceMinutes);
        return nowUtc - lastContact.Value < graceWindow;
    }
}
