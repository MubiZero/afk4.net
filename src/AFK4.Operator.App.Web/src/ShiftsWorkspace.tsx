import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients, formatMoney } from './operatorHelpers';
import { projectOperatorError } from './apiErrors';
import type { OperatorBackendContext } from './operatorTypes';
import type { ShiftRevenueDto } from './operatorApiClients';

interface ShiftRevenueClient {
  current(branchId: string): Promise<ShiftRevenueDto | null>;
  history(branchId: string, limit?: number): Promise<{ shifts: ShiftRevenueDto[]; limit: number }>;
}

export function ShiftsWorkspace({
  backend,
  branchId,
  currencyCode,
  client: injectedClient
}: {
  backend: OperatorBackendContext | null;
  branchId: string;
  currencyCode: string;
  client?: ShiftRevenueClient;
}) {
  const { t } = useI18n();
  const memoizedClient = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session).shiftRevenue : null),
    [backend?.config, backend?.session]
  );
  const client = injectedClient ?? memoizedClient;

  const [current, setCurrent] = useState<ShiftRevenueDto | null>(null);
  const [history, setHistory] = useState<ShiftRevenueDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    if (client === null) return undefined;
    let active = true;
    setLoading(true);
    setLoadError(null);
    Promise.all([client.current(branchId), client.history(branchId, 20)])
      .then(([cur, hist]) => {
        if (!active) return;
        setCurrent(cur);
        setHistory(hist.shifts.filter((s) => s.state === 'closed'));
      })
      .catch((error) => { if (active) setLoadError(projectOperatorError(error, t).detail); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [client, branchId]);

  if (loading) {
    return <main className="workspace-screen shifts-screen"><p className="workspace-loading">{t('op.shifts.loading')}</p></main>;
  }

  if (loadError) {
    return (
      <main className="workspace-screen shifts-screen">
        <section className="screen-head"><h1>{t('op.shifts.title')}</h1></section>
        <p className="workspace-error" role="alert">{loadError}</p>
      </main>
    );
  }

  return (
    <main className="workspace-screen shifts-screen">
      <section className="screen-head"><h1>{t('op.shifts.title')}</h1></section>

      {current ? (
        <section className="op-shifts-current">
          <h2>{t('op.shifts.current')}</h2>
          <div>{t('op.shifts.earned')}: {formatMoney(current.earned.total, currencyCode)}</div>
          <div>{t('op.shifts.time')}: {formatMoney(current.earned.time, currencyCode)}</div>
          <div>{t('op.shifts.goods')}: {formatMoney(current.earned.goods, currencyCode)}</div>
          <div>
            {t('op.shifts.inflow')}: {t('op.shifts.cash')} {formatMoney(current.inflow.cash, currencyCode)}{' '}
            · {t('op.shifts.nonCash')} {formatMoney(current.inflow.nonCash, currencyCode)}
          </div>
          <div>{t('op.shifts.walletTopUps')}: {formatMoney(current.inflow.walletTopUps, currencyCode)}</div>
          <div>
            {t('op.shifts.cashExpected')}: {formatMoney(current.cash.expected, currencyCode)}
            {current.cash.difference
              ? ` · ${t('op.shifts.cashDiff')}: ${formatMoney(current.cash.difference, currencyCode)}`
              : ''}
          </div>
        </section>
      ) : (
        <section className="op-shifts-empty">{t('op.shifts.noOpenShift')}</section>
      )}

      <section className="op-shifts-history">
        <h2>{t('op.shifts.history')}</h2>
        {history.length === 0 ? (
          <div>{t('op.shifts.historyEmpty')}</div>
        ) : (
          <ul>
            {history.map((s) => (
              <li key={s.shiftId}>
                {new Date(s.openedAtUtc).toLocaleDateString('ru-RU')} · {t('op.shifts.earned')} {formatMoney(s.earned.total, currencyCode)}
                {s.cash.difference ? ` · ${t('op.shifts.cashDiff')} ${formatMoney(s.cash.difference, currencyCode)}` : ''}
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}
