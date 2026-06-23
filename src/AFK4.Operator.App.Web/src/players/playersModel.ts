// Players-feature model surface (зеркало паттерна src/booking/). Общие мапперы
// (projectPlayerClient/playerPackageLabel/PlayerClientItem) остаются в operatorHelpers,
// т.к. их используют POS/Брони/Карта — здесь только ре-экспорт, чтобы у фичи был
// единый импорт. Players-эксклюзивные чистые функции живут здесь.
import { formatMinorUnits, formatTime, type PlayerClientItem, type TFunc } from '../operatorHelpers';
import type { LedgerEntryDto, PlayerPackageDto } from '../operatorApiClients';
import type { MessageKey } from '@afk4/i18n';

export { projectPlayerClient, playerPackageLabel, type PlayerClientItem } from '../operatorHelpers';

export function fixturePlayers(currencyCode: string, t: TFunc): PlayerClientItem[] {
  const example = t('op.helper.player.fixture.example');
  return [
    { name: 'Madina S.', status: 'active', balanceMinorUnits: 46000, debtMinorUnits: 0, last: example, tone: 'active', detail: t('op.helper.player.fixture.localCard'), phoneNumber: '+992 90 555 22 11', source: 'fixture' },
    { name: 'Amir K.', status: 'active', balanceMinorUnits: 12000, debtMinorUnits: 0, last: example, tone: 'active', detail: formatMinorUnits(12000, currencyCode), phoneNumber: '', source: 'fixture' },
    { name: 'Olim K.', status: 'debt', balanceMinorUnits: 0, debtMinorUnits: 3500, last: example, tone: 'debt', detail: t('op.helper.player.fixture.debtDetail'), phoneNumber: '', source: 'fixture' }
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
      return client.status === 'inactive';
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
