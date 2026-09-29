import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import { SectionHeader } from '@afk4/ui/react';
import { createAuthenticatedOperatorClients } from '../operatorHelpers';
import { Money } from '../operatorPrimitives';
import { hasAnyPermission, permissionNames } from '../operatorPermissions';
import { mapCatalogToStock, summarize } from './stockLevels';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';

// Шапка раздела «Склад»: название, стоимость склада и вкладки — на любой вкладке. Счётчики «на
// исходе» и «нет в наличии» живут в фильтрах Остатков: в шапке они повторяли те же числа второй
// раз. Стоимость перечитывается при stockNonce — раздел бампает его после
// приёмки/инвентаризации/списания.
export function StockHeader({
  backend,
  currencyCode,
  session,
  stockNonce = 0,
  tabs,
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
  stockNonce?: number;
  tabs?: ReactNode;
}) {
  const { t } = useI18n();
  const canView = hasAnyPermission(session, [permissionNames.viewInventory, permissionNames.manageInventoryStock]);

  const clients = useMemo(
    () => (backend && canView ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session, canView]
  );

  const [summary, setSummary] = useState<ReturnType<typeof summarize> | null>(null);

  useEffect(() => {
    if (clients === null || backend === null || !canView) { setSummary(null); return; }
    let alive = true;
    clients.pos.getCatalog(backend.branchId)
      .then((catalog) => { if (alive) setSummary(summarize(mapCatalogToStock(catalog))); })
      .catch(() => { if (alive) setSummary(null); });
    return () => { alive = false; };
  }, [clients, backend?.branchId, canView, stockNonce]);

  return (
    <SectionHeader
      title={t('op.stock.title')}
      counts={summary ? [{
        label: t('op.stock.summary.totalValue'),
        value: summary.partial ? (
          // Себестоимость известна не у всех товаров на полке — сумма настоящая, но не по всему
          // складу; «≈» и подсказка говорят об этом рядом с цифрой, а не только в тексте, который
          // легко пролистать.
          <span className="op-stock-total-partial" title={t('op.stock.summary.totalValuePartialHint')}>
            ≈ <Money minorUnits={summary.totalValueMinorUnits} currencyCode={currencyCode} />
          </span>
        ) : <Money minorUnits={summary.totalValueMinorUnits} currencyCode={currencyCode} />
      }] : []}
      tabs={tabs}
    />
  );
}
