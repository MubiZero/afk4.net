import { useState, type JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { SectionHeader, Tabs } from '@afk4/ui/react';
import { EmptyState } from '../operatorPrimitives';
import type { OperatorBackendContext, WorkspaceId } from '../operatorTypes';
import { RevenueReport } from './RevenueReport';
import { ShiftCashReport } from './ShiftCashReport';
import { ReportSchedules } from './ReportSchedules';
import { SummaryReport } from './SummaryReport';
import { GameplayTimeReport } from './GameplayTimeReport';
import { OperatorActionsReport } from './OperatorActionsReport';
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
    return (
      <section className="workspace-screen reports-workspace">
        <SectionHeader title={t('op.shell.navGroup.reports')} />
        <EmptyState title={t('op.reports.noAccess')} next={{ kind: 'denied', hint: t('op.error.accessHint') }} />
      </section>
    );
  }

  const current = destinations.some((destination) => destination.id === active) ? active : destinations[0].id;
  const panel = current === 'summary'
    ? <SummaryReport backend={backend} onNavigate={onNavigate} />
    : current === 'shiftsCash'
      ? <ShiftCashReport backend={backend} currencyCode={currencyCode} />
      : current === 'gameplay'
        ? <GameplayTimeReport backend={backend} />
        : current === 'operatorActions'
          ? <OperatorActionsReport backend={backend} />
          : current === 'schedules'
            ? <ReportSchedules backend={backend} />
            : <RevenueReport backend={backend} currencyCode={currencyCode} />;

  // Шапка раздела — как у Кассы и Склада: «Отчёты» и вкладки; имя отчёта — выбранная вкладка, и
  // внутри оно больше не повторяется заголовком с подписью.
  return (
    <section className="workspace-screen reports-workspace">
      <SectionHeader
        title={t('op.shell.navGroup.reports')}
        tabs={destinations.length > 1 ? (
          <Tabs
            label={t('op.shell.navGroup.reports')}
            value={current}
            onChange={setActive}
            items={destinations.map((destination) => ({ value: destination.id, label: t(destination.labelKey) }))}
          />
        ) : undefined}
      />
      <div className="reports-panel" role="tabpanel" aria-label={t(destinations.find((destination) => destination.id === current)!.labelKey)}>
        {panel}
      </div>
    </section>
  );
}
