import type { PlatformApiClient } from '@/api/platformApi';
import type { OrganizationOwnerInvite } from '@/api/types';
import { PageHeader } from '@/components/layout/PageHeader';
import { Workspace } from '@/components/layout/Workspace';
import { TabBoundary } from '@/components/shared/TabBoundary';
import { ErrorState, ForbiddenState, LoadingCards } from '@/components/ui/states';
import { useI18n, type MessageKey } from '@/i18n/I18nProvider';
import type { OrganizationTab } from '@/routing/platformRoute';
import { useOrganizationDetail } from './useOrganizationDetail';
import { ClientPassport } from './ClientPassport';
import { OrganizationClubsTab } from './OrganizationClubsTab';
import { OrganizationHealthSection } from './OrganizationHealthSection';
import { OrganizationInvoicesSection } from './OrganizationInvoicesSection';
import { OrganizationLimitsSection } from './OrganizationLimitsSection';
import { OrganizationOwnerInvitesSection } from './OrganizationOwnerInvitesSection';
import { OrganizationStatusSection } from './OrganizationStatusSection';
import { OrganizationSupportNotesSection } from './OrganizationSupportNotesSection';
import { OrganizationUpdateChannelSection } from './OrganizationUpdateChannelSection';
import { OrganizationHistoryTab } from './OrganizationHistoryTab';

const TABS: { value: OrganizationTab; labelKey: MessageKey; allowed: (access: OrganizationPageAccess) => boolean }[] = [
  { value: 'clubs', labelKey: 'platform.organization.tab.clubs', allowed: () => true },
  { value: 'invoices', labelKey: 'platform.organization.tab.invoices', allowed: access => access.canViewBilling },
  { value: 'limits', labelKey: 'platform.organization.tab.limits', allowed: access => access.canManageOrganization },
  { value: 'updates', labelKey: 'platform.organization.tab.updates', allowed: access => access.canManageUpdateChannel },
  { value: 'access', labelKey: 'platform.organization.tab.access', allowed: access => access.canManageAccess || access.canViewSupport },
  { value: 'history', labelKey: 'platform.organization.tab.history', allowed: access => access.canViewAudit }
];

export interface OrganizationPageAccess {
  canManageOrganization: boolean;
  canManageAccess: boolean;
  canViewSupport: boolean;
  canViewBilling: boolean;
  canManageBilling: boolean;
  canManageProfile: boolean;
  canManageUpdateChannel: boolean;
  canTransferOwner: boolean;
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
  const { t } = useI18n();
  const state = useOrganizationDetail(client.organizations, organizationId);
  if (state.status === 'loading') return <LoadingCards count={3} />;
  if (state.status === 'error') return <ErrorState message={t('platform.organization.drawer.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;
  const organization = state.data;
  const apply = (next: typeof organization) => { state.apply(next); onChanged(); };
  const visibleTabs = TABS.filter(item => item.allowed(access));
  if (!visibleTabs.some(item => item.value === tab)) {
    return <Workspace width="narrow"><ForbiddenState title={t('state.forbidden.title')} message={t('state.forbidden.message')} actionLabel={t('platform.organization.tab.clubs')} onAction={() => onTabChange('clubs')} /></Workspace>;
  }
  const activeTab = tab;
  const boundaryProps = { message: t('platform.organization.tabBoundary.error'), retryLabel: t('platform.organization.tabBoundary.retry') };

  return <Workspace>
    <PageHeader title={organization.name} description={`${organization.slug} · ${organization.status}`} />
    <div className="grid grid-cols-1 gap-5 lg:grid-cols-[280px_1fr] lg:items-start">
      <TabBoundary {...boundaryProps}>
        <ClientPassport
          client={{
            organizations: client.organizations,
            subscriptions: client.subscriptions,
            invoices: client.invoices,
            organizationOwnerInvites: client.organizationOwnerInvites
          }}
          organization={organization}
          access={access}
          onUpdated={apply}
        />
      </TabBoundary>

      <div className="flex min-w-0 flex-col gap-4">
        <div role="tablist" aria-label={t('platform.organization.tabs.label')} className="flex gap-1 overflow-x-auto border-b border-border">{visibleTabs.map(item => <button key={item.value} type="button" role="tab" aria-selected={activeTab === item.value} onClick={() => onTabChange(item.value)} className="min-h-10 shrink-0 border-b-2 border-transparent px-3 py-2 text-sm font-medium text-muted-foreground hover:text-foreground aria-selected:border-primary aria-selected:text-foreground">{t(item.labelKey)}</button>)}</div>

        {activeTab === 'clubs' ? <div role="tabpanel" className="flex flex-col gap-4">
            <TabBoundary {...boundaryProps}><OrganizationHealthSection client={client.organizations} organizationId={organizationId} /></TabBoundary>
            <TabBoundary {...boundaryProps}><OrganizationClubsTab client={client.pulse} organizationId={organizationId} branches={organization.branches} /></TabBoundary>
          </div> : null}
        {activeTab === 'invoices' ? <div role="tabpanel"><TabBoundary {...boundaryProps}><OrganizationInvoicesSection client={client.invoices} organizationId={organizationId} /></TabBoundary></div> : null}
        {activeTab === 'limits' ? <div role="tabpanel" className="flex flex-col gap-4">
            <TabBoundary {...boundaryProps}><OrganizationStatusSection client={client.organizations} organization={organization} onUpdated={apply} /></TabBoundary>
            <TabBoundary {...boundaryProps}><OrganizationLimitsSection client={client.organizations} organization={organization} onUpdated={apply} /></TabBoundary>
          </div> : null}
        {activeTab === 'updates' ? <div role="tabpanel"><TabBoundary {...boundaryProps}><OrganizationUpdateChannelSection client={client.organizations} organization={organization} onUpdated={apply} /></TabBoundary></div> : null}
        {activeTab === 'access' ? <div role="tabpanel" className="flex flex-col gap-4">
            {access.canManageAccess ? <TabBoundary {...boundaryProps}><OrganizationOwnerInvitesSection client={client.organizationOwnerInvites} organizationId={organizationId} branches={organization.branches} initialInvite={initialInvite} /></TabBoundary> : null}
            {access.canViewSupport ? <TabBoundary {...boundaryProps}><OrganizationSupportNotesSection client={client.supportNotes} organizationId={organizationId} /></TabBoundary> : null}
          </div> : null}
        {activeTab === 'history' ? <div role="tabpanel"><TabBoundary {...boundaryProps}><OrganizationHistoryTab client={client.audit} organizationId={organizationId} /></TabBoundary></div> : null}
      </div>
    </div>
  </Workspace>;
}
