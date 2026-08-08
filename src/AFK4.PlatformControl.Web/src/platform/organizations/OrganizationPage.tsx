import type { PlatformApiClient } from '@/api/platformApi';
import type { OrganizationOwnerInvite } from '@/api/types';
import { Page } from '@/components/layout/Page';
import { TabBoundary } from '@/components/shared/TabBoundary';
import { Tabs } from '@/components/ui/tabs';
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
import { SupportAccessSection } from './SupportAccessSection';
import { OrganizationUpdateChannelSection } from './OrganizationUpdateChannelSection';
import { OrganizationHistoryTab } from './OrganizationHistoryTab';
import { OrganizationDynamicsTab } from './OrganizationDynamicsTab';
import { OrganizationFeaturesTab } from './OrganizationFeaturesTab';

const TABS: { value: OrganizationTab; labelKey: MessageKey; allowed: (access: OrganizationPageAccess) => boolean }[] = [
  { value: 'clubs', labelKey: 'platform.organization.tab.clubs', allowed: () => true },
  { value: 'dynamics', labelKey: 'platform.organization.tab.dynamics', allowed: () => true },
  { value: 'features', labelKey: 'platform.organization.features.tab', allowed: () => true },
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
  canManageFeatures: boolean;
}

export function OrganizationPage({ client, organizationId, tab, access, initialInvite, onTabChange, onBack, onChanged }: {
  client: PlatformApiClient;
  organizationId: string;
  tab: OrganizationTab;
  access: OrganizationPageAccess;
  initialInvite: OrganizationOwnerInvite | null;
  onTabChange: (tab: OrganizationTab) => void;
  onBack: () => void;
  onChanged: () => void;
}) {
  const { t } = useI18n();
  const state = useOrganizationDetail(client.organizations, organizationId);
  // Возврат к списку доступен на ЛЮБОМ состоянии экрана, включая ошибку загрузки: раньше выйти
  // из карточки можно было только уходом в другой раздел рейла.
  const back = { label: t('platform.organization.backToClubs'), onBack };

  if (state.status === 'loading') return <Page back={back}><LoadingCards count={3} /></Page>;
  if (state.status === 'error') {
    return <Page back={back}><ErrorState message={t('platform.organization.drawer.error')} retryLabel={t('state.retry')} onRetry={state.retry} /></Page>;
  }

  const organization = state.data;
  const apply = (next: typeof organization) => { state.apply(next); onChanged(); };
  const visibleTabs = TABS.filter(item => item.allowed(access));
  if (!visibleTabs.some(item => item.value === tab)) {
    return (
      <Page back={back} width="form">
        <ForbiddenState title={t('state.forbidden.title')} message={t('state.forbidden.message')} actionLabel={t('platform.organization.tab.clubs')} onAction={() => onTabChange('clubs')} />
      </Page>
    );
  }

  const boundaryProps = { message: t('platform.organization.tabBoundary.error'), retryLabel: t('platform.organization.tabBoundary.retry') };
  // Панели вкладок и так размонтируются при переключении (каждая — условная ветка), но граница
  // паспорта живёт всё время жизни экрана: её resetKey обязан следить за организацией, иначе
  // застрявшая ошибка переедет на следующего клиента.
  const tabResetKey = `${organizationId}:${tab}`;

  return (
    <Page back={back} width="full" title={organization.name} description={organization.slug}>
      <div className="pc-client">
        <div className="pc-client-main">
          <Tabs
            label={t('platform.organization.tabs.label')}
            value={tab}
            onChange={onTabChange}
            items={visibleTabs.map(item => ({ value: item.value, label: t(item.labelKey) }))}
          />

          <div role="tabpanel" className="pc-client-panel">
            {tab === 'clubs' ? (
              <>
                <TabBoundary {...boundaryProps} resetKey={tabResetKey}><OrganizationHealthSection client={client.organizations} organizationId={organizationId} /></TabBoundary>
                <TabBoundary {...boundaryProps} resetKey={tabResetKey}>
                  <OrganizationClubsTab
                    client={client.pulse}
                    organizationsClient={client.organizations}
                    organizationId={organizationId}
                    branches={organization.branches}
                    limits={organization.limits}
                    onBranchCreated={branch => apply({ ...organization, branches: [...organization.branches, branch] })}
                  />
                </TabBoundary>
              </>
            ) : null}
            {tab === 'invoices' ? <TabBoundary {...boundaryProps} resetKey={tabResetKey}><OrganizationInvoicesSection client={client.invoices} organizationId={organizationId} /></TabBoundary> : null}
            {tab === 'limits' ? (
              <>
                <TabBoundary {...boundaryProps} resetKey={tabResetKey}><OrganizationStatusSection client={client.organizations} organization={organization} onUpdated={apply} /></TabBoundary>
                <TabBoundary {...boundaryProps} resetKey={tabResetKey}><OrganizationLimitsSection client={client.organizations} organization={organization} onUpdated={apply} /></TabBoundary>
              </>
            ) : null}
            {tab === 'updates' ? <TabBoundary {...boundaryProps} resetKey={tabResetKey}><OrganizationUpdateChannelSection client={client.organizations} organization={organization} onUpdated={apply} /></TabBoundary> : null}
            {tab === 'access' ? (
              <>
                {access.canManageAccess ? <TabBoundary {...boundaryProps} resetKey={tabResetKey}><OrganizationOwnerInvitesSection client={client.organizationOwnerInvites} organizationId={organizationId} branches={organization.branches} initialInvite={initialInvite} /></TabBoundary> : null}
                {access.canViewSupport ? <TabBoundary {...boundaryProps} resetKey={tabResetKey}><OrganizationSupportNotesSection client={client.supportNotes} organizationId={organizationId} /></TabBoundary> : null}
                {access.canViewSupport ? <TabBoundary {...boundaryProps} resetKey={tabResetKey}><SupportAccessSection client={client.supportAccess} organizationId={organizationId} /></TabBoundary> : null}
              </>
            ) : null}
            {tab === 'history' ? <TabBoundary {...boundaryProps} resetKey={tabResetKey}><OrganizationHistoryTab client={client.audit} organizationId={organizationId} /></TabBoundary> : null}
            {tab === 'dynamics' ? <TabBoundary {...boundaryProps} resetKey={tabResetKey}><OrganizationDynamicsTab client={client.branchDynamics} organizationId={organizationId} branches={organization.branches} /></TabBoundary> : null}
            {tab === 'features' ? <TabBoundary {...boundaryProps} resetKey={tabResetKey}><OrganizationFeaturesTab client={client.features} organizationId={organizationId} planCode={organization.planCode} canManage={access.canManageFeatures} /></TabBoundary> : null}
          </div>
        </div>

        <TabBoundary {...boundaryProps} resetKey={organizationId}>
          <ClientPassport
            client={{
              organizations: client.organizations,
              subscriptions: client.subscriptions,
              invoices: client.invoices,
              organizationOwnerInvites: client.organizationOwnerInvites,
              debt: client.debt
            }}
            organization={organization}
            access={access}
            onUpdated={apply}
          />
        </TabBoundary>
      </div>
    </Page>
  );
}
