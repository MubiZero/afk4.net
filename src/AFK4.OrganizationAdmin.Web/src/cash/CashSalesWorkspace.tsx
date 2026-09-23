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
  session,
  openOrder
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
  openOrder?: { orderId: string } | null;
}) {
  const canPos = hasAnyPermission(session, [
    permissionNames.createPosSale,
    permissionNames.payPosSale,
    permissionNames.refundPosSale,
    permissionNames.voidPosSale
  ]);
  // Лента гейтится тем же правом, что сервер спрашивает на очередь и на «Принять/Выдать».
  // Пока она висела на createPosSale, кассир видел заказы и получал 403 на каждое действие:
  // кнопка, которая выглядит рабочей и не работает, дороже отсутствующей — она обещает, что
  // заказ обслужен.
  const canOrders = hasAnyPermission(session, [permissionNames.serveShopOrders]);
  // Отмена возвращает деньги и спрашивает право сильнее.
  const canCancelOrders = hasAnyPermission(session, [permissionNames.manageShopOrders]);

  return (
    <main className="workspace-screen cash-sales-screen">
      {canOrders && <PosOrdersTicker backend={backend} canCancel={canCancelOrders} openOrder={openOrder} />}
      {canPos && <BackendPosWorkspace currencyCode={currencyCode} backend={backend} embedded />}
    </main>
  );
}
