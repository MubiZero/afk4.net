import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { ActiveSessionDto } from '@/api/types';
import { elapsedSeconds, projectRemainingSeconds, formatClock } from './liveSession';
import { formatMoney } from '@/lib/money';

export function LiveSessionCard({ session, fetchedAt }: { session: ActiveSessionDto; fetchedAt: Date }) {
  const { t } = useI18n();
  const [, setTick] = useState(0);
  useEffect(() => {
    const id = setInterval(() => setTick((t) => t + 1), 1000);
    return () => clearInterval(id);
  }, []);

  const clock = session.durationMode === 'fixed'
    ? formatClock(projectRemainingSeconds(session.remainingSeconds ?? 0, fetchedAt))
    : formatClock(elapsedSeconds(session.startedAtUtc));

  return (
    <section className="rounded-2xl border-l-2 border-[var(--accent)] bg-[var(--color-surface)] p-4">
      <div className="flex items-center justify-between text-xs text-[var(--text-2)]">
        <span>{session.durationMode === 'fixed' ? t('customer.dashboard.sessionRemaining') : t('customer.dashboard.sessionActive')}</span>
        <span className="font-bold text-[var(--text-1)]">{session.seatName}</span>
      </div>
      {/* aria-hidden on the per-second clock so screen-readers don't announce every tick; the sr-only label carries the meaning, re-read only on focus. */}
      <p aria-hidden="true" data-testid="session-timer" className="mt-1.5 text-3xl font-extrabold tracking-tight">{clock}</p>
      <span className="sr-only">
        {session.durationMode === 'fixed' ? 'Осталось времени: ' : 'Сессия идёт: '}{clock}
      </span>
      {/* Fixed sessions are pre-paid — no per-second accrual; only open sessions show a running cost. */}
      {session.durationMode === 'open' && session.accruedCostMinorUnits != null && (
        <p className="mt-1 text-sm text-[var(--text-2)]">
          ≈ <span className="text-[var(--accent)]">{formatMoney(session.accruedCostMinorUnits, session.currencyCode)}</span> {t('customer.dashboard.accrued')}
        </p>
      )}
    </section>
  );
}
