import type { MessageKey } from '@afk4/i18n';
import type { PcControlActionId } from './operatorTypes';
import type { SeatSummary } from './operatorData';

// Что делает пункт меню. Живые действия исполняются сразу (старт гостя, продление, управление ПК);
// «скоро» — честный toast про отсутствующий бэкенд (#37), без вранья про готовность.
// Завершение/оплата/пересадка/билленый старт сюда НЕ попадают: они требуют подтверждения и quote,
// которые живут в карточке справа (правый клик уже выбирает место и раскрывает карточку).
export type SeatMenuRun =
  | { kind: 'start-guest' }
  | { kind: 'extend'; minutes: number }
  | { kind: 'pc'; action: PcControlActionId }
  | { kind: 'soon'; detailKey: MessageKey };

export interface SeatMenuCaps {
  // Бэкенд готов к живым действиям над сессией (не fixture/offline-загрузка).
  actionsEnabled: boolean;
  canStart: boolean;
  canExtend: boolean;
  canLockUnlock: boolean;
}

export interface SeatMenuItem {
  id: string;
  labelKey: MessageKey;
  // Длинная подпись для строки фидбэка (e.g. «Статус ПК» против короткого «Статус» в меню).
  feedbackKey: MessageKey;
  hintKey?: MessageKey;
  run: SeatMenuRun;
  disabled: boolean;
  soon: boolean;
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
 * `disabled` — только временные причины (бэкенд не готов). «Скоро»-пункты показываем всегда,
 * когда контекст уместен — это честная карта возможностей, а не заглушка-обманка.
 */
export function buildSeatMenu(seat: SeatSummary, caps: SeatMenuCaps): SeatMenuSection[] {
  const hasSession = seatHasSession(seat);
  const isFree = seat.tone === 'ready' && !seat.activeSessionId && !hasSession;
  const hasDevice = Boolean(seat.deviceId);

  const session: SeatMenuItem[] = [];
  if (isFree && caps.canStart) {
    session.push({
      id: 'start-guest',
      labelKey: 'op.map.seatInvite',
      feedbackKey: 'op.map.seatInvite',
      run: { kind: 'start-guest' },
      disabled: !caps.actionsEnabled,
      soon: false
    });
  } else if (hasSession && caps.canExtend) {
    session.push({
      id: 'extend-15',
      labelKey: 'op.map.panel.extend15Action',
      feedbackKey: 'op.map.panel.extend15Action',
      run: { kind: 'extend', minutes: 15 },
      disabled: !caps.actionsEnabled,
      soon: false
    });
    session.push({
      id: 'extend-30',
      labelKey: 'op.map.panel.extend30Action',
      feedbackKey: 'op.map.panel.extend30Action',
      run: { kind: 'extend', minutes: 30 },
      disabled: !caps.actionsEnabled,
      soon: false
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
      disabled: false,
      soon: false
    });
    if (hasSession) {
      pc.push({
        id: 'pc-unlock',
        labelKey: 'op.map.actionUnlockBtn',
        feedbackKey: 'op.map.actionUnlock',
        run: { kind: 'pc', action: 'unlock' },
        disabled: false,
        soon: false
      });
    }
  }

  const soon: SeatMenuItem[] = [];
  if (hasDevice && caps.canLockUnlock) {
    soon.push({ id: 'soon-reboot', labelKey: 'op.map.rebootBtn', feedbackKey: 'op.map.actionReboot', hintKey: 'op.map.rebootHint', run: { kind: 'soon', detailKey: 'op.map.rebootDetail' }, disabled: false, soon: true });
    soon.push({ id: 'soon-shutdown', labelKey: 'op.map.shutdownBtn', feedbackKey: 'op.map.actionShutdown', hintKey: 'op.map.shutdownHint', run: { kind: 'soon', detailKey: 'op.map.shutdownDetail' }, disabled: false, soon: true });
    soon.push({ id: 'soon-wake', labelKey: 'op.map.wakeBtn', feedbackKey: 'op.map.actionWake', hintKey: 'op.map.wakeHint', run: { kind: 'soon', detailKey: 'op.map.wakeDetail' }, disabled: false, soon: true });
  }
  if (hasSession) {
    soon.push({ id: 'soon-active-window', labelKey: 'op.map.menu.activeWindow', feedbackKey: 'op.map.menu.activeWindow', hintKey: 'op.map.menu.activeWindowHint', run: { kind: 'soon', detailKey: 'op.map.menu.activeWindowDetail' }, disabled: false, soon: true });
    soon.push({ id: 'soon-fine', labelKey: 'op.map.menu.fine', feedbackKey: 'op.map.menu.fine', hintKey: 'op.map.menu.fineHint', run: { kind: 'soon', detailKey: 'op.map.menu.fineDetail' }, disabled: false, soon: true });
    soon.push({ id: 'soon-notify', labelKey: 'op.map.menu.notify', feedbackKey: 'op.map.menu.notify', hintKey: 'op.map.menu.notifyHint', run: { kind: 'soon', detailKey: 'op.map.menu.notifyDetail' }, disabled: false, soon: true });
  }

  const sections: SeatMenuSection[] = [
    { id: 'session', titleKey: null, items: session },
    { id: 'pc', titleKey: 'op.map.menu.sectionPc', items: pc },
    { id: 'soon', titleKey: 'op.map.menu.sectionSoon', items: soon }
  ];
  return sections.filter((section) => section.items.length > 0);
}
