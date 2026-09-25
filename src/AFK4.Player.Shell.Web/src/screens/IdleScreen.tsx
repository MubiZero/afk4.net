import { useEffect, useState } from 'react';
import type { PlayerShellStateDto } from '@afk4/contracts';
import { useI18n } from '@afk4/i18n';
import { SeatBadge } from '../ui/SeatBadge';

/**
 * Свободный ПК: к нему никто не подошёл (спека, §3, кадр 01). Витрина клуба — фон без кнопок,
 * номер ПК — наверху слева, читается первым. Карусель карточек витрины — в срезе P4d; пока на её
 * месте — оформление клуба.
 */
export function IdleScreen({ state, dimmed = false }: { state: PlayerShellStateDto; dimmed?: boolean }) {
  const { t } = useI18n();
  const branding = state.branding;

  return (
    <main className={dimmed ? 'idle-screen idle-screen--dimmed' : 'idle-screen'} aria-hidden={dimmed || undefined}>
      <div className="idle-screen__showcase" aria-hidden="true" />
      {/* Подошли — знак ПК уезжает в шапку окна входа, витрина притушается фоном. */}
      {dimmed ? null : (
        <header className="idle-screen__top">
          <SeatBadge seatLabel={state.seatLabel} zoneName={state.zoneName} free />
        </header>
      )}
      <div className="idle-screen__club">
        {branding?.logoUrl ? <img className="idle-screen__logo" src={branding.logoUrl} alt="" /> : null}
        {branding?.clubName ? <p className="idle-screen__club-name">{branding.clubName}</p> : null}
      </div>
      <p className="idle-screen__hint">{t('playerShell.idle.hint')}</p>
      {!dimmed && state.idleShutdownAtUtc ? <IdleShutdownNotice at={state.idleShutdownAtUtc} /> : null}
    </main>
  );
}

/**
 * Свободный ПК вот-вот выключится от простоя: сколько осталось и как оставить включённым. Движение
 * мыши отменяет выключение у агента — и полоса уходит со следующим состоянием.
 */
function IdleShutdownNotice({ at }: { at: string }) {
  const { t } = useI18n();
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);
  const seconds = Math.max(0, Math.ceil((new Date(at).getTime() - now) / 1000));

  return (
    <p className="idle-screen__shutdown" role="status" aria-live="polite">
      {t('playerShell.idle.shutdown', { seconds })}
    </p>
  );
}
