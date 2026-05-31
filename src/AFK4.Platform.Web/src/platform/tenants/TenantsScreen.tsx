import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { OwnerInvite } from '@/api/types';
import { useTenants } from './useTenants';
import { buildTenantRows, STATUS_OPTIONS, STATUS_LABEL, PLAN_OPTIONS, PLAN_LABEL } from './tenantsModel';
import { TenantsTable } from './TenantsTable';
import { TenantDrawer } from './TenantDrawer';

interface TenantsScreenProps {
  client: PlatformApiClient;
  selectedTenantId: string | null;
  initialInvite: OwnerInvite | null;
  onOpenTenant: (organizationId: string) => void;
  onCloseTenant: () => void;
  onCreateTenant: () => void;
}

export function TenantsScreen({
  client, selectedTenantId, initialInvite, onOpenTenant, onCloseTenant, onCreateTenant
}: TenantsScreenProps) {
  const { t } = useI18n();
  const state = useTenants(client);
  const [query, setQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [planFilter, setPlanFilter] = useState('all');

  const selectedName =
    state.status === 'ready'
      ? state.data.find(x => x.organizationId === selectedTenantId)?.name ?? ''
      : '';

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <Input
          aria-label={t('platform.tenants.search')}
          placeholder={t('platform.tenants.search')}
          value={query}
          onChange={e => setQuery(e.target.value)}
          className="max-w-xs"
        />
        <Select value={statusFilter} onValueChange={setStatusFilter}>
          <SelectTrigger aria-label={t('platform.tenants.filter.status')}><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('platform.tenants.filter.all')}</SelectItem>
            {STATUS_OPTIONS.map(s => <SelectItem key={s} value={s}>{t(STATUS_LABEL[s])}</SelectItem>)}
          </SelectContent>
        </Select>
        <Select value={planFilter} onValueChange={setPlanFilter}>
          <SelectTrigger aria-label={t('platform.tenants.filter.plan')}><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('platform.tenants.filter.all')}</SelectItem>
            {PLAN_OPTIONS.map(p => <SelectItem key={p} value={p}>{t(PLAN_LABEL[p])}</SelectItem>)}
          </SelectContent>
        </Select>
        <Button className="ml-auto" onClick={onCreateTenant}>{t('platform.tenants.new')}</Button>
      </div>

      {state.status === 'loading' ? (
        <LoadingCards count={3} />
      ) : state.status === 'error' ? (
        <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />
      ) : (
        <TenantsTable
          rows={buildTenantRows(state.data, { query, status: statusFilter, plan: planFilter })}
          selectedId={selectedTenantId}
          emptyMessage={t('platform.tenants.empty')}
          onSelect={onOpenTenant}
        />
      )}

      <Sheet open={selectedTenantId !== null} onOpenChange={open => { if (!open) onCloseTenant(); }}>
        <SheetContent closeLabel={t('common.close')}>
          {selectedTenantId !== null && (
            <>
              <SheetTitle>{selectedName}</SheetTitle>
              <TenantDrawer
                client={client}
                organizationId={selectedTenantId}
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
