import type { MessageKey } from '@afk4/i18n';
import type { PcControlActionId } from './operatorTypes';
import type { SeatSummary } from './operatorData';

// Что делает пункт меню. Здесь только то, что исполняется сразу: старт гостя, продление,
// управление ПК. Завершение/оплата/пересадка/билленый старт сюда НЕ попадают: они требуют
// подтверждения и quote, которые живут в карточке справа (правый клик уже выбирает место и
// раскрывает карточку).
//
// Пунктов «скоро» здесь тоже нет. Перезагрузка, выключение, wake-on-LAN, активное окно, штраф и
// «уведомить игрока» полгода стояли в меню и отвечали тостом: команд для них нет ни в контракте
// устройств, ни в агенте. Меню, где половина пунктов не работает, перестаёт быть картой
// возможностей — оно просто врёт. Вернутся вместе с командами на игровом ПК.
export type SeatMenuRun =
  | { kind: 'start-guest' }
  | { kind: 'extend'; minutes: number }
  | { kind: 'pc'; action: PcControlActionId }
  | { kind: 'resolve-assistance' };

export interface SeatMenuCaps {
  // Бэкенд готов к живым действиям над сессией (не fixture/offline-загрузка).
  actionsEnabled: boolean;
  canStart: boolean;
  canExtend: boolean;
  canLockUnlock: boolean;
  canResolveAssistance: boolean;
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
  const hasDevice = Boolean(seat.deviceId);

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

  const sections: SeatMenuSection[] = [
    { id: 'session', titleKey: null, items: session },
    { id: 'pc', titleKey: 'op.map.menu.sectionPc', items: pc }
  ];
  return sections.filter((section) => section.items.length > 0);
}
