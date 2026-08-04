import { useMemo } from 'react';
import { Page } from '@/components/layout/Page';
import { Tabs } from '@/components/ui/tabs';
import { EmptyState, ErrorState, LoadingCards } from '@/components/ui/states';
import { useI18n, type MessageKey } from '@/i18n/I18nProvider';
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
    <Page width="full" title={t('platform.clubs.title')} description={t('platform.clubs.subtitle')}>
      <Tabs
        label={t('platform.clubs.view.label')}
        value={view}
        onChange={onViewChange}
        items={VIEWS.map(candidate => ({ value: candidate, label: t(VIEW_LABEL_KEY[candidate]) }))}
      />

      {state.status === 'loading' ? (
        <LoadingCards count={3} />
      ) : state.status === 'error' ? (
        <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />
      ) : (
        <ClubsList organizations={state.data.organizations ?? []} view={view} emptyMessage={t(EMPTY_KEY[view])} onOpenOrganization={onOpenOrganization} />
      )}
    </Page>
  );
}

function ClubsList({ organizations, view, emptyMessage, onOpenOrganization }: {
  organizations: PulseOrganization[];
  view: PulseView;
  emptyMessage: string;
  onOpenOrganization: (organizationId: string) => void;
}) {
  const rows = useMemo(() => selectView(organizations, view), [organizations, view]);
  const density = resolveDensity(organizations.length);

  if (rows.length === 0) return <EmptyState message={emptyMessage} />;

  return (
    <ul className={density === 'dense' ? 'pulse-list is-dense' : 'pulse-list'}>
      {rows.map(organization => (
        <OrganizationPulseRow
          key={organization.organizationId}
          organization={organization}
          defaultExpanded={density === 'roomy'}
          onOpen={onOpenOrganization}
        />
      ))}
    </ul>
  );
}
