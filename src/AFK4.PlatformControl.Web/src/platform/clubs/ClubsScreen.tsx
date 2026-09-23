import { useMemo } from 'react';
import { Plus } from 'lucide-react';
import { Page } from '@/components/layout/Page';
import { Button } from '@/components/ui/button';
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

interface ClubsScreenProps {
  client: Pick<PlatformApiClient, 'pulse'>;
  view: PulseView;
  onViewChange: (view: PulseView) => void;
  onOpenOrganization: (organizationId: string) => void;
  /// Завести клуб. Не задан — у этого сотрудника нет права заводить, и кнопки нет вовсе.
  ///
  /// Экран заведения существовал и работал, но попасть на него можно было только набрав адрес
  /// руками: кнопка потерялась, когда список организаций заменили на этот пульс. Главное
  /// действие панели не должно зависеть от знания адреса.
  onCreateOrganization?: () => void;
}

export function ClubsScreen({ client, view, onViewChange, onOpenOrganization, onCreateOrganization }: ClubsScreenProps) {
  const { t, formatDate } = useI18n();
  const state = usePulse(client.pulse);

  return (
    <Page
      width="full"
      title={t('platform.clubs.title')}
      description={t('platform.clubs.subtitle')}
      actions={onCreateOrganization !== undefined ? (
        <Button onClick={onCreateOrganization}>
          <Plus size={16} aria-hidden="true" />
          {t('platform.clubs.create')}
        </Button>
      ) : undefined}
    >
      {/* На какой момент то, что на экране. Раздел называется «Сейчас», держат его открытым весь
          день, и без этой строки снимок часовой давности читается как положение дел сию минуту. */}
      {state.status === 'ready' ? (
        <p className="mgmt-drawer-hint">{t('platform.clubs.snapshotAt', { time: formatDate(state.data.generatedAtUtc) })}</p>
      ) : null}

      <Tabs
        label={t('platform.clubs.view.label')}
        value={view}
        onChange={onViewChange}
        items={VIEWS.map(candidate => ({ value: candidate, label: t(VIEW_LABEL_KEY[candidate]) }))}
      />

      {state.status === 'loading' ? (
        <LoadingCards count={3} />
      ) : state.status === 'error' ? (
        <ErrorState message={state.message} retryLabel={state.canRetry ? t('state.retry') : undefined} onRetry={state.canRetry ? state.retry : undefined} />
      ) : (
        <ClubsList organizations={state.data.organizations ?? []} view={view} onOpenOrganization={onOpenOrganization} onCreateOrganization={onCreateOrganization} />
      )}
    </Page>
  );
}

function ClubsList({ organizations, view, onOpenOrganization, onCreateOrganization }: {
  organizations: PulseOrganization[];
  view: PulseView;
  onOpenOrganization: (organizationId: string) => void;
  onCreateOrganization?: () => void;
}) {
  const { t } = useI18n();
  const rows = useMemo(() => selectView(organizations, view), [organizations, view]);
  const density = resolveDensity(organizations.length);

  // «Сейчас» и «Все» ничего не отсеивают — они пусты, только когда организаций нет вовсе, и тогда
  // следующий шаг один: завести первую. Раньше «Сейчас» отвечал на это «нет сетей с активными
  // сигналами», и первый вход в пустую платформу выглядел как спокойный день.
  if (organizations.length === 0) {
    return (
      <EmptyState
        message={t('platform.clubs.empty.all')}
        next={onCreateOrganization !== undefined
          ? { label: t('platform.clubs.empty.create'), onClick: onCreateOrganization }
          : { noPermission: t('state.empty.noPermission', { permission: t('platform.permission.organizations.create') }) }}
      />
    );
  }
  // Долги — единственный вид, который отсеивает, и его пустота — хорошая новость.
  if (rows.length === 0) return <EmptyState message={t('platform.clubs.empty.debt')} next="calm" />;

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
