import { useMemo, useState } from 'react';
import type { JSX } from 'react';
import { Megaphone } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { formatDateParts } from '@afk4/formatting';
import { AdCategoryNames, type ClubAdDto, type ClubAdsDto } from '@afk4/contracts';
import { ManagementScreen } from '../../management/ManagementScreen';
import { EmptyState } from '../../operatorPrimitives';
import { createAuthenticatedOperatorClients } from '../../operatorHelpers';
import { projectOperatorError } from '../../apiErrors';
import type { OperatorBackendContext } from '../../operatorTypes';
import { SkeletonTiles } from '../../LoadingSkeleton';
import { useSection } from '../useSection';
import { ReportAdModal, type ReportAdClient } from './ReportAdModal';

export interface ClubAdsClient extends ReportAdClient {
  listPlatformAds(): Promise<ClubAdsDto>;
}

const CATEGORY_KEY: Record<string, MessageKey> = {
  [AdCategoryNames.Food]: 'op.ads.category.food',
  [AdCategoryNames.Electronics]: 'op.ads.category.electronics',
  [AdCategoryNames.Games]: 'op.ads.category.games',
  [AdCategoryNames.Education]: 'op.ads.category.education',
  [AdCategoryNames.Services]: 'op.ads.category.services',
  [AdCategoryNames.Telecom]: 'op.ads.category.telecom',
  [AdCategoryNames.HealthBeauty]: 'op.ads.category.health_beauty',
  [AdCategoryNames.Finance]: 'op.ads.category.finance',
  [AdCategoryNames.Social]: 'op.ads.category.social',
  [AdCategoryNames.Other]: 'op.ads.category.other'
};

/**
 * «Сеть → Реклама» (спека рекламы, §8.4): клуб по закону тоже распространитель рекламы и отвечает
 * за время и место показа, поэтому видит, какая реклама платформы идёт и шла на его ПК, — тем же
 * видом, что игрок: таджикский первым, пометки закона внизу. Выбирать и снимать рекламу клуб не
 * может: содержание проверяет AFK4.
 */
export function AdsDestination({
  backend,
  client: injectedClient
}: {
  backend: OperatorBackendContext | null;
  client?: ClubAdsClient;
}): JSX.Element {
  const { t } = useI18n();
  const client = useMemo<ClubAdsClient | null>(() => {
    if (injectedClient !== undefined) return injectedClient;
    if (backend === null) return null;
    return createAuthenticatedOperatorClients(backend.config, backend.session).orgBilling;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [injectedClient, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const ads = useSection<ClubAdsDto>(
    () => (client ?? { listPlatformAds: async () => { throw new Error('no backend'); } }).listPlatformAds(),
    backend?.session.organizationId ?? (injectedClient ? 'test' : '')
  );
  const state = ads.status === 'ready' ? 'ready' : ads.status === 'error' ? 'error' : 'loading';
  const [reporting, setReporting] = useState<ClubAdDto | null>(null);
  const [reported, setReported] = useState(false);

  return (
    <ManagementScreen
      title={t('op.network.dest.ads')}
      subtitle={t('op.network.dest.ads.subtitle')}
      contentWidth="full"
      state={state}
      skeleton={<SkeletonTiles count={3} className="network-ads-grid" tileClassName="network-ad" />}
      failure={ads.status === 'error' ? projectOperatorError(ads.error, t) : undefined}
      onRetry={ads.status === 'error' ? ads.retry : undefined}
    >
      {reported ? <p className="network-ads-reported" role="status">{t('op.ads.report.sent')}</p> : null}
      {ads.status === 'ready' ? <AdsList data={ads.data} onReport={client ? setReporting : undefined} /> : null}
      {reporting && client ? (
        <ReportAdModal
          ad={reporting}
          client={client}
          onReported={(next) => {
            if (ads.status === 'ready') ads.apply(next);
            setReporting(null);
            setReported(true);
          }}
          onClose={() => setReporting(null)}
        />
      ) : null}
    </ManagementScreen>
  );
}

function AdsList({ data, onReport }: { data: ClubAdsDto; onReport?: (ad: ClubAdDto) => void }) {
  const { t } = useI18n();
  const icon = <Megaphone size={20} aria-hidden="true" />;

  if (data.ads.length === 0) {
    return data.adsEnabled
      ? <EmptyState icon={icon} title={t('op.ads.empty.none.title')} next={{ kind: 'calm', hint: t('op.ads.empty.none.hint') }} />
      : <EmptyState icon={icon} title={t('op.ads.empty.disabled.title')} next={{ kind: 'calm', hint: t('op.ads.empty.disabled.hint') }} />;
  }

  return (
    <>
      <p className="network-ads-lead">{t('op.ads.lead')}</p>
      {!data.adsEnabled ? <p className="network-ads-lead">{t('op.ads.disabledNow')}</p> : null}
      <ul className="network-ads-grid">
        {data.ads.map((ad) => <AdCard key={ad.creativeId} ad={ad} onReport={onReport} />)}
      </ul>
    </>
  );
}

function AdCard({ ad, onReport }: { ad: ClubAdDto; onReport?: (ad: ClubAdDto) => void }) {
  const { t, locale } = useI18n();
  const day = (iso: string) => formatDateParts(iso, locale, { day: 'numeric', month: 'long', year: 'numeric' });
  const legal = [
    ad.seller ? t('op.ads.seller', { ...ad.seller }) : null,
    ad.requiresCertification ? t('op.ads.certification') : null,
    ad.offerUntilUtc ? t('op.ads.offerUntil', { date: day(ad.offerUntilUtc) }) : null
  ].filter((line): line is string => line !== null);
  const categoryKey = CATEGORY_KEY[ad.category];

  return (
    <li className={`network-ad${ad.running ? ' is-running' : ''}`}>
      {ad.imageUrl ? <img className="network-ad-image" src={ad.imageUrl} alt="" loading="lazy" /> : null}
      <div className="network-ad-body">
        <div className="network-ad-head">
          <span className="network-ad-label">{t('op.ads.label', { advertiser: ad.advertiser })}</span>
          <span className={`ui-chip ui-chip--status ${ad.running ? 'is-live' : 'is-neutral'}`}>
            {t(ad.running ? 'op.ads.running' : 'op.ads.stopped')}
          </span>
        </div>
        {/* Таджикский — первым, как на экране ПК (закон о рекламе, ст. 5); русский ниже и тише. */}
        <strong className="network-ad-title" lang="tg">{ad.title}</strong>
        {ad.body ? <p className="network-ad-text" lang="tg">{ad.body}</p> : null}
        {ad.titleRu || ad.bodyRu ? (
          <p className="network-ad-ru" lang="ru">{[ad.titleRu, ad.bodyRu].filter(Boolean).join(' — ')}</p>
        ) : null}
        <dl className="network-ad-facts">
          <div><dt>{t('op.ads.fact.period')}</dt><dd>{`${day(ad.startsAtUtc)} — ${day(ad.endsAtUtc)}`}</dd></div>
          {categoryKey ? <div><dt>{t('op.ads.fact.category')}</dt><dd>{t(categoryKey)}</dd></div> : null}
          <div>
            <dt>{t('op.ads.fact.shown')}</dt>
            <dd>
              {ad.impressions > 0
                ? t('op.ads.shown', { count: ad.impressions, minutes: Math.round(ad.shownSeconds / 60) })
                : t('op.ads.notShown')}
            </dd>
          </div>
          {ad.lastShownDay ? <div><dt>{t('op.ads.fact.lastShown')}</dt><dd>{day(`${ad.lastShownDay}T12:00:00Z`)}</dd></div> : null}
        </dl>
        {legal.length > 0 ? <p className="network-ad-legal">{legal.join(' · ')}</p> : null}
        {ad.complaintAnswer && !ad.complaintOpen ? (
          <p className="network-ad-answer">{t('op.ads.report.answer', { answer: ad.complaintAnswer })}</p>
        ) : null}
        {onReport ? (
          <div className="network-ad-actions">
            {ad.complaintOpen ? (
              <span className="network-ad-complained">{t('op.ads.report.open')}</span>
            ) : (
              <button type="button" className="ui-btn ui-btn--sm" onClick={() => onReport(ad)}>{t('op.ads.report.button')}</button>
            )}
          </div>
        ) : null}
      </div>
    </li>
  );
}
