import { useState, type ReactNode } from 'react';
import { AdErrorCodeNames, AdModerationNames, type AdCampaignStateName } from '@afk4/contracts';
import { Page } from '@/components/layout/Page';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { ErrorState, EmptyState } from '@/components/ui/states';
import { Loading, SkeletonCard, SkeletonLine, SkeletonTable } from '@/components/ui/skeletons';
import { useBlockedReason } from '@/components/ui/blockedReason';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { MessageKey } from '@/i18n/messages';
import { PlatformApiError } from '@/api/platformTransport';
import type { AdsApi } from '@/api/platformClients/ads';
import type { AdCampaignDto, AdCreativeDto } from '@/api/types';
import { CampaignFormDialog } from './CampaignFormDialog';
import { CreativeFormDialog } from './CreativeFormDialog';
import { ModerationDialog } from './ModerationDialog';
import { AdThumb } from './AdImages';
import {
  approvedCount,
  campaignPhase,
  describeAdError,
  describeCategory,
  describeModeration,
  describePhase,
  emptyCreativeForm,
  formFromCampaign,
  formFromCreative,
  requestFromCampaignForm,
  requestFromCreativeForm,
  stateActionLabelKey,
  stateActions,
  stateDoneKey,
  withCreative,
  type CampaignForm,
  type CreativeForm
} from './adsModel';
import { useLoadable } from '../useLoadable';

export type AdCampaignClient = Pick<AdsApi,
  'listCampaigns' | 'listAdvertisers' | 'updateCampaign' | 'setCampaignState' | 'createCreative' | 'updateCreative' | 'moderateCreative'>;

type OpenDialog =
  | { kind: 'campaign'; form: CampaignForm }
  | { kind: 'creative'; creativeId: string | null; form: CreativeForm }
  | { kind: 'moderation'; mode: 'approve' | 'reject'; creative: AdCreativeDto };

export function AdCampaignPage({ client, campaignId, onBack }: {
  client: AdCampaignClient;
  campaignId: string;
  onBack: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  // Отдельного адреса у кампании на сервере нет: список короткий, кампания берётся из него.
  const state = useLoadable(async () => {
    const loaded = await client.listCampaigns();
    return (Array.isArray(loaded) ? loaded : []).find(campaign => campaign.campaignId === campaignId) ?? null;
  }, [campaignId]);
  const advertisers = useLoadable(async () => {
    const loaded = await client.listAdvertisers();
    return Array.isArray(loaded) ? loaded : [];
  });
  const [dialog, setDialog] = useState<OpenDialog | null>(null);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const campaign = state.status === 'ready' ? state.data : null;
  // Запуск без одобренного креатива сервер отклонит: показывать было бы нечего. Кнопка гаснет
  // заранее и говорит почему — вместо отказа после нажатия.
  const cannotStart = campaign !== null && approvedCount(campaign) === 0 && stateActions(campaign.state).includes('active');
  const startBlocked = useBlockedReason(cannotStart ? t('platform.ads.campaign.startBlocked') : null);

  const back = { label: t('platform.ads.campaign.back'), onBack };

  if (state.status === 'loading') {
    return (
      <Page back={back} title={<Loading><SkeletonLine width="12em" /></Loading>} description={<Loading><SkeletonLine width="8em" /></Loading>}>
        <Loading>
          <SkeletonCard />
          <SkeletonCard action><SkeletonTable columns={3} rows={2} /></SkeletonCard>
        </Loading>
      </Page>
    );
  }
  if (state.status === 'error') {
    return <Page back={back}><ErrorState title={t('platform.ads.campaign.error.load')} message={state.message} retryLabel={state.canRetry ? t('state.retry') : undefined} onRetry={state.canRetry ? state.retry : undefined} /></Page>;
  }
  if (campaign === null) {
    return <Page back={back}><EmptyState message={t('platform.ads.campaign.notFound')} next={{ label: t('platform.ads.campaign.back'), onClick: onBack }} /></Page>;
  }

  const current = campaign;
  const applyCampaign = state.apply;
  const advertiserList = advertisers.status === 'ready' ? advertisers.data : null;

  function open(next: OpenDialog) {
    setDialogError(null);
    setDialog(next);
  }

  function close() {
    if (pending) return;
    setDialog(null);
    setDialogError(null);
  }

  // Действие в окне: отказ остаётся в окне рядом с тем, что человек ввёл.
  async function runInDialog(action: () => Promise<AdCampaignDto>, doneKey: MessageKey) {
    if (pending) return;
    setPending(true);
    setDialogError(null);
    try {
      applyCampaign(await action());
      toast({ title: t(doneKey), variant: 'success' });
      setDialog(null);
    } catch (cause) {
      setDialogError(describeAdError(cause, t));
    } finally {
      setPending(false);
    }
  }

  async function changeState(next: AdCampaignStateName) {
    if (pending) return;
    setPending(true);
    try {
      applyCampaign(await client.setCampaignState(campaignId, next));
      toast({ title: t(stateDoneKey(next)), variant: 'success' });
    } catch (cause) {
      toast({ title: describeAdError(cause, t), variant: 'error' });
      // Такой отказ значит, что креативы на экране устарели: одобрение сняли в другой вкладке.
      if (cause instanceof PlatformApiError && cause.errorCode === AdErrorCodeNames.NotApproved) state.retry();
    } finally {
      setPending(false);
    }
  }

  function saveCampaign(form: CampaignForm) {
    void runInDialog(() => client.updateCampaign(campaignId, requestFromCampaignForm(form)), 'platform.ads.campaign.saved');
  }

  function saveCreative(creativeId: string | null, form: CreativeForm) {
    const request = requestFromCreativeForm(form);
    void runInDialog(
      async () => withCreative(current, creativeId === null
        ? await client.createCreative(campaignId, request)
        : await client.updateCreative(campaignId, creativeId, request)),
      creativeId === null ? 'platform.ads.creative.created' : 'platform.ads.creative.saved'
    );
  }

  function moderate(creative: AdCreativeDto, approve: boolean, reason: string | null) {
    void runInDialog(
      async () => withCreative(current, await client.moderateCreative(campaignId, creative.creativeId, {
        approve,
        reason: approve ? null : reason,
        confirmedAllowed: approve
      })),
      approve ? 'platform.ads.moderation.approvedToast' : 'platform.ads.moderation.rejectedToast'
    );
  }

  const phase = describePhase(current, new Date(), t);
  const actions = stateActions(current.state);

  return (
    <Page
      back={back}
      title={current.name}
      description={current.advertiserName === '' ? undefined : current.advertiserName}
      actions={
        <>
          {actions.map(next => (
            <Button
              key={next}
              variant={next === 'active' ? 'default' : 'outline'}
              disabled={pending || (next === 'active' && cannotStart)}
              aria-describedby={next === 'active' ? startBlocked.describedBy : undefined}
              onClick={() => void changeState(next)}
            >
              {t(stateActionLabelKey(next))}
            </Button>
          ))}
          <Button
            variant="outline"
            disabled={pending || advertiserList === null}
            onClick={() => open({ kind: 'campaign', form: formFromCampaign(current) })}
          >
            {t('platform.ads.edit')}
          </Button>
        </>
      }
    >
      {startBlocked.hint}

      <Card>
        <CardHeader>
          <CardTitle>{t('platform.ads.campaign.facts')}</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="pc-passport-facts pc-ad-facts">
            <Fact label={t('platform.ads.campaigns.column.state')}><Badge variant={phase.variant}>{phase.label}</Badge></Fact>
            <CampaignFacts campaign={current} />
          </dl>
          {campaignPhase(current, new Date()) === 'nothingToShow' ? (
            <p className="mgmt-drawer-hint">{t('platform.ads.phase.nothingToShowHint')}</p>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('platform.ads.creatives.title')}</CardTitle>
          <Button disabled={pending} onClick={() => open({ kind: 'creative', creativeId: null, form: emptyCreativeForm() })}>
            {t('platform.ads.creatives.create')}
          </Button>
        </CardHeader>
        <CardContent>
          <p className="mgmt-drawer-hint">{t('platform.ads.creatives.description')}</p>
          {current.creatives.length === 0 ? (
            <EmptyState
              message={t('platform.ads.creatives.empty')}
              next={{ label: t('platform.ads.creatives.createFirst'), onClick: () => open({ kind: 'creative', creativeId: null, form: emptyCreativeForm() }) }}
            />
          ) : (
            <CreativesTable
              creatives={current.creatives}
              pending={pending}
              onEdit={creative => open({ kind: 'creative', creativeId: creative.creativeId, form: formFromCreative(creative) })}
              onModerate={(creative, mode) => open({ kind: 'moderation', mode, creative })}
            />
          )}
        </CardContent>
      </Card>

      {dialog?.kind === 'campaign' && advertiserList !== null ? (
        <CampaignFormDialog
          mode="edit"
          form={dialog.form}
          advertisers={advertiserList}
          pending={pending}
          error={dialogError}
          onChange={form => setDialog({ kind: 'campaign', form })}
          onSubmit={() => saveCampaign(dialog.form)}
          onClose={close}
        />
      ) : null}
      {dialog?.kind === 'creative' ? (
        <CreativeFormDialog
          key={dialog.creativeId ?? 'new'}
          mode={dialog.creativeId === null ? 'create' : 'edit'}
          form={dialog.form}
          pending={pending}
          error={dialogError}
          onChange={form => setDialog({ ...dialog, form })}
          onSubmit={() => saveCreative(dialog.creativeId, dialog.form)}
          onClose={close}
        />
      ) : null}
      {dialog?.kind === 'moderation' ? (
        <ModerationDialog
          key={`${dialog.creative.creativeId}:${dialog.mode}`}
          mode={dialog.mode}
          creative={dialog.creative}
          advertiserName={current.advertiserName}
          pending={pending}
          error={dialogError}
          onConfirm={reason => moderate(dialog.creative, dialog.mode === 'approve', reason)}
          onClose={close}
        />
      ) : null}
    </Page>
  );
}

function CampaignFacts({ campaign }: { campaign: AdCampaignDto }) {
  const { t, formatDate } = useI18n();
  return (
    <>
      <Fact label={t('platform.ads.campaign.field.category')}>{describeCategory(campaign.category, t)}</Fact>
      <Fact label={t('platform.ads.campaign.field.startsAt')}>{formatDate(campaign.startsAtUtc)}</Fact>
      <Fact label={t('platform.ads.campaign.field.endsAt')}>{formatDate(campaign.endsAtUtc)}</Fact>
      <Fact label={t('platform.ads.campaign.cities')}>
        {campaign.cities.length === 0 ? t('platform.ads.campaign.allCities') : campaign.cities.join(', ')}
      </Fact>
      <Fact label={t('platform.ads.campaign.clubs')}>
        {campaign.organizationIds.length === 0
          ? t('platform.ads.campaign.allClubs')
          : t('platform.ads.campaign.clubCount', { count: campaign.organizationIds.length })}
      </Fact>
    </>
  );
}

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="pc-passport-row">
      <dt>{label}</dt>
      <dd>{children}</dd>
    </div>
  );
}

function CreativesTable({ creatives, pending, onEdit, onModerate }: {
  creatives: readonly AdCreativeDto[];
  pending: boolean;
  onEdit: (creative: AdCreativeDto) => void;
  onModerate: (creative: AdCreativeDto, mode: 'approve' | 'reject') => void;
}) {
  const { t } = useI18n();
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('platform.ads.creatives.column.creative')}</TableHead>
          <TableHead>{t('platform.ads.creatives.column.moderation')}</TableHead>
          <TableHead>{t('platform.ads.column.actions')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {creatives.map(creative => {
          const moderation = describeModeration(creative.moderation, t);
          return (
            <TableRow key={creative.creativeId}>
              <TableCell>
                <span className="pc-ad-creative">
                  <AdThumb key={creative.imageUrl ?? ''} url={creative.imageUrl} />
                  <span>
                    <strong>{creative.title}</strong>
                    {creative.body !== null ? <span className="mgmt-drawer-hint">{creative.body}</span> : null}
                  </span>
                </span>
              </TableCell>
              <TableCell>
                <span className="pc-ad-moderation">
                  <Badge variant={moderation.variant}>{moderation.label}</Badge>
                  {creative.moderation === AdModerationNames.Rejected && creative.rejectedReason !== null ? (
                    <span className="mgmt-drawer-hint">{t('platform.ads.moderation.reasonShown', { reason: creative.rejectedReason })}</span>
                  ) : null}
                </span>
              </TableCell>
              <TableCell>
                {/* «Изменить» есть у каждого креатива и стоит последней — на одном месте во всех строках. */}
                <span className="pc-cell-actions">
                  {creative.moderation !== AdModerationNames.Approved ? (
                    <Button size="sm" disabled={pending} onClick={() => onModerate(creative, 'approve')}>{t('platform.ads.moderation.approve')}</Button>
                  ) : null}
                  {creative.moderation !== AdModerationNames.Rejected ? (
                    <Button size="sm" variant="outline" disabled={pending} onClick={() => onModerate(creative, 'reject')}>{t('platform.ads.moderation.reject')}</Button>
                  ) : null}
                  <Button size="sm" variant="outline" disabled={pending} onClick={() => onEdit(creative)}>{t('platform.ads.edit')}</Button>
                </span>
              </TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}
