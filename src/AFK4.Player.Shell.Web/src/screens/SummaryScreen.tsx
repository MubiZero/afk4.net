import { useCallback, useEffect, useId, useState } from 'react';
import type { PlayerShellStateDto, PlayerVisitReceiptDto } from '@afk4/contracts';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { formatMoney } from '@afk4/money';
import { CheckCircle2, Star } from 'lucide-react';
import { PlayerApiError, getJson, postJson } from '../api/playerApi';
import { INTL_LOCALES, durationKey } from '../model/offers';
import { playedMinutes, type EndedVisit } from '../model/visit';
import { SeatBadge } from '../ui/SeatBadge';
import { TipPanel } from './summary/TipPanel';

/** Итог держится 25 секунд тишины (спека, §3): прочесть и уйти; дальше ПК сам выводит вошедшего. */
export const SUMMARY_SECONDS = 25;

/** Предел комментария — тот же, что у сервера. */
const COMMENT_MAX = 1000;

interface SummaryScreenProps {
  state: PlayerShellStateDto;
  visit: EndedVisit;
  baseUrl: string | null;
  /** Растёт, когда трогают мышь или клавиатуру: пока человек пишет отзыв, итог не уходит. */
  activity: number;
  onPlayMore: () => void;
  onLeave: () => void;
}

type RatingState = 'idle' | 'sending' | 'sent' | 'failed';

/**
 * Итог визита — после любого конца сессии: сколько сыграно, во что обошлось, что вернулось, и оценка
 * визита. «Встал» и «сколько потратил» — одно событие; узнавать вторую половину из истории кошелька
 * человек не должен.
 */
export function SummaryScreen({ state, visit, baseUrl, activity, onPlayMore, onLeave }: SummaryScreenProps) {
  const { t, locale } = useI18n();
  const commentId = useId();
  const [receipt, setReceipt] = useState<PlayerVisitReceiptDto | null>(null);
  const [rating, setRating] = useState(0);
  const [comment, setComment] = useState('');
  const [ratingState, setRatingState] = useState<RatingState>('idle');
  const [touched, setTouched] = useState(0);
  const [held, setHeld] = useState(false);
  const hold = useCallback((value: boolean) => setHeld(value), []);

  useEffect(() => {
    if (!baseUrl) return;
    getJson<PlayerVisitReceiptDto>(baseUrl, `/api/me/visits/${visit.sessionId}/receipt`)
      .then(setReceipt)
      // Чека нет — итог скажет то, что знает (возврат раннего выхода), без сумм.
      .catch(() => {});
  }, [baseUrl, visit.sessionId]);

  // Тишина 25 секунд — выход. Любое касание, клавиша или ввод начинают отсчёт заново; пока человек
  // решает, списать ли чаевые, отсчёта нет вовсе.
  useEffect(() => {
    if (held) return undefined;
    const timer = window.setTimeout(onLeave, SUMMARY_SECONDS * 1000);
    return () => window.clearTimeout(timer);
  }, [onLeave, activity, touched, held]);

  const currency = receipt?.currencyCode ?? visit.selfEnd?.refunded.currencyCode ?? 'TJS';
  const money = (minorUnits: number, code = currency) => formatMoney(minorUnits, code, INTL_LOCALES[locale]);
  const duration = (minutes: number) => {
    const { key, values } = durationKey(minutes);
    return t(key, values);
  };

  const minutes = (receipt && playedMinutes(receipt)) ?? visit.selfEnd?.billedMinutes ?? null;
  const refunded = visit.selfEnd?.refunded;
  const packageMinutes = visit.selfEnd?.packageMinutesReturned ?? 0;

  const sendRating = async () => {
    if (!baseUrl || rating === 0) return;
    setRatingState('sending');
    try {
      await postJson(baseUrl, '/api/me/reviews', { sessionId: visit.sessionId, rating, comment: comment.trim() || null });
      setRatingState('sent');
    } catch (reason) {
      // Оценка за этот визит уже есть — например, с телефона: для человека это то же «спасибо».
      setRatingState(reason instanceof PlayerApiError && reason.code === 'already_reviewed' ? 'sent' : 'failed');
    }
  };

  const facts: { key: MessageKey; values: Record<string, string>; strong?: boolean }[] = [];
  if (minutes !== null) facts.push({ key: 'playerShell.summary.billed', values: { duration: duration(minutes) } });
  if (refunded && refunded.minorUnits > 0) {
    facts.push({ key: 'playerShell.summary.refund', values: { amount: money(refunded.minorUnits, refunded.currencyCode) }, strong: true });
  }
  if (packageMinutes > 0) facts.push({ key: 'playerShell.summary.packageReturn', values: { duration: duration(packageMinutes) } });
  if (receipt) {
    facts.push({ key: 'playerShell.summary.timeCharge', values: { amount: money(receipt.timeChargeMinorUnits) } });
    if (receipt.posTotalMinorUnits > 0) facts.push({ key: 'playerShell.summary.barTotal', values: { amount: money(receipt.posTotalMinorUnits) } });
    facts.push({ key: 'playerShell.summary.grandTotal', values: { amount: money(receipt.grandTotalMinorUnits) }, strong: true });
  }

  return (
    <main
      className="summary"
      onPointerDown={() => setTouched((count) => count + 1)}
      onKeyDown={() => setTouched((count) => count + 1)}
    >
      <header className="summary__top">
        <SeatBadge seatLabel={state.seatLabel} zoneName={state.zoneName} />
      </header>
      <section className="summary__body" role="status">
        <CheckCircle2 className="summary__icon" aria-hidden="true" />
        <h1 className="summary__title">{t('playerShell.summary.title')}</h1>
        {facts.map((fact) => (
          <p key={fact.key} className={fact.strong ? 'summary__refund' : undefined}>{t(fact.key, fact.values)}</p>
        ))}
      </section>

      {baseUrl ? (
        <section className="rating" aria-labelledby={`${commentId}-title`}>
          {ratingState === 'sent' ? (
            <p className="rating__thanks" role="status">{t('playerShell.rating.thanks')}</p>
          ) : (
            <>
              <h2 id={`${commentId}-title`} className="rating__title">{t('playerShell.rating.title')}</h2>
              <div className="rating__stars" role="group" aria-labelledby={`${commentId}-title`}>
                {[1, 2, 3, 4, 5].map((value) => (
                  <button
                    key={value}
                    type="button"
                    className="rating__star"
                    aria-label={t('playerShell.rating.star', { count: value })}
                    aria-pressed={rating === value}
                    data-lit={value <= rating || undefined}
                    onClick={() => setRating(value)}
                  >
                    <Star aria-hidden="true" />
                  </button>
                ))}
              </div>
              {rating > 0 ? (
                <>
                  <label className="field rating__comment" htmlFor={commentId}>
                    <span className="field__label">{t('playerShell.rating.comment')}</span>
                    <textarea
                      id={commentId}
                      className="field__input"
                      rows={2}
                      maxLength={COMMENT_MAX}
                      value={comment}
                      onChange={(event) => setComment(event.currentTarget.value)}
                    />
                  </label>
                  <button type="button" className="btn btn--primary" disabled={ratingState === 'sending'} onClick={() => void sendRating()}>
                    {ratingState === 'sending' ? t('playerShell.rating.sending') : t('playerShell.rating.send')}
                  </button>
                </>
              ) : null}
              {ratingState === 'failed' ? <p className="rating__failed" role="alert">{t('playerShell.rating.failed')}</p> : null}
            </>
          )}
        </section>
      ) : null}

      {baseUrl ? <TipPanel baseUrl={baseUrl} sessionId={visit.sessionId} onHold={hold} /> : null}

      <footer className="summary__actions">
        <button type="button" className="btn btn--ghost" onClick={onLeave}>{t('playerShell.signOut')}</button>
        <button type="button" className="btn btn--primary" onClick={onPlayMore}>{t('playerShell.summary.again')}</button>
      </footer>
    </main>
  );
}
