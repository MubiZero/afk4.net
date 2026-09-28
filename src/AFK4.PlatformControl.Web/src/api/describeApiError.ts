import { PlatformApiError, PlatformStaleClientError } from './platformApi';
import { TransportErrorCodes } from './platformTransport';
import type { MessageKey } from '@/i18n/messages';

type Translate = (key: MessageKey, values?: Record<string, string | number>) => string;

/**
 * Причины отказа, которые человек за панелью исправляет сам.
 *
 * Сервер называет их машинным именем рядом с английской фразой (PlatformErrorCodeNames,
 * OffboardingErrorCodes). Без этого разбора панель показывала одно «не удалось сохранить
 * изменения» и на «счёт за период уже выставлен», и на «нет прав», и на обрыв сети — человек не
 * мог понять, чинить ли ему форму, права или интернет.
 */
const CODE_KEYS: Record<string, MessageKey> = {
  // Счета
  invoice_period_already_billed: 'platform.error.invoicePeriodAlreadyBilled',
  invoice_already_paid: 'platform.error.invoiceAlreadyPaid',
  invoice_already_void: 'platform.error.invoiceAlreadyVoid',
  paid_invoice_cannot_be_voided: 'platform.error.paidInvoiceCannotBeVoided',
  credit_note_not_payable: 'platform.error.creditNoteNotPayable',
  invoice_numbering_conflict: 'platform.error.invoiceNumberingConflict',

  // Подписка
  subscription_period_end_not_after_start: 'platform.error.subscriptionPeriodEnd',
  subscription_grace_not_in_future: 'platform.error.subscriptionGrace',
  subscription_trial_needs_period_end: 'platform.error.subscriptionTrialPeriodEnd',
  subscription_plan_not_found: 'platform.error.subscriptionPlanNotFound',

  // Организации и филиалы
  organization_slug_taken: 'platform.error.organizationSlugTaken',
  branch_slug_taken: 'platform.error.branchSlugTaken',
  owner_username_taken: 'platform.error.ownerUserNameTaken',
  owner_invite_no_email: 'platform.error.ownerInviteNoEmail',
  owner_invite_expired: 'platform.error.ownerInviteExpired',

  // Уход клуба с платформы — эти коды разбирались отдельным словарём в offboardingModel.
  not_deletion_pending: 'platform.offboarding.error.notLeaving',
  purge_not_due: 'platform.offboarding.error.notDue',
  slug_mismatch: 'platform.offboarding.error.slugMismatch',
  organization_purged: 'platform.offboarding.error.alreadyPurged'
};

/**
 * Человеческий текст ошибки для показа пользователю.
 *
 * Транспорт носит технические англоязычные сообщения («Sign-in failed.», «Platform API call
 * failed.») — они нужны в логах, но в интерфейс попадать не должны: русский экран с английской
 * строкой читается как поломка, а сам текст ничего не объясняет тому, кто его читает.
 *
 * Поэтому текст выбирается по КОДУ ответа, а вызывающий экран может уточнить отдельные коды
 * своей формулировкой (`overrides`) — например, 404 «код приглашения не найден».
 */
/**
 * Поможет ли повтор. Нехватка прав не чинится кнопкой «Повторить»: сколько ни жми, ответ будет
 * тот же, а кнопка обещает обратное — человек жмёт её вместо того, чтобы пойти к начальнику.
 */
export function retryCanHelp(cause: unknown): boolean {
  if (cause instanceof PlatformApiError) return cause.status !== 401 && cause.status !== 403;
  return true;
}

export function describeApiError(
  cause: unknown,
  t: Translate,
  overrides: Partial<Record<number, MessageKey>> = {}
): string {
  // Checked ahead of PlatformApiError: this isn't an HTTP-status failure, it's the client and
  // server disagreeing about the response contract (see PlatformStaleClientError). No override can
  // apply here — the fix is the same regardless of which route hit the mismatch.
  if (cause instanceof PlatformStaleClientError) return t('auth.error.staleClient');
  if (cause instanceof PlatformApiError) {
    const override = overrides[cause.status];
    if (override !== undefined) return t(override);
    // Код точнее статуса: 409 «счёт уже оплачен» и 409 «номер занят параллельным запросом»
    // чинятся по-разному, и одно «конфликт» на оба ничего человеку не говорит.
    const byCode = cause.errorCode !== null ? CODE_KEYS[cause.errorCode] : undefined;
    if (byCode !== undefined) return t(byCode);
    if (cause.status === 401 || cause.status === 403) return t('state.error.forbidden');
    // Статус 0 транспорт ставит, когда ответа не было вовсе. «Не дождались» и «не дозвонились» —
    // разные беды: в первом случае запрос ушёл и мог выполниться, во втором точно нет.
    if (cause.status === 0) {
      return cause.errorCode === TransportErrorCodes.Timeout ? t('state.error.timeout') : t('state.error.network');
    }
    return t('state.error.server');
  }
  // Сетевой сбой до ответа — fetch бросает TypeError без статуса.
  if (cause instanceof TypeError) return t('state.error.network');
  return t('state.error.server');
}

/**
 * Отказы шага 2FA (`/2fa/setup`, `/2fa/setup/confirm`, `/2fa/verify`) — сессия входа истекла,
 * опечатка в коде и блокировка по попыткам раньше отвечали одинаковым голым 401/429, и экран не
 * мог сказать, начинать ли заново или просто попробовать снова. `lockedUntilUtc` в теле ответа
 * `CODE_KEYS` не передаёт (там только статический текст по ключу) — отсюда отдельная функция,
 * а не запись в общий словарь.
 */
export type TwoFactorErrorOutcome =
  // Экран сам решает, что делать с истёкшей сессией (обычно — вернуться к паролю); текста для
  // него здесь нет, чтобы не плодить два места с формулировкой одного и того же «начните заново».
  | { kind: 'expired' }
  | { kind: 'message'; text: string };

export function describeTwoFactorError(
  cause: unknown,
  t: Translate,
  formatDateTime: (iso: string) => string
): TwoFactorErrorOutcome {
  if (cause instanceof PlatformApiError) {
    if (cause.errorCode === 'two_factor_challenge_expired') return { kind: 'expired' };
    if (cause.errorCode === 'two_factor_code_invalid') {
      return { kind: 'message', text: t('auth.twoFactor.error.invalidCode') };
    }
    if (cause.errorCode === 'two_factor_locked' || cause.status === 429) {
      const lockedUntilUtc = readLockedUntilUtc(cause);
      return {
        kind: 'message',
        text: lockedUntilUtc !== null
          ? t('auth.twoFactor.error.lockedOutUntil', { time: formatDateTime(lockedUntilUtc) })
          : t('auth.twoFactor.error.lockedOut')
      };
    }
    // A bare 401 with none of the codes above is either an older API build that predates them, or
    // a build/deploy skew window (the panel and the API deploy independently) — falling back to
    // "wrong code" keeps the old behaviour instead of a blank "server error".
    if (cause.status === 401) return { kind: 'message', text: t('auth.twoFactor.error.invalidCode') };
  }
  return { kind: 'message', text: describeApiError(cause, t) };
}

function readLockedUntilUtc(cause: PlatformApiError): string | null {
  try {
    const parsed = JSON.parse(cause.body) as { lockedUntilUtc?: string };
    return parsed.lockedUntilUtc ?? null;
  } catch {
    return null;
  }
}
