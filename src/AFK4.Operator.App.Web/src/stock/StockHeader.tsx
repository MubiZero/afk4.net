import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients } from '../operatorHelpers';
import { formatMinorUnits } from '../currencyFormat';
import { StateFlag } from '../operatorPrimitives';
import { hasAnyPermission, permissionNames } from '../operatorPermissions';
import { mapCatalogToStock, summarize } from './stockLevels';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';

// Якорь раздела «Склад» (зеркало .cash-head): заголовок + метрики склада, видные из любой вкладки.
// Метрики перечитываются при stockNonce — раздел бампает его после приёмки/инвентаризации/списания,
// чтобы шапка не показывала устаревшую стоимость/счётчики.
export function StockHeader({
  backend,
  currencyCode,
  session,
  stockNonce = 0,
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
  stockNonce?: number;
}) {
  const { t } = useI18n();
  const canView = hasAnyPermission(session, [permissionNames.viewInventory, permissionNames.manageInventoryStock]);

  const clients = useMemo(
    () => (backend && canView ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session, canView]
  );

  const [summary, setSummary] = useState<{ totalValueMinorUnits: number; lowCount: number; outCount: number } | null>(null);

  useEffect(() => {
    if (clients === null || backend === null || !canView) { setSummary(null); return; }
    let alive = true;
    clients.pos.getCatalog(backend.branchId)
      .then((catalog) => { if (alive) setSummary(summarize(mapCatalogToStock(catalog))); })
      .catch(() => { if (alive) setSummary(null); });
    return () => { alive = false; };
  }, [clients, backend?.branchId, canView, stockNonce]);

  return (
    <section className="cash-head">
      <h1>
        <span className="cash-head-name">{t('op.stock.title')}</span>
      </h1>
      {summary && (
        <div className="cash-head-metrics">
          <StateFlag label={t('op.stock.summary.totalValue')} value={formatMinorUnits(summary.totalValueMinorUnits, currencyCode)} />
          {summary.lowCount > 0 && (
            <StateFlag label={t('op.stock.status.low')} value={String(summary.lowCount)} tone="warning" />
          )}
          {summary.outCount > 0 && (
            <StateFlag label={t('op.stock.status.out')} value={String(summary.outCount)} critical />
          )}
        </div>
      )}
    </section>
  );
}
