import type { ReactNode } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { OrganizationOwnerInvite, OrganizationDetail } from '@/api/types';
import { useOrganizationDetail } from './useOrganizationDetail';
import { OrganizationStatusSection } from './OrganizationStatusSection';
import { OrganizationSubscriptionSection } from './OrganizationSubscriptionSection';
import { OrganizationInvoicesSection } from './OrganizationInvoicesSection';
import { OrganizationLimitsSection } from './OrganizationLimitsSection';
import { OrganizationOwnerInvitesSection } from './OrganizationOwnerInvitesSection';
import { OrganizationSupportNotesSection } from './OrganizationSupportNotesSection';
import { OrganizationHealthSection } from './OrganizationHealthSection';

interface OrganizationDrawerProps {
  client: PlatformApiClient;
  organizationId: string;
  initialInvite: OrganizationOwnerInvite | null;
  onChanged: () => void;
}

export function OrganizationDrawer({ client, organizationId, initialInvite, onChanged }: OrganizationDrawerProps) {
  const { t, formatDate } = useI18n();
  const state = useOrganizationDetail(client.organizations, organizationId);

  if (state.status === 'loading') return <LoadingCards count={3} />;
  if (state.status === 'error') {
    return <ErrorState message={t('platform.organization.drawer.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;
  }

  const organization: OrganizationDetail = state.data;
  const handleUpdated = (next: OrganizationDetail) => { state.apply(next); onChanged(); };

  return (
    <div className="flex flex-col gap-4 overflow-y-auto">
      <Card>
        <CardHeader><CardTitle>{t('platform.organization.section.overview')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-2 text-sm">
          <Field label={t('platform.organization.overview.slug')}><code>{organization.slug}</code></Field>
          <Field label={t('platform.organization.overview.created')}>{formatDate(organization.createdAtUtc)}</Field>
          <Field label={t('platform.organization.overview.updated')}>{formatDate(organization.updatedAtUtc)}</Field>
          <Field label={t('platform.organization.overview.branches')}>
            {organization.branches.length === 0 ? '—' : organization.branches.map(b => b.name).join(', ')}
          </Field>
          {organization.statusReason !== null && (
            <Field label={t('platform.organization.overview.statusReason')}>{organization.statusReason}</Field>
          )}
        </CardContent>
      </Card>

      <OrganizationStatusSection client={client.organizations} organization={organization} onUpdated={handleUpdated} />
      <OrganizationSubscriptionSection client={client.subscriptions} plans={client.plans} organizationId={organization.organizationId} />
      <OrganizationLimitsSection client={client.organizations} organization={organization} onUpdated={handleUpdated} />
      <OrganizationInvoicesSection client={client.invoices} organizationId={organization.organizationId} />

      <OrganizationOwnerInvitesSection client={client.organizationOwnerInvites} organizationId={organization.organizationId} branches={organization.branches} initialInvite={initialInvite} />
      <OrganizationSupportNotesSection client={client.supportNotes} organizationId={organization.organizationId} />

      <OrganizationHealthSection client={client.organizations} organizationId={organization.organizationId} />
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
