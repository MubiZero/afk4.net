import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { OrganizationOwnerInvite } from '@/api/types';
import { useOrganizations } from './useOrganizations';
import { buildOrganizationRows, STATUS_OPTIONS, STATUS_LABEL, PLAN_OPTIONS, PLAN_LABEL } from './organizationsModel';
import { OrganizationsTable } from './OrganizationsTable';

interface OrganizationsScreenProps {
  client: PlatformApiClient;
  selectedOrganizationId: string | null;
  initialInvite: OrganizationOwnerInvite | null;
  onOpenOrganization: (organizationId: string) => void;
  onCloseOrganization: () => void;
  onCreateOrganization: () => void;
  query: string;
  statusFilter: string;
  planFilter: string;
  sort: string;
  onQueryChange: (change: Partial<{ query: string; statusFilter: string; planFilter: string; sort: string }>) => void;
}

export function OrganizationsScreen({
  client, selectedOrganizationId, initialInvite: _initialInvite, onOpenOrganization, onCloseOrganization: _onCloseOrganization, onCreateOrganization,
  query, statusFilter, planFilter, sort, onQueryChange
}: OrganizationsScreenProps) {
  const { t } = useI18n();
  const state = useOrganizations(client.organizations);
  return (
    <div className="flex flex-col gap-4">
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <Input
          aria-label={t('platform.organizations.search')}
          placeholder={t('platform.organizations.search')}
          value={query}
          onChange={e => onQueryChange({ query: e.target.value })}
          className="max-w-xs"
        />
        <Select value={statusFilter} onValueChange={value => onQueryChange({ statusFilter: value })}>
          <SelectTrigger aria-label={t('platform.organizations.filter.status')}><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('platform.organizations.filter.all')}</SelectItem>
            {STATUS_OPTIONS.map(s => <SelectItem key={s} value={s}>{t(STATUS_LABEL[s])}</SelectItem>)}
          </SelectContent>
        </Select>
        <Select value={planFilter} onValueChange={value => onQueryChange({ planFilter: value })}>
          <SelectTrigger aria-label={t('platform.organizations.filter.plan')}><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('platform.organizations.filter.all')}</SelectItem>
            {PLAN_OPTIONS.map(p => <SelectItem key={p} value={p}>{t(PLAN_LABEL[p])}</SelectItem>)}
          </SelectContent>
        </Select>
        <Select value={sort} onValueChange={value => onQueryChange({ sort: value })}>
          <SelectTrigger aria-label="Сортировка"><SelectValue /></SelectTrigger>
          <SelectContent><SelectItem value="attention">Сначала требуют внимания</SelectItem><SelectItem value="name">По названию</SelectItem></SelectContent>
        </Select>
        <Button className="ml-auto" onClick={onCreateOrganization}>{t('platform.organizations.new')}</Button>
      </div>

      {state.status === 'loading' ? (
        <LoadingCards count={3} />
      ) : state.status === 'error' ? (
        <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />
      ) : (
        <OrganizationsTable
          rows={buildOrganizationRows(state.data, { query, status: statusFilter, plan: planFilter, sort })}
          selectedId={selectedOrganizationId}
          emptyMessage={t('platform.organizations.empty')}
          onSelect={onOpenOrganization}
        />
      )}

    </div>
  );
}
