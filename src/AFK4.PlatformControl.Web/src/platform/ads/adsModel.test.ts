import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { AdCategoryNames, AdModerationCheckNames } from '@afk4/contracts';
import { PlatformApiError } from '@/api/platformTransport';
import type { AdCampaignDto, AdCreativeDto, AdvertiserDto } from '@/api/types';
import { toLocalInput } from '@/lib/localDateTime';
import {
  AD_LIMITS,
  AD_MODERATION_CHECKS,
  approvedCount,
  campaignPhase,
  canEditCreative,
  cardNotices,
  categoryNoteKey,
  creativeActions,
  defaultReportRange,
  describeAdError,
  describeCategory,
  emptyCampaignForm,
  formatWordingFlags,
  formFromAdvertiser,
  formFromCampaign,
  formFromCreative,
  hasLegalDetails,
  moderationCheckLabelKey,
  parseCities,
  parseOrganizationIds,
  pendingCount,
  reportTotals,
  requestFromAdvertiserForm,
  requestFromCampaignForm,
  requestFromCreativeForm,
  showsPermitField,
  stateActions,
  validateAdvertiserForm,
  validateCampaignForm,
  validateCreativeForm,
  validateReportRange,
  withCreative,
  type AdvertiserForm,
  type CampaignForm,
  type CreativeForm,
  type FieldError
} from './adsModel';

const t = (key: string, values?: Record<string, string | number>) => (values === undefined ? key : `${key} ${JSON.stringify(values)}`);

const ORG_A = '11111111-1111-1111-1111-111111111111';
const ORG_B = '22222222-2222-2222-2222-222222222222';

function creative(overrides: Partial<AdCreativeDto> = {}): AdCreativeDto {
  return {
    creativeId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
    campaignId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    title: 'Тахфиф ба ноутбукҳо',
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
    permitNumber: '',
    distanceSelling: false,
    requiresCertification: false,
    containsOffer: false,
    ...overrides
  };
}

function advertiserForm(overrides: Partial<AdvertiserForm> = {}): AdvertiserForm {
  return {
    name: 'Техномир',
    legalName: 'ООО «Техномир»',
    taxId: '123456789',
    address: 'Душанбе, пр. Рудаки, 1',
    contact: '',
    ...overrides
  };
}

function creativeForm(overrides: Partial<CreativeForm> = {}): CreativeForm {
  return { title: 'Тахфиф', body: '', titleRu: '', bodyRu: '', imageUrl: '', ...overrides };
}

const repoSource = (...path: string[]) =>
  readFileSync(join(import.meta.dir, '..', '..', '..', '..', '..', 'src', ...path), 'utf8');

const NO_COMPLIANCE = { permitNumber: null, distanceSelling: false, requiresCertification: false, containsOffer: false };

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

  it('запущенная без одобренного креатива — «нечего показывать»', () => {
    expect(campaignPhase(campaign({ creatives: [creative({ moderation: 'pending' })] }), inside)).toBe('nothingToShow');
    expect(campaignPhase(campaign({ creatives: [] }), inside)).toBe('nothingToShow');
  });

  // Снятый с показа одобренный креатив сервер ПК не отдаёт: кампания «запущена», а показывать нечего.
  it('снятый с показа креатив не считается ни одобренным, ни ждущим', () => {
    const archived = campaign({
      creatives: [
        creative({ archivedAtUtc: '2026-09-10T00:00:00Z' }),
        creative({ creativeId: 'dddddddd-dddd-dddd-dddd-dddddddddddd', moderation: 'pending', archivedAtUtc: '2026-09-10T00:00:00Z' })
      ]
    });
    expect(approvedCount(archived)).toBe(0);
    expect(pendingCount(archived)).toBe(0);
    expect(campaignPhase(archived, inside)).toBe('nothingToShow');
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
    const archived = creative({ archivedAtUtc: '2026-09-20T12:00:00Z' });
    expect(withCreative(campaign(), archived).creatives).toEqual([archived]);
    const added = creative({ creativeId: 'dddddddd-dddd-dddd-dddd-dddddddddddd' });
    expect(withCreative(campaign(), added).creatives.map(item => item.creativeId))
      .toEqual(['cccccccc-cccc-cccc-cccc-cccccccccccc', 'dddddddd-dddd-dddd-dddd-dddddddddddd']);
  });
});

// Показанную рекламу закон велит хранить год как была (ст. 22): одобренный креатив не правят и
// не отклоняют задним числом, а снимают с показа.
describe('что можно сделать с креативом', () => {
  it('ждущий — одобрить, отклонить, изменить', () => {
    expect(creativeActions(creative({ moderation: 'pending' }))).toEqual(['approve', 'reject', 'edit']);
    expect(canEditCreative(creative({ moderation: 'pending' }))).toBe(true);
  });

  it('отклонённый — одобрить или изменить', () => {
    expect(creativeActions(creative({ moderation: 'rejected' }))).toEqual(['approve', 'edit']);
    expect(canEditCreative(creative({ moderation: 'rejected' }))).toBe(true);
  });

  it('одобренный — только снять с показа; «Изменить» стоит, но погашена', () => {
    expect(creativeActions(creative())).toEqual(['archive', 'edit']);
    expect(canEditCreative(creative())).toBe(false);
  });

  it('снятый с показа — ничего', () => {
    const archived = creative({ archivedAtUtc: '2026-09-20T12:00:00Z' });
    expect(creativeActions(archived)).toEqual([]);
    expect(canEditCreative(archived)).toBe(false);
    expect(creativeActions({ ...archived, moderation: 'pending' })).toEqual([]);
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

  it('у здоровья, финансов и социальной рекламы форма называет условие закона', () => {
    expect(categoryNoteKey('health_beauty')).toBe('platform.ads.campaign.categoryNote.health_beauty');
    expect(categoryNoteKey('finance')).toBe('platform.ads.campaign.categoryNote.finance');
    expect(categoryNoteKey('social')).toBe('platform.ads.campaign.categoryNote.social');
    expect(categoryNoteKey('food')).toBeNull();
    expect(categoryNoteKey('crypto')).toBeNull();
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

  // Ст. 17: лекарства без рецепта, медтехника, БАД и косметика — только с разрешением Минздрава.
  it('«Здоровье и красота» без номера разрешения не проходит; у других категорий номер не нужен', () => {
    expect(validateCampaignForm(campaignForm({ category: 'health_beauty' })).permitNumber?.key).toBe('platform.ads.error.permitRequired');
    expect(validateCampaignForm(campaignForm({ category: 'health_beauty', permitNumber: '   ' })).permitNumber?.key)
      .toBe('platform.ads.error.permitRequired');
    expect(validateCampaignForm(campaignForm({ category: 'health_beauty', permitNumber: '№ 123/45' }))).toEqual({});
    expect(validateCampaignForm(campaignForm({ category: 'food' }))).toEqual({});
    expect(validateCampaignForm(campaignForm({ permitNumber: 'x'.repeat(AD_LIMITS.permitMax + 1) })).permitNumber)
      .toEqual({ key: 'platform.ads.error.permitTooLong', values: { max: AD_LIMITS.permitMax } });
  });

  it('поле разрешения видно у «Здоровья и красоты» и там, где номер уже вписан', () => {
    expect(showsPermitField(campaignForm({ category: 'health_beauty' }))).toBe(true);
    expect(showsPermitField(campaignForm({ category: 'food' }))).toBe(false);
    // Сменили категорию, а номер остался: скрыть — значит молча отправить его с «Едой».
    expect(showsPermitField(campaignForm({ category: 'food', permitNumber: '№ 123' }))).toBe(true);
  });

  it('запрос: даты в UTC, пустые города и клубы — пустые списки («все»), отметки закона — как есть', () => {
    const form = campaignForm({ name: '  Осень ', cities: 'Душанбе', organizationIds: ORG_A, containsOffer: true, permitNumber: '  ' });
    expect(requestFromCampaignForm(form)).toEqual({
      advertiserId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      name: 'Осень',
      category: 'electronics',
      startsAtUtc: new Date('2026-09-01T09:00').toISOString(),
      endsAtUtc: new Date('2026-09-30T21:00').toISOString(),
      cities: ['Душанбе'],
      organizationIds: [ORG_A],
      compliance: { ...NO_COMPLIANCE, containsOffer: true }
    });
    expect(requestFromCampaignForm(campaignForm()).cities).toEqual([]);
    expect(requestFromCampaignForm(campaignForm()).organizationIds).toEqual([]);
    expect(requestFromCampaignForm(campaignForm({ category: 'health_beauty', permitNumber: ' № 123/45 ' })).compliance?.permitNumber)
      .toBe('№ 123/45');
  });

  it('форма правки повторяет кампанию вместе с отметками закона', () => {
    const compliance = { permitNumber: '№ 7', distanceSelling: true, requiresCertification: true, containsOffer: false };
    const source = campaign({ category: 'health_beauty', cities: ['Душанбе', 'Худжанд'], organizationIds: [ORG_A, ORG_B], compliance });
    const form = formFromCampaign(source);
    expect(form.startsAt).toBe(toLocalInput(source.startsAtUtc));
    expect(requestFromCampaignForm(form)).toEqual({
      advertiserId: source.advertiserId,
      name: source.name,
      category: source.category,
      startsAtUtc: new Date(source.startsAtUtc).toISOString(),
      endsAtUtc: new Date(source.endsAtUtc).toISOString(),
      cities: source.cities,
      organizationIds: source.organizationIds,
      compliance
    });
    // Кампания, сохранённая до закона, приходит без отметок — форма считает их снятыми.
    expect(requestFromCampaignForm(formFromCampaign(campaign({ compliance: null }))).compliance).toEqual(NO_COMPLIANCE);
  });

  it('новая кампания — на месяц вперёд, категория «другое», без отметок', () => {
    const now = new Date('2026-09-25T10:00:00Z');
    const form = emptyCampaignForm(now, '');
    expect(form.category).toBe('other');
    expect(new Date(form.endsAt).getTime() - new Date(form.startsAt).getTime()).toBe(30 * 24 * 60 * 60 * 1000);
    expect(validateCampaignForm({ ...form, advertiserId: 'x', name: 'Осень' })).toEqual({});
    expect(requestFromCampaignForm(form).compliance).toEqual(NO_COMPLIANCE);
  });

  it('карточка допечатывает продавца, пометку о сертификации и срок предложения — по отметкам', () => {
    expect(cardNotices(null)).toEqual([]);
    expect(cardNotices({ distanceSelling: false })).toEqual([]);
    expect(cardNotices({ distanceSelling: true, requiresCertification: true, containsOffer: true })).toEqual(['seller', 'certification', 'offer']);
    expect(cardNotices({ containsOffer: true })).toEqual(['offer']);
  });
});

describe('рекламодатель', () => {
  it('пропускает рекламодателя с реквизитами; пустой контакт уходит как null', () => {
    expect(validateAdvertiserForm(advertiserForm())).toEqual({});
    expect(requestFromAdvertiserForm(advertiserForm({ name: ' Техномир ', legalName: ' ООО «Техномир» ', address: ' Душанбе ', contact: '  ' })))
      .toEqual({ name: 'Техномир', contact: null, legalName: 'ООО «Техномир»', taxId: '123456789', address: 'Душанбе' });
  });

  it('название обязательно, контакт — не длиннее предела', () => {
    expect(validateAdvertiserForm(advertiserForm({ name: '' })).name?.key).toBe('platform.ads.error.nameRequired');
    expect(validateAdvertiserForm(advertiserForm({ contact: 'x'.repeat(AD_LIMITS.contactMax + 1) })).contact?.key)
      .toBe('platform.ads.error.contactTooLong');
  });

  // Ст. 14(1): при продаже на расстоянии карточка печатает наименование, ИНН и адрес продавца.
  it('наименование, ИНН и адрес обязательны', () => {
    const errors = validateAdvertiserForm(advertiserForm({ legalName: ' ', taxId: '', address: '  ' }));
    expect(errors.legalName?.key).toBe('platform.ads.error.legalNameRequired');
    expect(errors.taxId?.key).toBe('platform.ads.error.taxIdRequired');
    expect(errors.address?.key).toBe('platform.ads.error.addressRequired');
  });

  it('наименование и адрес — не длиннее пределов сервера', () => {
    expect(validateAdvertiserForm(advertiserForm({ legalName: 'x'.repeat(AD_LIMITS.legalNameMax + 1) })).legalName)
      .toEqual({ key: 'platform.ads.error.legalNameTooLong', values: { max: AD_LIMITS.legalNameMax } });
    expect(validateAdvertiserForm(advertiserForm({ legalName: 'x'.repeat(AD_LIMITS.legalNameMax) })).legalName).toBeUndefined();
    expect(validateAdvertiserForm(advertiserForm({ address: 'x'.repeat(AD_LIMITS.addressMax + 1) })).address)
      .toEqual({ key: 'platform.ads.error.addressTooLong', values: { max: AD_LIMITS.addressMax } });
  });

  it('ИНН — от 9 до 14 цифр; пробелы разбивки убираются, буквы и чужие цифры — нет', () => {
    const taxIdError = (taxId: string) => validateAdvertiserForm(advertiserForm({ taxId })).taxId;
    const invalid: FieldError = { key: 'platform.ads.error.taxId', values: { min: AD_LIMITS.taxIdMinDigits, max: AD_LIMITS.taxIdMaxDigits } };
    expect(taxIdError('123456789')).toBeUndefined();
    expect(taxIdError('12345678901234')).toBeUndefined();
    expect(taxIdError('12345678')).toEqual(invalid);
    expect(taxIdError('123456789012345')).toEqual(invalid);
    expect(taxIdError('12345678A')).toEqual(invalid);
    // Восточноарабские цифры сервер (`char.IsAsciiDigit`) не примет.
    expect(taxIdError('١٢٣٤٥٦٧٨٩')).toEqual(invalid);
    expect(taxIdError(' 123 456 789 ')).toBeUndefined();
    expect(requestFromAdvertiserForm(advertiserForm({ taxId: ' 123 456 789 ' })).taxId).toBe('123456789');
  });

  it('рекламодатель, заведённый до закона, открывается с пустыми реквизитами и так и помечается', () => {
    const legacy: AdvertiserDto = { advertiserId: 'a', name: 'Техномир', contact: '', createdAtUtc: '2026-09-01T00:00:00Z' };
    expect(formFromAdvertiser(legacy)).toEqual({ name: 'Техномир', legalName: '', taxId: '', address: '', contact: '' });
    expect(hasLegalDetails(legacy)).toBe(false);
    expect(hasLegalDetails({ ...legacy, legalName: 'ООО «Техномир»', taxId: '123456789', address: 'Душанбе' })).toBe(true);
  });
});

describe('креатив', () => {
  it('заголовок на таджикском обязателен и до 120, текст до 280, картинка только https', () => {
    expect(validateCreativeForm(creativeForm({ imageUrl: 'https://cdn.example.com/a.jpg' }))).toEqual({});
    expect(validateCreativeForm(creativeForm({ title: '', titleRu: 'Скидка' })).title?.key).toBe('platform.ads.error.titleRequired');
    expect(validateCreativeForm(creativeForm({ title: 'x'.repeat(AD_LIMITS.titleMax + 1) })).title)
      .toEqual({ key: 'platform.ads.error.titleTooLong', values: { max: AD_LIMITS.titleMax } });
    expect(validateCreativeForm(creativeForm({ body: 'x'.repeat(AD_LIMITS.bodyMax + 1) })).body?.key).toBe('platform.ads.error.bodyTooLong');
    // ПК качают картинки только по https: http-адрес экран не показал бы никогда.
    expect(validateCreativeForm(creativeForm({ imageUrl: 'http://cdn.example.com/a.jpg' })).imageUrl?.key).toBe('platform.ads.error.imageUrl');
  });

  it('русский — по желанию, с теми же пределами', () => {
    expect(validateCreativeForm(creativeForm({ titleRu: '', bodyRu: '' }))).toEqual({});
    expect(validateCreativeForm(creativeForm({ titleRu: 'x'.repeat(AD_LIMITS.titleMax + 1) })).titleRu)
      .toEqual({ key: 'platform.ads.error.titleTooLong', values: { max: AD_LIMITS.titleMax } });
    expect(validateCreativeForm(creativeForm({ bodyRu: 'x'.repeat(AD_LIMITS.bodyMax + 1) })).bodyRu)
      .toEqual({ key: 'platform.ads.error.bodyTooLong', values: { max: AD_LIMITS.bodyMax } });
  });

  it('запрос: таджикский в title и body, русский отдельно, пустое — null', () => {
    expect(requestFromCreativeForm(creativeForm({ title: ' Тахфиф ', body: ' ', titleRu: ' Скидка ', bodyRu: '' })))
      .toEqual({ title: 'Тахфиф', body: null, imageUrl: null, titleRu: 'Скидка', bodyRu: null });
    const source = creative({ body: 'То охири моҳ', titleRu: 'Скидка', bodyRu: 'До конца месяца', imageUrl: 'https://cdn.example.com/a.jpg' });
    expect(requestFromCreativeForm(formFromCreative(source)))
      .toEqual({ title: source.title, body: source.body, imageUrl: source.imageUrl, titleRu: 'Скидка', bodyRu: 'До конца месяца' });
  });
});

describe('модерация', () => {
  it('отметки — все строки контракта, по порядку, и у каждой есть подпись', () => {
    expect([...AD_MODERATION_CHECKS]).toEqual(Object.values(AdModerationCheckNames));
    expect(AD_MODERATION_CHECKS).toHaveLength(6);
    for (const check of AD_MODERATION_CHECKS) expect(moderationCheckLabelKey(check)).toBe(`platform.ads.moderation.check.${check}`);
  });

  it('слова, которые закон разрешает только с документом, — списком в кавычках', () => {
    expect(formatWordingFlags(['лучш', 'беҳтарин'])).toBe('«лучш», «беҳтарин»');
    expect(formatWordingFlags([])).toBeNull();
    expect(formatWordingFlags(null)).toBeNull();
    expect(formatWordingFlags(undefined)).toBeNull();
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
    expect(describeAdError(new PlatformApiError(400, 'Health and beauty ads need a permit.', 'ad_permit_required'), t))
      .toBe('platform.ads.error.permitRequired');
    expect(describeAdError(new PlatformApiError(409, 'An approved creative cannot be edited.', 'ad_creative_locked'), t))
      .toBe('platform.ads.error.creativeLocked');
    // Предел картинки фраза называет в мегабайтах — из того же AdLimits.
    expect(describeAdError(new PlatformApiError(409, 'The image could not be downloaded.', 'ad_image_unavailable'), t))
      .toBe('platform.ads.error.imageUnavailable {"mb":2}');
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
    // Значение — число или произведение чисел: `2 * 1024 * 1024`.
    const limit = (name: string) => {
      const match = source.match(new RegExp(`public const int ${name}\\s*=\\s*([\\d_]+(?:\\s*\\*\\s*[\\d_]+)*);`));
      expect(match).not.toBeNull();
      return match![1].split('*').reduce((product, factor) => product * Number(factor.trim().replace(/_/g, '')), 1);
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
      legalNameMax: limit('LegalNameMax'),
      addressMax: limit('AddressMax'),
      taxIdMinDigits: limit('TaxIdMinDigits'),
      taxIdMaxDigits: limit('TaxIdMaxDigits'),
      permitMax: limit('PermitMax'),
      imageMaxBytes: limit('ImageMaxBytes'),
      reportMaxDays: limit('ReportMaxDays')
    });
  });
});
