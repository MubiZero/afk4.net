import { describe, expect, it } from 'bun:test';
import type { SeatSummary } from './operatorData';
import { buildBulkMenu, buildSeatMenu, type SeatMenuCaps, type SeatMenuItem } from './seatMenu';

function seat(overrides: Partial<SeatSummary>): SeatSummary {
  return {
    id: 's',
    zone: 'Зал A',
    name: 'PC-01',
    tone: 'ready',
    stateLabel: 'Свободно',
    player: 'Гость',
    remaining: 'Свободно',
    device: 'Device',
    command: 'Idle',
    app: 'Shell',
    deviceId: 'dev-1',
    ...overrides
  };
}

const allCaps: SeatMenuCaps = {
  actionsEnabled: true,
  canStart: true,
  canExtend: true,
  canLockUnlock: true,
  canResolveAssistance: true,
  canPause: true,
  canMaintain: true
};
const noCaps: SeatMenuCaps = {
  actionsEnabled: false,
  canResolveAssistance: false,
  canPause: false,
  canStart: false,
  canExtend: false,
  canLockUnlock: false,
  canMaintain: false
};

function flat(sections: ReturnType<typeof buildSeatMenu>): SeatMenuItem[] {
  return sections.flatMap((section) => section.items);
}
function ids(sections: ReturnType<typeof buildSeatMenu>): string[] {
  return flat(sections).map((item) => item.id);
}

describe('buildSeatMenu', () => {
  it('offers a one-tap guest seating on a free seat, never a billed/destructive op', () => {
    const sections = buildSeatMenu(seat({ tone: 'ready' }), allCaps);
    const all = ids(sections);
    expect(all).toContain('start-guest');
    // No extend on a free seat, and the menu never carries end/checkout/transfer (those live in the panel).
    expect(all).not.toContain('extend-15');
    expect(all.some((id) => id.startsWith('end') || id.startsWith('checkout') || id.startsWith('transfer'))).toBe(false);
  });

  it('leads an active seat with quick extends and a session-aware unlock', () => {
    const sections = buildSeatMenu(seat({ tone: 'active', activeSessionId: 'sess-1' }), allCaps);
    const all = ids(sections);
    expect(all).toContain('extend-15');
    expect(all).toContain('extend-30');
    expect(all).toContain('pc-unlock');
    expect(all).not.toContain('start-guest');
  });

  it('omits unlock when there is no session to unlock into', () => {
    const sections = buildSeatMenu(seat({ tone: 'ready' }), allCaps);
    expect(ids(sections)).not.toContain('pc-unlock');
  });

  it('disables — but still shows — live ops while the backend is not ready', () => {
    const sections = buildSeatMenu(seat({ tone: 'ready' }), { ...allCaps, actionsEnabled: false });
    const start = flat(sections).find((item) => item.id === 'start-guest');
    expect(start).toBeDefined();
    expect(start?.disabled).toBe(true);
  });

  it('hides PC actions a role cannot perform, and the device-less seat has none', () => {
    const noPc = buildSeatMenu(seat({ tone: 'ready' }), { ...allCaps, canLockUnlock: false, canMaintain: false });
    expect(ids(noPc).some((id) => id.startsWith('pc-'))).toBe(false);
    const noDevice = buildSeatMenu(seat({ tone: 'ready', deviceId: null }), allCaps);
    expect(ids(noDevice).some((id) => id.startsWith('pc-'))).toBe(false);
  });

  // Перезагрузка, выключение, wake-on-LAN, активное окно, штраф и «уведомить игрока» полгода
  // стояли в меню и отвечали тостом: команд для них нет ни в контракте устройств, ни в агенте.
  // Меню, где половина пунктов не работает, — не карта возможностей, а обман.
  it('в меню нет пунктов, за которыми нет команды', () => {
    const sections = buildSeatMenu(seat({ tone: 'active', activeSessionId: 'sess-1' }), allCaps);
    expect(flat(sections).every((item) => item.run.kind !== 'start-guest' || item.id === 'start-guest')).toBe(true);
    expect(ids(sections).some((id) => id.startsWith('soon-'))).toBe(false);
  });

  it('каждый пункт ведёт к живому действию, а не к тосту', () => {
    const live = new Set(['start-guest', 'extend', 'pause', 'resume', 'pc', 'pc-command']);
    const sections = buildSeatMenu(seat({ tone: 'active', activeSessionId: 'sess-1' }), allCaps);
    expect(flat(sections).every((item) => live.has(item.run.kind))).toBe(true);
  });

  // Полное меню места: команды ПК из карточки — и здесь, закрытые по тем же причинам.
  it('несёт команды ПК из карточки и говорит, почему занятый ПК не перезагрузить', () => {
    const free = buildSeatMenu(seat({ tone: 'ready' }), allCaps);
    expect(ids(free)).toEqual(expect.arrayContaining(['pc-reboot', 'pc-shutdown', 'pc-message', 'pc-sign-out', 'pc-maintenance-on']));

    const busy = flat(buildSeatMenu(seat({ tone: 'active', activeSessionId: 'sess-1' }), allCaps));
    const reboot = busy.find((item) => item.id === 'pc-reboot');
    expect(reboot?.disabled).toBe(true);
    expect(reboot?.hintKey).toBe('op.pc.bulk.skip.session');
    expect(busy.find((item) => item.id === 'pc-message')?.disabled).toBe(false);
  });

  it('обслуживание — своё право: без него пункта нет', () => {
    const sections = buildSeatMenu(seat({ tone: 'ready' }), { ...allCaps, canMaintain: false });
    expect(ids(sections)).not.toContain('pc-maintenance-on');
  });

  it('меню нескольких мест — только общие команды ПК', () => {
    const sections = buildBulkMenu([seat({ id: 'a' }), seat({ id: 'b', tone: 'active', activeSessionId: 's' })], allCaps);
    expect(flat(sections).every((item) => item.run.kind === 'bulk')).toBe(true);
    expect(ids(sections)).toEqual(expect.arrayContaining(['bulk-lock', 'bulk-reboot', 'bulk-message']));
    // Будить некого: все выбранные на связи.
    expect(ids(sections)).not.toContain('bulk-wake');
  });

  // Пауза и снятие — одна кнопка в двух состояниях: ставить паузу на паузе нечего.
  it('на паузе меню предлагает продолжить, а не паузу', () => {
    const active = buildSeatMenu(seat({ tone: 'active', activeSessionId: 'sess-1' }), allCaps);
    expect(ids(active)).toContain('session-pause');
    expect(ids(active)).not.toContain('session-resume');

    const paused = buildSeatMenu(
      seat({ tone: 'active', activeSessionId: 'sess-1', sessionState: 'Paused' }), allCaps);
    expect(ids(paused)).toContain('session-resume');
    expect(ids(paused)).not.toContain('session-pause');
  });

  it('без права паузы пункта нет', () => {
    const sections = buildSeatMenu(
      seat({ tone: 'active', activeSessionId: 'sess-1' }), { ...allCaps, canPause: false });
    expect(ids(sections)).not.toContain('session-pause');
  });

  it('returns an empty menu when the operator can do nothing here', () => {
    const sections = buildSeatMenu(seat({ tone: 'ready', deviceId: null }), noCaps);
    expect(sections).toHaveLength(0);
  });


  // Место зовёт оператора: снять вызов — первый пункт меню, до всего остального.
  it('зовущее место первым пунктом предлагает снять вызов', () => {
    const sections = buildSeatMenu(
      seat({ tone: 'active', activeSessionId: 'sess-1', assistanceRequestedAtUtc: '2026-09-16T10:00:00Z' }),
      allCaps);
    expect(ids(sections)[0]).toBe('resolve-assistance');
  });

  it('без права снимать вызов пункта нет, даже когда место зовёт', () => {
    const sections = buildSeatMenu(
      seat({ tone: 'active', activeSessionId: 'sess-1', assistanceRequestedAtUtc: '2026-09-16T10:00:00Z' }),
      { ...allCaps, canResolveAssistance: false });
    expect(ids(sections)).not.toContain('resolve-assistance');
  });

  it('место, которое не зовёт, пункта не показывает', () => {
    const sections = buildSeatMenu(seat({ tone: 'active', activeSessionId: 'sess-1' }), allCaps);
    expect(ids(sections)).not.toContain('resolve-assistance');
  });
});
