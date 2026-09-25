import type { MessageKey } from '@afk4/i18n';
import type { SeatSummary } from '../operatorData';

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
  if (!seat.deviceId) return [];

  const busy = sessionRuns(seat);
  const offline = seat.isDeviceOnline === false;
  const unreachable: MessageKey | null = offline ? 'op.pc.blocked.offline' : null;
  const onlyFree: MessageKey | null = busy ? 'op.pc.blocked.session' : null;
  const options: PcCommandOption[] = [];

  if (access.canDispatch) {
    // Выключенный ПК команду не получит — его будит сосед по сети; включённому будить нечего.
    if (offline) {
      options.push({ id: 'wake', blockedReason: null, confirm: null });
    }
    options.push({ id: 'reboot', blockedReason: unreachable ?? onlyFree, confirm: 'danger' });
    options.push({ id: 'shutdown', blockedReason: unreachable ?? onlyFree, confirm: 'danger' });
    options.push({ id: 'message', blockedReason: unreachable, confirm: 'text' });
    options.push({ id: 'sign-out', blockedReason: unreachable, confirm: 'warning' });
  }

  if (access.canMaintain) {
    options.push(
      seat.maintenanceSinceUtc
        ? { id: 'maintenance-off', blockedReason: null, confirm: null }
        : { id: 'maintenance-on', blockedReason: onlyFree, confirm: 'warning' }
    );
  }

  return options;
}

/** Предел сообщения — тот же, что у сервера: длиннее поверх игры не прочтут. */
export const PC_MESSAGE_MAX_LENGTH = 500;
