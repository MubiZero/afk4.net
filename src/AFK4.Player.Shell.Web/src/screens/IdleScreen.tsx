import type { PlayerShellStateDto } from '@afk4/contracts';
import { useI18n } from '@afk4/i18n';
import { SeatBadge } from '../ui/SeatBadge';

/**
 * Свободный ПК: к нему никто не подошёл (спека, §3, кадр 01). Витрина клуба — фон без кнопок,
 * номер ПК — наверху слева, читается первым. Карусель карточек витрины — в срезе P4d; пока на её
 * месте — оформление клуба.
 */
export function IdleScreen({ state }: { state: PlayerShellStateDto }) {
  const { t } = useI18n();
  const branding = state.branding;

  return (
    <main className="idle-screen">
      <div className="idle-screen__showcase" aria-hidden="true" />
      <header className="idle-screen__top">
        <SeatBadge seatLabel={state.seatLabel} zoneName={state.zoneName} free />
      </header>
      <div className="idle-screen__club">
        {branding?.logoUrl ? <img className="idle-screen__logo" src={branding.logoUrl} alt="" /> : null}
        {branding?.clubName ? <p className="idle-screen__club-name">{branding.clubName}</p> : null}
      </div>
      <p className="idle-screen__hint">{t('playerShell.idle.hint')}</p>
    </main>
  );
}
