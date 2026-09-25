import { useEffect, useMemo, useState } from 'react';
import { MessageSquareText, Star } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../ManagementScreen';
import { EmptyState, PartialLoadFailure } from '../../../operatorPrimitives';
import { DeferredSkeleton, SkeletonTable } from '../../../LoadingSkeleton';
import { createAuthenticatedOperatorClients } from '../../../operatorHelpers';
import { projectOperatorError, type OperatorErrorProjection } from '../../../apiErrors';
import type { BranchReviewDto, BranchReviewsPageDto } from '../../../api/clients/reviews';
import type { DestinationProps } from '../types';

type Filter = { rating: number | null; withComment: boolean };

/**
 * «Отзывы» (решение владельца 24.09): что игроки пишут о филиале — из приложения и с итога
 * сессии на ПК. У каждого отзыва — ПК, за которым сидели: жалобу на мышь находят на ПК 07.
 */
export function ReviewsDestination({ backend, onDirtyChange }: DestinationProps) {
  const { t, formatDate, formatNumber } = useI18n();
  const client = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session).reviews : null),
    [backend?.config, backend?.session]
  );
  const branchId = backend?.branchId ?? null;
  const [filter, setFilter] = useState<Filter>({ rating: null, withComment: false });
  const [page, setPage] = useState<BranchReviewsPageDto | null>(null);
  const [items, setItems] = useState<BranchReviewDto[]>([]);
  const [failure, setFailure] = useState<OperatorErrorProjection | null>(null);
  const [loadingMore, setLoadingMore] = useState(false);

  useEffect(() => { onDirtyChange?.(false); }, [onDirtyChange]);

  useEffect(() => {
    if (client === null || branchId === null) return undefined;
    let active = true;
    setFailure(null);
    client.list(branchId, filter)
      .then((next) => { if (active) { setPage(next); setItems(next.items); } })
      .catch((error) => { if (active) setFailure(projectOperatorError(error, t)); });
    return () => { active = false; };
  }, [client, branchId, filter.rating, filter.withComment]);

  const more = async () => {
    if (client === null || branchId === null || !page?.nextBefore) return;
    setLoadingMore(true);
    try {
      const next = await client.list(branchId, { ...filter, before: page.nextBefore });
      setPage(next);
      setItems((current) => [...current, ...next.items]);
    } catch (error) {
      setFailure(projectOperatorError(error, t));
    } finally {
      setLoadingMore(false);
    }
  };

  const content = () => {
    if (page === null && failure === null) {
      return <DeferredSkeleton><SkeletonTable gridTemplate="1fr 2fr 1fr" /></DeferredSkeleton>;
    }

    if (page === null && failure !== null) {
      return (
        <EmptyState
          title={t('op.management.state.errorTitle')}
          description={failure.detail}
          next={{ kind: 'action', label: t('op.management.state.retry'), onClick: () => setFilter({ ...filter }) }}
        />
      );
    }

    const summary = page!;
    const maxCount = Math.max(1, ...summary.countsByRating);
    return (
      <div className="reviews-screen">
        <section className="management-panel reviews-summary" aria-label={t('op.reviews.summary')}>
          <div className="reviews-average">
            <strong>{summary.rating === null ? '—' : formatNumber(summary.rating)}</strong>
            <span>{t('op.reviews.count', { count: summary.reviewCount })}</span>
          </div>
          <ol className="reviews-bars">
            {[5, 4, 3, 2, 1].map((stars) => {
              const count = summary.countsByRating[stars - 1] ?? 0;
              return (
                <li key={stars}>
                  <button
                    type="button"
                    className="reviews-bar"
                    aria-pressed={filter.rating === stars}
                    onClick={() => setFilter({ ...filter, rating: filter.rating === stars ? null : stars })}
                  >
                    <span className="reviews-bar-label">{stars}<Star size={11} aria-hidden="true" /></span>
                    <span className="reviews-bar-track"><span style={{ width: `${(count / maxCount) * 100}%` }} /></span>
                    <span className="reviews-bar-count">{count}</span>
                  </button>
                </li>
              );
            })}
          </ol>
        </section>

        <div className="reviews-filters">
          <label className="mgmt-check">
            <input
              type="checkbox"
              checked={filter.withComment}
              onChange={(event) => setFilter({ ...filter, withComment: event.target.checked })}
            />
            {t('op.reviews.withComment')}
          </label>
          {filter.rating !== null && (
            <button type="button" className="ui-btn ui-btn--sm" onClick={() => setFilter({ ...filter, rating: null })}>
              {t('op.reviews.allRatings')}
            </button>
          )}
        </div>

        {failure !== null && <PartialLoadFailure text={failure.detail} failure={failure} onRetry={() => void more()} />}

        {items.length === 0 ? (
          <EmptyState
            icon={<MessageSquareText size={22} aria-hidden="true" />}
            title={summary.reviewCount === 0 ? t('op.reviews.empty') : t('op.reviews.emptyFiltered')}
            next={summary.reviewCount === 0
              ? { kind: 'calm', hint: t('op.reviews.emptyHint') }
              : { kind: 'action', label: t('op.reviews.allRatings'), onClick: () => setFilter({ rating: null, withComment: false }) }}
          />
        ) : (
          <ul className="reviews-list">
            {items.map((review) => (
              <li key={review.reviewId} className="management-panel reviews-item">
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
                  ? <p>{review.comment}</p>
                  : <p className="reviews-no-comment">{t('op.reviews.noComment')}</p>}
              </li>
            ))}
          </ul>
        )}

        {page?.nextBefore && (
          <button type="button" className="ui-btn reviews-more" disabled={loadingMore} onClick={() => void more()}>
            {t('op.reviews.more')}
          </button>
        )}
      </div>
    );
  };

  return (
    <ManagementScreen title={t('op.management.dest.reviews')} subtitle={t('op.management.dest.reviews.subtitle')} contentWidth="wide">
      {content()}
    </ManagementScreen>
  );
}
