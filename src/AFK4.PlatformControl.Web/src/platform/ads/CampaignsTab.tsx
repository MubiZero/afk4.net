import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { ErrorState, EmptyState, PartialFailure } from '@/components/ui/states';
import { Loading, SkeletonCard, SkeletonTable } from '@/components/ui/skeletons';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { AdsApi } from '@/api/platformClients/ads';
import type { AdCampaignDto } from '@/api/types';
import { CampaignFormDialog } from './CampaignFormDialog';
import {
  approvedCount,
  describeAdError,
  describeCategory,
  describePhase,
  emptyCampaignForm,
  pendingCount,
  requestFromCampaignForm,
  type CampaignForm
} from './adsModel';
import { useLoadable } from '../useLoadable';

export type CampaignsClient = Pick<AdsApi, 'listCampaigns' | 'listAdvertisers' | 'createCampaign'>;

export function CampaignsTab({ client, onOpenCampaign, onOpenAdvertisers }: {
  client: CampaignsClient;
  onOpenCampaign: (campaignId: string) => void;
  onOpenAdvertisers: () => void;
}) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const campaigns = useLoadable(async () => {
    const loaded = await client.listCampaigns();
    return Array.isArray(loaded) ? loaded : [];
  });
  // Рекламодатели нужны только форме новой кампании: их отказ не прячет список кампаний.
  const advertisers = useLoadable(async () => {
    const loaded = await client.listAdvertisers();
    return Array.isArray(loaded) ? loaded : [];
  });
  const [form, setForm] = useState<CampaignForm | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const advertiserList = advertisers.status === 'ready' ? advertisers.data : null;

  function openNew() {
    if (advertiserList === null || advertiserList.length === 0) return;
    setSaveError(null);
    // Рекламодатель один — выбирать не из чего, подставляем его.
    setForm(emptyCampaignForm(new Date(), advertiserList.length === 1 ? advertiserList[0].advertiserId : ''));
  }

  function close() {
    if (pending) return;
    setForm(null);
    setSaveError(null);
  }

  async function save() {
    if (form === null || pending) return;
    setPending(true);
    setSaveError(null);
    try {
      const created = await client.createCampaign(requestFromCampaignForm(form));
      toast({ title: t('platform.ads.campaigns.created'), variant: 'success' });
      setForm(null);
      // Следующий шаг у новой кампании один — креатив, и живёт он на её странице.
      onOpenCampaign(created.campaignId);
    } catch (cause) {
      setSaveError(describeAdError(cause, t));
    } finally {
      setPending(false);
    }
  }

  if (campaigns.status === 'error') {
    return <ErrorState title={t('platform.ads.campaigns.error.load')} message={campaigns.message} retryLabel={campaigns.canRetry ? t('state.retry') : undefined} onRetry={campaigns.canRetry ? campaigns.retry : undefined} />;
  }
  if (campaigns.status === 'loading') {
    return (
      <Loading>
        <SkeletonCard action>
          <p className="mgmt-drawer-hint">{t('platform.ads.campaigns.description')}</p>
          <SkeletonTable columns={7} />
        </SkeletonCard>
      </Loading>
    );
  }

  const list = campaigns.data;
  const noAdvertisers = advertiserList !== null && advertiserList.length === 0;
  const now = new Date();

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.ads.campaigns.title')}</CardTitle>
        {noAdvertisers ? null : (
          <Button disabled={advertiserList === null} onClick={openNew}>{t('platform.ads.campaigns.create')}</Button>
        )}
      </CardHeader>
      <CardContent>
        <p className="mgmt-drawer-hint">{t('platform.ads.campaigns.description')}</p>

        {advertisers.status === 'error' ? (
          <PartialFailure title={t('platform.ads.campaigns.error.advertisers')} retryLabel={t('state.retry')} onRetry={advertisers.retry} />
        ) : null}

        {list.length === 0 ? (
          noAdvertisers ? (
            <EmptyState message={t('platform.ads.campaigns.emptyNoAdvertisers')} next={{ label: t('platform.ads.campaigns.toAdvertisers'), onClick: onOpenAdvertisers }} />
          ) : (
            <EmptyState
              message={t('platform.ads.campaigns.empty')}
              next={advertiserList === null ? 'formAbove' : { label: t('platform.ads.campaigns.createFirst'), onClick: openNew }}
            />
          )
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.ads.campaigns.column.name')}</TableHead>
                <TableHead>{t('platform.ads.campaigns.column.advertiser')}</TableHead>
                <TableHead>{t('platform.ads.campaign.field.category')}</TableHead>
                <TableHead>{t('platform.ads.campaigns.column.dates')}</TableHead>
                <TableHead>{t('platform.ads.campaigns.column.state')}</TableHead>
                <TableHead>{t('platform.ads.campaigns.column.creatives')}</TableHead>
                <TableHead>{t('platform.ads.column.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {list.map(campaign => {
                const phase = describePhase(campaign, now, t);
                return (
                  <TableRow key={campaign.campaignId}>
                    <TableCell>{campaign.name}</TableCell>
                    <TableCell>{campaign.advertiserName === '' ? '—' : campaign.advertiserName}</TableCell>
                    <TableCell>{describeCategory(campaign.category, t)}</TableCell>
                    <TableCell className="pc-ad-dates">{formatDate(campaign.startsAtUtc)} — {formatDate(campaign.endsAtUtc)}</TableCell>
                    <TableCell><Badge variant={phase.variant}>{phase.label}</Badge></TableCell>
                    <TableCell><CreativesSummary campaign={campaign} /></TableCell>
                    <TableCell>
                      <span className="pc-cell-actions">
                        <Button size="sm" variant="outline" onClick={() => onOpenCampaign(campaign.campaignId)}>
                          {t('platform.ads.campaigns.open')}
                        </Button>
                      </span>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}

        {form === null || advertiserList === null ? null : (
          <CampaignFormDialog
            mode="create"
            form={form}
            advertisers={advertiserList}
            pending={pending}
            error={saveError}
            onChange={setForm}
            onSubmit={() => void save()}
            onClose={close}
          />
        )}
      </CardContent>
    </Card>
  );
}

function CreativesSummary({ campaign }: { campaign: AdCampaignDto }) {
  const { t } = useI18n();
  const total = campaign.creatives.length;
  if (total === 0) return <span className="mgmt-drawer-hint">{t('platform.ads.campaigns.noCreatives')}</span>;
  const pending = pendingCount(campaign);
  return (
    <span>
      {t('platform.ads.campaigns.creativesApproved', { approved: approvedCount(campaign), total })}
      {pending > 0 ? <> · {t('platform.ads.campaigns.creativesPending', { count: pending })}</> : null}
    </span>
  );
}
