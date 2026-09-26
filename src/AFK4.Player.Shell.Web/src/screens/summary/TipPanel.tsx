import { useEffect, useRef, useState } from 'react';
import type { MoneyDto, PlayerTipOfferDto, PlayerTipResponse } from '@afk4/contracts';
import { TipUnavailableReasonNames } from '@afk4/contracts';
import { useI18n } from '@afk4/i18n';
import { formatMoney } from '@afk4/money';
import { PlayerApiError, getJson, postJson } from '../../api/playerApi';
import { INTL_LOCALES } from '../../model/offers';

const tipPath = (sessionId: string) => `/api/me/visits/${sessionId}/tip`;

type TipState =
  | { kind: 'choose' }
  | { kind: 'confirm'; amount: MoneyDto }
  | { kind: 'sending'; amount: MoneyDto }
  | { kind: 'sent'; amount: MoneyDto }
  | { kind: 'failed'; amount: MoneyDto; reason: string | null };

/**
 * Чаевые администратору смены (спека чаевых, §3). Клуб их не включил, смены нет или время вышло —
 * блока нет: объяснять игроку, почему он не может дать чаевые, незачем. Деньги не «празднуются»
 * до ответа сервера; пока открыт вопрос «списать?», итог не уходит сам.
 */
export function TipPanel({
  baseUrl,
  sessionId,
  onHold
}: {
  baseUrl: string;
  sessionId: string;
  // true — человек решает, итог не должен закрыться под рукой.
  onHold: (held: boolean) => void;
}) {
  const { t, locale } = useI18n();
  const [offer, setOffer] = useState<PlayerTipOfferDto | null>(null);
  const [state, setState] = useState<TipState>({ kind: 'choose' });
  const idempotencyKey = useRef<string | null>(null);
  const money = (value: MoneyDto) => formatMoney(value.minorUnits, value.currencyCode, INTL_LOCALES[locale]);

  useEffect(() => {
    getJson<PlayerTipOfferDto>(baseUrl, tipPath(sessionId)).then(setOffer).catch(() => {});
  }, [baseUrl, sessionId]);

  const held = state.kind === 'confirm' || state.kind === 'sending';
  useEffect(() => onHold(held), [held, onHold]);

  if (offer === null) return null;
  if (!offer.available && offer.unavailableReason !== TipUnavailableReasonNames.NotEnoughBalance) return null;

  const name = offer.recipientName ?? '';

  const send = async (amount: MoneyDto) => {
    idempotencyKey.current ??= crypto.randomUUID();
    setState({ kind: 'sending', amount });
    try {
      const response = await postJson<PlayerTipResponse>(baseUrl, tipPath(sessionId), {
        amount,
        idempotencyKey: idempotencyKey.current
      });
      setState({ kind: 'sent', amount: response.amount });
    } catch (reason) {
      setState({ kind: 'failed', amount, reason: reason instanceof PlayerApiError ? reason.code : null });
    }
  };

  if (state.kind === 'sent') {
    return <p className="tip__thanks" role="status">{t('playerShell.tip.thanks', { amount: money(state.amount), name })}</p>;
  }

  return (
    <section className="tip" aria-label={t('playerShell.tip.title', { name })}>
      <h2 className="tip__title">{t('playerShell.tip.title', { name })}</h2>
      {state.kind === 'confirm' || state.kind === 'sending' ? (
        <div className="tip__confirm">
          <p>{t('playerShell.tip.confirm', { amount: money(state.amount) })}</p>
          <div className="tip__actions">
            <button type="button" className="btn btn--ghost" disabled={state.kind === 'sending'} onClick={() => setState({ kind: 'choose' })}>
              {t('playerShell.tip.cancel')}
            </button>
            <button type="button" className="btn btn--primary" disabled={state.kind === 'sending'} onClick={() => void send(state.amount)}>
              {state.kind === 'sending' ? t('playerShell.tip.sending') : t('playerShell.tip.send')}
            </button>
          </div>
        </div>
      ) : (
        <div className="tip__amounts" role="group" aria-label={t('playerShell.tip.title', { name })}>
          {offer.presets.map((preset) => (
            <button
              key={preset.minorUnits}
              type="button"
              className="btn tip__amount"
              // Не хватает баланса — сумма серая: нажать её значило бы получить отказ.
              disabled={preset.minorUnits > offer.balance.minorUnits}
              onClick={() => {
                idempotencyKey.current = null;
                setState({ kind: 'confirm', amount: preset });
              }}
            >
              {money(preset)}
            </button>
          ))}
        </div>
      )}
      {state.kind === 'failed' ? (
        <p className="tip__failed" role="alert">
          {state.reason === TipUnavailableReasonNames.NotEnoughBalance ? t('playerShell.tip.noBalance') : t('playerShell.tip.failed')}
        </p>
      ) : null}
    </section>
  );
}
