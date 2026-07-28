import type { JSX } from 'react';
import { DashboardWorkspace } from '../../DashboardWorkspace';
import type { OperatorBackendContext, WorkspaceId } from '../../operatorTypes';

// Обёртка над существующим DashboardWorkspace как destination раздела «Отчёты» — сам
// DashboardWorkspace не меняется (поведение переносится как есть, см. Task 5 бриф).
export function OverviewDestination({ backend, currencyCode, onNavigate, onOpenSeat }: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  onNavigate: (workspace: WorkspaceId) => void;
  onOpenSeat: (seatId: string) => void;
}): JSX.Element {
  return <DashboardWorkspace currencyCode={currencyCode} backend={backend} onNavigate={onNavigate} onOpenSeat={onOpenSeat} />;
}
