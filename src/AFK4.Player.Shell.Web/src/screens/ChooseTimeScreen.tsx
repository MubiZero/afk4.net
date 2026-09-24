import { useState } from 'react';
import { ShellBridgeRequestTypeNames, type PlayerShellStateDto, type ShellAuthStateDto } from '@afk4/contracts';
import { useI18n } from '@afk4/i18n';
import { requestHost } from '../host/shellHost';
import { SeatBadge } from '../ui/SeatBadge';

/**
 * Вошедший на свободном ПК (спека, §3, кадр 02). Предложения времени с готовыми ценами приходят с
 * сервера (start-offers, P2c) и лягут сюда следующим срезом; до тех пор вошедший видит, что вход
 * состоялся, и может выйти.
 */
export function ChooseTimeScreen({ state, auth }: { state: PlayerShellStateDto; auth: ShellAuthStateDto }) {
  const { t } = useI18n();
  const [leaving, setLeaving] = useState(false);

  const signOut = async () => {
    setLeaving(true);
    // Выход подтверждает событие auth.changed; не ответил хост — сервер всё равно погасит вход.
    await requestHost(ShellBridgeRequestTypeNames.AuthSignOut).catch(() => setLeaving(false));
  };

  return (
    <main className="choose-time">
      <header className="choose-time__top">
        <SeatBadge seatLabel={state.seatLabel} zoneName={state.zoneName} free />
        <button type="button" className="btn btn--ghost" onClick={signOut} disabled={leaving}>
          {t('playerShell.signOut')}
        </button>
      </header>
      <h1 className="choose-time__greeting">
        {t('playerShell.chooseTime.greeting', { name: auth.displayName ?? '' })}
      </h1>
    </main>
  );
}
