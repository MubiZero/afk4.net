import { useEffect } from 'react';
import type { MoneyDto, PlayerSelfEndSessionResponse, PlayerShellStateDto } from '@afk4/contracts';
import { useI18n } from '@afk4/i18n';
import { formatMoney } from '@afk4/money';
import { CheckCircle2 } from 'lucide-react';
import { INTL_LOCALES, durationKey } from '../model/offers';
import { SeatBadge } from '../ui/SeatBadge';

/** Итог держится 25 секунд (спека, §3): прочесть и уйти; дальше ПК сам выводит вошедшего. */
export const SUMMARY_SECONDS = 25;

interface SummaryScreenProps {
  state: PlayerShellStateDto;
  result: PlayerSelfEndSessionResponse;
  onPlayMore: () => void;
  onLeave: () => void;
}

/**
 * Итог раннего выхода: сколько сыграно и что вернулось. «Встал раньше» и «вернули столько-то» —
 * одно событие, узнавать вторую половину из истории кошелька человек не должен.
 */
export function SummaryScreen({ state, result, onPlayMore, onLeave }: SummaryScreenProps) {
  const { t, locale } = useI18n();

  useEffect(() => {
    const timer = window.setTimeout(onLeave, SUMMARY_SECONDS * 1000);
    return () => window.clearTimeout(timer);
  }, [onLeave]);

  const packageMinutes = result.packageMinutesReturned ?? 0;
  const money = (value: MoneyDto) => formatMoney(value.minorUnits, value.currencyCode, INTL_LOCALES[locale]);
  const duration = (minutes: number) => {
    const { key, values } = durationKey(minutes);
    return t(key, values);
  };

  return (
    <main className="summary">
      <header className="summary__top">
        <SeatBadge seatLabel={state.seatLabel} zoneName={state.zoneName} />
      </header>
      <section className="summary__body" role="status">
        <CheckCircle2 className="summary__icon" aria-hidden="true" />
        <h1 className="summary__title">{t('playerShell.summary.title')}</h1>
        <p>{t('playerShell.summary.billed', { duration: duration(result.billedMinutes) })}</p>
        {result.refunded.minorUnits > 0 ? (
          <p className="summary__refund">{t('playerShell.summary.refund', { amount: money(result.refunded) })}</p>
        ) : null}
        {packageMinutes > 0 ? (
          <p>{t('playerShell.summary.packageReturn', { duration: duration(packageMinutes) })}</p>
        ) : null}
      </section>
      <footer className="summary__actions">
        <button type="button" className="btn btn--ghost" onClick={onLeave}>{t('playerShell.signOut')}</button>
        <button type="button" className="btn btn--primary" onClick={onPlayMore}>{t('playerShell.summary.again')}</button>
      </footer>
    </main>
  );
}
