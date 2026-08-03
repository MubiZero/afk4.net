import type { ReactNode } from 'react';
import type { PlatformApiClient } from '@/api/platformApi';
import type { OrganizationOwnerInvite } from '@/api/types';
import { PageHeader } from '@/components/layout/PageHeader';
import { Workspace } from '@/components/layout/Workspace';
import { Card, CardContent } from '@/components/ui/card';
import { ErrorState, ForbiddenState, LoadingCards } from '@/components/ui/states';
import { useI18n, type MessageKey } from '@/i18n/I18nProvider';
import type { OrganizationTab } from '@/routing/platformRoute';
import { useOrganizationDetail } from './useOrganizationDetail';
import { OrganizationHealthSection } from './OrganizationHealthSection';
import { OrganizationInvoicesSection } from './OrganizationInvoicesSection';
import { OrganizationLimitsSection } from './OrganizationLimitsSection';
import { OrganizationOwnerInvitesSection } from './OrganizationOwnerInvitesSection';
import { OrganizationStatusSection } from './OrganizationStatusSection';
import { OrganizationSubscriptionSection } from './OrganizationSubscriptionSection';
import { OrganizationSupportNotesSection } from './OrganizationSupportNotesSection';
import { OrganizationHistoryTab } from './OrganizationHistoryTab';

const TABS: { value: OrganizationTab; labelKey: MessageKey; allowed: (access: OrganizationPageAccess) => boolean }[] = [
  { value: 'summary', labelKey: 'platform.organization.tab.summary', allowed: () => true },
  { value: 'clubs', labelKey: 'platform.organization.tab.clubs', allowed: () => true },
  { value: 'access', labelKey: 'platform.organization.tab.access', allowed: access => access.canManageAccess },
  { value: 'subscription', labelKey: 'platform.organization.tab.subscription', allowed: access => access.canViewBilling },
  { value: 'invoices', labelKey: 'platform.organization.tab.invoices', allowed: access => access.canViewBilling },
  { value: 'support', labelKey: 'platform.organization.tab.support', allowed: access => access.canViewSupport },
  { value: 'history', labelKey: 'platform.organization.tab.history', allowed: access => access.canViewAudit }
];

export interface OrganizationPageAccess {
  canManageOrganization: boolean;
  canManageAccess: boolean;
  canViewSupport: boolean;
  canViewBilling: boolean;
  canViewAudit: boolean;
}

export function OrganizationPage({ client, organizationId, tab, access, initialInvite, onTabChange, onChanged }: {
  client: PlatformApiClient;
  organizationId: string;
  tab: OrganizationTab;
  access: OrganizationPageAccess;
  initialInvite: OrganizationOwnerInvite | null;
  onTabChange: (tab: OrganizationTab) => void;
  onChanged: () => void;
}) {
  const { t, formatDate } = useI18n();
  const state = useOrganizationDetail(client.organizations, organizationId);
  if (state.status === 'loading') return <LoadingCards count={3} />;
  if (state.status === 'error') return <ErrorState message={t('platform.organization.drawer.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;
  const organization = state.data;
  const apply = (next: typeof organization) => { state.apply(next); onChanged(); };
  const visibleTabs = TABS.filter(item => item.allowed(access));
  if (!visibleTabs.some(item => item.value === tab)) {
    return <Workspace width="narrow"><ForbiddenState title={t('state.forbidden.title')} message={t('state.forbidden.message')} actionLabel={t('platform.organization.tab.summary')} onAction={() => onTabChange('summary')} /></Workspace>;
  }
  const activeTab = tab;

  return <Workspace>
    <PageHeader title={organization.name} description={`${organization.slug} · ${organization.status}`} />
    <div role="tablist" aria-label={t('platform.organization.tabs.label')} className="flex gap-1 overflow-x-auto border-b border-border">{visibleTabs.map(item => <button key={item.value} type="button" role="tab" aria-selected={activeTab === item.value} onClick={() => onTabChange(item.value)} className="min-h-10 shrink-0 border-b-2 border-transparent px-3 py-2 text-sm font-medium text-muted-foreground hover:text-foreground aria-selected:border-primary aria-selected:text-foreground">{t(item.labelKey)}</button>)}</div>
    {activeTab === 'summary' ? <div role="tabpanel" className="grid gap-4 xl:grid-cols-2">
        <Card><CardContent className="grid gap-3 py-5 text-sm"><Field label={t('platform.organization.overview.slug')}>{organization.slug}</Field><Field label={t('platform.organization.overview.created')}>{formatDate(organization.createdAtUtc)}</Field><Field label={t('platform.organization.overview.updated')}>{formatDate(organization.updatedAtUtc)}</Field><Field label={t('platform.organization.overview.branches')}>{organization.branches.length}</Field></CardContent></Card>
        {access.canManageOrganization ? <OrganizationStatusSection client={client.organizations} organization={organization} onUpdated={apply} /> : null}
        {access.canManageOrganization ? <OrganizationLimitsSection client={client.organizations} organization={organization} onUpdated={apply} /> : null}
        <OrganizationHealthSection client={client.organizations} organizationId={organizationId} />
      </div> : null}
    {activeTab === 'clubs' ? <div role="tabpanel" className="overflow-hidden rounded-lg border border-border bg-card">{organization.branches.map(branch => <div key={branch.branchId} className="flex items-center justify-between gap-4 border-b border-border px-4 py-3 last:border-0"><div><div className="font-semibold">{branch.name}</div><div className="text-xs text-muted-foreground">{branch.city}</div></div><code className="text-xs">{branch.slug}</code></div>)}</div> : null}
    {activeTab === 'access' ? <div role="tabpanel"><OrganizationOwnerInvitesSection client={client.organizationOwnerInvites} organizationId={organizationId} branches={organization.branches} initialInvite={initialInvite} /></div> : null}
    {activeTab === 'subscription' ? <div role="tabpanel"><OrganizationSubscriptionSection client={client.subscriptions} plans={client.plans} organizationId={organizationId} /></div> : null}
    {activeTab === 'invoices' ? <div role="tabpanel"><OrganizationInvoicesSection client={client.invoices} organizationId={organizationId} /></div> : null}
    {activeTab === 'support' ? <div role="tabpanel"><OrganizationSupportNotesSection client={client.supportNotes} organizationId={organizationId} /></div> : null}
    {activeTab === 'history' ? <div role="tabpanel"><OrganizationHistoryTab client={client.audit} organizationId={organizationId} /></div> : null}
  </Workspace>;
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return <div className="flex items-center justify-between gap-4"><span className="text-muted-foreground">{label}</span><span className="text-right font-medium">{children}</span></div>;
}
