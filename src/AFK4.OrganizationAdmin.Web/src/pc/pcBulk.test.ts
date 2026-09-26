import { describe, expect, it } from 'bun:test';
import type { SeatSummary } from '../operatorData';
import { bulkCommands, planBulk } from './pcBulk';

function seat(id: string, overrides: Partial<SeatSummary> = {}): SeatSummary {
  return {
    id,
    zone: 'Зал A',
    name: `PC-${id}`,
    tone: 'ready',
    stateLabel: 'Свободно',
    player: 'Гость',
    remaining: 'Свободно',
    device: 'Device',
    command: 'Idle',
    app: 'Shell',
    deviceId: `dev-${id}`,
    isDeviceOnline: true,
    ...overrides
  };
}

const all = { canDispatch: true, canMaintain: true };

describe('planBulk', () => {
  // Из двенадцати выбранных перезагрузятся свободные, а про остальных сказано, почему нет.
  it('sends to those it can and names why the rest are skipped', () => {
    const plan = planBulk([
      seat('01'),
      seat('02', { tone: 'active', activeSessionId: 's' }),
      seat('03', { isDeviceOnline: false }),
      seat('04', { deviceId: null })
    ], 'reboot', all);

    expect(plan.targets.map((target) => target.id)).toEqual(['01']);
    expect(plan.skipped.map((skip) => [skip.seat.id, skip.reason])).toEqual([
      ['02', 'op.pc.bulk.skip.session'],
      ['03', 'op.pc.bulk.skip.offline'],
      ['04', 'op.pc.bulk.skip.noDevice']
    ]);
  });

  it('wakes only the ones that are off', () => {
    const plan = planBulk([seat('01'), seat('02', { isDeviceOnline: false })], 'wake', all);

    expect(plan.targets.map((target) => target.id)).toEqual(['02']);
    expect(plan.skipped[0].reason).toBe('op.pc.bulk.skip.alreadyOn');
  });

  it('a message reaches a seat with a running session', () => {
    const plan = planBulk([seat('01', { tone: 'active', activeSessionId: 's' })], 'message', all);

    expect(plan.targets).toHaveLength(1);
  });

  it('maintenance needs its own right', () => {
    const plan = planBulk([seat('01')], 'maintenance-on', { canDispatch: true, canMaintain: false });

    expect(plan.skipped[0].reason).toBe('op.pc.bulk.skip.notAllowed');
  });
});

describe('bulkCommands', () => {
  it('offers wake and return-to-floor only when someone needs them', () => {
    expect(bulkCommands([seat('01')], all)).not.toContain('wake');
    expect(bulkCommands([seat('01')], all)).not.toContain('maintenance-off');
    expect(bulkCommands([seat('01', { isDeviceOnline: false })], all)).toContain('wake');
    expect(bulkCommands([seat('01', { maintenanceSinceUtc: '2026-09-25T10:00:00Z' })], all)).toContain('maintenance-off');
  });

  it('a role without device rights gets nothing', () => {
    expect(bulkCommands([seat('01')], { canDispatch: false, canMaintain: false })).toEqual([]);
  });
});

describe('консоли в общей команде', () => {
  it('пропускаются с причиной', () => {
    const plan = planBulk([seat('01'), seat('02', { isConsole: true })], 'reboot', all);

    expect(plan.targets.map((target) => target.id)).toEqual(['01']);
    expect(plan.skipped).toEqual([{ seat: expect.objectContaining({ id: '02' }), reason: 'op.pc.bulk.skip.console' }]);
  });
});

