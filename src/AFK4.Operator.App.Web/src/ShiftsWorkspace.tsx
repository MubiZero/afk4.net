import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients } from './operatorHelpers';
import type { OperatorBackendContext } from './operatorTypes';
import type { MoneyDto, ShiftRevenueDto } from './operatorApiClients';

interface ShiftRevenueClient {
  current(branchId: string): Promise<ShiftRevenueDto | null>;
  history(branchId: string, limit?: number): Promise<{ shifts: ShiftRevenueDto[]; limit: number }>;
}

function formatMoney(m: MoneyDto): string {
  const major = (m.minorUnits / 100).toFixed(m.minorUnits % 100 === 0 ? 0 : 2);
  return major.replace(/\B(?=(\d{3})+(?!\d))/g, ' ');
}

export function ShiftsWorkspace({
  backend,
  branchId,
  client: injectedClient
}: {
  backend: OperatorBackendContext | null;
  branchId: string;
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

  useEffect(() => {
    if (client === null) return undefined;
    let active = true;
    setLoading(true);
    Promise.all([client.current(branchId), client.history(branchId, 20)])
      .then(([cur, hist]) => {
        if (!active) return;
        setCurrent(cur);
        setHistory(hist.shifts.filter((s) => s.state === 'closed'));
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [client, branchId]);

  if (loading) {
    return <main className="workspace-screen shifts-screen"><p>{t('op.shifts.loading')}</p></main>;
  }

  return (
    <main className="workspace-screen shifts-screen">
      <section className="screen-head"><h1>{t('op.shifts.title')}</h1></section>

      {current ? (
        <section className="op-shifts-current">
          <h2>{t('op.shifts.current')}</h2>
          <div>{t('op.shifts.earned')}: {formatMoney(current.earned.total)}</div>
          <div>{t('op.shifts.time')}: {formatMoney(current.earned.time)}</div>
          <div>{t('op.shifts.goods')}: {formatMoney(current.earned.goods)}</div>
          <div>
            {t('op.shifts.inflow')}: {t('op.shifts.cash')} {formatMoney(current.inflow.cash)}{' '}
            · {t('op.shifts.nonCash')} {formatMoney(current.inflow.nonCash)}
          </div>
          <div>{t('op.shifts.walletTopUps')}: {formatMoney(current.inflow.walletTopUps)}</div>
          <div>
            {t('op.shifts.cashExpected')}: {formatMoney(current.cash.expected)}
            {current.cash.difference
              ? ` · ${t('op.shifts.cashDiff')}: ${formatMoney(current.cash.difference)}`
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
                {new Date(s.openedAtUtc).toLocaleDateString('ru-RU')} · {t('op.shifts.earned')} {formatMoney(s.earned.total)}
                {s.cash.difference ? ` · ${t('op.shifts.cashDiff')} ${formatMoney(s.cash.difference)}` : ''}
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}
