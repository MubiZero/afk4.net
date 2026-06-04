import { useCallback } from 'react';
import { useI18n } from '@afk4/i18n';
import type { PlayerApiClient } from '@/api/playerApi';
import type { PlayerVisitDto } from '@/api/types';
import { useCursorList } from '@/lib/useCursorList';
import { formatMoney } from '@/lib/money';
import { formatDateTime, formatDuration } from '@/lib/datetime';

export function VisitsScreen({ api, onOpenReceipt }: { api: PlayerApiClient; onOpenReceipt: (sessionId: string) => void }) {
  const { t } = useI18n();
  const fetchPage = useCallback((cursor?: string) => api.getVisits(cursor), [api]);
  const list = useCursorList<PlayerVisitDto>(fetchPage);

  if (list.status === 'loading') {
    return (
      <div className="space-y-3 px-6 py-6" role="status" aria-label={t('a11y.loading.visits')}>
        {[0, 1, 2].map((i) => <div key={i} className="h-20 animate-pulse rounded-2xl bg-[var(--color-surface)]" />)}
      </div>
    );
  }
  if (list.status === 'error') {
    return (
      <div className="px-6 py-10 text-center">
        <p className="text-sm text-red-400">{t('customer.history.loadError')}</p>
        <button type="button" onClick={list.retry} className="mt-3 text-sm text-[var(--accent)] focus-visible:outline-2 focus-visible:outline-[var(--accent)]">{t('customer.common.retry')}</button>
      </div>
    );
  }
  if (list.items.length === 0) {
    return <p className="px-6 py-12 text-center text-[var(--text-2)]">{t('customer.history.noVisits')}</p>;
  }

  return (
    <div className="space-y-3 px-6 py-6">
      {list.items.map((visit) => (
        <article key={visit.sessionId} className="rounded-2xl bg-[var(--color-surface)] p-4">
          <div className="flex items-center justify-between">
            <span className="font-bold text-[var(--text-1)]">{visit.seatName}</span>
            <span className="text-lg font-extrabold tracking-tight">{formatMoney(visit.grandTotalMinorUnits, visit.currencyCode)}</span>
          </div>
          <p className="mt-1 text-sm text-[var(--text-2)]">
            {formatDateTime(visit.startedAtUtc)} · {formatDuration(visit.startedAtUtc, visit.endedAtUtc)}
          </p>
          {visit.hasReceipt && (
            <button
              type="button"
              onClick={() => onOpenReceipt(visit.sessionId)}
              className="mt-2 min-h-[44px] text-sm text-[var(--accent)] focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
            >
              {t('customer.receipt.openLink')}
            </button>
          )}
        </article>
      ))}
      {list.hasMore && (
        <button
          type="button"
          onClick={list.loadMore}
          disabled={list.loadingMore}
          className="min-h-[44px] w-full rounded-xl border border-[var(--color-border)] text-sm text-[var(--text-2)] disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
        >
          {list.loadingMore ? t('customer.common.loading') : t('customer.common.loadMore')}
        </button>
      )}
    </div>
  );
}
