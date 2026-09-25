import { Page } from '@/components/layout/Page';
import { Tabs } from '@/components/ui/tabs';
import { useI18n } from '@/i18n/I18nProvider';
import type { AdsTab } from '@/routing/platformRoute';
import { AdvertisersTab, type AdvertisersClient } from './AdvertisersTab';
import { CampaignsTab, type CampaignsClient } from './CampaignsTab';
import { ReportTab, type ReportClient } from './ReportTab';

export type AdsClient = AdvertisersClient & CampaignsClient & ReportClient;

// Раздел «Реклама»: AFK4 продаёт место в витрине свободного ПК у клубов на бесплатном тарифе.
// Модерация — на странице кампании, рядом с креативами: отдельного права и отдельной очереди
// у маленькой команды платформы нет.
export function AdsScreen({ client, tab, onTabChange, onOpenCampaign }: {
  client: AdsClient;
  tab: AdsTab;
  onTabChange: (tab: AdsTab) => void;
  onOpenCampaign: (campaignId: string) => void;
}) {
  const { t } = useI18n();
  return (
    <Page title={t('nav.platform.ads')} description={t('platform.ads.subtitle')}>
      <Tabs
        label={t('platform.ads.tabs.label')}
        value={tab}
        onChange={onTabChange}
        items={[
          { value: 'campaigns', label: t('platform.ads.tab.campaigns') },
          { value: 'advertisers', label: t('platform.ads.tab.advertisers') },
          { value: 'report', label: t('platform.ads.tab.report') }
        ]}
      />

      <div role="tabpanel">
        {tab === 'campaigns' ? <CampaignsTab client={client} onOpenCampaign={onOpenCampaign} onOpenAdvertisers={() => onTabChange('advertisers')} /> : null}
        {tab === 'advertisers' ? <AdvertisersTab client={client} /> : null}
        {tab === 'report' ? <ReportTab client={client} /> : null}
      </div>
    </Page>
  );
}
