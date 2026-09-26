import type { MessageKey } from '@afk4/i18n';
import type { PcControlActionId } from './operatorTypes';
import type { SeatSummary } from './operatorData';
import { pcCommandsFor, type PcCommandId } from './pc/pcCommandOptions';
import { PC_COMMAND_LABELS, type PcCommandOrLock } from './pc/pcCommandCopy';
import { bulkCommands, shortBlockReason } from './pc/pcBulk';

// Что делает пункт меню. Завершение, оплата, пересадка и старт с тарифом сюда не попадают: им
// нужны расчёт и подтверждение, которые живут в карточке справа (правый клик уже выбирает место
// и раскрывает карточку).
//
// Пунктов «скоро» здесь тоже нет: меню, где половина пунктов не работает, перестаёт быть картой
// возможностей — оно просто врёт. Команды ПК, которые спрашивают «точно?» или просят текст,
// открывают окно подтверждения: пункт меню не исполняет их молча.
export type SeatMenuRun =
  | { kind: 'start-guest' }
  | { kind: 'extend'; minutes: number }
  | { kind: 'pause' }
  | { kind: 'resume' }
  | { kind: 'pc'; action: PcControlActionId }
  | { kind: 'pc-command'; command: PcCommandId }
  | { kind: 'bulk'; command: PcCommandOrLock }
  | { kind: 'resolve-assistance' };

export interface SeatMenuCaps {
  // Бэкенд готов к живым действиям над сессией (не fixture/offline-загрузка).
  actionsEnabled: boolean;
  canStart: boolean;
  canExtend: boolean;
  canLockUnlock: boolean;
  canResolveAssistance: boolean;
  canPause: boolean;
  /** organization.devices.maintenance — обслуживание, своё право. */
  canMaintain: boolean;
}

export interface SeatMenuItem {
  id: string;
  labelKey: MessageKey;
  // Длинная подпись для строки фидбэка (e.g. «Статус ПК» против короткого «Статус» в меню).
  feedbackKey: MessageKey;
  hintKey?: MessageKey;
  run: SeatMenuRun;
  disabled: boolean;
}

export interface SeatMenuSection {
  id: string;
  titleKey: MessageKey | null;
  items: SeatMenuItem[];
}

function seatHasSession(seat: SeatSummary): boolean {
  return Boolean(seat.activeSessionId) || seat.hasActiveSession === true || seat.tone === 'active';
}

/**
 * Чистый билдер состава контекст-меню по состоянию места и правам оператора.
 * Присутствие пункта = есть право (нерелевантное роли не показываем);
 * `disabled` — только временные причины (бэкенд не готов).
 */
export function buildSeatMenu(seat: SeatSummary, caps: SeatMenuCaps): SeatMenuSection[] {
  const hasSession = seatHasSession(seat);
  const isFree = seat.tone === 'ready' && !seat.activeSessionId && !hasSession;
  // У консоли нет агента — запереть, отпереть или перезагрузить её некому.
  const hasDevice = Boolean(seat.deviceId) && !seat.isConsole;

  // Вызов оператора идёт первым пунктом: место зовёт человека, а не ждёт настройки.
  const session: SeatMenuItem[] = [];
  if (seat.assistanceRequestedAtUtc && caps.canResolveAssistance) {
    session.push({
      id: 'resolve-assistance',
      labelKey: 'op.map.menu.resolveAssistance',
      feedbackKey: 'op.map.actionResolveAssistance',
      hintKey: 'op.map.menu.resolveAssistanceHint',
      run: { kind: 'resolve-assistance' },
      disabled: !caps.actionsEnabled
    });
  }

  if (isFree && caps.canStart) {
    session.push({
      id: 'start-guest',
      labelKey: 'op.map.seatInvite',
      feedbackKey: 'op.map.seatInvite',
      run: { kind: 'start-guest' },
      disabled: !caps.actionsEnabled
    });
  } else if (hasSession && caps.canExtend) {
    session.push({
      id: 'extend-15',
      labelKey: 'op.map.panel.extend15Action',
      feedbackKey: 'op.map.panel.extend15Action',
      run: { kind: 'extend', minutes: 15 },
      disabled: !caps.actionsEnabled
    });
    session.push({
      id: 'extend-30',
      labelKey: 'op.map.panel.extend30Action',
      feedbackKey: 'op.map.panel.extend30Action',
      run: { kind: 'extend', minutes: 30 },
      disabled: !caps.actionsEnabled
    });
  }

  // Пауза и снятие — одна кнопка в двух состояниях: ставить паузу на паузе нечего.
  if (hasSession && caps.canPause) {
    const paused = seat.sessionState === 'Paused';
    session.push({
      id: paused ? 'session-resume' : 'session-pause',
      labelKey: paused ? 'op.map.menu.resume' : 'op.map.menu.pause',
      feedbackKey: paused ? 'op.map.actionResume' : 'op.map.actionPause',
      hintKey: paused ? 'op.map.menu.resumeHint' : 'op.map.menu.pauseHint',
      run: paused ? { kind: 'resume' } : { kind: 'pause' },
      disabled: !caps.actionsEnabled
    });
  }

  // Статус ПК больше не пункт меню — он живёт единым блоком внизу карточки места.
  const pc: SeatMenuItem[] = [];
  if (hasDevice && caps.canLockUnlock) {
    pc.push({
      id: 'pc-lock',
      labelKey: 'op.map.actionLockBtn',
      feedbackKey: 'op.map.actionLock',
      run: { kind: 'pc', action: 'lock' },
      disabled: false
    });
    if (hasSession) {
      pc.push({
        id: 'pc-unlock',
        labelKey: 'op.map.actionUnlockBtn',
        feedbackKey: 'op.map.actionUnlock',
        run: { kind: 'pc', action: 'unlock' },
        disabled: false
      });
    }
  }

  // Остальные команды ПК — те же, что в карточке места, и закрыты по тем же причинам: почему,
  // сказано подсказкой у пункта, а не отказом после.
  for (const option of hasDevice ? pcCommandsFor(seat, { canDispatch: caps.canLockUnlock, canMaintain: caps.canMaintain }) : []) {
    pc.push({
      id: `pc-${option.id}`,
      labelKey: PC_COMMAND_LABELS[option.id],
      feedbackKey: PC_COMMAND_LABELS[option.id],
      hintKey: option.blockedReason === null ? undefined : shortBlockReason(option.blockedReason),
      run: { kind: 'pc-command', command: option.id },
      disabled: option.blockedReason !== null
    });
  }

  const sections: SeatMenuSection[] = [
    { id: 'session', titleKey: null, items: session },
    { id: 'pc', titleKey: 'op.map.menu.sectionPc', items: pc }
  ];
  return sections.filter((section) => section.items.length > 0);
}

/** Меню нескольких выбранных мест: только команды ПК, по одной на всех. */
export function buildBulkMenu(seats: SeatSummary[], caps: SeatMenuCaps): SeatMenuSection[] {
  const items = bulkCommands(seats, { canDispatch: caps.canLockUnlock, canMaintain: caps.canMaintain })
    .map((command): SeatMenuItem => ({
      id: `bulk-${command}`,
      labelKey: PC_COMMAND_LABELS[command],
      feedbackKey: PC_COMMAND_LABELS[command],
      run: { kind: 'bulk', command },
      disabled: false
    }));
  return items.length > 0 ? [{ id: 'bulk', titleKey: 'op.map.menu.sectionPc', items }] : [];
}
