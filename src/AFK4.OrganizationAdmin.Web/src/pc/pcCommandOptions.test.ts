import { describe, expect, it } from 'bun:test';
import type { SeatSummary } from '../operatorData';
import { pcCommandsFor } from './pcCommandOptions';

function seat(overrides: Partial<SeatSummary> = {}): SeatSummary {
  return {
    id: 'seat-1', zone: 'Зал A', name: 'ПК 07', tone: 'ready', stateLabel: 'Свободен', player: '', remaining: '',
    device: '', command: '', app: '', deviceId: 'dev-1', isDeviceOnline: true, isDeviceLocked: true,
    activeSessionId: null, hasActiveSession: false, ...overrides
  };
}

const everything = { canDispatch: true, canMaintain: true };

describe('команды ПК в карточке места', () => {
  it('свободный ПК на связи: всё можно', () => {
    expect(pcCommandsFor(seat(), everything)).toEqual([
      { id: 'reboot', blockedReason: null, confirm: 'danger' },
      { id: 'shutdown', blockedReason: null, confirm: 'danger' },
      { id: 'message', blockedReason: null, confirm: 'text' },
      { id: 'sign-out', blockedReason: null, confirm: 'warning' },
      { id: 'maintenance-on', blockedReason: null, confirm: 'warning' }
    ]);
  });

  it('за ПК играют: питание и обслуживание закрыты и сказано почему, сообщение и выход — можно', () => {
    const options = pcCommandsFor(seat({ tone: 'active', activeSessionId: 's-1', hasActiveSession: true }), everything);

    expect(options.find((o) => o.id === 'reboot')!.blockedReason).toBe('op.pc.blocked.session');
    expect(options.find((o) => o.id === 'shutdown')!.blockedReason).toBe('op.pc.blocked.session');
    expect(options.find((o) => o.id === 'maintenance-on')!.blockedReason).toBe('op.pc.blocked.session');
    expect(options.find((o) => o.id === 'message')!.blockedReason).toBeNull();
    expect(options.find((o) => o.id === 'sign-out')!.blockedReason).toBeNull();
  });

  it('ПК не на связи: разбудить можно, остальное до него не дойдёт', () => {
    const options = pcCommandsFor(seat({ isDeviceOnline: false, tone: 'offline' }), everything);

    expect(options[0]).toEqual({ id: 'wake', blockedReason: null, confirm: null });
    for (const id of ['reboot', 'shutdown', 'message', 'sign-out'] as const) {
      expect(options.find((o) => o.id === id)!.blockedReason).toBe('op.pc.blocked.offline');
    }
  });

  it('включённый ПК будить нечего', () => {
    expect(pcCommandsFor(seat(), everything).some((o) => o.id === 'wake')).toBe(false);
  });

  it('ПК, который клуб увёл на обслуживание, можно вернуть в зал', () => {
    const options = pcCommandsFor(seat({ tone: 'service', maintenanceSinceUtc: '2026-09-25T10:00:00Z' }), everything);

    expect(options.at(-1)).toEqual({ id: 'maintenance-off', blockedReason: null, confirm: null });
    expect(options.some((o) => o.id === 'maintenance-on')).toBe(false);
  });

  it('обслуживание — своё право: без него кнопок обслуживания нет', () => {
    const ids = pcCommandsFor(seat(), { canDispatch: true, canMaintain: false }).map((o) => o.id);

    expect(ids).not.toContain('maintenance-on');
    expect(ids).toContain('reboot');
  });

  it('с одним правом на обслуживание — только обслуживание', () => {
    expect(pcCommandsFor(seat(), { canDispatch: false, canMaintain: true }).map((o) => o.id)).toEqual(['maintenance-on']);
  });

  it('место без ПК — команд нет', () => {
    expect(pcCommandsFor(seat({ deviceId: null }), everything)).toEqual([]);
  });
});
