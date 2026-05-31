import type { ReactNode } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { OwnerInvite, TenantDetail } from '@/api/types';
import { useTenantDetail } from './useTenantDetail';
import { TenantStatusSection } from './TenantStatusSection';
import { TenantSubscriptionSection } from './TenantSubscriptionSection';
import { TenantInvoicesSection } from './TenantInvoicesSection';
import { TenantLimitsSection } from './TenantLimitsSection';
import { TenantOwnerInvitesSection } from './TenantOwnerInvitesSection';
import { TenantSupportNotesSection } from './TenantSupportNotesSection';
import { HealthSection } from '@/components/HealthSection';

interface TenantDrawerProps {
  client: PlatformApiClient;
  organizationId: string;
  initialInvite: OwnerInvite | null;
  onChanged: () => void;
}

export function TenantDrawer({ client, organizationId, initialInvite, onChanged }: TenantDrawerProps) {
  const { t, formatDate } = useI18n();
  const state = useTenantDetail(client, organizationId);

  if (state.status === 'loading') return <LoadingCards count={3} />;
  if (state.status === 'error') {
    return <ErrorState message={t('platform.tenant.drawer.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;
  }

  const tenant: TenantDetail = state.data;
  const handleUpdated = (next: TenantDetail) => { state.apply(next); onChanged(); };

  return (
    <div className="flex flex-col gap-4 overflow-y-auto">
      <Card>
        <CardHeader><CardTitle>{t('platform.tenant.section.overview')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-2 text-sm">
          <Field label={t('platform.tenant.overview.slug')}><code>{tenant.slug}</code></Field>
          <Field label={t('platform.tenant.overview.created')}>{formatDate(tenant.createdAtUtc)}</Field>
          <Field label={t('platform.tenant.overview.updated')}>{formatDate(tenant.updatedAtUtc)}</Field>
          <Field label={t('platform.tenant.overview.branches')}>
            {tenant.branches.length === 0 ? '—' : tenant.branches.map(b => b.name).join(', ')}
          </Field>
          {tenant.statusReason !== null && (
            <Field label={t('platform.tenant.overview.statusReason')}>{tenant.statusReason}</Field>
          )}
        </CardContent>
      </Card>

      <TenantStatusSection client={client} tenant={tenant} onUpdated={handleUpdated} />
      <TenantSubscriptionSection client={client} organizationId={tenant.organizationId} />
      <TenantLimitsSection client={client} tenant={tenant} onUpdated={handleUpdated} />
      <TenantInvoicesSection client={client} organizationId={tenant.organizationId} />

      <TenantOwnerInvitesSection client={client} organizationId={tenant.organizationId} branches={tenant.branches} initialInvite={initialInvite} />
      <TenantSupportNotesSection client={client} organizationId={tenant.organizationId} />

      {/* Interim: legacy Health section embedded unchanged until later plans redesign it. */}
      <HealthSection client={client} organizationId={tenant.organizationId} />
    </div>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex justify-between gap-3">
      <span className="text-muted-foreground">{label}</span>
      <span className="text-right">{children}</span>
    </div>
  );
}
