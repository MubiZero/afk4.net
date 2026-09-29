import type { MessageKey } from '@/i18n/messages';
import { CLUB_AUDIT_ACTION_LABELS, dictionaryLabel } from '@afk4/i18n';

type Translate = (key: MessageKey) => string;

/**
 * Журнал платформы — инструмент разбора: по нему сверяют, кто и что сделал, когда клуб пишет
 * «у нас пропала подписка». Три из четырёх колонок были машинными именами сервера
 * («OrganizationOwnerInvite», «Denied», «PlatformApi»), и читать их приходилось по памяти.
 *
 * Действие (`action`) — словами для того, что делают сами сотрудники платформы (клубы, счета,
 * подписки, доступ, реклама, обновления; решение владельца 29.09: сырые коды — не на экране).
 * Код при этом остаётся мелкой строкой под словами: по нему ищут в фильтре и в логах. Действия
 * из клубов (продажи, сессии, смены, команды ПК) — тот же словарь, что у Панели
 * (CLUB_AUDIT_ACTION_LABELS из @afk4/i18n): карточка клуба на Пульте платформы показывает записи
 * из клуба (отказы прав, команды устройств) тем же кодом, и раньше для них словаря здесь не
 * было вовсе — «devices.commands.dispatch» так и оставался на экране кодом.
 */
export function auditActionLabel(action: string, t: Translate): string | null {
  const key = `platform.audit.action.${action}` as MessageKey;
  const label = t(key);
  return label === key ? dictionaryLabel(CLUB_AUDIT_ACTION_LABELS, action, t) : label;
}

export function auditTargetLabel(targetType: string, t: Translate): string {
  return translateOrKeep(`platform.audit.target.${targetType}` as MessageKey, targetType, t);
}

export function auditSourceLabel(sourceApp: string, t: Translate): string {
  return translateOrKeep(`platform.audit.source.${sourceApp}` as MessageKey, sourceApp, t);
}

/// Исход один на три состояния: разрешили и сделали, отказали в доступе, разрешили — но не вышло.
/// Последнее — не отказ, и в разборе инцидента это разные вещи.
export function auditOutcomeLabel(outcome: string, t: Translate): string {
  return translateOrKeep(`journal.outcome.${outcome.toLowerCase()}` as MessageKey, outcome, t);
}

export function auditOutcomeVariant(outcome: string): 'secondary' | 'destructive' | 'outline' {
  switch (outcome.toLowerCase()) {
    case 'denied': return 'destructive';
    case 'failed': return 'outline';
    default: return 'secondary';
  }
}

/// Значение, появившееся на сервере раньше своего перевода, показывается как есть: непереведённый
/// код неприятен, но выдумывать за сервер название события в журнале нельзя.
function translateOrKeep(key: MessageKey, fallback: string, t: Translate): string {
  const label = t(key);
  return label === key ? fallback : label;
}
