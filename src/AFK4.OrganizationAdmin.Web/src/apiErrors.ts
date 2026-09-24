import type { MessageKey } from '@afk4/i18n';
import { PlatformApiError } from './platformApi';

type TFn = (key: MessageKey, values?: Record<string, string | number>) => string;

export interface OperatorErrorProjection {
  title: string;
  detail: string;
  // Поможет ли «Повторить». Экран, который рисует отказ загрузки, показывает кнопку только по
  // этому признаку: под отказом, который повтор не исправит, она обещает то, чего не будет.
  retryCanHelp: boolean;
  // К кому идти, когда не хватает прав: единственное настоящее действие в этом случае.
  accessHint?: string;
}

/**
 * Отказ по правам, который приложение проверило само, не дойдя до сервера.
 *
 * Отдельный класс, а не голый Error: проекция отличает его от прочих сообщений приложения так же,
 * как 403 сервера, — без «Повторить» и с подсказкой, к кому идти за доступом.
 */
export class PermissionRefusal extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'PermissionRefusal';
  }
}

const codeMessageKeys = {
  // Пять промахов подряд запирают вход на четверть часа. Под общим «неверный логин или пароль»
  // человек продолжал бы подбирать и злиться, не понимая, почему верный пароль не подходит.
  too_many_password_attempts: 'op.error.code.tooManyPasswordAttempts',
  open_shift_required: 'op.error.code.openShiftRequired',
  invalid_payment_split: 'op.error.code.invalidPaymentSplit',
  mixed_currency: 'op.error.code.mixedCurrency',
  insufficient_funds: 'op.error.code.insufficientFunds',
  player_required_for_wallet: 'op.error.code.playerRequiredForWallet',
  out_of_stock: 'op.error.code.outOfStock',
  organization_mismatch: 'op.error.code.organizationMismatch',
  reservation_not_found: 'op.error.code.reservationNotFound',
  reservation_confirmation_required: 'op.error.code.reservationConfirmationRequired',
  reservation_already_started: 'op.error.code.reservationAlreadyStarted',
  reservation_expired: 'op.error.code.reservationExpired',
  seat_unavailable: 'op.error.code.seatUnavailable',
  // Клуб сам закрыл этот ПК на обслуживание: «место недоступно» не сказало бы, что делать дальше.
  device_in_maintenance: 'op.error.code.deviceInMaintenance',
  version_conflict: 'op.error.code.versionConflict',
  // Сессию изменили с тех пор, как оператор её открыл. Отдельно от version_conflict: там речь про
  // бронь, и оператору полезно знать, что именно устарело.
  stale_version: 'op.error.code.staleSessionVersion',
  idempotency_key_required: 'op.error.code.idempotencyKeyRequired',
  idempotency_conflict: 'op.error.code.idempotencyConflict',
  session_start_invalid: 'op.error.code.sessionStartInvalid',
  session_start_conflict: 'op.error.code.sessionStartConflict',
  plan_limit_reached: 'op.error.code.planLimitReached',
  // Касса и смены: до сих пор эти отказы не имели машинного имени, и кассир видел на экране
  // английскую фразу сервера вместе с сырым телом ответа.
  shift_already_open: 'op.error.code.shiftAlreadyOpen',
  shift_already_closed: 'op.error.code.shiftAlreadyClosed',
  shift_currency_mismatch: 'op.error.code.shiftCurrencyMismatch',
  shift_sign_off_required: 'op.error.code.shiftSignOffRequired',
  shift_sign_off_must_differ: 'op.error.code.shiftSignOffMustDiffer',
  shift_sign_off_not_authorized: 'op.error.code.shiftSignOffNotAuthorized',
  cash_movement_needs_open_shift: 'op.error.code.cashMovementNeedsOpenShift',
  tariff_name_taken: 'op.error.code.tariffNameTaken',
  // Брони: отказ почти всегда про состояние, которое успело измениться, — сосед подтвердил её
  // раньше, гость уже сидит, заявку уже отклонили. Дальше оператор делает разное, поэтому
  // отличать их нужно, а не сводить к одному «не получилось».
  reservation_not_pending: 'op.error.code.reservationNotPending',
  reservation_not_changeable: 'op.error.code.reservationNotChangeable',
  reservation_not_seatable: 'op.error.code.reservationNotSeatable',
  reservation_seat_required: 'op.error.code.reservationSeatRequired',
  reservation_not_cancellable: 'op.error.code.reservationNotCancellable',
  reservation_cancel_reason_required: 'op.error.code.reservationCancelReasonRequired',
  reservation_not_rejectable: 'op.error.code.reservationNotRejectable',
  reservation_refusal_note_required: 'op.error.code.reservationRefusalNoteRequired',
  reservation_reject_reason_unsupported: 'op.error.code.reservationRejectReasonUnsupported',
  reservation_no_show_not_allowed: 'op.error.code.reservationNoShowNotAllowed'
} as const satisfies Record<string, MessageKey>;

/**
 * Сессию изменили с тех пор, как оператор её открыл.
 *
 * Отдельный признак, а не просто 409: экран на него обновляет карту, и отличать этот отказ от
 * прочих конфликтов нужно именно для этого.
 */
export function isStaleSessionVersion(error: unknown): boolean {
  if (!(error instanceof PlatformApiError) || error.status !== 409) return false;
  try {
    return (JSON.parse(error.body) as { code?: string }).code === 'stale_version';
  } catch {
    return false;
  }
}

/// Отказ «сумма выше порога сотрудника, но операция может быть проведена по одобрению».
/// Сервер отвечает на него 409 с `requiresApproval: true` (EndpointHelpers.Audit) — это не
/// ошибка ввода и не запрет, а развилка: то же действие можно отправить старшему.
export function requiresManagerApproval(error: unknown): boolean {
  if (!(error instanceof PlatformApiError) || error.status !== 409) return false;
  try {
    const parsed = JSON.parse(error.body) as { requiresApproval?: unknown };
    return parsed.requiresApproval === true;
  } catch {
    return false;
  }
}

interface PlanLimitBody extends Record<string, number> {
  limit: number;
  current: number;
}

function readPlanLimit(body: string): PlanLimitBody | null {
  try {
    const parsed = JSON.parse(body) as { planLimit?: { limit?: unknown; current?: unknown } };
    const limit = parsed.planLimit?.limit;
    const current = parsed.planLimit?.current;
    return typeof limit === 'number' && typeof current === 'number' ? { limit, current } : null;
  } catch {
    return null;
  }
}

/**
 * Что сказать, когда сервер отказал, но своего имени отказу не дал.
 *
 * Не «что-то пошло не так»: статус всё же отличает «нет прав» от «уже изменили» и от «сервер
 * упал», а это три разных следующих шага для того, кто стоит за кассой.
 */
function statusMessageKey(status: number): MessageKey {
  if (status === 401 || status === 403) return 'op.error.status.forbidden';
  if (status === 404) return 'op.error.status.notFound';
  if (status === 409 || status === 412 || status === 422) return 'op.error.status.conflict';
  if (status >= 500 || status === 0) return 'op.error.status.server';
  return 'op.error.status.invalid';
}

/**
 * Фраза для отказа, который сервер назвал знакомым кодом, — или null, если не назвал.
 *
 * Отдельно от projectOperatorError: у кассы есть свои формулировки для части кодов, и ей нужно
 * спросить общий словарь только про остальные, не подменяя своим «не удалось» то, что словарь
 * умеет объяснить точно.
 */
export function knownErrorMessage(error: unknown, t: TFn): string | null {
  if (!(error instanceof PlatformApiError)) return null;
  const code = readKnownErrorCode(error.body);
  if (code === null) return null;
  const planLimit = code === 'plan_limit_reached' ? readPlanLimit(error.body) : null;
  return t(codeMessageKeys[code], planLimit ?? undefined);
}

// Коды, которые сами просят обновить и повторить: данные изменились у соседа, и свежий запрос
// получит свежий ответ. Остальные известные коды — правила бизнеса, их повтор не меняет.
const codesRetryResolves: ReadonlySet<keyof typeof codeMessageKeys> = new Set([
  'version_conflict',
  'stale_version',
  'idempotency_key_required'
]);

/**
 * Поможет ли повтор того же запроса.
 *
 * Проходят сбои, которые зависят от момента: нет связи, сервер упал или не успел (5xx, 408),
 * попросил подождать (429), данные успели измениться (409 без кода). Не проходят отказы, ответ
 * на которые повтор не изменит: вход истёк (401), не хватает прав (403), того уже нет (404),
 * сервер не принял данные (400, 422), отказ по правилу бизнеса. Неизвестный отказ повтор не
 * запрещает: спрятать кнопку там, где она помогла бы, хуже, чем показать лишнюю.
 */
export function retryCanHelp(error: unknown): boolean {
  if (error instanceof PermissionRefusal) return false;
  if (!(error instanceof PlatformApiError)) return true;
  if (requiresManagerApproval(error)) return false;
  const code = readKnownErrorCode(error.body);
  if (code !== null) return codesRetryResolves.has(code);
  const { status } = error;
  return status === 0 || status === 408 || status === 409 || status === 412 || status === 429 || status >= 500;
}

// 401 — истёкший вход, а не нехватка прав: к управляющему с ним идти незачем.
function isAccessRefusal(error: unknown): boolean {
  return error instanceof PermissionRefusal || (error instanceof PlatformApiError && error.status === 403);
}

export function projectOperatorError(error: unknown, t: TFn): OperatorErrorProjection {
  const title = t('op.error.actionFailed.title');
  const next = {
    retryCanHelp: retryCanHelp(error),
    accessHint: isAccessRefusal(error) ? t('op.error.accessHint') : undefined
  };

  if (error instanceof PlatformApiError) {
    const known = knownErrorMessage(error, t);
    if (known !== null) {
      return { title, detail: known, ...next };
    }

    // Текст самой PlatformApiError — диагностика для журнала: «Platform API returned 400 Bad
    // Request: {"Error":"An open shift already exists for this branch."}». До сих пор он ехал
    // прямо на экран кассы: английская фраза и сырой JSON вместо причины.
    return { title, detail: t(statusMessageKey(error.status)), ...next };
  }

  // Сбой до ответа сервера: fetch бросает TypeError («Failed to fetch», «NetworkError…»).
  // Это текст браузера, на русском экране он читается как поломка программы.
  if (error instanceof TypeError) {
    return { title, detail: t('op.error.status.network'), ...next };
  }

  // Остальные Error приложение бросает само и уже локализованными (throw new Error(t(...))).
  if (error instanceof Error && error.message.trim().length > 0) {
    return { title, detail: error.message, ...next };
  }

  if (typeof error === 'string' && error.trim().length > 0) {
    return { title, detail: error, ...next };
  }

  return { title, detail: t('op.error.actionFailed.noDetail'), ...next };
}

// Код отказа сервер называет двумя именами. Старт брони отвечает `code` и кладёт рядом
// человеческое пояснение в `error`; касса, склад и смены отвечают одним только `error`, и там
// код лежит именно в нём. Читаем оба поля и берём то, что нашлось в словаре: по одному `code`
// половина отказов доезжала бы до оператора английской строкой из бэкенда.
//
// Свободный текст (`error: "End time must be after start time."`) словарю не принадлежит и
// проходит мимо — его покажет запасная ветка, как показывала всегда.
function readKnownErrorCode(body: string): keyof typeof codeMessageKeys | null {
  try {
    const parsed = JSON.parse(body) as { code?: unknown; error?: unknown };
    for (const candidate of [parsed.code, parsed.error]) {
      if (typeof candidate === 'string' && candidate in codeMessageKeys) {
        return candidate as keyof typeof codeMessageKeys;
      }
    }
    return null;
  } catch {
    return null;
  }
}
