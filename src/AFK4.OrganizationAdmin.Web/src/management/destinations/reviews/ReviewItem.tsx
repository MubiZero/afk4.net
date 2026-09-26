import { useState } from 'react';
import { EyeOff, Star } from 'lucide-react';
import { ReviewHideReasonNames } from '@afk4/contracts';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { projectOperatorError } from '../../../apiErrors';
import type { BranchReviewDto, ReviewHideReasonName } from '../../../api/clients/reviews';

/** Предел ответа — зеркало `ReviewLimits.ReplyMax` (статические пределы кодоген не переносит). */
export const REPLY_MAX = 1000;

const REASON_KEY: Record<ReviewHideReasonName, MessageKey> = {
  [ReviewHideReasonNames.Insult]: 'op.reviews.hide.reason.insult',
  [ReviewHideReasonNames.PersonalData]: 'op.reviews.hide.reason.personal_data',
  [ReviewHideReasonNames.Spam]: 'op.reviews.hide.reason.spam',
  [ReviewHideReasonNames.Other]: 'op.reviews.hide.reason.other'
};
const REASONS = Object.keys(REASON_KEY) as ReviewHideReasonName[];

export interface ReviewActions {
  reply(review: BranchReviewDto, text: string): Promise<void>;
  hide(review: BranchReviewDto, reason: ReviewHideReasonName): Promise<void>;
  show(review: BranchReviewDto): Promise<void>;
}

type Mode = { kind: 'idle' } | { kind: 'reply'; draft: string } | { kind: 'hide'; reason: ReviewHideReasonName | null };

/**
 * Отзыв в Панели. Ответ клуба игрок видит под отзывом в приложении; скрытый текст прячется от
 * игроков, а звёзды остаются в оценке — иначе рейтинг чистили бы от неудобных единиц.
 */
export function ReviewItem({ review, actions }: { review: BranchReviewDto; actions: ReviewActions | null }) {
  const { t, formatDate } = useI18n();
  const [mode, setMode] = useState<Mode>({ kind: 'idle' });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const hidden = review.commentHiddenAtUtc != null;

  const run = async (action: () => Promise<void>) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      setMode({ kind: 'idle' });
    } catch (failure) {
      setError(projectOperatorError(failure, t).detail);
    } finally {
      setBusy(false);
    }
  };

  return (
    <li className="management-panel reviews-item">
      <header>
        <span className="reviews-stars" aria-label={t('op.reviews.stars', { count: review.rating })}>
          {Array.from({ length: 5 }, (_, index) => (
            <Star key={index} size={14} aria-hidden="true" className={index < review.rating ? 'is-on' : undefined} />
          ))}
        </span>
        <strong>{review.authorName || t('op.reviews.anonymous')}</strong>
        {review.seatName && <span className="ui-chip ui-chip--xs">{review.seatName}</span>}
        <time dateTime={review.createdAtUtc}>{formatDate(review.createdAtUtc)}</time>
      </header>

      {review.comment
        ? <p className={hidden ? 'reviews-comment is-hidden' : 'reviews-comment'}>{review.comment}</p>
        : <p className="reviews-no-comment">{t('op.reviews.noComment')}</p>}

      {hidden && (
        <p className="reviews-hidden-note">
          <EyeOff size={13} aria-hidden="true" />
          {t('op.reviews.hidden', { reason: t(REASON_KEY[review.commentHiddenReason ?? ReviewHideReasonNames.Other] ?? REASON_KEY.other) })}
        </p>
      )}

      {review.reply && mode.kind !== 'reply' && (
        <blockquote className="reviews-reply">
          <span className="reviews-reply-label">
            {t('op.reviews.reply.label')}
            {review.repliedAtUtc && <time dateTime={review.repliedAtUtc}>{formatDate(review.repliedAtUtc)}</time>}
          </span>
          <p>{review.reply}</p>
        </blockquote>
      )}

      {mode.kind === 'reply' && actions && (
        <form
          className="reviews-editor"
          onSubmit={(event) => {
            event.preventDefault();
            void run(() => actions.reply(review, mode.draft));
          }}
        >
          <label htmlFor={`review-reply-${review.reviewId}`}>{t('op.reviews.reply.label')}</label>
          <textarea
            id={`review-reply-${review.reviewId}`}
            rows={3}
            maxLength={REPLY_MAX}
            autoFocus
            placeholder={t('op.reviews.reply.placeholder')}
            value={mode.draft}
            disabled={busy}
            onChange={(event) => setMode({ kind: 'reply', draft: event.currentTarget.value })}
          />
          <div className="reviews-editor-actions">
            <button type="submit" className="ui-btn ui-btn--sm ui-btn--primary" disabled={busy || mode.draft.trim() === ''}>
              {t('op.reviews.reply.send')}
            </button>
            <button type="button" className="ui-btn ui-btn--sm" disabled={busy} onClick={() => setMode({ kind: 'idle' })}>
              {t('op.reviews.cancel')}
            </button>
          </div>
        </form>
      )}

      {mode.kind === 'hide' && actions && (
        <form
          className="reviews-editor"
          onSubmit={(event) => {
            event.preventDefault();
            if (mode.reason) void run(() => actions.hide(review, mode.reason!));
          }}
        >
          <fieldset disabled={busy}>
            <legend>{t('op.reviews.hide.title')}</legend>
            <p className="reviews-editor-hint">{t('op.reviews.hide.hint')}</p>
            <div className="reviews-reasons">
              {REASONS.map((reason) => (
                <label key={reason} className="mgmt-check">
                  <input
                    type="radio"
                    name={`review-hide-${review.reviewId}`}
                    checked={mode.reason === reason}
                    onChange={() => setMode({ kind: 'hide', reason })}
                  />
                  {t(REASON_KEY[reason])}
                </label>
              ))}
            </div>
          </fieldset>
          <div className="reviews-editor-actions">
            <button type="submit" className="ui-btn ui-btn--sm ui-btn--danger" disabled={busy || mode.reason === null}>
              {t('op.reviews.hide.confirm')}
            </button>
            <button type="button" className="ui-btn ui-btn--sm" disabled={busy} onClick={() => setMode({ kind: 'idle' })}>
              {t('op.reviews.cancel')}
            </button>
          </div>
        </form>
      )}

      {error && <p className="reviews-error" role="alert">{error}</p>}

      {actions && mode.kind === 'idle' && (
        <div className="reviews-item-actions">
          <button type="button" className="ui-btn ui-btn--sm" onClick={() => setMode({ kind: 'reply', draft: review.reply ?? '' })}>
            {review.reply ? t('op.reviews.reply.edit') : t('op.reviews.reply')}
          </button>
          {review.reply && (
            <button type="button" className="ui-btn ui-btn--sm" disabled={busy} onClick={() => void run(() => actions.reply(review, ''))}>
              {t('op.reviews.reply.remove')}
            </button>
          )}
          {review.comment && (hidden
            ? (
              <button type="button" className="ui-btn ui-btn--sm" disabled={busy} onClick={() => void run(() => actions.show(review))}>
                {t('op.reviews.show')}
              </button>
            )
            : (
              <button type="button" className="ui-btn ui-btn--sm" onClick={() => setMode({ kind: 'hide', reason: null })}>
                {t('op.reviews.hide')}
              </button>
            ))}
        </div>
      )}
    </li>
  );
}
