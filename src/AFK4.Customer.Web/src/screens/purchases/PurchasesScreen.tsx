import { useCallback } from 'react';
import type { PlayerApiClient } from '@/api/playerApi';
import type { PlayerPurchaseDto } from '@/api/types';
import { useCursorList } from '@/lib/useCursorList';
import { formatMoney } from '@/lib/money';
import { formatDateTime } from '@/lib/datetime';

export function PurchasesScreen({ api }: { api: PlayerApiClient }) {
  const fetchPage = useCallback((cursor?: string) => api.getPurchases(cursor), [api]);
  const list = useCursorList<PlayerPurchaseDto>(fetchPage);

  if (list.status === 'loading') {
    return (
      <div className="space-y-3 px-6 py-6" role="status" aria-label="Загрузка покупок">
        {[0, 1, 2].map((i) => <div key={i} className="h-20 animate-pulse rounded-2xl bg-[var(--color-surface)]" />)}
      </div>
    );
  }
  if (list.status === 'error') {
    return (
      <div className="px-6 py-10 text-center">
        <p className="text-sm text-red-400">Не удалось загрузить покупки.</p>
        <button type="button" onClick={list.retry} className="mt-3 text-sm text-[var(--accent)] focus-visible:outline-2 focus-visible:outline-[var(--accent)]">Повторить</button>
      </div>
    );
  }
  if (list.items.length === 0) {
    return <p className="px-6 py-12 text-center text-[var(--text-2)]">Пока нет покупок</p>;
  }

  return (
    <div className="space-y-3 px-6 py-6">
      {list.items.map((purchase) => (
        <article key={purchase.posSaleId} className="rounded-2xl bg-[var(--color-surface)] p-4">
          <div className="flex items-center justify-between">
            <span className="text-sm text-[var(--text-2)]">{formatDateTime(purchase.createdAtUtc)}</span>
            <span className="text-lg font-extrabold tracking-tight">{formatMoney(purchase.totalMinorUnits, purchase.currencyCode)}</span>
          </div>
          <ul className="mt-2 space-y-1">
            {purchase.lines.map((line, idx) => (
              <li key={idx} className="text-sm text-[var(--text-1)]">
                {`${line.productName} × ${line.quantity}`}
              </li>
            ))}
          </ul>
        </article>
      ))}
      {list.hasMore && (
        <button
          type="button"
          onClick={list.loadMore}
          disabled={list.loadingMore}
          className="min-h-[44px] w-full rounded-xl border border-[var(--color-border)] text-sm text-[var(--text-2)] disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
        >
          {list.loadingMore ? 'Загрузка…' : 'Показать ещё'}
        </button>
      )}
    </div>
  );
}
