import { useState } from 'react';
import { ShellBridgeRequestTypeNames, ShellPipeErrorCodeNames, type PlayerShellStateDto } from '@afk4/contracts';
import { HostBridgeRequestError } from '@afk4/host-bridge';
import { useI18n } from '@afk4/i18n';
import { Loader2, Wrench } from 'lucide-react';
import { requestHost } from '../host/shellHost';
import { clubTime } from '../model/offers';

/**
 * Полоса обслуживания (спека оболочки, §6.5). Хост сжимает окно в полосу над рабочим столом
 * техника; здесь — что это за ПК, кто и когда его открыл и кнопка «Вернуть в зал».
 *
 * Кнопку может нажать любой у ПК, и это безопасно: возврат в зал только закрывает машину обратно.
 */
export function MaintenanceBand({ state }: { state: PlayerShellStateDto }) {
  const { t, locale } = useI18n();
  const [returning, setReturning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const since = state.maintenanceSinceUtc ? clubTime(state.maintenanceSinceUtc, undefined, locale) : null;
  const who = state.maintenanceByName?.trim() || null;
  const detail = since === null
    ? null
    : who === null
      ? t('playerShell.maintenance.onAt', { time: since })
      : t('playerShell.maintenance.onBy', { time: since, name: who });

  const returnToFloor = async () => {
    setReturning(true);
    setError(null);
    try {
      // Удачный ответ пуст: экран «Свободен» придёт следующим состоянием от агента.
      await requestHost(ShellBridgeRequestTypeNames.MaintenanceReturn);
    } catch (reason) {
      const code = reason instanceof HostBridgeRequestError ? reason.code : null;
      setError(code === ShellPipeErrorCodeNames.PlatformUnreachable
        ? t('playerShell.maintenance.returnOffline')
        : t('playerShell.maintenance.returnFailed'));
      setReturning(false);
    }
  };

  return (
    <div className="maintenance-band" role="status">
      <Wrench className="maintenance-band__icon" aria-hidden="true" />
      <strong className="maintenance-band__title">
        {state.seatLabel
          ? t('playerShell.maintenance.bandTitle', { seat: state.seatLabel })
          : t('playerShell.maintenance.title')}
      </strong>
      {detail ? <span className="maintenance-band__detail">{detail}</span> : null}
      {error ? <span className="maintenance-band__error" role="alert">{error}</span> : null}
      <button type="button" className="btn btn--primary maintenance-band__return" onClick={returnToFloor} disabled={returning}>
        {returning ? <Loader2 className="spin" aria-hidden="true" /> : null}
        {t('playerShell.maintenance.return')}
      </button>
    </div>
  );
}
