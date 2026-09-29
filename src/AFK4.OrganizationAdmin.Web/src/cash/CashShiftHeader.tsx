import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import { SectionHeader, type HeaderCounts } from '@afk4/ui/react';
import { createAuthenticatedOperatorClients } from '../operatorHelpers';
import { Money } from '../operatorPrimitives';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import type { ShiftRevenueDto } from '../operatorApiClients';
import { buildCashHeader } from './cashModel';
import { CashShiftCommandBar, type CashShiftActionsClient } from './CashShiftCommandBar';

interface ShiftRevenueReader {
  current(branchId: string): Promise<ShiftRevenueDto | null>;
}

// Шапка раздела «Касса»: деньги смены и её команды (открыть/внести/изъять/закрыть) — на любой
// вкладке, включая «Смену»: вкладка их не повторяет, и шапка не пропадает при переключении.
// Действие → onShiftChanged → раздел бампает shiftNonce → шапка и вкладка перечитывают смену.
export function CashShiftHeader({
  backend,
  currencyCode,
  session = null,
  shiftNonce = 0,
  onShiftChanged = () => {},
  client: injectedClient,
  actions,
  tabs
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session?: OperatorAuthSession | null;
  shiftNonce?: number;
  onShiftChanged?: () => void;
  client?: ShiftRevenueReader;
  actions?: CashShiftActionsClient;
  tabs?: ReactNode;
}) {
  const { t } = useI18n();
  // Боевой клиент строим только при backend && !injectedClient: тесты подают injectedClient с
  // фейковым backend, на котором createAuthenticatedOperatorClients/PlatformApiClient падает на init.
  // injectedClient — в deps массива (закрывает exhaustive-deps; иначе тест с фейк-backend ломался — урок S0).
  const memoizedClient = useMemo(
    () => (backend && !injectedClient ? createAuthenticatedOperatorClients(backend.config, backend.session).shiftRevenue : null),
    [backend?.config, backend?.session, injectedClient]
  );
  const client = injectedClient ?? memoizedClient;
  // undefined — ещё не спросили: до ответа шапка не утверждает ни «открыта», ни «не открыта».
  const [revenue, setRevenue] = useState<ShiftRevenueDto | null | undefined>(undefined);

  useEffect(() => {
    if (client === null || backend === null) return undefined;
    let active = true;
    client.current(backend.branchId)
      .then((cur) => { if (active) setRevenue(cur); })
      .catch(() => { if (active) setRevenue(undefined); });
    return () => { active = false; };
  }, [client, backend?.branchId, shiftNonce]);

  const known = revenue !== undefined;
  const header = buildCashHeader(revenue ?? null);
  const counts: HeaderCounts = !known
    ? []
    : header.isOpen
      ? [
        { label: t('op.cash.metric.inHand'), value: <Money minorUnits={header.cashInHand?.minorUnits ?? 0} currencyCode={currencyCode} /> },
        { label: t('op.cash.metric.revenue'), value: <Money minorUnits={header.revenueTotal?.minorUnits ?? 0} currencyCode={currencyCode} /> }
      ]
      : [{ label: t('op.cash.tab.shift'), value: t('op.cash.header.closed'), tone: 'warning' }];

  return (
    <SectionHeader
      title={t('op.cash.title')}
      counts={counts}
      tabs={tabs}
      action={known ? (
        <CashShiftCommandBar
          backend={backend}
          session={session}
          shiftId={revenue?.shiftId ?? null}
          isOpen={header.isOpen}
          openedByStaffUserId={revenue?.openedByStaffUserId ?? null}
          expectedCash={header.cashInHand}
          currencyCode={currencyCode}
          revenue={revenue ?? null}
          onShiftChanged={onShiftChanged}
          actions={actions}
        />
      ) : undefined}
    />
  );
}
