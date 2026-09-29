import type { MessageKey } from '@afk4/i18n';
import type { SeatSummary } from '../operatorData';
import { isPendingSeatCommand } from '../floorMapState';

/** Команды ПК из карточки места, кроме блокировки: у той свой ряд. */
export type PcCommandId = 'reboot' | 'shutdown' | 'wake' | 'maintenance-on' | 'maintenance-off' | 'sign-out' | 'message';

export interface PcCommandAccess {
  /** organization.devices.commands.dispatch */
  canDispatch: boolean;
  /** organization.devices.maintenance — обслуживание закрывает ПК для игроков, это своё право. */
  canMaintain: boolean;
}

export interface PcCommandOption {
  id: PcCommandId;
  /** Почему сейчас нельзя; null — можно. Кнопка остаётся на месте: пропавшая кнопка ничего не объясняет. */
  blockedReason: MessageKey | null;
  /** Что спросить перед отправкой: опасные — «точно?», сообщение — текст. */
  confirm: 'danger' | 'warning' | 'text' | null;
}

function sessionRuns(seat: SeatSummary): boolean {
  return Boolean(seat.activeSessionId) || seat.hasActiveSession === true || seat.tone === 'active';
}

/**
 * Что можно сделать с ПК места. Сервер проверяет то же самое — «только свободный ПК» для питания и
 * обслуживания, — а здесь это объясняется до нажатия, а не отказом после.
 */
export function pcCommandsFor(seat: SeatSummary, access: PcCommandAccess): PcCommandOption[] {
  // У консоли нет агента: команду ПК выполнить некому, а сервер отвечал «выполнено · no_agent».
  if (!seat.deviceId || seat.isConsole) return [];

  const busy = sessionRuns(seat);
  const offline = seat.isDeviceOnline === false;
  // Прошлая команда ещё не ответила — вторая команда либо продублирует её, либо ударит по не
  // подтверждённому состоянию. Место и так это показывает тоном «ожидание».
  const pending = isPendingSeatCommand(seat);
  const unreachable: MessageKey | null = offline
    ? 'op.pc.blocked.offline'
    : pending
      ? 'op.pc.blocked.pending'
      : null;
  const onlyFree: MessageKey | null = unreachable ?? (busy ? 'op.pc.blocked.session' : null);
  // Сообщение и выход из аккаунта нужны игроку на ПК — на свободном месте отправлять их некому.
  const needsPlayer: MessageKey | null = unreachable ?? (busy ? null : 'op.pc.blocked.noPlayer');
  const options: PcCommandOption[] = [];

  if (access.canDispatch) {
    // Выключенный ПК команду не получит — его будит сосед по сети; включённому будить нечего.
    if (offline) {
      options.push({ id: 'wake', blockedReason: null, confirm: null });
    }
    options.push({ id: 'reboot', blockedReason: onlyFree, confirm: 'danger' });
    options.push({ id: 'shutdown', blockedReason: onlyFree, confirm: 'danger' });
    options.push({ id: 'message', blockedReason: needsPlayer, confirm: 'text' });
    options.push({ id: 'sign-out', blockedReason: needsPlayer, confirm: 'warning' });
  }

  if (access.canMaintain) {
    options.push(
      seat.maintenanceSinceUtc
        // Возврат в зал не смотрит на связь (её нет и у неподтверждённого ПК), но не спорит со
        // своей же командой в полёте.
        ? { id: 'maintenance-off', blockedReason: pending ? 'op.pc.blocked.pending' : null, confirm: null }
        : { id: 'maintenance-on', blockedReason: onlyFree, confirm: 'warning' }
    );
  }

  return options;
}

/** Предел сообщения — тот же, что у сервера: длиннее поверх игры не прочтут. */
export const PC_MESSAGE_MAX_LENGTH = 500;

export type PcLockCommandId = 'lock' | 'unlock';

export interface PcLockOption {
  id: PcLockCommandId;
  /** Кнопка недоступна — команда не изменит состояние или до ПК не достучаться. */
  disabled: boolean;
  /** Пояснение у кнопки: причина отказа при disabled, иначе — пометка (статус не подтверждён). */
  hintKey: MessageKey | null;
}

/**
 * Блокировка/разблокировка ПК места — своя пара кнопок (карточка места держит их отдельным рядом
 * от pcCommandsFor, но правила теперь общие для панели и контекстного меню). Доступна только та
 * кнопка, что меняет состояние: разблокированному ПК нечего блокировать второй раз, заблокированному
 * — разблокировать второй раз. Статус блокировки не подтверждён (например, свежее место без
 * сердцебиения) — не гадаем, какая из двух окажется вхолостую: даём обе, но говорим, что статус
 * не подтверждён.
 */
export function pcLockCommandsFor(seat: SeatSummary): PcLockOption[] {
  if (!seat.deviceId || seat.isConsole) return [];

  if (seat.isDeviceOnline === false) {
    return [
      { id: 'lock', disabled: true, hintKey: 'op.pc.blocked.offline' },
      { id: 'unlock', disabled: true, hintKey: 'op.pc.blocked.offline' }
    ];
  }

  if (isPendingSeatCommand(seat)) {
    return [
      { id: 'lock', disabled: true, hintKey: 'op.pc.blocked.pending' },
      { id: 'unlock', disabled: true, hintKey: 'op.pc.blocked.pending' }
    ];
  }

  if (seat.isDeviceLocked === true) {
    return [
      { id: 'lock', disabled: true, hintKey: 'op.pc.blocked.alreadyLocked' },
      { id: 'unlock', disabled: false, hintKey: null }
    ];
  }

  if (seat.isDeviceLocked === false) {
    return [
      { id: 'lock', disabled: false, hintKey: null },
      { id: 'unlock', disabled: true, hintKey: 'op.pc.blocked.alreadyUnlocked' }
    ];
  }

  return [
    { id: 'lock', disabled: false, hintKey: 'op.pc.blocked.lockUnknown' },
    { id: 'unlock', disabled: false, hintKey: 'op.pc.blocked.lockUnknown' }
  ];
}
