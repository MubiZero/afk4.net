import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { PlayerApiClient } from '@/api/playerApi';
import type { PlayerDashboardDto } from '@/api/types';
import { formatMoney } from '@/lib/money';
import { LiveSessionCard } from './LiveSessionCard';
import { WalletPanel } from '@/screens/wallet/WalletPanel';

type Load =
  | { state: 'loading' }
  | { state: 'error' }
  // Счёта в этом клубе ещё нет — это не поломка, а нормальное начало: он откроется первым действием.
  | { state: 'noClub' }
  | { state: 'ready'; data: PlayerDashboardDto; fetchedAt: Date };

export function DashboardScreen({ api, displayName, phoneVerified, features = null }: { api: PlayerApiClient; displayName: string; phoneVerified: boolean; features?: string[] | null }) {
  const { t } = useI18n();
  const [load, setLoad] = useState<Load>({ state: 'loading' });

  useEffect(() => {
    let cancelled = false;
    async function refresh() {
      try {
        const data = await api.getDashboard();
        if (!cancelled) setLoad({ state: 'ready', data, fetchedAt: new Date() });
      } catch (caught: unknown) {
        // 409 club_not_selected — это не сбой: у человека просто ещё нет счёта в этом клубе.
        // Показать «не удалось загрузить» значило бы позвать чинить то, что не сломано.
        const noClub = (caught as { message?: string }).message === 'club_not_selected';
        if (!cancelled) {
          setLoad((prev) => (prev.state === 'ready' ? prev : { state: noClub ? 'noClub' : 'error' }));
        }
      }
    }
    void refresh();
    const id = setInterval(refresh, 30_000);
    return () => {
      cancelled = true;
      clearInterval(id);
    };
  }, [api]);

  return (
    <main className="space-y-5 px-6 py-8">
      <header>
        <p className="text-sm text-[var(--text-2)]">{t('customer.dashboard.welcome')}</p>
        <h1 className="text-2xl font-extrabold tracking-tight">{displayName}</h1>
      </header>

      {load.state === 'loading' && (
        <div role="status" aria-label={t('a11y.loading.dashboard')} className="h-28 animate-pulse rounded-2xl bg-[var(--color-surface)]" />
      )}
      {load.state === 'error' && (
        <p className="text-sm text-red-400">{t('customer.dashboard.loadError')}</p>
      )}
      {load.state === 'noClub' && (
        <section className="rounded-2xl border border-dashed border-[var(--color-border)] p-6 text-center">
          <p className="font-bold">{t('customer.dashboard.noClub')}</p>
          <p className="mt-1 text-sm text-[var(--text-2)]">{t('customer.dashboard.noClubNote')}</p>
        </section>
      )}

      {load.state === 'ready' && (
        <>
          <section className="rounded-2xl bg-[var(--color-surface)] p-4">
            <p className="text-xs uppercase tracking-wide text-[var(--text-3)]">{t('customer.dashboard.balance')}</p>
            <p className="mt-1 text-3xl font-extrabold tracking-tight">
              {formatMoney(load.data.walletBalance.minorUnits, load.data.walletBalance.currencyCode)}
            </p>
            {/* Придержанное уже вычтено из остатка — без этой строки человек видел бы, что денег
                стало меньше, и не понимал, куда они делись. */}
            {load.data.heldBalance.minorUnits > 0 && (
              <p className="mt-2 text-sm text-[var(--text-2)]">
                {t('customer.dashboard.held')}:{' '}
                <span className="font-bold text-[var(--text-1)]">
                  {formatMoney(load.data.heldBalance.minorUnits, load.data.heldBalance.currencyCode)}
                </span>
                <br />
                <span className="text-[var(--text-3)]">{t('customer.dashboard.heldNote')}</span>
              </p>
            )}
            {load.data.debtBalance.minorUnits > 0 && (
              <p className="mt-1 text-sm text-red-400">
                {t('customer.dashboard.debt')}: {formatMoney(load.data.debtBalance.minorUnits, load.data.debtBalance.currencyCode)}
              </p>
            )}
          </section>

          <WalletPanel api={api} phoneVerified={phoneVerified} features={features} />

          {load.data.activeSession
            ? <LiveSessionCard session={load.data.activeSession} fetchedAt={load.fetchedAt} />
            : (
              <section className="rounded-2xl border border-dashed border-[var(--color-border)] p-6 text-center text-[var(--text-2)]">
                {t('customer.dashboard.noSession')}
              </section>
            )}
        </>
      )}
    </main>
  );
}
