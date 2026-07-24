import type { ComponentProps } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OperatorAuthSession } from './authClient';
import type { OperatorFloorMapState } from './floorMapState';
import type { MapFilterId, OperatorBackendContext, WorkspaceId } from './operatorTypes';
import { WorkspaceErrorBoundary } from './WorkspaceErrorBoundary';
import { MapWorkspace } from './MapWorkspace';
import { BackendBookingWorkspace } from './BackendBookingWorkspace';
import { CashWorkspace } from './cash/CashWorkspace';
import { BackendPlayersWorkspace } from './BackendPlayersWorkspace';
import { StockWorkspace } from './stock/StockWorkspace';
import { ManagementWorkspace } from './management/ManagementWorkspace';
import { NetworkWorkspace } from './network/NetworkWorkspace';
import { ReportsWorkspace } from './reports/ReportsWorkspace';

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
        <ReportsWorkspace
          backend={backend}
          currencyCode={currencyCode}
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
      {workspace === 'cash' && <CashWorkspace currencyCode={currencyCode} backend={backend} session={session} />}
      {workspace === 'stock' && <StockWorkspace currencyCode={currencyCode} backend={backend} session={session} />}
      {workspace === 'players' && <BackendPlayersWorkspace currencyCode={currencyCode} backend={backend} />}
      {workspace === 'management' && (
        <ManagementWorkspace backend={backend} session={session} currencyCode={currencyCode} />
      )}
      {workspace === 'network' && <NetworkWorkspace backend={backend} />}
    </WorkspaceErrorBoundary>
  );
}
