import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients, formatMoney } from '../operatorHelpers';
import { StateFlag } from '../operatorPrimitives';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import type { ShiftRevenueDto } from '../operatorApiClients';
import { buildCashHeader } from './cashModel';
import { CashShiftCommandBar, type CashShiftActionsClient } from './CashShiftCommandBar';

interface ShiftRevenueReader {
  current(branchId: string): Promise<ShiftRevenueDto | null>;
}

// Якорь раздела «Касса»: статус текущей смены (виден из любой вкладки) + командная панель смены
// (открыть/внести/изъять/закрыть). Действие → onShiftChanged → раздел бампает shiftNonce →
// шапка и вкладка «Смена» перечитывают смену.
export function CashShiftHeader({
  backend,
  currencyCode,
  session = null,
  shiftNonce = 0,
  onShiftChanged = () => {},
  client: injectedClient,
  actions
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session?: OperatorAuthSession | null;
  shiftNonce?: number;
  onShiftChanged?: () => void;
  client?: ShiftRevenueReader;
  actions?: CashShiftActionsClient;
}) {
  const { t } = useI18n();
  const memoizedClient = useMemo(
    () => (backend && !injectedClient ? createAuthenticatedOperatorClients(backend.config, backend.session).shiftRevenue : null),
    [backend?.config, backend?.session, injectedClient]
  );
  const client = injectedClient ?? memoizedClient;
  const [revenue, setRevenue] = useState<ShiftRevenueDto | null>(null);

  useEffect(() => {
    if (client === null || backend === null) return undefined;
    let active = true;
    setRevenue(null);
    client.current(backend.branchId)
      .then((cur) => { if (active) setRevenue(cur); })
      .catch(() => { if (active) setRevenue(null); });
    return () => { active = false; };
  }, [client, backend?.branchId, shiftNonce]);

  const header = buildCashHeader(revenue);

  return (
    <section className="cash-head">
      <h1>
        <strong className="cash-head-name">{t('op.cash.title')}</strong>
        {' · '}
        <span className="cash-head-tagline">
          {header.isOpen ? t('op.cash.header.open') : t('op.cash.header.closed')}
        </span>
      </h1>
      {header.isOpen && (
        <div className="cash-head-metrics">
          <StateFlag label={t('op.cash.metric.inHand')} value={formatMoney(header.cashInHand, currencyCode)} />
          <StateFlag label={t('op.cash.metric.revenue')} value={formatMoney(header.revenueTotal, currencyCode)} />
        </div>
      )}
      <CashShiftCommandBar
        backend={backend}
        session={session}
        shiftId={revenue?.shiftId ?? null}
        isOpen={header.isOpen}
        expectedCash={header.cashInHand}
        currencyCode={currencyCode}
        onShiftChanged={onShiftChanged}
        actions={actions}
      />
    </section>
  );
}
