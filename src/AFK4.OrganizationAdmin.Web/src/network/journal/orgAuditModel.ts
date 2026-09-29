import type { MessageKey } from '@afk4/i18n';
import { formatDateParts } from '@afk4/formatting';
import type { OrgAuditRecordDto } from '../../api/clients/orgAudit';
import { knownAuditActionLabel, type TFunc } from '../../operatorHelpers';

// Chip tone vocabulary matches .ui-chip--status's real modifiers in 02-ui-kit.css (see also
// billingModel.ts / stock/journalModel.ts's movementStatusTone) — is-booking is unused here, no
// audit outcome maps to it.
export type OutcomeTone = 'is-live' | 'is-neutral' | 'is-booking' | 'is-warning' | 'is-danger';

// Строка журнала для человека: что сделано — словами, код — только если словарь его не знает (и
// тогда мелко, как код). В выгрузку для поддержки идут сырые записи (orgAuditCsv), а не эти строки.
export interface AuditRow {
  id: string;
  date: string;
  actor: string;
  action: string;
  actionIsCode: boolean;
  target: string;
  targetIsCode: boolean;
  outcome: string;
  outcomeTone: OutcomeTone;
  source: string;
  /** Подробности записи, развёрнутые для чтения; null — подробностей нет. */
  details: string | null;
}

export function outcomeChipTone(outcome: string): OutcomeTone {
  if (outcome === 'Succeeded') return 'is-live';
  if (outcome === 'Denied') return 'is-danger';
  if (outcome === 'Failed') return 'is-danger';
  return 'is-neutral';
}

const OUTCOME_LABELS: Readonly<Record<string, MessageKey>> = {
  succeeded: 'op.network.journal.outcome.succeeded',
  denied: 'op.network.journal.outcome.denied',
  failed: 'op.network.journal.outcome.failed'
};

// Тип объекта записи (приходит строкой с сервера) → слово. Ключ — в нижнем регистре.
const TARGET_LABELS: Readonly<Record<string, MessageKey>> = {
  organization: 'op.network.journal.target.organization',
  branch: 'op.network.journal.target.branch',
  device: 'op.network.journal.target.device',
  devicecredential: 'op.network.journal.target.deviceCredential',
  devicecommand: 'op.network.journal.target.deviceCommand',
  deviceseatassignment: 'op.network.journal.target.deviceSeat',
  newsitem: 'op.network.journal.target.news',
  staffuser: 'op.network.journal.target.staff',
  staffinvite: 'op.network.journal.target.staffInvite',
  shift: 'op.network.journal.target.shift',
  moneyaction: 'op.network.journal.target.moneyAction',
  money_action: 'op.network.journal.target.moneyAction',
  moneyactionrequest: 'op.network.journal.target.moneyAction',
  organizationsubscription: 'op.network.journal.target.subscription',
  invoice: 'op.network.journal.target.invoice',
  ledgerentry: 'op.network.journal.target.ledgerEntry',
  clubreview: 'op.network.journal.target.review',
  organizationtipsettings: 'op.network.journal.target.tips',
  organizationloyaltysettings: 'op.network.journal.target.loyalty',
  organizationreferralsettings: 'op.network.journal.target.referral',
  organizationbirthdaygiftsettings: 'op.network.journal.target.birthdayGift',
  eskhatamerchantconfig: 'op.network.journal.target.eskhata',
  dcpaylinkconfig: 'op.network.journal.target.dushanbeCity'
};

// Откуда пришло действие.
const SOURCE_LABELS: Readonly<Record<string, MessageKey>> = {
  OrganizationAdmin: 'op.network.journal.source.admin',
  PlatformApi: 'op.network.journal.source.server',
  Agent: 'op.network.journal.source.pc',
  PlatformBilling: 'op.network.journal.source.billing',
  SetupWizard: 'op.network.journal.source.wizard',
  PlatformControl: 'op.network.journal.source.platform'
};

// Подробности — JSON записи. На экране — развёрнутым для чтения и только по «Подробнее»: строкой в
// таблице он был нечитаемой колбасой из скобок. Пустой объект — подробностей нет.
function readableJson(json: string | null | undefined): string | null {
  if (!json || !json.trim()) return null;
  try {
    const parsed: unknown = JSON.parse(json);
    if (parsed === null || (typeof parsed === 'object' && Object.keys(parsed).length === 0)) return null;
    return JSON.stringify(parsed, null, 2);
  } catch {
    return json;
  }
}

// Идентификатор объекта стоял в колонке «Объект» («shift (3f2a…)»); человеку он ничего не говорит,
// а поддержке нужен — поэтому он первой строкой подробностей.
function readableDetails(targetId: string | null | undefined, json: string | null | undefined): string | null {
  const parts = [targetId ?? null, readableJson(json)].filter((part): part is string => Boolean(part));
  return parts.length === 0 ? null : parts.join('\n');
}

/** Дата записи: год — только если не текущий; в журнале за неделю он повторялся в каждой строке. */
export function journalDate(iso: string, locale: string, now: Date = new Date()): string {
  const sameYear = new Date(iso).getFullYear() === now.getFullYear();
  return formatDateParts(iso, locale, {
    day: 'numeric',
    month: 'short',
    ...(sameYear ? {} : { year: 'numeric' }),
    hour: '2-digit',
    minute: '2-digit'
  });
}

/**
 * Строки журнала для человека. Исполнитель раньше был сырым GUID: владелец, открывший журнал
 * ответить «кто трогал подписку», получал столбец идентификаторов. Имя приносит сервер; без имени
 * — начало id, чтобы строку было с чем сверить. Код действия, тип объекта, итог и источник
 * раньше шли как есть («cash.shift.opened», «shift», «success», «PlatformApi»).
 */
export function toAuditRows(
  records: readonly OrgAuditRecordDto[],
  fmt: { formatDate: (iso: string) => string; t: TFunc },
  systemLabel: string
): AuditRow[] {
  const { t } = fmt;
  return records.map((r) => {
    const action = knownAuditActionLabel(r.action, t);
    const targetKey = TARGET_LABELS[r.targetType.toLowerCase()];
    const outcomeKey = OUTCOME_LABELS[r.outcome.toLowerCase()];
    const sourceKey = SOURCE_LABELS[r.sourceApp];
    return {
      id: r.auditRecordId,
      date: fmt.formatDate(r.createdAtUtc),
      actor: r.actorDisplayName ?? (r.actorStaffUserId ?? r.actorPlatformAdminUserId)?.slice(0, 8) ?? systemLabel,
      action: action ?? r.action,
      actionIsCode: action === null,
      target: targetKey === undefined ? r.targetType : t(targetKey),
      targetIsCode: targetKey === undefined,
      outcome: outcomeKey === undefined ? r.outcome : t(outcomeKey),
      outcomeTone: outcomeChipTone(r.outcome),
      source: sourceKey === undefined ? (r.sourceApp ?? '') : t(sourceKey),
      details: readableDetails(r.targetId, r.detailsJson)
    };
  });
}
