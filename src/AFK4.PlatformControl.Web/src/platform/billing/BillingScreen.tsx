import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import { SubscriptionsTab } from './SubscriptionsTab';
import { InvoicesTab } from './InvoicesTab';
import { PlansTab } from './PlansTab';

export function BillingScreen({ client }: { client: PlatformApiClient }) {
  const { t } = useI18n();
  return (
    <Tabs defaultValue="subscriptions" className="flex flex-col gap-4">
      <TabsList>
        <TabsTrigger value="subscriptions">{t('platform.billing.tab.subscriptions')}</TabsTrigger>
        <TabsTrigger value="invoices">{t('platform.billing.tab.invoices')}</TabsTrigger>
        <TabsTrigger value="plans">{t('platform.billing.tab.plans')}</TabsTrigger>
      </TabsList>
      <TabsContent value="subscriptions"><SubscriptionsTab client={client.subscriptions} /></TabsContent>
      <TabsContent value="invoices"><InvoicesTab client={client.invoices} /></TabsContent>
      <TabsContent value="plans"><PlansTab client={client.plans} /></TabsContent>
    </Tabs>
  );
}
