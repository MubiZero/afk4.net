import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import { SubscriptionsTab } from './SubscriptionsTab';
import { InvoicesTab } from './InvoicesTab';
import { PlansTab } from './PlansTab';

export function BillingScreen({ client, tab, onTabChange, canManage }: { client: PlatformApiClient; tab: 'plans' | 'subscriptions' | 'invoices'; onTabChange: (tab: 'plans' | 'subscriptions' | 'invoices') => void; canManage: boolean }) {
  const { t } = useI18n();
  return (
    <Tabs value={tab} onValueChange={value => onTabChange(value as typeof tab)} className="flex flex-col gap-4">
      <TabsList>
        <TabsTrigger value="subscriptions">{t('platform.billing.tab.subscriptions')}</TabsTrigger>
        <TabsTrigger value="invoices">{t('platform.billing.tab.invoices')}</TabsTrigger>
        <TabsTrigger value="plans">{t('platform.billing.tab.plans')}</TabsTrigger>
      </TabsList>
      <TabsContent value="subscriptions"><SubscriptionsTab client={client.subscriptions} /></TabsContent>
      <TabsContent value="invoices"><InvoicesTab client={client.invoices} canManage={canManage} /></TabsContent>
      <TabsContent value="plans"><PlansTab client={client.plans} canManage={canManage} /></TabsContent>
    </Tabs>
  );
}
