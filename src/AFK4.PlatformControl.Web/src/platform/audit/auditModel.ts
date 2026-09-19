import type { MessageKey } from '@/i18n/messages';

type Translate = (key: MessageKey) => string;

/**
 * Журнал платформы — инструмент разбора: по нему сверяют, кто и что сделал, когда клуб пишет
 * «у нас пропала подписка». Три из четырёх колонок были машинными именами сервера
 * («OrganizationOwnerInvite», «Denied», «PlatformApi»), и читать их приходилось по памяти.
 *
 * Действие (`action`) намеренно остаётся машинным: их около двух сотен, они совпадают с тем, что
 * пишется в логи, и по ним же ищут. Перевести половину и оставить половину кодами — хуже, чем
 * честный код везде; полный перевод — отдельная работа с продуктовым решением.
 */
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
