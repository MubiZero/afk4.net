import { useState } from 'react';
import { WifiOff } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { Button, Money } from '@afk4/ui/react';
import type { SeatSummary } from '../operatorData';
import { formatTime } from '../operatorHelpers';
import { useOfflineDeskContext } from './useOfflineDesk';

const DURATIONS = [30, 60, 120, 180];
const EXTENSIONS = [15, 30];

const REASON_KEYS: Record<string, MessageKey> = {
  'no-host': 'op.offline.reason.noHost',
  'no-cache': 'op.offline.reason.noCache',
  'no-shift': 'op.offline.reason.noShift',
  'no-address': 'op.offline.reason.noAddress'
};

/**
 * Касса без связи в карточке места: посадить гостя за наличные, продлить, закончить. Деньги берутся у
 * стойки, ПК открывается арендой с ключом этого ПК, запись ложится в журнал и уйдёт на сервер, когда
 * связь вернётся (docs/roadmap/offline-front-desk.md).
 */
export function OfflineSeatActions({ seat }: { seat: SeatSummary }) {
  const { t } = useI18n();
  const desk = useOfflineDeskContext();
  const [tariffId, setTariffId] = useState<string | null>(null);
  const [minutes, setMinutes] = useState(60);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!desk || !desk.isOffline || !seat.deviceId || seat.isConsole === true || seat.isKioskAbsent === true) return null;

  const session = desk.sessionsBySeat.get(seat.id) ?? null;
  const reason = desk.unavailableReason ?? (seat.deviceNetworkAddress ? null : 'no-address');
  const run = async (action: () => Promise<void>) => {
    setBusy(true);
    setError(null);
    try {
      await action();
    } catch (failure) {
      setError(t('op.offline.failed', { reason: failure instanceof Error ? failure.message : String(failure) }));
    } finally {
      setBusy(false);
    }
  };

  const header = (
    <p className="offline-seat-title"><WifiOff aria-hidden="true" size={14} /> {t('op.offline.title')}</p>
  );

  if (reason !== null && session === null) {
    return <section className="offline-seat">{header}<p className="offline-seat-hint">{t(REASON_KEYS[reason])}</p></section>;
  }

  if (session !== null) {
    return (
      <section className="offline-seat" aria-label={t('op.offline.title')}>
        {header}
        <p className="offline-seat-hint">
          {t('op.offline.sessionUntil', { time: formatTime(session.endsAtUtc) })} · <Money minorUnits={session.paidMinorUnits} currencyCode={session.currencyCode} />
        </p>
        <div className="offline-seat-actions">
          {EXTENSIONS.map((extra) => (
            <Button key={extra} size="sm" disabled={busy} onClick={() => void run(() => desk.extend(session, extra))}>
              {t('op.offline.extend', { minutes: extra })} · <Money minorUnits={desk.extensionPriceFor(session, extra)} currencyCode={session.currencyCode} />
            </Button>
          ))}
          <Button size="sm" variant="danger" disabled={busy} onClick={() => void run(() => desk.end(session))}>{t('op.offline.end')}</Button>
        </div>
        {error !== null && <p className="offline-seat-error" role="alert">{error}</p>}
      </section>
    );
  }

  if (seat.activeSessionId) return null;

  const tariff = desk.tariffsNow.find((option) => option.tariffRuleVersionId === tariffId) ?? desk.tariffsNow[0] ?? null;
  if (tariff === null) {
    return <section className="offline-seat">{header}<p className="offline-seat-hint">{t('op.offline.reason.noTariffNow')}</p></section>;
  }

  const price = desk.priceFor(tariff, minutes);
  return (
    <section className="offline-seat" aria-label={t('op.offline.title')}>
      {header}
      <label className="offline-seat-field">
        {t('op.offline.tariff')}
        <select value={tariff.tariffRuleVersionId} disabled={busy} onChange={(event) => setTariffId(event.currentTarget.value)}>
          {desk.tariffsNow.map((option) => <option key={option.tariffRuleVersionId} value={option.tariffRuleVersionId}>{option.name}</option>)}
        </select>
      </label>
      <div className="offline-seat-actions" role="group" aria-label={t('op.offline.duration')}>
        {DURATIONS.map((value) => (
          <Button key={value} size="sm" variant={value === minutes ? 'primary' : 'secondary'} aria-pressed={value === minutes} disabled={busy} onClick={() => setMinutes(value)}>
            {t('op.offline.minutes', { minutes: value })}
          </Button>
        ))}
      </div>
      <Button variant="primary" block disabled={busy} onClick={() => void run(() => desk.seatGuest(seat, tariff, minutes))}>
        {t('op.offline.seat')} · <Money minorUnits={price} currencyCode={tariff.currencyCode} />
      </Button>
      <p className="offline-seat-hint">{t('op.offline.cashNote')}</p>
      {error !== null && <p className="offline-seat-error" role="alert">{error}</p>}
    </section>
  );
}
