import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { AdCategoryNames } from '@afk4/contracts';
import { PlatformApiError } from '@/api/platformTransport';
import type { AdCampaignDto, AdCreativeDto } from '@/api/types';
import { toLocalInput } from '@/lib/localDateTime';
import {
  AD_LIMITS,
  campaignPhase,
  defaultReportRange,
  describeAdError,
  describeCategory,
  emptyCampaignForm,
  formFromCampaign,
  parseCities,
  parseOrganizationIds,
  reportTotals,
  requestFromAdvertiserForm,
  requestFromCampaignForm,
  requestFromCreativeForm,
  stateActions,
  validateAdvertiserForm,
  validateCampaignForm,
  validateCreativeForm,
  validateReportRange,
  withCreative,
  type CampaignForm
} from './adsModel';

const t = (key: string) => key;

const ORG_A = '11111111-1111-1111-1111-111111111111';
const ORG_B = '22222222-2222-2222-2222-222222222222';

function creative(overrides: Partial<AdCreativeDto> = {}): AdCreativeDto {
  return {
    creativeId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
    campaignId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    title: 'Скидка на ноутбуки',
    body: null,
    imageUrl: null,
    moderation: 'approved',
    rejectedReason: null,
    moderatedAtUtc: '2026-09-20T10:00:00Z',
    createdAtUtc: '2026-09-19T10:00:00Z',
    ...overrides
  };
}

function campaign(overrides: Partial<AdCampaignDto> = {}): AdCampaignDto {
  return {
    campaignId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    advertiserId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    advertiserName: 'Техномир',
    name: 'Осень',
    category: 'electronics',
    startsAtUtc: '2026-09-01T00:00:00Z',
    endsAtUtc: '2026-10-01T00:00:00Z',
    cities: [],
    organizationIds: [],
    state: 'active',
    creatives: [creative()],
    createdAtUtc: '2026-08-30T00:00:00Z',
    updatedAtUtc: '2026-08-30T00:00:00Z',
    ...overrides
  };
}

function campaignForm(overrides: Partial<CampaignForm> = {}): CampaignForm {
  return {
    advertiserId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    name: 'Осень',
    category: 'electronics',
    startsAt: '2026-09-01T09:00',
    endsAt: '2026-09-30T21:00',
    cities: '',
    organizationIds: '',
    ...overrides
  };
}

const repoSource = (...path: string[]) =>
  readFileSync(join(import.meta.dir, '..', '..', '..', '..', '..', 'src', ...path), 'utf8');

describe('где кампания на самом деле', () => {
  const inside = new Date('2026-09-15T12:00:00Z');

  it('черновик и пауза — как есть, в любые даты', () => {
    expect(campaignPhase(campaign({ state: 'draft' }), inside)).toBe('draft');
    expect(campaignPhase(campaign({ state: 'paused' }), inside)).toBe('paused');
  });

  it('запущенная идёт только в своих датах', () => {
    expect(campaignPhase(campaign(), inside)).toBe('running');
    expect(campaignPhase(campaign(), new Date('2026-08-31T23:59:00Z'))).toBe('scheduled');
    // Конец — не включительно: сервер берёт кампанию, пока EndsAtUtc > сейчас.
    expect(campaignPhase(campaign(), new Date('2026-10-01T00:00:00Z'))).toBe('ended');
  });

  // Правка креатива снимает одобрение: кампания «запущена», а на ПК её нет. Список обязан это сказать.
  it('запущенная без одобренного креатива — «нечего показывать»', () => {
    expect(campaignPhase(campaign({ creatives: [creative({ moderation: 'pending' })] }), inside)).toBe('nothingToShow');
    expect(campaignPhase(campaign({ creatives: [] }), inside)).toBe('nothingToShow');
  });

  it('неизвестное состояние с сервера не выдаёт себя за известное', () => {
    expect(campaignPhase(campaign({ state: 'archived' as AdCampaignDto['state'] }), inside)).toBe('unknown');
  });

  it('из черновика можно только запустить, из запущенной — на паузу или в черновик', () => {
    expect(stateActions('draft')).toEqual(['active']);
    expect(stateActions('active')).toEqual(['paused', 'draft']);
    expect(stateActions('paused')).toEqual(['active', 'draft']);
    expect(stateActions('archived')).toEqual([]);
  });

  it('креатив из ответа встаёт на своё место, новый — в конец', () => {
    const edited = creative({ moderation: 'pending', title: 'Новая скидка' });
    expect(withCreative(campaign(), edited).creatives).toEqual([edited]);
    const added = creative({ creativeId: 'dddddddd-dddd-dddd-dddd-dddddddddddd' });
    expect(withCreative(campaign(), added).creatives.map(item => item.creativeId))
      .toEqual(['cccccccc-cccc-cccc-cccc-cccccccccccc', 'dddddddd-dddd-dddd-dddd-dddddddddddd']);
  });
});

describe('категории', () => {
  it('у каждой категории контракта есть подпись', () => {
    for (const category of Object.values(AdCategoryNames)) {
      expect(describeCategory(category, t)).toBe(`platform.ads.category.${category}`);
    }
  });

  it('категорию новее экрана показывает машинным именем', () => {
    expect(describeCategory('crypto', t)).toBe('crypto');
  });
});

describe('форма кампании', () => {
  it('пропускает правильную кампанию', () => {
    expect(validateCampaignForm(campaignForm())).toEqual({});
  });

  it('требует рекламодателя, название и даты', () => {
    const errors = validateCampaignForm(campaignForm({ advertiserId: '', name: '  ', startsAt: '', endsAt: '' }));
    expect(errors.advertiserId?.key).toBe('platform.ads.error.advertiserRequired');
    expect(errors.name?.key).toBe('platform.ads.error.nameRequired');
    expect(errors.startsAt?.key).toBe('platform.ads.error.dateRequired');
    expect(errors.endsAt?.key).toBe('platform.ads.error.dateRequired');
  });

  it('конец — строго позже начала', () => {
    expect(validateCampaignForm(campaignForm({ endsAt: '2026-09-01T09:00' })).endsAt?.key).toBe('platform.ads.error.endsBeforeStart');
    expect(validateCampaignForm(campaignForm({ endsAt: '2026-08-01T09:00' })).endsAt?.key).toBe('platform.ads.error.endsBeforeStart');
  });

  it('города — через запятую, без пустых и без повторов с другим регистром', () => {
    expect(parseCities(' Душанбе, худжанд ,,Душанбе\nДУШАНБЕ, Худжанд ')).toEqual(['Душанбе', 'худжанд']);
    expect(parseCities('')).toEqual([]);
    const tooMany = Array.from({ length: AD_LIMITS.maxCities + 1 }, (_, index) => `Город ${index}`).join(', ');
    expect(validateCampaignForm(campaignForm({ cities: tooMany })).cities)
      .toEqual({ key: 'platform.ads.error.tooManyCities', values: { max: AD_LIMITS.maxCities } });
  });

  it('клубы — идентификаторы организаций; опечатку называет, а не отправляет', () => {
    expect(parseOrganizationIds(`${ORG_A}\n${ORG_B.toUpperCase()}, ${ORG_A}`)).toEqual({ ids: [ORG_A, ORG_B], invalid: [] });
    expect(parseOrganizationIds('club-7').invalid).toEqual(['club-7']);
    expect(validateCampaignForm(campaignForm({ organizationIds: `${ORG_A}\nclub-7` })).organizationIds?.key)
      .toBe('platform.ads.error.organizationId');
  });

  it('запрос: даты в UTC, пустые города и клубы — пустые списки («все»)', () => {
    const form = campaignForm({ name: '  Осень ', cities: 'Душанбе', organizationIds: ORG_A });
    expect(requestFromCampaignForm(form)).toEqual({
      advertiserId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      name: 'Осень',
      category: 'electronics',
      startsAtUtc: new Date('2026-09-01T09:00').toISOString(),
      endsAtUtc: new Date('2026-09-30T21:00').toISOString(),
      cities: ['Душанбе'],
      organizationIds: [ORG_A]
    });
    expect(requestFromCampaignForm(campaignForm()).cities).toEqual([]);
    expect(requestFromCampaignForm(campaignForm()).organizationIds).toEqual([]);
  });

  it('форма правки повторяет кампанию', () => {
    const source = campaign({ cities: ['Душанбе', 'Худжанд'], organizationIds: [ORG_A, ORG_B] });
    const form = formFromCampaign(source);
    expect(form.startsAt).toBe(toLocalInput(source.startsAtUtc));
    expect(requestFromCampaignForm(form)).toEqual({
      advertiserId: source.advertiserId,
      name: source.name,
      category: source.category,
      startsAtUtc: new Date(source.startsAtUtc).toISOString(),
      endsAtUtc: new Date(source.endsAtUtc).toISOString(),
      cities: source.cities,
      organizationIds: source.organizationIds
    });
  });

  it('новая кампания — на месяц вперёд, категория «другое»', () => {
    const now = new Date('2026-09-25T10:00:00Z');
    const form = emptyCampaignForm(now, '');
    expect(form.category).toBe('other');
    expect(new Date(form.endsAt).getTime() - new Date(form.startsAt).getTime()).toBe(30 * 24 * 60 * 60 * 1000);
    expect(validateCampaignForm({ ...form, advertiserId: 'x', name: 'Осень' })).toEqual({});
  });
});

describe('рекламодатель и креатив', () => {
  it('рекламодатель: название обязательно, пустой контакт уходит как null', () => {
    expect(validateAdvertiserForm({ name: '', contact: '' }).name?.key).toBe('platform.ads.error.nameRequired');
    expect(validateAdvertiserForm({ name: 'Техномир', contact: 'x'.repeat(AD_LIMITS.contactMax + 1) }).contact?.key)
      .toBe('platform.ads.error.contactTooLong');
    expect(requestFromAdvertiserForm({ name: ' Техномир ', contact: '  ' })).toEqual({ name: 'Техномир', contact: null });
  });

  it('креатив: заголовок до 120, текст до 280, картинка только https', () => {
    expect(validateCreativeForm({ title: 'Скидка', body: '', imageUrl: 'https://cdn.example.com/a.jpg' })).toEqual({});
    expect(validateCreativeForm({ title: '', body: '', imageUrl: '' }).title?.key).toBe('platform.ads.error.titleRequired');
    expect(validateCreativeForm({ title: 'x'.repeat(AD_LIMITS.titleMax + 1), body: '', imageUrl: '' }).title)
      .toEqual({ key: 'platform.ads.error.titleTooLong', values: { max: AD_LIMITS.titleMax } });
    expect(validateCreativeForm({ title: 'Скидка', body: 'x'.repeat(AD_LIMITS.bodyMax + 1), imageUrl: '' }).body?.key)
      .toBe('platform.ads.error.bodyTooLong');
    // ПК качают картинки только по https: http-адрес экран не показал бы никогда.
    expect(validateCreativeForm({ title: 'Скидка', body: '', imageUrl: 'http://cdn.example.com/a.jpg' }).imageUrl?.key)
      .toBe('platform.ads.error.imageUrl');
    expect(requestFromCreativeForm({ title: ' Скидка ', body: ' ', imageUrl: '' })).toEqual({ title: 'Скидка', body: null, imageUrl: null });
  });
});

describe('отчёт', () => {
  it('по умолчанию — последние 30 дней по UTC, сегодня включительно', () => {
    // В Душанбе уже 26-е, по UTC ещё 25-е: отчёт спрашивает днями UTC, как сервер их и складывает.
    expect(defaultReportRange(new Date('2026-09-25T20:00:00Z'))).toEqual({ from: '2026-08-27', to: '2026-09-25' });
  });

  it('период проверяется так же, как на сервере', () => {
    expect(validateReportRange('2026-09-01', '2026-09-30')).toBeNull();
    expect(validateReportRange('', '2026-09-30')?.key).toBe('platform.ads.report.error.dates');
    expect(validateReportRange('2026-09-30', '2026-09-01')?.key).toBe('platform.ads.report.error.order');
    expect(validateReportRange('2026-06-01', '2026-09-01')).toBeNull();
    expect(validateReportRange('2026-06-01', '2026-09-02')).toEqual({ key: 'platform.ads.report.error.tooLong', values: { max: 92 } });
  });

  it('итог — сумма строк', () => {
    const row = { day: '2026-09-25', campaignId: '', campaignName: '', creativeId: '', creativeTitle: '', organizationId: '',
      organizationName: '', branchId: '', branchName: '', city: '', impressions: 10, shownSeconds: 90 };
    expect(reportTotals([row, { ...row, impressions: 5, shownSeconds: 45 }])).toEqual({ impressions: 15, shownSeconds: 135 });
    expect(reportTotals([])).toEqual({ impressions: 0, shownSeconds: 0 });
  });
});

describe('отказ сервера', () => {
  it('каждый код рекламы называет своей фразой, а не английским текстом сервера', () => {
    expect(describeAdError(new PlatformApiError(400, 'Unknown category.', 'ad_invalid'), t)).toBe('platform.ads.error.invalid');
    expect(describeAdError(new PlatformApiError(409, 'The campaign has no approved creative.', 'ad_campaign_without_approved_creative'), t))
      .toBe('platform.ads.error.notApproved');
    expect(describeAdError(new PlatformApiError(400, 'Confirm…', 'ad_moderation_confirmation_required'), t))
      .toBe('platform.ads.error.confirmationRequired');
  });

  it('404 — запись, которой уже нет; нехватку прав отдаёт общему разбору', () => {
    expect(describeAdError(new PlatformApiError(404, 'Not Found', null), t)).toBe('platform.ads.error.notFound');
    expect(describeAdError(new PlatformApiError(403, 'Forbidden', null), t)).toBe('state.error.forbidden');
  });
});

// Форма проверяет то же, что сервер, чтобы назвать поле до отправки. Разойдись правила — форма
// либо пропустит то, что сервер отвергнет, либо не даст сохранить то, что он принял бы.
describe('совпадение с сервером', () => {
  it('пределы — те же, что AdLimits', () => {
    const source = repoSource('AFK4.Shared.Contracts', 'Ads', 'AdContracts.cs');
    const limit = (name: string) => {
      const match = source.match(new RegExp(`public const int ${name}\\s*=\\s*([\\d_]+);`));
      expect(match).not.toBeNull();
      return Number(match![1].replace(/_/g, ''));
    };
    expect({ ...AD_LIMITS } as Record<string, number>).toEqual({
      nameMax: limit('NameMax'),
      contactMax: limit('ContactMax'),
      titleMax: limit('TitleMax'),
      bodyMax: limit('BodyMax'),
      imageUrlMax: limit('ImageUrlMax'),
      maxCities: limit('MaxCities'),
      maxOrganizations: limit('MaxOrganizations'),
      reasonMax: limit('ReasonMax'),
      reportMaxDays: limit('ReportMaxDays')
    });
  });
});
