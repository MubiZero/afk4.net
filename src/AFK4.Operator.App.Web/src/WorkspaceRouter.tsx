import type { ComponentProps } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OperatorAuthSession } from './authClient';
import type { OperatorFloorMapState } from './floorMapState';
import type { MapFilterId, OperatorBackendContext, WorkspaceId } from './operatorTypes';
import { WorkspaceErrorBoundary } from './WorkspaceErrorBoundary';
import { MapWorkspace } from './MapWorkspace';
import { DashboardWorkspace } from './DashboardWorkspace';
import { BackendBookingWorkspace } from './BackendBookingWorkspace';
import { BackendPosWorkspace } from './BackendPosWorkspace';
import { ShopOrdersWorkspace } from './ShopOrdersWorkspace';
import { BackendPlayersWorkspace } from './BackendPlayersWorkspace';
import { BackendPaymentsWorkspace } from './BackendPaymentsWorkspace';
import { PaymentGatewaysWorkspace } from './PaymentGatewaysWorkspace';
import { BackendLogsWorkspace } from './BackendLogsWorkspace';
import { BackendSettingsWorkspace } from './BackendSettingsWorkspace';
import { ReviewWorkspace } from './ReviewWorkspace';
import { LoyaltySettingsWorkspace } from './LoyaltySettingsWorkspace';
import { NewsWorkspace } from './NewsWorkspace';
import { ShiftsWorkspace } from './ShiftsWorkspace';

// Маршрутизатор контента: какой экран показать под активным workspace. Обёрнут в
// WorkspaceErrorBoundary с key={workspace} — переключение раздела сбрасывает границу ошибок.
// Состояние и данные шелл прокидывает пропсами; маршрутизатор сам ничего не загружает.
export function WorkspaceRouter({
  workspace,
  session,
  backend,
  currencyCode,
  displayedFloorMap,
  actionsEnabled,
  selectedSeatId,
  mapFilter,
  offlineActionAudit,
  onSelectSeat,
  onStartSeat,
  onFilterChange,
  onPcControlAction,
  onSeatAction,
  onNavigate,
  onOpenSeat
}: {
  workspace: WorkspaceId;
  session: OperatorAuthSession | null;
  backend: OperatorBackendContext | null;
  currencyCode: string;
  displayedFloorMap: OperatorFloorMapState;
  actionsEnabled: boolean;
  selectedSeatId: string;
  mapFilter: MapFilterId;
  offlineActionAudit: string[];
  onSelectSeat: (seatId: string) => void;
  onStartSeat?: (seatId: string) => void;
  onFilterChange: (filter: MapFilterId) => void;
  onPcControlAction: ComponentProps<typeof MapWorkspace>['onPcControlAction'];
  onSeatAction: ComponentProps<typeof MapWorkspace>['onSeatAction'];
  onNavigate: (workspace: WorkspaceId) => void;
  onOpenSeat: (seatId: string) => void;
}) {
  const { t } = useI18n();
  return (
    <WorkspaceErrorBoundary key={workspace} message={t('op.shell.workspaceError')}>
      {workspace === 'map' && (
        <MapWorkspace
          floorMap={displayedFloorMap}
          session={session}
          actionsEnabled={actionsEnabled}
          selectedSeatId={selectedSeatId}
          activeFilter={mapFilter}
          offlineActionAudit={offlineActionAudit}
          onSelectSeat={onSelectSeat}
          onStartSeat={onStartSeat}
          onFilterChange={onFilterChange}
          onPcControlAction={onPcControlAction}
          onSeatAction={onSeatAction}
        />
      )}
      {workspace === 'dashboard' && (
        <DashboardWorkspace
          currencyCode={currencyCode}
          backend={backend}
          onNavigate={onNavigate}
          onOpenSeat={onOpenSeat}
        />
      )}
      {workspace === 'booking' && (
        <BackendBookingWorkspace
          floorMap={displayedFloorMap}
          backend={backend}
          currencyCode={currencyCode}
          onOpenSeat={onOpenSeat}
        />
      )}
      {workspace === 'pos' && <BackendPosWorkspace currencyCode={currencyCode} backend={backend} />}
      {workspace === 'shop_orders' && <ShopOrdersWorkspace backend={backend} />}
      {workspace === 'players' && <BackendPlayersWorkspace currencyCode={currencyCode} backend={backend} />}
      {workspace === 'payments' && <BackendPaymentsWorkspace currencyCode={currencyCode} backend={backend} />}
      {workspace === 'payment_cards' && backend !== null && (
        <PaymentGatewaysWorkspace backend={backend} />
      )}
      {workspace === 'logs' && <BackendLogsWorkspace currencyCode={currencyCode} backend={backend} />}
      {workspace === 'settings' && <BackendSettingsWorkspace currencyCode={currencyCode} backend={backend} />}
      {workspace === 'review' && <ReviewWorkspace currencyCode={currencyCode} backend={backend} />}
      {workspace === 'loyalty' && backend !== null && (
        <LoyaltySettingsWorkspace backend={backend} />
      )}
      {workspace === 'news' && backend !== null && (
        <NewsWorkspace backend={backend} />
      )}
      {workspace === 'shifts' && backend !== null && (
        <ShiftsWorkspace backend={backend} branchId={backend.branchId} currencyCode={currencyCode} />
      )}
    </WorkspaceErrorBoundary>
  );
}
