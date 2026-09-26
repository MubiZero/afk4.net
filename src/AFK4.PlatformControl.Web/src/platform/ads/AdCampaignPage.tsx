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
import type { AdCampaignDto, AdCreativeDto, AdvertiserDto } from '@/api/types';
import { CampaignFormDialog } from './CampaignFormDialog';
import { CreativeFormDialog } from './CreativeFormDialog';
import { ModerationDialog, type ModerationDecision } from './ModerationDialog';
import { ArchiveCreativeDialog } from './ArchiveCreativeDialog';
import { AdCreativeText } from './AdCreativePreview';
import { AdThumb } from './AdImages';
import {
  approvedCount,
  campaignPhase,
  canEditCreative,
  cardNotices,
  creativeActions,
  describeAdError,
  describeCategory,
  describeModeration,
  describePhase,
  emptyCreativeForm,
  formFromCampaign,
  formFromCreative,
  hasLegalDetails,
  isArchived,
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
import { describeMediaError } from '../mediaErrors';

export type AdCampaignClient = Pick<AdsApi,
  | 'listCampaigns'
  | 'listAdvertisers'
  | 'updateCampaign'
  | 'setCampaignState'
  | 'createCreative'
  | 'updateCreative'
  | 'moderateCreative'
  | 'archiveCreative'
  | 'uploadImage'>;

type OpenDialog =
  | { kind: 'campaign'; form: CampaignForm }
  | { kind: 'creative'; creativeId: string | null; form: CreativeForm }
  | { kind: 'moderation'; mode: 'approve' | 'reject'; creative: AdCreativeDto }
  | { kind: 'archive'; creative: AdCreativeDto };

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
  // Причина погашенной «Изменить» у одобренных — одна на таблицу, а не в каждой строке.
  const hasLocked = campaign?.creatives.some(creative => !isArchived(creative) && !canEditCreative(creative)) ?? false;
  const editBlocked = useBlockedReason(hasLocked ? t('platform.ads.creative.locked') : null);

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
  const reloadCampaign = state.retry;
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
      // Креатив одобрили в другой вкладке: список на экране устарел — перечитываем его тихо.
      if (cause instanceof PlatformApiError && cause.errorCode === AdErrorCodeNames.CreativeLocked) reloadCampaign();
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
      // Такой отказ значит, что креативы на экране устарели: креатив сняли с показа в другой вкладке.
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

  function moderate(creative: AdCreativeDto, approve: boolean, decision: ModerationDecision) {
    void runInDialog(
      async () => withCreative(current, await client.moderateCreative(campaignId, creative.creativeId, {
        approve,
        reason: approve ? null : decision.reason,
        confirmed: approve ? decision.confirmed : []
      })),
      approve ? 'platform.ads.moderation.approvedToast' : 'platform.ads.moderation.rejectedToast'
    );
  }

  function archive(creative: AdCreativeDto) {
    void runInDialog(
      async () => withCreative(current, await client.archiveCreative(campaignId, creative.creativeId)),
      'platform.ads.creative.archivedToast'
    );
  }

  const phase = describePhase(current, new Date(), t);
  const actions = stateActions(current.state);
  const advertiser = advertiserList?.find(candidate => candidate.advertiserId === current.advertiserId) ?? null;
  const addCreative = () => open({ kind: 'creative', creativeId: null, form: emptyCreativeForm() });

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
            <CampaignFacts campaign={current} advertiser={advertiser} />
          </dl>
          {campaignPhase(current, new Date()) === 'nothingToShow' ? (
            <p className="mgmt-drawer-hint">{t('platform.ads.phase.nothingToShowHint')}</p>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('platform.ads.creatives.title')}</CardTitle>
          <Button disabled={pending} onClick={addCreative}>{t('platform.ads.creatives.create')}</Button>
        </CardHeader>
        <CardContent>
          <p className="mgmt-drawer-hint">{t('platform.ads.creatives.description')}</p>
          {current.creatives.length === 0 ? (
            <EmptyState message={t('platform.ads.creatives.empty')} next={{ label: t('platform.ads.creatives.createFirst'), onClick: addCreative }} />
          ) : (
            <>
              {editBlocked.hint}
              <CreativesTable
                creatives={current.creatives}
                pending={pending}
                editBlockedBy={editBlocked.describedBy}
                onEdit={creative => open({ kind: 'creative', creativeId: creative.creativeId, form: formFromCreative(creative) })}
                onModerate={(creative, mode) => open({ kind: 'moderation', mode, creative })}
                onArchive={creative => open({ kind: 'archive', creative })}
              />
            </>
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
          onUploadImage={async file => {
            try {
              return (await client.uploadImage(file)).url;
            } catch (cause) {
              throw new Error(describeMediaError(cause, t));
            }
          }}
        />
      ) : null}
      {dialog?.kind === 'moderation' ? (
        <ModerationDialog
          key={`${dialog.creative.creativeId}:${dialog.mode}`}
          mode={dialog.mode}
          creative={dialog.creative}
          advertiserName={current.advertiserName}
          category={current.category}
          pending={pending}
          error={dialogError}
          onConfirm={decision => moderate(dialog.creative, dialog.mode === 'approve', decision)}
          onClose={close}
        />
      ) : null}
      {dialog?.kind === 'archive' ? (
        <ArchiveCreativeDialog
          key={dialog.creative.creativeId}
          creative={dialog.creative}
          advertiserName={current.advertiserName}
          lastOnAir={current.state === 'active' && approvedCount(current) === 1 && dialog.creative.moderation === AdModerationNames.Approved}
          pending={pending}
          error={dialogError}
          onConfirm={() => archive(dialog.creative)}
          onClose={close}
        />
      ) : null}
    </Page>
  );
}

function CampaignFacts({ campaign, advertiser }: { campaign: AdCampaignDto; advertiser: AdvertiserDto | null }) {
  const { t, formatDate } = useI18n();
  const permit = campaign.compliance?.permitNumber ?? null;
  const notices = cardNotices(campaign.compliance);
  return (
    <>
      <Fact label={t('platform.ads.campaign.field.category')}>{describeCategory(campaign.category, t)}</Fact>
      {permit !== null ? <Fact label={t('platform.ads.campaign.permit')}>{permit}</Fact> : null}
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
      {/* Что карточка скажет по закону сама — чтобы проверить до запуска, а не на экране клуба. */}
      <Fact label={t('platform.ads.campaign.cardNotices')}>
        {notices.length === 0 ? t('platform.ads.campaign.notice.none') : (
          <ul className="pc-ad-notices">
            {notices.map(notice => (
              <li key={notice}>
                {notice === 'seller' ? (
                  advertiser !== null && hasLegalDetails(advertiser)
                    ? t('platform.ads.campaign.notice.seller', { legalName: advertiser.legalName ?? '', taxId: advertiser.taxId ?? '', address: advertiser.address ?? '' })
                    : t('platform.ads.campaign.notice.sellerUnknown')
                ) : notice === 'certification'
                  ? t('platform.ads.campaign.notice.certification')
                  : t('platform.ads.campaign.notice.offer', { date: formatDate(campaign.endsAtUtc) })}
              </li>
            ))}
          </ul>
        )}
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

function CreativesTable({ creatives, pending, editBlockedBy, onEdit, onModerate, onArchive }: {
  creatives: readonly AdCreativeDto[];
  pending: boolean;
  /** id причины, почему «Изменить» у одобренного погашена. */
  editBlockedBy: string | undefined;
  onEdit: (creative: AdCreativeDto) => void;
  onModerate: (creative: AdCreativeDto, mode: 'approve' | 'reject') => void;
  onArchive: (creative: AdCreativeDto) => void;
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
          const archived = isArchived(creative);
          const actions = creativeActions(creative);
          const editable = canEditCreative(creative);
          return (
            <TableRow key={creative.creativeId} className={archived ? 'pc-ad-archived' : undefined}>
              <TableCell>
                <span className="pc-ad-creative">
                  <AdThumb key={creative.imageUrl ?? ''} url={creative.imageUrl} />
                  <span>
                    <AdCreativeText creative={creative} />
                  </span>
                </span>
              </TableCell>
              <TableCell>
                <span className="pc-ad-moderation">
                  {archived
                    ? <Badge variant="secondary">{t('platform.ads.creative.archived')}</Badge>
                    : <Badge variant={moderation.variant}>{moderation.label}</Badge>}
                  {!archived && creative.moderation === AdModerationNames.Rejected && creative.rejectedReason !== null ? (
                    <span className="mgmt-drawer-hint">{t('platform.ads.moderation.reasonShown', { reason: creative.rejectedReason })}</span>
                  ) : null}
                </span>
              </TableCell>
              <TableCell>
                {/* «Изменить» стоит последней — на одном месте во всех строках; у одобренного она погашена. */}
                <span className="pc-cell-actions">
                  {actions.includes('approve') ? (
                    <Button size="sm" disabled={pending} onClick={() => onModerate(creative, 'approve')}>{t('platform.ads.moderation.approve')}</Button>
                  ) : null}
                  {actions.includes('reject') ? (
                    <Button size="sm" variant="outline" disabled={pending} onClick={() => onModerate(creative, 'reject')}>{t('platform.ads.moderation.reject')}</Button>
                  ) : null}
                  {actions.includes('archive') ? (
                    <Button size="sm" variant="outline" disabled={pending} onClick={() => onArchive(creative)}>{t('platform.ads.creative.archive')}</Button>
                  ) : null}
                  {actions.includes('edit') ? (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={pending || !editable}
                      aria-describedby={editable ? undefined : editBlockedBy}
                      onClick={() => onEdit(creative)}
                    >
                      {t('platform.ads.edit')}
                    </Button>
                  ) : null}
                </span>
              </TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}
