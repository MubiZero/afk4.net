import { useMemo } from 'react';
import { EmptyState, ErrorState, LoadingCards } from '@/components/ui/states';
import { useI18n, type MessageKey } from '@/i18n/I18nProvider';
import { cn } from '@/lib/utils';
import type { PlatformApiClient } from '@/api/platformApi';
import type { PulseOrganization } from '@/api/types';
import { usePulse } from './usePulse';
import { resolveDensity, selectView, type PulseView } from './pulseModel';
import { OrganizationPulseRow } from './OrganizationPulseRow';

const VIEWS: readonly PulseView[] = ['now', 'all', 'debt'];

const VIEW_LABEL_KEY: Record<PulseView, MessageKey> = {
  now: 'platform.clubs.view.now',
  all: 'platform.clubs.view.all',
  debt: 'platform.clubs.view.debt'
};

const EMPTY_KEY: Record<PulseView, MessageKey> = {
  now: 'platform.clubs.empty.now',
  all: 'platform.clubs.empty.all',
  debt: 'platform.clubs.empty.debt'
};

interface ClubsScreenProps {
  client: Pick<PlatformApiClient, 'pulse'>;
  view: PulseView;
  onViewChange: (view: PulseView) => void;
  onOpenOrganization: (organizationId: string) => void;
}

export function ClubsScreen({ client, view, onViewChange, onOpenOrganization }: ClubsScreenProps) {
  const { t } = useI18n();
  const state = usePulse(client.pulse);

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-lg font-semibold text-foreground">{t('platform.clubs.title')}</h1>

      <div role="tablist" aria-label={t('platform.clubs.view.label')} className="flex gap-1 border-b border-border">
        {VIEWS.map(candidate => (
          <button
            key={candidate}
            type="button"
            role="tab"
            aria-selected={view === candidate}
            onClick={() => onViewChange(candidate)}
            className={cn(
              '-mb-px border-b-2 border-transparent px-3 py-2 text-sm font-medium text-muted-foreground transition-colors hover:text-foreground',
              view === candidate && 'border-primary text-foreground'
            )}
          >
            {t(VIEW_LABEL_KEY[candidate])}
          </button>
        ))}
      </div>

      {state.status === 'loading' ? (
        <LoadingCards count={3} />
      ) : state.status === 'error' ? (
        <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />
      ) : (
        <ClubsList organizations={state.data.organizations} view={view} emptyMessage={t(EMPTY_KEY[view])} onOpenOrganization={onOpenOrganization} />
      )}
    </div>
  );
}

function ClubsList({ organizations, view, emptyMessage, onOpenOrganization }: { organizations: PulseOrganization[]; view: PulseView; emptyMessage: string; onOpenOrganization: (organizationId: string) => void }) {
  const rows = useMemo(() => selectView(organizations, view), [organizations, view]);
  const density = resolveDensity(organizations.length);

  if (rows.length === 0) return <EmptyState message={emptyMessage} />;

  return (
    <div className="flex flex-col gap-3">
      {rows.map(organization => (
        <OrganizationPulseRow key={organization.organizationId} organization={organization} defaultExpanded={density === 'roomy'} onOpen={onOpenOrganization} />
      ))}
    </div>
  );
}
