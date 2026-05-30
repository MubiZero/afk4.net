import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { TariffsTab } from './tariffs/TariffsTab';
import { CatalogTab } from './catalog/CatalogTab';
import { PackagesTab } from './packages/PackagesTab';

export function MonetizationScreen({ client, branchId, organizationId, canManageTariffs, canManageCatalog, canManagePackages }: {
  client: ClubApiClient;
  branchId: string;
  organizationId: string;
  canManageTariffs: boolean;
  canManageCatalog: boolean;
  canManagePackages: boolean;
}) {
  const { t } = useI18n();
  return (
    <Tabs defaultValue="tariffs">
      <TabsList>
        <TabsTrigger value="tariffs">{t('monetization.tab.tariffs')}</TabsTrigger>
        <TabsTrigger value="products">{t('monetization.tab.products')}</TabsTrigger>
        <TabsTrigger value="loyalty">{t('monetization.tab.loyalty')}</TabsTrigger>
      </TabsList>
      <TabsContent value="tariffs">
        <TariffsTab client={client} branchId={branchId} organizationId={organizationId} canManage={canManageTariffs} />
      </TabsContent>
      <TabsContent value="products">
        <CatalogTab client={client} branchId={branchId} organizationId={organizationId} canManage={canManageCatalog} />
      </TabsContent>
      <TabsContent value="loyalty">
        <PackagesTab client={client} branchId={branchId} organizationId={organizationId} canManage={canManagePackages} />
      </TabsContent>
    </Tabs>
  );
}
