import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { AD_ROUTES, AdsApi } from './ads';
import type { PlatformTransport } from '../platformTransport';

interface Call { method: string; path: string; body?: unknown }

function recordingApi(): { api: AdsApi; calls: Call[] } {
  const calls: Call[] = [];
  const transport = {
    send: async (method: string, path: string, body?: unknown) => {
      calls.push({ method, path, body });
      return [] as unknown;
    }
  } as unknown as PlatformTransport;
  return { api: new AdsApi(transport), calls };
}

const CAMPAIGN = '11111111-1111-1111-1111-111111111111';
const CREATIVE = '22222222-2222-2222-2222-222222222222';

describe('AdsApi', () => {
  it('ходит по адресам кабинета рекламы', async () => {
    const { api, calls } = recordingApi();
    const creative = { title: 'Тахфиф ба ноутбукҳо', body: null, imageUrl: null, titleRu: 'Скидка на ноутбуки', bodyRu: null };
    const advertiser = { name: 'Техномир', contact: null, legalName: 'ООО «Техномир»', taxId: '123456789', address: 'Душанбе, пр. Рудаки, 1' };
    const confirmed = ['not_club_or_betting', 'no_banned_goods', 'minors', 'truthful', 'ethical', 'tajik_on_image'];

    await api.updateAdvertiser('a-1', advertiser);
    await api.setCampaignState(CAMPAIGN, 'active');
    await api.createCreative(CAMPAIGN, creative);
    await api.updateCreative(CAMPAIGN, CREATIVE, creative);
    await api.moderateCreative(CAMPAIGN, CREATIVE, { approve: true, reason: null, confirmed });
    await api.archiveCreative(CAMPAIGN, CREATIVE);

    expect(calls).toEqual([
      { method: 'PUT', path: '/api/platform/ads/advertisers/a-1', body: advertiser },
      { method: 'POST', path: `/api/platform/ads/campaigns/${CAMPAIGN}/state`, body: { state: 'active' } },
      { method: 'POST', path: `/api/platform/ads/campaigns/${CAMPAIGN}/creatives`, body: creative },
      { method: 'PUT', path: `/api/platform/ads/campaigns/${CAMPAIGN}/creatives/${CREATIVE}`, body: creative },
      {
        method: 'POST',
        path: `/api/platform/ads/campaigns/${CAMPAIGN}/creatives/${CREATIVE}/moderation`,
        body: { approve: true, reason: null, confirmed }
      },
      // Снять с показа — без тела: решение одно, спрашивать нечего.
      { method: 'POST', path: `/api/platform/ads/campaigns/${CAMPAIGN}/creatives/${CREATIVE}/archive`, body: undefined }
    ]);
  });

  it('отчёт: период днями, кампания — только если выбрана', async () => {
    const { api, calls } = recordingApi();

    await api.report({ from: '2026-09-01', to: '2026-09-30', campaignId: null });
    await api.report({ from: '2026-09-01', to: '2026-09-30', campaignId: CAMPAIGN });

    expect(calls.map(call => call.path)).toEqual([
      '/api/platform/ads/report?from=2026-09-01&to=2026-09-30',
      `/api/platform/ads/report?from=2026-09-01&to=2026-09-30&campaignId=${CAMPAIGN}`
    ]);
  });

  // Генератор контрактов не переносит статические классы с адресами, поэтому здесь их зеркало.
  it('адреса — те же, что AdRoutes', () => {
    const source = readFileSync(
      join(import.meta.dir, '..', '..', '..', '..', 'AFK4.Shared.Contracts', 'Ads', 'AdContracts.cs'), 'utf8');
    const route = (name: string) => {
      const match = source.match(new RegExp(`public const string ${name}\\s*=\\s*"([^"]+)";`));
      expect(match).not.toBeNull();
      return match![1];
    };
    expect({ ...AD_ROUTES } as Record<string, string>).toEqual({
      advertisers: route('Advertisers'),
      campaigns: route('Campaigns'),
      report: route('Report')
    });
  });
});
