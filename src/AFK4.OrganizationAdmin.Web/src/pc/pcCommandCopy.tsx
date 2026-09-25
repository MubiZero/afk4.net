import type { ReactNode } from 'react';
import { LockKeyhole, LogOut, MessageSquare, Power, RotateCw, Sunrise, Wrench } from 'lucide-react';
import type { MessageKey } from '@afk4/i18n';
import type { PcCommandId } from './pcCommandOptions';

/** Команда ПК, которую можно отправить из карточки, меню места или сразу нескольким местам. */
export type PcCommandOrLock = PcCommandId | 'lock';

export const PC_COMMAND_LABELS: Record<PcCommandOrLock, MessageKey> = {
  lock: 'op.map.actionLockBtn',
  reboot: 'op.pc.reboot',
  shutdown: 'op.pc.shutdown',
  wake: 'op.pc.wake',
  'maintenance-on': 'op.pc.maintenanceOn',
  'maintenance-off': 'op.pc.maintenanceOff',
  'sign-out': 'op.pc.signOut',
  message: 'op.pc.message'
};

export const PC_COMMAND_ICONS: Record<PcCommandOrLock, ReactNode> = {
  lock: <LockKeyhole size={14} aria-hidden="true" />,
  reboot: <RotateCw size={14} aria-hidden="true" />,
  shutdown: <Power size={14} aria-hidden="true" />,
  wake: <Sunrise size={14} aria-hidden="true" />,
  'maintenance-on': <Wrench size={14} aria-hidden="true" />,
  'maintenance-off': <Wrench size={14} aria-hidden="true" />,
  'sign-out': <LogOut size={14} aria-hidden="true" />,
  message: <MessageSquare size={14} aria-hidden="true" />
};

export type PcConfirmCopy = { title: MessageKey; impact: MessageKey; confirm: MessageKey };

/** Что спросить перед командой. Нет записи — команда безопасна и уходит сразу. */
export const PC_COMMAND_CONFIRM: Partial<Record<PcCommandOrLock, PcConfirmCopy>> = {
  reboot: { title: 'op.pc.confirm.rebootTitle', impact: 'op.pc.confirm.rebootImpact', confirm: 'op.pc.reboot' },
  shutdown: { title: 'op.pc.confirm.shutdownTitle', impact: 'op.pc.confirm.shutdownImpact', confirm: 'op.pc.shutdown' },
  'maintenance-on': { title: 'op.pc.confirm.maintenanceTitle', impact: 'op.pc.confirm.maintenanceImpact', confirm: 'op.pc.maintenanceOn' },
  'sign-out': { title: 'op.pc.confirm.signOutTitle', impact: 'op.pc.confirm.signOutImpact', confirm: 'op.pc.confirm.signOutButton' },
  message: { title: 'op.pc.confirm.messageTitle', impact: 'op.pc.confirm.messageImpact', confirm: 'op.pc.confirm.messageSend' }
};

/**
 * Подтверждение для нескольких мест. Даже безопасная команда на десяток ПК спрашивает: человеку
 * надо увидеть список, кому она уйдёт и кому нет, прежде чем зал разом перезагрузится.
 */
export const PC_BULK_CONFIRM: Record<PcCommandOrLock, PcConfirmCopy> = {
  lock: { title: 'op.pc.confirm.lockTitle', impact: 'op.pc.confirm.lockImpact', confirm: 'op.map.actionLockBtn' },
  wake: { title: 'op.pc.confirm.wakeTitle', impact: 'op.pc.confirm.wakeImpact', confirm: 'op.pc.wake' },
  'maintenance-off': { title: 'op.pc.confirm.maintenanceOffTitle', impact: 'op.pc.confirm.maintenanceOffImpact', confirm: 'op.pc.maintenanceOff' },
  reboot: PC_COMMAND_CONFIRM.reboot!,
  shutdown: PC_COMMAND_CONFIRM.shutdown!,
  'maintenance-on': PC_COMMAND_CONFIRM['maintenance-on']!,
  'sign-out': PC_COMMAND_CONFIRM['sign-out']!,
  message: PC_COMMAND_CONFIRM.message!
};

export const DANGEROUS_PC_COMMANDS: ReadonlySet<PcCommandOrLock> = new Set(['reboot', 'shutdown']);
