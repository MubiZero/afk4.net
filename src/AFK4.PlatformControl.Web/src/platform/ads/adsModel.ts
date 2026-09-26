import type { MessageKey } from '@afk4/i18n';
import {
  AdCampaignStateNames,
  AdCategoryNames,
  AdErrorCodeNames,
  AdModerationCheckNames,
  AdModerationNames,
  type AdCampaignStateName,
  type AdCategoryName,
  type AdModerationCheckName,
  type AdModerationName
} from '@afk4/contracts';
import { PlatformApiError } from '@/api/platformTransport';
import { describeApiError } from '@/api/describeApiError';
import type { BadgeVariant } from '@/components/ui/badge';
import { fromLocalInput, toLocalInput } from '@/lib/localDateTime';
import type {
  AdCampaignComplianceDto,
  AdCampaignDto,
  AdCreativeDto,
  AdImpressionRowDto,
  AdvertiserDto,
  UpsertAdCampaignRequest,
  UpsertAdCreativeRequest,
  UpsertAdvertiserRequest
} from '@/api/types';

type Translate = (key: MessageKey, values?: Record<string, string | number>) => string;

/**
 * Зеркало `AdLimits` и проверок `PlatformAds.Validate` на сервере. Форма ловит ошибку до
 * отправки и называет поле; сервер остаётся последним словом. Совпадение значений держит тест,
 * читающий C#.
 */
export const AD_LIMITS = {
  nameMax: 160,
  contactMax: 400,
  titleMax: 120,
  bodyMax: 280,
  imageUrlMax: 2048,
  maxCities: 50,
  maxOrganizations: 200,
  reasonMax: 400,
  legalNameMax: 200,
  addressMax: 300,
  taxIdMinDigits: 9,
  taxIdMaxDigits: 14,
  permitMax: 120,
  imageMaxBytes: 2 * 1024 * 1024,
  reportMaxDays: 92
} as const;

/** Предел картинки в мегабайтах — для фраз: байты человеку ничего не говорят. */
export const IMAGE_MAX_MB = AD_LIMITS.imageMaxBytes / (1024 * 1024);

/** Порядок категорий в форме — тот, в котором их перечисляет контракт. */
export const AD_CATEGORIES: readonly AdCategoryName[] = Object.values(AdCategoryNames);

export interface FieldError {
  key: MessageKey;
  values?: Record<string, number>;
}

type Errors<Field extends string> = Partial<Record<Field, FieldError>>;

export function hasErrors(errors: Errors<string>): boolean {
  return Object.keys(errors).length > 0;
}

function blankToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

// ── Подписи словарей ─────────────────────────────────────────────────────────────────────

const CATEGORY_LABEL_KEY: Record<AdCategoryName, MessageKey> = {
  food: 'platform.ads.category.food',
  electronics: 'platform.ads.category.electronics',
  games: 'platform.ads.category.games',
  education: 'platform.ads.category.education',
  services: 'platform.ads.category.services',
  telecom: 'platform.ads.category.telecom',
  health_beauty: 'platform.ads.category.health_beauty',
  finance: 'platform.ads.category.finance',
  social: 'platform.ads.category.social',
  other: 'platform.ads.category.other'
};

/** Категория, пришедшая с сервера новее этого экрана, показывается своим машинным именем. */
export function describeCategory(category: string, t: Translate): string {
  return (AD_CATEGORIES as readonly string[]).includes(category) ? t(CATEGORY_LABEL_KEY[category as AdCategoryName]) : category;
}

// Категории, у которых закон ставит своё условие, — форма называет его под списком.
const CATEGORY_NOTE_KEY: Partial<Record<AdCategoryName, MessageKey>> = {
  health_beauty: 'platform.ads.campaign.categoryNote.health_beauty',
  finance: 'platform.ads.campaign.categoryNote.finance',
  social: 'platform.ads.campaign.categoryNote.social'
};

export function categoryNoteKey(category: string): MessageKey | null {
  return CATEGORY_NOTE_KEY[category as AdCategoryName] ?? null;
}

const MODERATION_LABEL_KEY: Record<AdModerationName, MessageKey> = {
  pending: 'platform.ads.moderation.pending',
  approved: 'platform.ads.moderation.approved',
  rejected: 'platform.ads.moderation.rejected'
};

const MODERATION_VARIANT: Record<AdModerationName, BadgeVariant> = {
  pending: 'warning',
  approved: 'success',
  rejected: 'destructive'
};

export function describeModeration(moderation: string, t: Translate): { label: string; variant: BadgeVariant } {
  const known = moderation in MODERATION_LABEL_KEY ? moderation as AdModerationName : null;
  return known === null
    ? { label: moderation, variant: 'outline' }
    : { label: t(MODERATION_LABEL_KEY[known]), variant: MODERATION_VARIANT[known] };
}

// ── Где кампания на самом деле ───────────────────────────────────────────────────────────

/**
 * Что сейчас происходит с кампанией на ПК. Состояние на сервере — только черновик, запущена и
 * пауза, а «запущена» ещё не значит «показывается»: реклама идёт только в своих датах и только
 * одобренными креативами, которые не сняты с показа. Человек смотрит в список, чтобы понять,
 * крутится ли реклама, — поэтому здесь и сказано именно это.
 */
export type CampaignPhase = 'draft' | 'paused' | 'scheduled' | 'running' | 'ended' | 'nothingToShow' | 'unknown';

/** Снятый с показа креатив остаётся в кампании навсегда: на него ссылается отчёт показов. */
export function isArchived(creative: AdCreativeDto): boolean {
  return creative.archivedAtUtc !== undefined && creative.archivedAtUtc !== null;
}

/** Креативы, которые показываются или ждут решения, — без снятых с показа. */
export function liveCreatives(campaign: AdCampaignDto): AdCreativeDto[] {
  return campaign.creatives.filter(creative => !isArchived(creative));
}

/** Одобренные и не снятые — ровно те, что сервер отдаёт ПК. */
export function approvedCount(campaign: AdCampaignDto): number {
  return liveCreatives(campaign).filter(creative => creative.moderation === AdModerationNames.Approved).length;
}

export function pendingCount(campaign: AdCampaignDto): number {
  return liveCreatives(campaign).filter(creative => creative.moderation === AdModerationNames.Pending).length;
}

export function campaignPhase(campaign: AdCampaignDto, now: Date): CampaignPhase {
  if (campaign.state === AdCampaignStateNames.Draft) return 'draft';
  if (campaign.state === AdCampaignStateNames.Paused) return 'paused';
  if (campaign.state !== AdCampaignStateNames.Active) return 'unknown';
  if (now.getTime() >= new Date(campaign.endsAtUtc).getTime()) return 'ended';
  if (approvedCount(campaign) === 0) return 'nothingToShow';
  return now.getTime() < new Date(campaign.startsAtUtc).getTime() ? 'scheduled' : 'running';
}

const PHASE_LABEL_KEY: Record<Exclude<CampaignPhase, 'unknown'>, MessageKey> = {
  draft: 'platform.ads.phase.draft',
  paused: 'platform.ads.phase.paused',
  scheduled: 'platform.ads.phase.scheduled',
  running: 'platform.ads.phase.running',
  ended: 'platform.ads.phase.ended',
  nothingToShow: 'platform.ads.phase.nothingToShow'
};

const PHASE_VARIANT: Record<CampaignPhase, BadgeVariant> = {
  draft: 'outline',
  paused: 'secondary',
  scheduled: 'default',
  running: 'success',
  ended: 'outline',
  nothingToShow: 'warning',
  unknown: 'outline'
};

export function describePhase(campaign: AdCampaignDto, now: Date, t: Translate): { label: string; variant: BadgeVariant } {
  const phase = campaignPhase(campaign, now);
  return {
    label: phase === 'unknown' ? campaign.state : t(PHASE_LABEL_KEY[phase]),
    variant: PHASE_VARIANT[phase]
  };
}

/**
 * Куда можно перевести кампанию из её состояния. Из черновика — только запустить: пауза у того,
 * что не шло, ничего не значит.
 */
export function stateActions(state: string): AdCampaignStateName[] {
  switch (state) {
    case AdCampaignStateNames.Draft: return [AdCampaignStateNames.Active];
    case AdCampaignStateNames.Active: return [AdCampaignStateNames.Paused, AdCampaignStateNames.Draft];
    case AdCampaignStateNames.Paused: return [AdCampaignStateNames.Active, AdCampaignStateNames.Draft];
    default: return [];
  }
}

const STATE_ACTION_KEY: Record<AdCampaignStateName, MessageKey> = {
  active: 'platform.ads.campaign.start',
  paused: 'platform.ads.campaign.pause',
  draft: 'platform.ads.campaign.toDraft'
};

export function stateActionLabelKey(state: AdCampaignStateName): MessageKey {
  return STATE_ACTION_KEY[state];
}

const STATE_DONE_KEY: Record<AdCampaignStateName, MessageKey> = {
  active: 'platform.ads.campaign.started',
  paused: 'platform.ads.campaign.paused',
  draft: 'platform.ads.campaign.drafted'
};

export function stateDoneKey(state: AdCampaignStateName): MessageKey {
  return STATE_DONE_KEY[state];
}

export type CreativeAction = 'approve' | 'reject' | 'archive' | 'edit';

/**
 * Что можно сделать с креативом. Одобренный мог быть показан и остаётся таким, каким его видели
 * игроки: его не правят и не отклоняют задним числом — снимают с показа и добавляют новый. «Изменить» у него остаётся на своём месте погашенной, причина — рядом.
 * Снятый с показа — только история.
 */
export function creativeActions(creative: AdCreativeDto): CreativeAction[] {
  if (isArchived(creative)) return [];
  switch (creative.moderation) {
    case AdModerationNames.Approved: return ['archive', 'edit'];
    case AdModerationNames.Rejected: return ['approve', 'edit'];
    default: return ['approve', 'reject', 'edit'];
  }
}

export function canEditCreative(creative: AdCreativeDto): boolean {
  return !isArchived(creative) && creative.moderation !== AdModerationNames.Approved;
}

/** Креатив из ответа сервера встаёт на своё место в кампании, новый — в конец. */
export function withCreative(campaign: AdCampaignDto, creative: AdCreativeDto): AdCampaignDto {
  const exists = campaign.creatives.some(candidate => candidate.creativeId === creative.creativeId);
  return {
    ...campaign,
    creatives: exists
      ? campaign.creatives.map(candidate => (candidate.creativeId === creative.creativeId ? creative : candidate))
      : [...campaign.creatives, creative]
  };
}

// ── Рекламодатель ────────────────────────────────────────────────────────────────────────

/**
 * Реквизиты — наименование, ИНН и адрес — обязательны: они нужны договору, а при продаже на
 * расстоянии карточка печатает их сама (закон РТ «О рекламе», ст. 14(1)).
 */
export interface AdvertiserForm {
  name: string;
  legalName: string;
  taxId: string;
  address: string;
  contact: string;
}

export type AdvertiserFormField = keyof AdvertiserForm;

export function emptyAdvertiserForm(): AdvertiserForm {
  return { name: '', legalName: '', taxId: '', address: '', contact: '' };
}

export function formFromAdvertiser(advertiser: AdvertiserDto): AdvertiserForm {
  return {
    name: advertiser.name,
    legalName: advertiser.legalName ?? '',
    taxId: advertiser.taxId ?? '',
    address: advertiser.address ?? '',
    contact: advertiser.contact
  };
}

/** ИНН без пробелов: номер часто вставляют с разбивкой «123 456 789», а сервер ждёт одни цифры. */
export function normalizeTaxId(value: string): string {
  return value.replace(/\s+/g, '');
}

// Только ASCII-цифры, как `char.IsAsciiDigit` на сервере: другие цифры Юникода он не примет.
const TAX_ID_DIGITS = /^[0-9]+$/;

export function validateAdvertiserForm(form: AdvertiserForm): Errors<AdvertiserFormField> {
  const errors: Errors<AdvertiserFormField> = {};
  const name = form.name.trim();
  if (name === '') errors.name = { key: 'platform.ads.error.nameRequired' };
  else if (name.length > AD_LIMITS.nameMax) errors.name = { key: 'platform.ads.error.nameTooLong', values: { max: AD_LIMITS.nameMax } };

  const legalName = form.legalName.trim();
  if (legalName === '') errors.legalName = { key: 'platform.ads.error.legalNameRequired' };
  else if (legalName.length > AD_LIMITS.legalNameMax) {
    errors.legalName = { key: 'platform.ads.error.legalNameTooLong', values: { max: AD_LIMITS.legalNameMax } };
  }

  const taxId = normalizeTaxId(form.taxId);
  if (taxId === '') errors.taxId = { key: 'platform.ads.error.taxIdRequired' };
  else if (!TAX_ID_DIGITS.test(taxId) || taxId.length < AD_LIMITS.taxIdMinDigits || taxId.length > AD_LIMITS.taxIdMaxDigits) {
    errors.taxId = { key: 'platform.ads.error.taxId', values: { min: AD_LIMITS.taxIdMinDigits, max: AD_LIMITS.taxIdMaxDigits } };
  }

  const address = form.address.trim();
  if (address === '') errors.address = { key: 'platform.ads.error.addressRequired' };
  else if (address.length > AD_LIMITS.addressMax) {
    errors.address = { key: 'platform.ads.error.addressTooLong', values: { max: AD_LIMITS.addressMax } };
  }

  if (form.contact.trim().length > AD_LIMITS.contactMax) {
    errors.contact = { key: 'platform.ads.error.contactTooLong', values: { max: AD_LIMITS.contactMax } };
  }
  return errors;
}

export function requestFromAdvertiserForm(form: AdvertiserForm): UpsertAdvertiserRequest {
  return {
    name: form.name.trim(),
    contact: blankToNull(form.contact),
    legalName: form.legalName.trim(),
    taxId: normalizeTaxId(form.taxId),
    address: form.address.trim()
  };
}

/** Реквизиты заполнены — рекламодатели, заведённые до закона, их не имеют, пока их не поправят. */
export function hasLegalDetails(advertiser: AdvertiserDto): boolean {
  return (advertiser.legalName ?? '') !== '' && (advertiser.taxId ?? '') !== '' && (advertiser.address ?? '') !== '';
}

// ── Кампания ─────────────────────────────────────────────────────────────────────────────

export interface CampaignForm {
  advertiserId: string;
  name: string;
  category: AdCategoryName;
  /** Значение `datetime-local` в местном времени читающего. */
  startsAt: string;
  endsAt: string;
  /** Через запятую; пусто — все города. */
  cities: string;
  /** По одному в строке или через запятую; пусто — все клубы. */
  organizationIds: string;
  /** Номер разрешения Минздрава — обязателен для «Здоровья и красоты» (ст. 17). */
  permitNumber: string;
  /** Отметки, по которым карточка сама допишет то, чего требует закон. */
  distanceSelling: boolean;
  requiresCertification: boolean;
  containsOffer: boolean;
}

export type CampaignFormField = keyof CampaignForm;

const DAY_MS = 24 * 60 * 60 * 1000;

export function emptyCampaignForm(now: Date, advertiserId: string): CampaignForm {
  return {
    advertiserId,
    name: '',
    category: AdCategoryNames.Other,
    startsAt: toLocalInput(now.toISOString()),
    endsAt: toLocalInput(new Date(now.getTime() + 30 * DAY_MS).toISOString()),
    cities: '',
    organizationIds: '',
    permitNumber: '',
    distanceSelling: false,
    requiresCertification: false,
    containsOffer: false
  };
}

export function formFromCampaign(campaign: AdCampaignDto): CampaignForm {
  const compliance = campaign.compliance ?? {};
  return {
    advertiserId: campaign.advertiserId,
    name: campaign.name,
    // Как есть, без подмены: неизвестная категория, подменённая на «другое», молча переписалась бы
    // при сохранении. Сервер такую не примет, и форма скажет об этом до отправки.
    category: campaign.category,
    startsAt: toLocalInput(campaign.startsAtUtc),
    endsAt: toLocalInput(campaign.endsAtUtc),
    cities: campaign.cities.join(', '),
    organizationIds: campaign.organizationIds.join('\n'),
    permitNumber: compliance.permitNumber ?? '',
    distanceSelling: compliance.distanceSelling ?? false,
    requiresCertification: compliance.requiresCertification ?? false,
    containsOffer: compliance.containsOffer ?? false
  };
}

/**
 * Поле разрешения видно у «Здоровья и красоты» и там, где номер уже вписан: скрытое непустое поле
 * молча уходило бы на сервер с другой категорией.
 */
export function showsPermitField(form: CampaignForm): boolean {
  return form.category === AdCategoryNames.HealthBeauty || form.permitNumber.trim() !== '';
}

/** Города через запятую или с новой строки. Повтор с другим регистром сервер всё равно склеит. */
export function parseCities(text: string): string[] {
  const seen = new Set<string>();
  const cities: string[] = [];
  for (const part of text.split(/[,\n]/)) {
    const city = part.trim();
    if (city === '' || seen.has(city.toLocaleLowerCase())) continue;
    seen.add(city.toLocaleLowerCase());
    cities.push(city);
  }
  return cities;
}

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Идентификаторы организаций: разделитель — пробел, запятая, точка с запятой или новая строка. */
export function parseOrganizationIds(text: string): { ids: string[]; invalid: string[] } {
  const ids: string[] = [];
  const invalid: string[] = [];
  for (const part of text.split(/[\s,;]+/)) {
    const id = part.trim().toLowerCase();
    if (id === '') continue;
    if (!GUID_PATTERN.test(id)) invalid.push(part.trim());
    else if (!ids.includes(id)) ids.push(id);
  }
  return { ids, invalid };
}

export function validateCampaignForm(form: CampaignForm): Errors<CampaignFormField> {
  const errors: Errors<CampaignFormField> = {};
  if (form.advertiserId === '') errors.advertiserId = { key: 'platform.ads.error.advertiserRequired' };
  const name = form.name.trim();
  if (name === '') errors.name = { key: 'platform.ads.error.nameRequired' };
  else if (name.length > AD_LIMITS.nameMax) errors.name = { key: 'platform.ads.error.nameTooLong', values: { max: AD_LIMITS.nameMax } };
  if (!(AD_CATEGORIES as readonly string[]).includes(form.category)) errors.category = { key: 'platform.ads.error.category' };

  const startsAt = fromLocalInput(form.startsAt);
  const endsAt = fromLocalInput(form.endsAt);
  if (startsAt === null) errors.startsAt = { key: 'platform.ads.error.dateRequired' };
  if (endsAt === null) errors.endsAt = { key: 'platform.ads.error.dateRequired' };
  else if (startsAt !== null && new Date(endsAt).getTime() <= new Date(startsAt).getTime()) {
    errors.endsAt = { key: 'platform.ads.error.endsBeforeStart' };
  }

  if (parseCities(form.cities).length > AD_LIMITS.maxCities) {
    errors.cities = { key: 'platform.ads.error.tooManyCities', values: { max: AD_LIMITS.maxCities } };
  }
  const organizations = parseOrganizationIds(form.organizationIds);
  if (organizations.invalid.length > 0) errors.organizationIds = { key: 'platform.ads.error.organizationId' };
  else if (organizations.ids.length > AD_LIMITS.maxOrganizations) {
    errors.organizationIds = { key: 'platform.ads.error.tooManyOrganizations', values: { max: AD_LIMITS.maxOrganizations } };
  }

  const permit = form.permitNumber.trim();
  if (form.category === AdCategoryNames.HealthBeauty && permit === '') errors.permitNumber = { key: 'platform.ads.error.permitRequired' };
  else if (permit.length > AD_LIMITS.permitMax) {
    errors.permitNumber = { key: 'platform.ads.error.permitTooLong', values: { max: AD_LIMITS.permitMax } };
  }
  return errors;
}

/** Запрос из формы, которая прошла проверку: даты в ней уже разобраны. */
export function requestFromCampaignForm(form: CampaignForm): UpsertAdCampaignRequest {
  return {
    advertiserId: form.advertiserId,
    name: form.name.trim(),
    category: form.category,
    startsAtUtc: fromLocalInput(form.startsAt) ?? '',
    endsAtUtc: fromLocalInput(form.endsAt) ?? '',
    cities: parseCities(form.cities),
    organizationIds: parseOrganizationIds(form.organizationIds).ids,
    compliance: {
      permitNumber: blankToNull(form.permitNumber),
      distanceSelling: form.distanceSelling,
      requiresCertification: form.requiresCertification,
      containsOffer: form.containsOffer
    }
  };
}

export type CardNotice = 'seller' | 'certification' | 'offer';

/** Что карточка кампании допечатает сама, кроме метки «Реклама» и рекламодателя. */
export function cardNotices(compliance: AdCampaignComplianceDto | null | undefined): CardNotice[] {
  const notices: CardNotice[] = [];
  if (compliance?.distanceSelling === true) notices.push('seller');
  if (compliance?.requiresCertification === true) notices.push('certification');
  if (compliance?.containsOffer === true) notices.push('offer');
  return notices;
}

// ── Креатив ──────────────────────────────────────────────────────────────────────────────

/**
 * `title` и `body` — таджикский текст: реклама идёт на государственном языке (ст. 5 закона о
 * рекламе, закон о госязыке), заголовок на нём обязателен и стоит на карточке первым. Русский —
 * вторая строка по желанию.
 */
export interface CreativeForm {
  title: string;
  body: string;
  titleRu: string;
  bodyRu: string;
  imageUrl: string;
}

export type CreativeFormField = keyof CreativeForm;

export function emptyCreativeForm(): CreativeForm {
  return { title: '', body: '', titleRu: '', bodyRu: '', imageUrl: '' };
}

export function formFromCreative(creative: AdCreativeDto): CreativeForm {
  return {
    title: creative.title,
    body: creative.body ?? '',
    titleRu: creative.titleRu ?? '',
    bodyRu: creative.bodyRu ?? '',
    imageUrl: creative.imageUrl ?? ''
  };
}

/** Картинка годится для показа: https-адрес, как требует сервер, — ПК качают только по https. */
export function isHttpsUrl(value: string): boolean {
  const trimmed = value.trim();
  if (trimmed === '' || trimmed.length > AD_LIMITS.imageUrlMax) return false;
  try {
    return new URL(trimmed).protocol === 'https:';
  } catch {
    return false;
  }
}

export function validateCreativeForm(form: CreativeForm): Errors<CreativeFormField> {
  const errors: Errors<CreativeFormField> = {};
  const title = form.title.trim();
  if (title === '') errors.title = { key: 'platform.ads.error.titleRequired' };
  else if (title.length > AD_LIMITS.titleMax) errors.title = { key: 'platform.ads.error.titleTooLong', values: { max: AD_LIMITS.titleMax } };
  if (form.body.trim().length > AD_LIMITS.bodyMax) {
    errors.body = { key: 'platform.ads.error.bodyTooLong', values: { max: AD_LIMITS.bodyMax } };
  }
  if (form.titleRu.trim().length > AD_LIMITS.titleMax) {
    errors.titleRu = { key: 'platform.ads.error.titleTooLong', values: { max: AD_LIMITS.titleMax } };
  }
  if (form.bodyRu.trim().length > AD_LIMITS.bodyMax) {
    errors.bodyRu = { key: 'platform.ads.error.bodyTooLong', values: { max: AD_LIMITS.bodyMax } };
  }
  if (form.imageUrl.trim() !== '' && !isHttpsUrl(form.imageUrl)) errors.imageUrl = { key: 'platform.ads.error.imageUrl' };
  return errors;
}

export function requestFromCreativeForm(form: CreativeForm): UpsertAdCreativeRequest {
  return {
    title: form.title.trim(),
    body: blankToNull(form.body),
    imageUrl: blankToNull(form.imageUrl),
    titleRu: blankToNull(form.titleRu),
    bodyRu: blankToNull(form.bodyRu)
  };
}

// ── Модерация ────────────────────────────────────────────────────────────────────────────

/**
 * Отметки модератора — по статьям закона, в порядке контракта. Сервер не одобрит без каждой:
 * код не отличит сок от пива, и отметка — решение человека, которое уходит в журнал.
 */
/** Отметки, которые нужны любой кампании; у «Финансов» к ним добавляется своя (ст. 18). */
export const AD_MODERATION_CHECKS: readonly AdModerationCheckName[] = Object.values(AdModerationCheckNames)
  .filter(check => check !== AdModerationCheckNames.FinanceTerms);

/** Зеркало AdModerationCheckNames.RequiredFor: сервер одобрит только со всеми этими отметками. */
export function moderationChecksFor(category: string): readonly AdModerationCheckName[] {
  return category === AdCategoryNames.Finance ? [...AD_MODERATION_CHECKS, AdModerationCheckNames.FinanceTerms] : AD_MODERATION_CHECKS;
}

const MODERATION_CHECK_LABEL_KEY: Record<AdModerationCheckName, MessageKey> = {
  not_club_or_betting: 'platform.ads.moderation.check.not_club_or_betting',
  no_banned_goods: 'platform.ads.moderation.check.no_banned_goods',
  minors: 'platform.ads.moderation.check.minors',
  truthful: 'platform.ads.moderation.check.truthful',
  ethical: 'platform.ads.moderation.check.ethical',
  tajik_on_image: 'platform.ads.moderation.check.tajik_on_image',
  finance_terms: 'platform.ads.moderation.check.finance_terms'
};

export function moderationCheckLabelKey(check: AdModerationCheckName): MessageKey {
  return MODERATION_CHECK_LABEL_KEY[check];
}

/** Слова, которые закон разрешает только с документом (ст. 7), — списком в кавычках. */
export function formatWordingFlags(flags: readonly string[] | null | undefined): string | null {
  if (flags === null || flags === undefined || flags.length === 0) return null;
  return flags.map(flag => `«${flag}»`).join(', ');
}

// ── Отчёт ────────────────────────────────────────────────────────────────────────────────

/** День по UTC: показы сервер складывает по дням UTC, и отчёт спрашивают теми же днями. */
function utcDay(date: Date): string {
  return date.toISOString().slice(0, 10);
}

/** Последние 30 дней, сегодняшний включительно. */
export function defaultReportRange(now: Date): { from: string; to: string } {
  return { from: utcDay(new Date(now.getTime() - 29 * DAY_MS)), to: utcDay(now) };
}

const DAY_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

function dayNumber(day: string): number | null {
  if (!DAY_PATTERN.test(day)) return null;
  const time = Date.parse(`${day}T00:00:00Z`);
  return Number.isNaN(time) ? null : Math.round(time / DAY_MS);
}

/** Сервер отвечает отказом на период длиннее 92 дней — форма говорит это раньше. */
export function validateReportRange(from: string, to: string): FieldError | null {
  const first = dayNumber(from);
  const last = dayNumber(to);
  if (first === null || last === null) return { key: 'platform.ads.report.error.dates' };
  if (last < first) return { key: 'platform.ads.report.error.order' };
  if (last - first > AD_LIMITS.reportMaxDays) return { key: 'platform.ads.report.error.tooLong', values: { max: AD_LIMITS.reportMaxDays } };
  return null;
}

export function reportTotals(rows: readonly AdImpressionRowDto[]): { impressions: number; shownSeconds: number } {
  return rows.reduce(
    (total, row) => ({ impressions: total.impressions + row.impressions, shownSeconds: total.shownSeconds + row.shownSeconds }),
    { impressions: 0, shownSeconds: 0 }
  );
}

// ── Отказы сервера ───────────────────────────────────────────────────────────────────────

// Отказ `ad_invalid` приходит с английской фразой про поле. Формы проверяют те же правила до
// отправки, поэтому сюда доходит только расхождение с сервером — его и называем, а не «сбой».
const ERROR_CODE_MESSAGE: Record<string, FieldError> = {
  [AdErrorCodeNames.Invalid]: { key: 'platform.ads.error.invalid' },
  [AdErrorCodeNames.NotApproved]: { key: 'platform.ads.error.notApproved' },
  [AdErrorCodeNames.ConfirmationRequired]: { key: 'platform.ads.error.confirmationRequired' },
  [AdErrorCodeNames.PermitRequired]: { key: 'platform.ads.error.permitRequired' },
  // Креатив одобрили в другой вкладке, пока здесь была открыта его правка.
  [AdErrorCodeNames.CreativeLocked]: { key: 'platform.ads.error.creativeLocked' },
  [AdErrorCodeNames.ImageUnavailable]: { key: 'platform.ads.error.imageUnavailable', values: { mb: IMAGE_MAX_MB } }
};

export function describeAdError(cause: unknown, t: Translate): string {
  if (cause instanceof PlatformApiError && cause.errorCode !== null) {
    const message = ERROR_CODE_MESSAGE[cause.errorCode];
    if (message !== undefined) return t(message.key, message.values);
  }
  // 404 здесь — запись, которой уже нет: кампанию или креатив открыли по старой ссылке.
  return describeApiError(cause, t, { 404: 'platform.ads.error.notFound' });
}
