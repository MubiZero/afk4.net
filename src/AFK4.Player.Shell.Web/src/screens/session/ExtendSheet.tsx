import { useCallback, useEffect, useRef, useState } from 'react';
import type { MoneyDto, PlayerDurationOfferDto, PlayerExtendOffersDto } from '@afk4/contracts';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { formatMoney } from '@afk4/money';
import { getJson, postJson } from '../../api/playerApi';
import { INTL_LOCALES, clubTime, durationKey } from '../../model/offers';
import { extendUnavailableKey, sessionActionErrorKey } from '../../model/session';
import { Sheet } from '../../ui/Sheet';

interface ExtendSheetProps {
  baseUrl: string;
  sessionId: string;
  onClose: () => void;
  /** Продлили — экран скажет «продлено до …»; новый конец принесёт агент. */
  onExtended: (endsAtUtc: string) => void;
}

/**
 * Продление по тарифу старта (P2c): варианты с готовыми суммами считает сервер. Пока ждём ответа —
 * «Продлеваем…», лист закрыть нельзя: игрок должен увидеть, чем кончилось.
 */
export function ExtendSheet({ baseUrl, sessionId, onClose, onExtended }: ExtendSheetProps) {
  const { t, locale } = useI18n();
  const [offers, setOffers] = useState<PlayerExtendOffersDto | null>(null);
  const [failed, setFailed] = useState(false);
  const [choice, setChoice] = useState<PlayerDurationOfferDto | null>(null);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<MessageKey | null>(null);
  const idempotencyKey = useRef<string>(crypto.randomUUID());

  const load = useCallback(async () => {
    setFailed(false);
    try {
      setOffers(await getJson<PlayerExtendOffersDto>(baseUrl, `/api/me/sessions/${sessionId}/extend-offers`));
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

  const extend = async () => {
    if (!choice || sending) return;
    setSending(true);
    setError(null);
    try {
      await postJson(baseUrl, `/api/me/sessions/${sessionId}/extend`, {
        additionalMinutes: choice.minutes,
        idempotencyKey: idempotencyKey.current
      });
      onExtended(choice.endsAtUtc);
    } catch (reason) {
      setError(sessionActionErrorKey(reason));
      setSending(false);
      setChoice(null);
      idempotencyKey.current = crypto.randomUUID();
      void load();
    }
  };

  const unavailable = extendUnavailableKey(offers?.unavailableReason);

  return (
    <Sheet title={t('playerShell.extend.title')} onClose={onClose} closeDisabled={sending}>
      {offers ? (
        <>
          <p className="sheet__meta">{t('playerShell.chooseTime.balance', { amount: money(offers.balance) })}</p>
          {unavailable ? (
            <p className="sheet__note">{t(unavailable)}</p>
          ) : (
            <div className="offer-group__options" role="group" aria-label={t('playerShell.extend.pick')}>
              {offers.options.map((option) => {
                const until = t('playerShell.chooseTime.until', { time: clubTime(option.endsAtUtc, undefined, locale) });
                const price = option.affordable
                  ? money(option.amount)
                  : t('playerShell.chooseTime.short', {
                      amount: money({ ...option.balanceAfter, minorUnits: -option.balanceAfter.minorUnits })
                    });
                return (
                  <button
                    key={option.minutes}
                    type="button"
                    className="offer-tile"
                    aria-label={[`+${duration(option.minutes)}`, until, price].join(', ')}
                    aria-pressed={choice?.minutes === option.minutes}
                    disabled={sending || !option.affordable}
                    onClick={() => {
                      setChoice(option);
                      setError(null);
                    }}
                  >
                    <span className="offer-tile__duration">+{duration(option.minutes)}</span>
                    <span className="offer-tile__until">{until}</span>
                    <span className="offer-tile__amount mono">{price}</span>
                  </button>
                );
              })}
            </div>
          )}
        </>
      ) : failed ? (
        <div className="offers__failed" role="alert">
          <p>{t('playerShell.chooseTime.loadFailed')}</p>
          <button type="button" className="btn btn--ghost" onClick={() => void load()}>{t('playerShell.chooseTime.retry')}</button>
        </div>
      ) : (
        <div className="offer-group__options" aria-hidden="true">
          {[0, 1, 2, 3].map((index) => <span key={index} className="offer-tile skeleton" />)}
        </div>
      )}

      {error ? <p className="sheet__error" role="alert">{t(error)}</p> : null}
      {!unavailable ? (
        <footer className="sheet__actions">
          <button type="button" className="btn btn--primary" disabled={!choice || sending} onClick={() => void extend()}>
            {sending
              ? t('playerShell.extend.extending')
              : choice
                ? t('playerShell.extend.confirm', { amount: money(choice.amount) })
                : t('playerShell.extend.pick')}
          </button>
        </footer>
      ) : null}
    </Sheet>
  );
}
