import { hasAnyPermission, permissionNames } from '../operatorPermissions';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import { BackendPosWorkspace } from '../BackendPosWorkspace';
import { PosOrdersTicker } from '../PosOrdersTicker';

// Вкладка «Продажи» = POS («Касса») с лентой входящих заказов сверху. Раньше касса и заказы были
// двумя сегментами-переключателями; заказы payment-free (игрок оформляет из Player Shell, касса лишь
// ведёт статус), поэтому отдельный экран был лишним — слили в один, чтобы заказ был всегда на виду.
export function CashSalesWorkspace({
  backend,
  currencyCode,
  session
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
}) {
  const canPos = hasAnyPermission(session, [
    permissionNames.createPosSale,
    permissionNames.payPosSale,
    permissionNames.refundPosSale,
    permissionNames.voidPosSale
  ]);
  const canOrders = hasAnyPermission(session, [permissionNames.createPosSale]);

  return (
    <main className="workspace-screen cash-sales-screen">
      {canOrders && <PosOrdersTicker backend={backend} />}
      {canPos && <BackendPosWorkspace currencyCode={currencyCode} backend={backend} embedded />}
    </main>
  );
}
