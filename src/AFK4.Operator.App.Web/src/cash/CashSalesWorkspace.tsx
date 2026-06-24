import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { hasAnyPermission, permissionNames } from '../operatorPermissions';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import { BackendPosWorkspace } from '../BackendPosWorkspace';
import { ShopOrdersWorkspace } from '../ShopOrdersWorkspace';

type SalesSegment = 'pos' | 'orders';

// Вкладка «Продажи» = POS («Касса») + очередь заказов магазина («Заказы») как независимые
// под-режимы. В данных они не связаны (заказ из Player Shell без оплаты, касса лишь меняет
// статус) — поэтому сегмент-переключатель, а не сшивка оплаты. Сегменты гейтятся правами.
export function CashSalesWorkspace({
  backend,
  currencyCode,
  session
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
}) {
  const { t } = useI18n();
  const canPos = hasAnyPermission(session, [
    permissionNames.createPosSale,
    permissionNames.payPosSale,
    permissionNames.refundPosSale,
    permissionNames.voidPosSale
  ]);
  const canOrders = hasAnyPermission(session, [permissionNames.createPosSale]);

  const segments: { id: SalesSegment; label: string }[] = [];
  if (canPos) segments.push({ id: 'pos', label: t('op.cash.sales.segPos') });
  if (canOrders) segments.push({ id: 'orders', label: t('op.cash.sales.segOrders') });

  const [active, setActive] = useState<SalesSegment>(() => segments[0]?.id ?? 'pos');

  return (
    <main className="workspace-screen cash-sales-screen">
      {segments.length > 1 && (
        <div className="cash-sales-segments" role="tablist" aria-label={t('op.cash.sales.tab')}>
          {segments.map((segment) => (
            <button
              key={segment.id}
              type="button"
              role="tab"
              aria-selected={active === segment.id}
              className={active === segment.id ? 'active' : undefined}
              onClick={() => setActive(segment.id)}
            >
              {segment.label}
            </button>
          ))}
        </div>
      )}

      {active === 'pos' && canPos && (
        <BackendPosWorkspace currencyCode={currencyCode} backend={backend} embedded />
      )}
      {active === 'orders' && canOrders && (
        <ShopOrdersWorkspace backend={backend} embedded />
      )}
    </main>
  );
}
