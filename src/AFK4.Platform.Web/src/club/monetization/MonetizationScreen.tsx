import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { useI18n } from '@/i18n/I18nProvider';
import type { TariffsApi } from '@/api/clients/tariffs';
import type { CatalogApi } from '@/api/clients/catalog';
import type { PackagesApi } from '@/api/clients/packages';
import { TariffsTab } from './tariffs/TariffsTab';
import { CatalogTab } from './catalog/CatalogTab';
import { PackagesTab } from './packages/PackagesTab';

export function MonetizationScreen({ tariffs, catalog, packages, branchId, organizationId, canManageTariffs, canManageCatalog, canManagePackages }: {
  tariffs: TariffsApi;
  catalog: CatalogApi;
  packages: PackagesApi;
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
        <TariffsTab client={tariffs} branchId={branchId} organizationId={organizationId} canManage={canManageTariffs} />
      </TabsContent>
      <TabsContent value="products">
        <CatalogTab client={catalog} branchId={branchId} organizationId={organizationId} canManage={canManageCatalog} />
      </TabsContent>
      <TabsContent value="loyalty">
        <PackagesTab client={packages} branchId={branchId} organizationId={organizationId} canManage={canManagePackages} />
      </TabsContent>
    </Tabs>
  );
}
