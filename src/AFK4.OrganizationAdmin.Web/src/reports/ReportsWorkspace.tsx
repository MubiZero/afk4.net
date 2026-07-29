import { useState, type JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { EmptyState } from '../operatorPrimitives';
import type { OperatorBackendContext, WorkspaceId } from '../operatorTypes';
import { RevenueReport } from './RevenueReport';
import { ShiftCashReport } from './ShiftCashReport';
import { SummaryReport } from './SummaryReport';
import { allowedReportsDestinations, type ReportsDestinationId } from './reportsNav';

export function ReportsWorkspace({ backend, currencyCode, onNavigate }: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  onNavigate: (workspace: WorkspaceId) => void;
  onOpenSeat: (seatId: string) => void;
}): JSX.Element {
  const { t } = useI18n();
  const destinations = allowedReportsDestinations(backend?.session ?? null);
  const [active, setActive] = useState<ReportsDestinationId>('summary');

  if (destinations.length === 0) {
    return <section className="workspace-screen"><EmptyState title={t('op.reports.noAccess')} /></section>;
  }

  const current = destinations.some((destination) => destination.id === active) ? active : destinations[0].id;
  const panel = current === 'summary'
    ? <SummaryReport backend={backend} currencyCode={currencyCode} onNavigate={onNavigate} />
    : current === 'shiftsCash'
      ? <ShiftCashReport backend={backend} currencyCode={currencyCode} />
      : <RevenueReport backend={backend} currencyCode={currencyCode} />;

  return (
    <section className="reports-workspace">
      <div className="reports-tabs" role="tablist" aria-label={t('op.shell.navGroup.reports')}>
        {destinations.map((destination) => (
          <button
            id={`reports-tab-${destination.id}`}
            key={destination.id}
            type="button"
            role="tab"
            aria-selected={destination.id === current}
            aria-controls={`reports-panel-${destination.id}`}
            onClick={() => setActive(destination.id)}
          >
            {t(destination.labelKey)}
          </button>
        ))}
      </div>
      <div id={`reports-panel-${current}`} role="tabpanel" aria-labelledby={`reports-tab-${current}`}>
        {panel}
      </div>
    </section>
  );
}
