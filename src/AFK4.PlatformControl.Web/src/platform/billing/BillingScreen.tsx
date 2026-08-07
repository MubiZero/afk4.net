import { Page } from '@/components/layout/Page';
import { Tabs } from '@/components/ui/tabs';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { BillingTab } from '@/routing/platformRoute';
import { DebtSection, type DebtSectionAccess } from './DebtSection';
import { PayableQueue } from './PayableQueue';
import { SubscriptionsTab } from './SubscriptionsTab';
import { InvoicesTab } from './InvoicesTab';
import { PlansTab } from './PlansTab';

export function BillingScreen({ client, tab, onTabChange, canManage, debtAccess }: {
  client: PlatformApiClient;
  tab: BillingTab;
  onTabChange: (tab: BillingTab) => void;
  canManage: boolean;
  debtAccess: DebtSectionAccess;
}) {
  const { t } = useI18n();
  return (
    <Page title={t('nav.platform.money')} description={t('platform.billing.subtitle')}>
      <DebtSection client={client} access={debtAccess} />
      <PayableQueue client={client.invoices} canManage={canManage} />

      <Tabs
        label={t('platform.billing.tabs.label')}
        value={tab}
        onChange={onTabChange}
        items={[
          { value: 'subscriptions', label: t('platform.billing.tab.subscriptions') },
          { value: 'invoices', label: t('platform.billing.tab.invoices') },
          { value: 'plans', label: t('platform.billing.tab.plans') }
        ]}
      />

      <div role="tabpanel">
        {tab === 'subscriptions' ? <SubscriptionsTab client={client.subscriptions} /> : null}
        {tab === 'invoices' ? <InvoicesTab client={client.invoices} canManage={canManage} /> : null}
        {tab === 'plans' ? <PlansTab client={client.plans} canManage={canManage} /> : null}
      </div>
    </Page>
  );
}
