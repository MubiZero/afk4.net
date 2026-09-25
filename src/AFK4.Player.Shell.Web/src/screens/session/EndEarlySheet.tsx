import { useCallback, useEffect, useRef, useState } from 'react';
import type { MoneyDto, PlayerEndQuoteDto, PlayerSelfEndSessionResponse } from '@afk4/contracts';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { formatMoney } from '@afk4/money';
import { getJson, postJson } from '../../api/playerApi';
import { INTL_LOCALES, durationKey } from '../../model/offers';
import { sessionActionErrorKey } from '../../model/session';
import { Sheet } from '../../ui/Sheet';

interface EndEarlySheetProps {
  baseUrl: string;
  sessionId: string;
  onClose: () => void;
  onEnded: (result: PlayerSelfEndSessionResponse) => void;
}

/**
 * «Встать раньше» (P2c/P2e): сколько спишут и сколько вернётся — до нажатия, тем же расчётом, что
 * у самого выхода. Экран не обещает одну сумму, чтобы вернуть другую.
 */
export function EndEarlySheet({ baseUrl, sessionId, onClose, onEnded }: EndEarlySheetProps) {
  const { t, locale } = useI18n();
  const [quote, setQuote] = useState<PlayerEndQuoteDto | null>(null);
  const [failed, setFailed] = useState(false);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<MessageKey | null>(null);
  const idempotencyKey = useRef<string>(crypto.randomUUID());

  const load = useCallback(async () => {
    setFailed(false);
    try {
      setQuote(await getJson<PlayerEndQuoteDto>(baseUrl, `/api/me/sessions/${sessionId}/end-quote`));
    } catch {
      setFailed(true);
    }
  }, [baseUrl, sessionId]);

  useEffect(() => {
    void load();
  }, [load]);

  const money = (value: MoneyDto) => formatMoney(value.minorUnits, value.currencyCode, INTL_LOCALES[locale]);
  const duration = (minutes: number) => {
    const { key, values } = durationKey(minutes);
    return t(key, values);
  };

  const end = async () => {
    if (sending) return;
    setSending(true);
    setError(null);
    try {
      onEnded(await postJson<PlayerSelfEndSessionResponse>(baseUrl, `/api/me/sessions/${sessionId}/end`, {
        idempotencyKey: idempotencyKey.current
      }));
    } catch (reason) {
      setError(sessionActionErrorKey(reason));
      setSending(false);
    }
  };

  return (
    <Sheet title={t('playerShell.endEarly.title')} onClose={onClose} closeDisabled={sending}>
      {quote ? (
        <ul className="sheet__facts">
          <li>{t('playerShell.endEarly.billed', { duration: duration(quote.billedMinutes) })}</li>
          <li className="sheet__fact--strong">
            {quote.refund.minorUnits > 0
              ? t('playerShell.endEarly.refund', { amount: money(quote.refund) })
              : t('playerShell.endEarly.noRefund')}
          </li>
          {quote.packageMinutesReturned > 0 ? (
            <li>{t('playerShell.endEarly.packageReturn', { duration: duration(quote.packageMinutesReturned) })}</li>
          ) : null}
        </ul>
      ) : failed ? (
        <div className="offers__failed" role="alert">
          <p>{t('playerShell.chooseTime.loadFailed')}</p>
          <button type="button" className="btn btn--ghost" onClick={() => void load()}>{t('playerShell.chooseTime.retry')}</button>
        </div>
      ) : (
        <ul className="sheet__facts" aria-hidden="true">
          <li><span className="skeleton skeleton--title" /></li>
          <li><span className="skeleton skeleton--title" /></li>
        </ul>
      )}

      {error ? <p className="sheet__error" role="alert">{t(error)}</p> : null}
      <footer className="sheet__actions">
        <button type="button" className="btn btn--ghost" onClick={onClose} disabled={sending}>
          {t('playerShell.endEarly.cancel')}
        </button>
        <button type="button" className="btn btn--primary" disabled={!quote || sending} onClick={() => void end()}>
          {sending ? t('playerShell.endEarly.ending') : t('playerShell.endEarly.confirm')}
        </button>
      </footer>
    </Sheet>
  );
}
