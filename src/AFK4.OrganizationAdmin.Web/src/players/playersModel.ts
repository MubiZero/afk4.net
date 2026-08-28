// Players-feature model surface (зеркало паттерна src/booking/). Общие мапперы
// (projectPlayerClient/playerPackageLabel/PlayerClientItem) остаются в operatorHelpers,
// т.к. их используют POS/Брони/Карта — здесь только ре-экспорт, чтобы у фичи был
// единый импорт. Players-эксклюзивные чистые функции живут здесь.
import { formatMinorUnits, formatTime, readString, type PlayerClientItem, type TFunc } from '../operatorHelpers';
import type { LedgerEntryDto, PlayerPackageDto, SessionTimelineItemDto } from '../operatorApiClients';
import type { MessageKey } from '@afk4/i18n';

export { projectPlayerClient, playerPackageLabel, type PlayerClientItem } from '../operatorHelpers';

export function fixturePlayers(currencyCode: string, t: TFunc): PlayerClientItem[] {
  const example = t('op.helper.player.fixture.example');
  return [
    { name: 'Madina S.', isActive: true, status: 'active', balanceMinorUnits: 46000, debtMinorUnits: 0, last: example, tone: 'active', detail: t('op.helper.player.fixture.localCard'), phoneNumber: '+992 90 555 22 11', source: 'fixture', createdAtUtc: null, lastActivityAtUtc: null, activePackageName: null, activePackageRemainingMinutes: 0, platformPersonId: null, createdFromApp: false },
    { name: 'Amir K.', isActive: true, status: 'active', balanceMinorUnits: 12000, debtMinorUnits: 0, last: example, tone: 'active', detail: formatMinorUnits(12000, currencyCode), phoneNumber: '', source: 'fixture', createdAtUtc: null, lastActivityAtUtc: null, activePackageName: null, activePackageRemainingMinutes: 0, platformPersonId: null, createdFromApp: false },
    { name: 'Olim K.', isActive: true, status: 'debt', balanceMinorUnits: 0, debtMinorUnits: 3500, last: example, tone: 'debt', detail: t('op.helper.player.fixture.debtDetail'), phoneNumber: '', source: 'fixture', createdAtUtc: null, lastActivityAtUtc: null, activePackageName: null, activePackageRemainingMinutes: 0, platformPersonId: null, createdFromApp: false }
  ];
}

// Maps the stable status key from projectPlayerClient/fixturePlayers to a localized label.
export function playerStatusLabel(status: string, t: TFunc): string {
  switch (status) {
    case 'active':
      return t('op.players.status.active');
    case 'debt':
      return t('op.players.status.debt');
    case 'inactive':
      return t('op.players.status.inactive');
    default:
      return status;
  }
}

// Карта entryType → существующий каталожный ключ ledger.type.* (ru/en/tg уже заполнены).
// Значения entryType — из AFK4.Shared.Contracts/Billing/LedgerEntryTypeNames.
const LEDGER_TYPE_KEYS: Record<string, MessageKey> = {
  top_up: 'ledger.type.top_up',
  gameplay_charge: 'ledger.type.gameplay_charge',
  package_purchase: 'ledger.type.package_purchase',
  package_consumption: 'ledger.type.package_consumption',
  bonus_grant: 'ledger.type.bonus_grant',
  bonus_consumption: 'ledger.type.bonus_consumption',
  refund: 'ledger.type.refund',
  manual_correction: 'ledger.type.manual_correction',
  postpaid_debt: 'ledger.type.postpaid_debt',
  debt_payment: 'ledger.type.debt_payment',
  wallet_payment: 'ledger.type.wallet_payment',
  reversal: 'ledger.type.reversal',
  cashback: 'ledger.type.cashback'
};

export function ledgerTypeLabel(entryType: string, t: TFunc): string {
  const key = LEDGER_TYPE_KEYS[entryType];
  return key ? t(key) : t('op.players.ledger.type.fallback');
}

export interface LedgerEntryView {
  id: string;
  timeLabel: string;
  typeLabel: string;
  description: string;
  reason: string;
  accountType: string;
  quantitySeconds: number;
  amountMinorUnits: number;
  currencyCode: string;
  isCredit: boolean;   // знак суммы: >=0 = кредит (зелёный), <0 = дебет (красный)
  isReversal: boolean; // запись реверсирует другую (reversesLedgerEntryId != null)
}

export function projectLedgerEntry(entry: LedgerEntryDto, t: TFunc): LedgerEntryView {
  const minorUnits = entry.amount?.minorUnits ?? 0;
  return {
    id: entry.ledgerEntryId,
    timeLabel: formatTime(entry.createdAtUtc),
    typeLabel: ledgerTypeLabel(entry.entryType, t),
    description: entry.description ?? '',
    reason: entry.reason ?? '',
    accountType: entry.accountType,
    quantitySeconds: entry.quantitySeconds ?? 0,
    amountMinorUnits: minorUnits,
    currencyCode: entry.amount?.currencyCode ?? '',
    isCredit: minorUnits >= 0,
    isReversal: entry.reversesLedgerEntryId != null
  };
}

export interface PlayerPackageView {
  id: string;
  name: string;
  remainingIncludedMinutes: number;
  remainingBonusMinutes: number;
  totalRemainingMinutes: number;
  expiryLabel: string | null; // локализованная дата срока; null = бессрочно
  isExpired: boolean;
}

export function projectPlayerPackage(pkg: PlayerPackageDto, t: TFunc, locale: string): PlayerPackageView {
  const includedMinutes = Math.floor((pkg.remainingIncludedSeconds ?? 0) / 60);
  const bonusMinutes = Math.floor((pkg.remainingBonusSeconds ?? 0) / 60);
  const expiresAt = pkg.expiresAtUtc ? new Date(pkg.expiresAtUtc) : null;
  const validExpiry = expiresAt !== null && !Number.isNaN(expiresAt.getTime());
  return {
    id: pkg.playerPackageId,
    name: pkg.name || t('op.players.profile.packageFallback'),
    remainingIncludedMinutes: includedMinutes,
    remainingBonusMinutes: bonusMinutes,
    totalRemainingMinutes: includedMinutes + bonusMinutes,
    expiryLabel: validExpiry ? expiresAt!.toLocaleDateString(locale, { day: '2-digit', month: 'short', year: 'numeric' }) : null,
    isExpired: validExpiry ? expiresAt!.getTime() < Date.now() : false
  };
}

// Честные сегменты по реальным полям, на СТАБИЛЬНЫХ id (label локализуется на render —
// id не зависит от языка, что чинит латентный баг с фильтром по локализованной строке).
export type ClientSegmentId = 'all' | 'debt' | 'inactive';

export interface ClientSegment {
  id: ClientSegmentId;
  label: string;
  count: number;
}

export function matchesSegment(client: PlayerClientItem, id: ClientSegmentId): boolean {
  switch (id) {
    case 'all':
      return true;
    case 'debt':
      return client.debtMinorUnits > 0;
    case 'inactive':
      return !client.isActive;
    default:
      return false;
  }
}

export function buildClientSegments(clients: PlayerClientItem[], t: TFunc): ClientSegment[] {
  const ids: ClientSegmentId[] = ['all', 'debt', 'inactive'];
  const labels: Record<ClientSegmentId, MessageKey> = {
    all: 'op.players.segments.all',
    debt: 'op.players.segments.debt',
    inactive: 'op.players.segments.inactive'
  };
  return ids.map((id) => ({
    id,
    label: t(labels[id]),
    count: clients.filter((c) => matchesSegment(c, id)).length
  }));
}

// Сводка «по всей базе» для шапки раздела (зеркало счётчиков в шапке «Карты»/«Броней»):
// сколько клиентов, сумма депозитов и сумма долгов по загруженному списку. Это денежная
// картина базы — намеренно отличается от сегмент-чипов списка (те — фильтры-навигация).
export interface ClientOverview {
  count: number;
  depositMinorUnits: number;
  debtMinorUnits: number;
}

export function buildClientOverview(clients: PlayerClientItem[]): ClientOverview {
  return clients.reduce<ClientOverview>((acc, c) => ({
    count: acc.count + 1,
    depositMinorUnits: acc.depositMinorUnits + Math.max(0, c.balanceMinorUnits),
    debtMinorUnits: acc.debtMinorUnits + Math.max(0, c.debtMinorUnits)
  }), { count: 0, depositMinorUnits: 0, debtMinorUnits: 0 });
}

// ── Кросс-контекст профиля: «играет сейчас» + «ближайшая бронь» ──────────────────
// Сессия «занимает место», если совпадает аккаунт, она не завершена и в активном состоянии.
const BLOCKING_SESSION_STATES = ['active', 'paused', 'ending'];
// «Живые» брони (не отменены/не отыграны). Сервер уже отсортировал по времени старта,
// поэтому первая подходящая — ближайшая.
const UPCOMING_RESERVATION_STATES = ['pending', 'confirmed'];

export interface ClientLiveContext {
  session: { seatName: string; untilLabel: string | null } | null; // untilLabel null = открытый счёт
  nextBooking: { timeLabel: string; seatName: string | null } | null;
}

export function buildClientContext(
  sessions: SessionTimelineItemDto[],
  reservations: Array<Record<string, unknown>>,
  playerAccountId: string
): ClientLiveContext {
  const activeSession = sessions.find((session) =>
    session.playerAccountId === playerAccountId &&
    session.endedAtUtc == null &&
    BLOCKING_SESSION_STATES.includes(session.state)
  ) ?? null;

  // Брони уже отфильтрованы сервером по playerAccountId, но перепроверяем на клиенте: если бэкенд
  // когда-нибудь проигнорирует параметр, в профиль не должна просочиться чужая бронь.
  const nextBooking = reservations.find((reservation) =>
    readString(reservation, 'playerAccountId') === playerAccountId &&
    UPCOMING_RESERVATION_STATES.includes(readString(reservation, 'state'))
  ) ?? null;

  return {
    session: activeSession === null ? null : {
      seatName: activeSession.seatName,
      untilLabel: activeSession.endsAtUtc ? formatTime(activeSession.endsAtUtc) : null
    },
    nextBooking: nextBooking === null ? null : {
      timeLabel: formatTime(readString(nextBooking, 'startsAtUtc')),
      seatName: readString(nextBooking, 'seatName') || null
    }
  };
}

// «Один проход» вариант buildClientContext для таблицы: sessions/reservations грузятся ОДИН
// раз для всего списка (а не рефетчатся на клиента), затем раскладываются по playerAccountId.
// Фикстурные клиенты (без playerAccountId) не имеют серверного контекста — пропускаем их.
export function buildClientContextMap(
  sessions: SessionTimelineItemDto[],
  reservations: Array<Record<string, unknown>>,
  clients: PlayerClientItem[]
): Map<string, ClientLiveContext> {
  const contextByPlayerId = new Map<string, ClientLiveContext>();
  for (const client of clients) {
    if (!client.playerAccountId) continue;
    contextByPlayerId.set(client.playerAccountId, buildClientContext(sessions, reservations, client.playerAccountId));
  }
  return contextByPlayerId;
}

// ── Таблица клиентов: тег «Новый», относительный визит, банк времени пакета ──────

const MS_PER_DAY = 24 * 60 * 60 * 1000;

// Клиент — «Новый», если зарегистрирован младше порога (по умолчанию неделя). nowMs передаётся
// аргументом (а не Date.now() внутри), чтобы функция была детерминируемой в тестах.
export function isNewClient(createdAtUtc: string | null, nowMs: number, thresholdDays = 7): boolean {
  if (!createdAtUtc) return false;
  const createdMs = new Date(createdAtUtc).getTime();
  if (Number.isNaN(createdMs)) return false;
  return nowMs - createdMs < thresholdDays * MS_PER_DAY;
}

// Компактная метка последнего визита для узкой колонки таблицы: сейчас/вчера/N дн./N нед./—.
// «N дн.»/«N нед.» — намеренно плюрал-инвариантная форма (не ICU-plural): в русском «1 дн.»/
// «5 дн.» звучат нормально сокращённо, а полная фраза «5 дней» не влезает в колонку.
export function relativeVisitLabel(lastActivityAtUtc: string | null, nowMs: number, t: TFunc): string {
  if (!lastActivityAtUtc) return '—';
  const lastMs = new Date(lastActivityAtUtc).getTime();
  if (Number.isNaN(lastMs)) return '—';

  const days = Math.floor(Math.max(0, nowMs - lastMs) / MS_PER_DAY);
  if (days === 0) return t('op.players.visit.now');
  if (days === 1) return t('op.players.visit.yesterday');
  if (days < 7) return t('op.players.visit.daysAgo', { n: days });
  return t('op.players.visit.weeksAgo', { n: Math.ceil(days / 7) });
}

// «Банк времени» под именем клиента в таблице — название активного пакета + остаток минут.
// null, если у клиента нет активного пакета (не рендерить пустую строку).
export function activePackageLabel(name: string | null, minutes: number, t: TFunc): string | null {
  if (!name) return null;
  return t('op.players.package.timeBank', { name, minutes });
}
