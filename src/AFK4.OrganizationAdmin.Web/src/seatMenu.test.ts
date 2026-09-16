import { describe, expect, it } from 'bun:test';
import type { SeatSummary } from './operatorData';
import { buildSeatMenu, type SeatMenuCaps, type SeatMenuItem } from './seatMenu';

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
  canResolveAssistance: true
};
const noCaps: SeatMenuCaps = {
  actionsEnabled: false,
  canResolveAssistance: false,
  canStart: false,
  canExtend: false,
  canLockUnlock: false
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
    const noPc = buildSeatMenu(seat({ tone: 'ready' }), { ...allCaps, canLockUnlock: false });
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

  it('каждый пункт ведёт к живому действию: старт, продление или команда ПК', () => {
    const sections = buildSeatMenu(seat({ tone: 'active', activeSessionId: 'sess-1' }), allCaps);
    const kinds = new Set(flat(sections).map((item) => item.run.kind));
    expect([...kinds].every((kind) => kind === 'start-guest' || kind === 'extend' || kind === 'pc')).toBe(true);
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
