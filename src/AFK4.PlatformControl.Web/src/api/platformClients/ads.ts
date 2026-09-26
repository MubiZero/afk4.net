import type { PlatformTransport } from '../platformTransport';
import type {
  AdCampaignDto,
  AdCampaignStateName,
  AdCreativeDto,
  AdImpressionRowDto,
  AdvertiserDto,
  ModerateAdCreativeRequest,
  UpsertAdCampaignRequest,
  UpsertAdCreativeRequest,
  UpsertAdvertiserRequest
} from '../types';

/**
 * Зеркало `AdRoutes` из контрактов: генератор переносит в TypeScript словари и записи, а
 * статические классы с адресами — нет. Совпадение держит тест, читающий C#.
 */
export const AD_ROUTES = {
  advertisers: '/api/platform/ads/advertisers',
  campaigns: '/api/platform/ads/campaigns',
  report: '/api/platform/ads/report'
} as const;

export interface AdReportQuery {
  /** День по UTC, `2026-09-25`. */
  from: string;
  to: string;
  /** Пусто — все кампании. */
  campaignId: string | null;
}

/**
 * Реклама платформы в витрине свободного ПК. Удаления нет ни у чего: у показов есть креатив, у
 * креатива — кампания, отчёт за прошлый месяц не должен терять строки. Ненужную кампанию ставят на паузу или возвращают в черновик,
 * ненужный креатив снимают с показа.
 */
export class AdsApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public listAdvertisers(): Promise<AdvertiserDto[]> {
    return this.transport.send<AdvertiserDto[]>('GET', AD_ROUTES.advertisers);
  }

  public createAdvertiser(request: UpsertAdvertiserRequest): Promise<AdvertiserDto> {
    return this.transport.send<AdvertiserDto>('POST', AD_ROUTES.advertisers, request);
  }

  public updateAdvertiser(advertiserId: string, request: UpsertAdvertiserRequest): Promise<AdvertiserDto> {
    return this.transport.send<AdvertiserDto>('PUT', `${AD_ROUTES.advertisers}/${encodeURIComponent(advertiserId)}`, request);
  }

  public listCampaigns(): Promise<AdCampaignDto[]> {
    return this.transport.send<AdCampaignDto[]>('GET', AD_ROUTES.campaigns);
  }

  public createCampaign(request: UpsertAdCampaignRequest): Promise<AdCampaignDto> {
    return this.transport.send<AdCampaignDto>('POST', AD_ROUTES.campaigns, request);
  }

  public updateCampaign(campaignId: string, request: UpsertAdCampaignRequest): Promise<AdCampaignDto> {
    return this.transport.send<AdCampaignDto>('PUT', campaignPath(campaignId), request);
  }

  public setCampaignState(campaignId: string, state: AdCampaignStateName): Promise<AdCampaignDto> {
    return this.transport.send<AdCampaignDto>('POST', `${campaignPath(campaignId)}/state`, { state });
  }

  public createCreative(campaignId: string, request: UpsertAdCreativeRequest): Promise<AdCreativeDto> {
    return this.transport.send<AdCreativeDto>('POST', `${campaignPath(campaignId)}/creatives`, request);
  }

  /**
   * Правка возвращает креатив на модерацию: одобряли другой текст и другую картинку. Одобренный
   * не правится вовсе (409 `ad_creative_locked`): его могли показать, а показанное хранят как было.
   */
  public updateCreative(campaignId: string, creativeId: string, request: UpsertAdCreativeRequest): Promise<AdCreativeDto> {
    return this.transport.send<AdCreativeDto>('PUT', creativePath(campaignId, creativeId), request);
  }

  public moderateCreative(campaignId: string, creativeId: string, request: ModerateAdCreativeRequest): Promise<AdCreativeDto> {
    return this.transport.send<AdCreativeDto>('POST', `${creativePath(campaignId, creativeId)}/moderation`, request);
  }

  /** Снять с показа. Обратного действия нет: чтобы показывать другое, добавляют новый креатив. */
  public archiveCreative(campaignId: string, creativeId: string): Promise<AdCreativeDto> {
    return this.transport.send<AdCreativeDto>('POST', `${creativePath(campaignId, creativeId)}/archive`);
  }

  public report(query: AdReportQuery): Promise<AdImpressionRowDto[]> {
    const params = new URLSearchParams({ from: query.from, to: query.to });
    if (query.campaignId !== null && query.campaignId !== '') params.set('campaignId', query.campaignId);
    return this.transport.send<AdImpressionRowDto[]>('GET', `${AD_ROUTES.report}?${params}`);
  }
}

function campaignPath(campaignId: string): string {
  return `${AD_ROUTES.campaigns}/${encodeURIComponent(campaignId)}`;
}

function creativePath(campaignId: string, creativeId: string): string {
  return `${campaignPath(campaignId)}/creatives/${encodeURIComponent(creativeId)}`;
}
