import { Page } from '@/components/layout/Page';
import { Tabs } from '@/components/ui/tabs';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { BillingTab } from '@/routing/platformRoute';
import { DebtSection, type DebtSectionAccess } from './DebtSection';
import { PayableQueue } from './PayableQueue';
import { SubscriptionsTab } from './SubscriptionsTab';
import { InvoicesTab } from './InvoicesTab';
import { PlansAndTermsTab } from './PlansTab';
import { AnalyticsTab } from './AnalyticsTab';

export function BillingScreen({ client, tab, onTabChange, canManageInvoices, canManagePlans, debtAccess }: {
  client: PlatformApiClient;
  tab: BillingTab;
  onTabChange: (tab: BillingTab) => void;
  /// «Отметить оплаченным» и «Аннулировать» сервер спрашивает по праву на счета, а не по любому
  /// из трёх денежных: с правом только на тарифы эти кнопки обещали бы отказ.
  canManageInvoices: boolean;
  /// Тарифы сервер правит по своему праву, а не по любому из трёх денежных.
  canManagePlans: boolean;
  debtAccess: DebtSectionAccess;
}) {
  const { t } = useI18n();
  return (
    <Page title={t('nav.platform.money')} description={t('platform.billing.subtitle')}>
      <DebtSection client={client} access={debtAccess} />
      <PayableQueue client={client.invoices} canManage={canManageInvoices} />

      <Tabs
        label={t('platform.billing.tabs.label')}
        value={tab}
        onChange={onTabChange}
        items={[
          { value: 'subscriptions', label: t('platform.billing.tab.subscriptions') },
          { value: 'invoices', label: t('platform.billing.tab.invoices') },
          { value: 'plans', label: t('platform.billing.tab.plans') },
          { value: 'analytics', label: t('platform.billing.tab.analytics') }
        ]}
      />

      <div role="tabpanel">
        {tab === 'subscriptions' ? <SubscriptionsTab client={client.subscriptions} /> : null}
        {tab === 'invoices' ? <InvoicesTab client={client.invoices} canManage={canManageInvoices} /> : null}
        {tab === 'plans' ? <PlansAndTermsTab client={client.plans} canManage={canManagePlans} /> : null}
        {tab === 'analytics' ? <AnalyticsTab client={client.analytics} /> : null}
      </div>
    </Page>
  );
}
