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
    billing: 'Fast guest',
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
  canLockUnlock: true
};
const noCaps: SeatMenuCaps = {
  actionsEnabled: false,
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

  it('marks not-yet-built actions as "soon" honestly instead of pretending they work', () => {
    const sections = buildSeatMenu(seat({ tone: 'active', activeSessionId: 'sess-1' }), allCaps);
    const soon = flat(sections).filter((item) => item.soon);
    expect(soon.map((item) => item.id)).toContain('soon-reboot');
    expect(soon.map((item) => item.id)).toContain('soon-fine');
    // Every "soon" item routes to an honest deferred toast, not a live handler.
    expect(soon.every((item) => item.run.kind === 'soon')).toBe(true);
  });

  it('returns an empty menu when the operator can do nothing here', () => {
    const sections = buildSeatMenu(seat({ tone: 'ready', deviceId: null }), noCaps);
    expect(sections).toHaveLength(0);
  });
});
