import type { MessageKey } from '@afk4/i18n';
import type { SeatSummary } from '../operatorData';
import { pcCommandsFor, type PcCommandAccess } from './pcCommandOptions';
import type { PcCommandOrLock } from './pcCommandCopy';

/**
 * Короткая причина для списка «не получат» и подсказки в меню: полная фраза карточки места,
 * повторённая у каждого из двенадцати ПК, превращает список в стену текста.
 */
export function shortBlockReason(reason: MessageKey): MessageKey {
  if (reason === 'op.pc.blocked.session') return 'op.pc.bulk.skip.session';
  if (reason === 'op.pc.blocked.offline') return 'op.pc.bulk.skip.offline';
  return reason;
}

/** Порядок команд в полосе и меню выбранных мест: сначала частое и безопасное. */
export const BULK_COMMAND_ORDER: readonly PcCommandOrLock[] = [
  'lock', 'message', 'sign-out', 'wake', 'reboot', 'shutdown', 'maintenance-on', 'maintenance-off'
];

export interface BulkSkip {
  seat: SeatSummary;
  reason: MessageKey;
}

export interface BulkPlan {
  command: PcCommandOrLock;
  targets: SeatSummary[];
  skipped: BulkSkip[];
}

/**
 * Кому из выбранных уйдёт команда и почему не уйдёт остальным. Правило для каждого места то же,
 * что у его карточки: сервер всё равно проверит его сам, а человек должен знать заранее, что из
 * двенадцати выбранных перезагрузятся девять.
 */
export function planBulk(seats: SeatSummary[], command: PcCommandOrLock, access: PcCommandAccess): BulkPlan {
  const targets: SeatSummary[] = [];
  const skipped: BulkSkip[] = [];
  for (const seat of seats) {
    const reason = blockReason(seat, command, access);
    if (reason === null) {
      targets.push(seat);
    } else {
      skipped.push({ seat, reason });
    }
  }

  return { command, targets, skipped };
}

/** Команды, которые можно отправить хотя бы одному из выбранных. */
export function bulkCommands(seats: SeatSummary[], access: PcCommandAccess): PcCommandOrLock[] {
  return BULK_COMMAND_ORDER.filter((command) =>
    // Выключенному нечего будить и выводить из обслуживания, если никто не на нём, — такие команды
    // в полосе только путали бы; остальные показываем, даже если их пока некому отправить.
    command === 'wake' || command === 'maintenance-off'
      ? seats.some((seat) => blockReason(seat, command, access) === null)
      : allowedAtAll(command, access));
}

function allowedAtAll(command: PcCommandOrLock, access: PcCommandAccess): boolean {
  return command === 'maintenance-on' || command === 'maintenance-off' ? access.canMaintain : access.canDispatch;
}

function blockReason(seat: SeatSummary, command: PcCommandOrLock, access: PcCommandAccess): MessageKey | null {
  if (!seat.deviceId) return 'op.pc.bulk.skip.noDevice';
  // Консоль в общей команде пропускается с причиной: агента у неё нет, команду выполнить некому.
  if (seat.isConsole) return 'op.pc.bulk.skip.console';
  if (!allowedAtAll(command, access)) return 'op.pc.bulk.skip.notAllowed';

  if (command === 'lock') {
    return null;
  }

  const option = pcCommandsFor(seat, access).find((candidate) => candidate.id === command);
  if (option) return option.blockedReason === null ? null : shortBlockReason(option.blockedReason);

  // Команды нет в карточке этого места — назовём почему, а не «нельзя».
  if (command === 'wake') return 'op.pc.bulk.skip.alreadyOn';
  if (command === 'maintenance-off') return 'op.pc.bulk.skip.notInMaintenance';
  if (command === 'maintenance-on') return 'op.pc.bulk.skip.alreadyInMaintenance';
  return 'op.pc.bulk.skip.notAllowed';
}
