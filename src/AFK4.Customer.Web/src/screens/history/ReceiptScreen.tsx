import { useEffect, useState } from 'react';
import type { PlayerApiClient } from '@/api/playerApi';
import type { PlayerVisitReceiptDto } from '@/api/types';
import { formatMoney } from '@/lib/money';
import { formatDateTime, formatDuration } from '@/lib/datetime';

type Load =
  | { state: 'loading' }
  | { state: 'notfound' }
  | { state: 'error' }
  | { state: 'ready'; receipt: PlayerVisitReceiptDto };

export function ReceiptScreen({ api, sessionId, onBack }: { api: PlayerApiClient; sessionId: string; onBack: () => void }) {
  const [load, setLoad] = useState<Load>({ state: 'loading' });

  useEffect(() => {
    let cancelled = false;
    api.getVisitReceipt(sessionId)
      .then((receipt) => { if (!cancelled) setLoad({ state: 'ready', receipt }); })
      .catch((error: unknown) => {
        if (cancelled) return;
        const status = (error as { status?: number }).status;
        setLoad({ state: status === 404 ? 'notfound' : 'error' });
      });
    return () => { cancelled = true; };
  }, [api, sessionId]);

  return (
    <main className="px-6 py-6">
      <button type="button" onClick={onBack} className="mb-4 min-h-[44px] text-sm text-[var(--text-2)] focus-visible:outline-2 focus-visible:outline-[var(--accent)]">← Назад</button>

      {load.state === 'loading' && <div role="status" aria-label="Загрузка чека" className="h-48 animate-pulse rounded-2xl bg-[var(--color-surface)]" />}
      {load.state === 'notfound' && <p className="py-12 text-center text-[var(--text-2)]">Чек не найден</p>}
      {load.state === 'error' && <p className="py-12 text-center text-red-400">Не удалось загрузить чек.</p>}

      {load.state === 'ready' && (
        <article className="space-y-4 rounded-2xl bg-[var(--color-surface)] p-5">
          <header className="flex items-baseline justify-between">
            <h1 className="text-lg font-extrabold tracking-tight">{load.receipt.receiptNumber}</h1>
            <span className="text-sm text-[var(--text-2)]">{formatDateTime(load.receipt.createdAtUtc)}</span>
          </header>
          <p className="text-sm text-[var(--text-2)]">
            {load.receipt.seatName} · {formatDuration(load.receipt.startedAtUtc, load.receipt.endedAtUtc)}
          </p>

          <div className="flex justify-between border-t border-[var(--color-border)] pt-3 text-sm">
            <span className="text-[var(--text-2)]">Время</span>
            <span>{formatMoney(load.receipt.timeChargeMinorUnits, load.receipt.currencyCode)}</span>
          </div>

          {load.receipt.posLines.length > 0 && (
            <ul className="space-y-1.5">
              {load.receipt.posLines.map((line, index) => (
                <li key={index} className="flex justify-between text-sm">
                  <span className="text-[var(--text-2)]"><span>{line.productName}</span> × {line.quantity}</span>
                  <span>{formatMoney(line.lineTotalMinorUnits, load.receipt.currencyCode)}</span>
                </li>
              ))}
            </ul>
          )}

          <div className="flex justify-between border-t border-[var(--color-border)] pt-3 text-base font-extrabold">
            <span>Итого</span>
            <span>{formatMoney(load.receipt.grandTotalMinorUnits, load.receipt.currencyCode)}</span>
          </div>
        </article>
      )}
    </main>
  );
}
