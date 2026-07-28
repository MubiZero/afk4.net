import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { OrganizationOwnerInvite } from '@/api/types';
import { useOrganizations } from './useOrganizations';
import { buildOrganizationRows, STATUS_OPTIONS, STATUS_LABEL, PLAN_OPTIONS, PLAN_LABEL } from './organizationsModel';
import { OrganizationsTable } from './OrganizationsTable';
import { OrganizationDrawer } from './OrganizationDrawer';

interface OrganizationsScreenProps {
  client: PlatformApiClient;
  selectedOrganizationId: string | null;
  initialInvite: OrganizationOwnerInvite | null;
  onOpenOrganization: (organizationId: string) => void;
  onCloseOrganization: () => void;
  onCreateOrganization: () => void;
}

export function OrganizationsScreen({
  client, selectedOrganizationId, initialInvite, onOpenOrganization, onCloseOrganization, onCreateOrganization
}: OrganizationsScreenProps) {
  const { t } = useI18n();
  const state = useOrganizations(client.organizations);
  const [query, setQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [planFilter, setPlanFilter] = useState('all');

  const selectedName =
    (state.status === 'ready'
      ? state.data.find(x => x.organizationId === selectedOrganizationId)?.name
      : undefined) ?? t('nav.platform.organizations');

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <Input
          aria-label={t('platform.organizations.search')}
          placeholder={t('platform.organizations.search')}
          value={query}
          onChange={e => setQuery(e.target.value)}
          className="max-w-xs"
        />
        <Select value={statusFilter} onValueChange={setStatusFilter}>
          <SelectTrigger aria-label={t('platform.organizations.filter.status')}><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('platform.organizations.filter.all')}</SelectItem>
            {STATUS_OPTIONS.map(s => <SelectItem key={s} value={s}>{t(STATUS_LABEL[s])}</SelectItem>)}
          </SelectContent>
        </Select>
        <Select value={planFilter} onValueChange={setPlanFilter}>
          <SelectTrigger aria-label={t('platform.organizations.filter.plan')}><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('platform.organizations.filter.all')}</SelectItem>
            {PLAN_OPTIONS.map(p => <SelectItem key={p} value={p}>{t(PLAN_LABEL[p])}</SelectItem>)}
          </SelectContent>
        </Select>
        <Button className="ml-auto" onClick={onCreateOrganization}>{t('platform.organizations.new')}</Button>
      </div>

      {state.status === 'loading' ? (
        <LoadingCards count={3} />
      ) : state.status === 'error' ? (
        <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />
      ) : (
        <OrganizationsTable
          rows={buildOrganizationRows(state.data, { query, status: statusFilter, plan: planFilter })}
          selectedId={selectedOrganizationId}
          emptyMessage={t('platform.organizations.empty')}
          onSelect={onOpenOrganization}
        />
      )}

      <Sheet open={selectedOrganizationId !== null} onOpenChange={open => { if (!open) onCloseOrganization(); }}>
        <SheetContent closeLabel={t('common.close')}>
          {selectedOrganizationId !== null && (
            <>
              <SheetTitle>{selectedName}</SheetTitle>
              <OrganizationDrawer
                client={client}
                organizationId={selectedOrganizationId}
                initialInvite={initialInvite}
                onChanged={() => { if (state.status === 'ready') state.retry(); }}
              />
            </>
          )}
        </SheetContent>
      </Sheet>
    </>
  );
}
