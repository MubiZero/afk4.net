import { useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { EmptyState } from '../operatorPrimitives';
import type { OperatorBackendContext, WorkspaceId } from '../operatorTypes';
import { allowedReportsDestinations, type ReportsDestinationId } from './reportsNav';
import { OverviewDestination } from './overview/OverviewDestination';
import { HistoryDestination } from './history/HistoryDestination';
import { BranchJournalDestination } from './journal/BranchJournalDestination';

// Каркас раздела «Отчёты»: левый рейл разделов, доступных сессии (Обзор/История/Журнал), +
// активный экран раздела справа — тот же паттерн, что NetworkWorkspace/ManagementWorkspace.
export function ReportsWorkspace({ backend, currencyCode, onNavigate, onOpenSeat }: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  onNavigate: (workspace: WorkspaceId) => void;
  onOpenSeat: (seatId: string) => void;
}): JSX.Element {
  const { t } = useI18n();
  const session = backend?.session ?? null;
  const destinations = allowedReportsDestinations(session);
  const [active, setActive] = useState<ReportsDestinationId | null>(destinations[0]?.id ?? null);

  if (destinations.length === 0) {
    return (
      <section className="workspace-screen">
        <EmptyState title={t('op.reports.noAccess')} />
      </section>
    );
  }

  // Сессия могла лишиться прав между рендерами — активный раздел мог перестать быть доступным.
  // Падаем на первый разрешённый вместо пустого экрана (см. тот же приём в ManagementWorkspace).
  const currentId: ReportsDestinationId = destinations.some((d) => d.id === active)
    ? (active as ReportsDestinationId)
    : destinations[0].id;

  function renderActive(): JSX.Element {
    switch (currentId) {
      case 'overview':
        return <OverviewDestination backend={backend} currencyCode={currencyCode} onNavigate={onNavigate} onOpenSeat={onOpenSeat} />;
      case 'history':
        return <HistoryDestination backend={backend} />;
      case 'journal':
        return <BranchJournalDestination backend={backend} />;
    }
  }

  return (
    <div className="management-layout">
      <nav className="management-nav">
        {destinations.map((destination) => {
          const Icon = destination.Icon;
          return (
            <button
              key={destination.id}
              type="button"
              className={destination.id === currentId ? 'active' : undefined}
              onClick={() => setActive(destination.id)}
            >
              <Icon size={16} aria-hidden="true" />
              <span>{t(destination.labelKey)}</span>
            </button>
          );
        })}
      </nav>

      <div className="management-active-pane">{renderActive()}</div>
    </div>
  );
}
