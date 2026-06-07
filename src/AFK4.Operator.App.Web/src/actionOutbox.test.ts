import { afterEach, describe, expect, it } from 'bun:test';
import {
  acknowledgeAction,
  enqueueAction,
  loadActionOutbox,
  reconcileActionOutbox,
  type OperatorActionOutboxEntry
} from './actionOutbox';
import type { SeatSummary } from './operatorData';
import { createTranslator } from '@afk4/i18n';

const t = createTranslator('ru');

const deviceA = 'aaaaaaaa-1111-4111-8111-111111111111';
const deviceB = 'bbbbbbbb-2222-4222-8222-222222222222';
const sessionId = '44444444-4444-4444-8444-444444444444';

function entry(
  deviceId: string,
  commandType: 'lock' | 'unlock',
  expectedSessionId: string | null = sessionId
): OperatorActionOutboxEntry {
  return {
    idempotencyKey: `${deviceId}-${commandType}`,
    deviceId,
    seatId: '11111111-1111-4111-8111-111111111111',
    seatName: 'PC-010',
    commandType,
    expectedSessionId,
    queuedAtMs: 1_000
  };
}

function seat(deviceId: string, activeSessionId: string | null): SeatSummary {
  return {
    id: '11111111-1111-4111-8111-111111111111',
    zone: 'Main Hall',
    name: 'PC-010',
    tone: 'active',
    stateLabel: 'В сессии',
    player: 'Активный клиент',
    remaining: 'играет сейчас',
    billing: 'Wallet',
    device: 'PC-010 · Online · unlocked',
    command: 'Lease fresh',
    app: 'Shell',
    deviceId,
    deviceName: 'PC-010',
    isDeviceOnline: true,
    isDeviceLocked: false,
    hasActiveSession: activeSessionId !== null,
    activeSessionId,
    rawState: 'Active',
    remainingSeconds: 1800,
    remainingDeadlineMs: null,
    accruedCostMinorUnits: null,
    currencyCode: null,
    sortOrder: 1
  };
}

afterEach(() => {
  localStorage.clear();
});

describe('actionOutbox', () => {
  it('persists an enqueued action', () => {
    enqueueAction(entry(deviceA, 'lock'));

    const pending = loadActionOutbox();
    expect(pending).toHaveLength(1);
    expect(pending[0].deviceId).toBe(deviceA);
    expect(pending[0].commandType).toBe('lock');
  });

  it('replaces a prior action for the same device (latest intent wins)', () => {
    enqueueAction(entry(deviceA, 'lock'));
    enqueueAction(entry(deviceA, 'unlock'));

    const pending = loadActionOutbox();
    expect(pending).toHaveLength(1);
    expect(pending[0].commandType).toBe('unlock');
  });

  it('preserves enqueue order across devices', () => {
    enqueueAction(entry(deviceA, 'lock'));
    enqueueAction(entry(deviceB, 'unlock'));

    expect(loadActionOutbox().map((item) => item.deviceId)).toEqual([deviceA, deviceB]);
  });

  it('removes an acknowledged action and is a no-op for unknown keys', () => {
    enqueueAction(entry(deviceA, 'lock'));

    acknowledgeAction('does-not-exist');
    expect(loadActionOutbox()).toHaveLength(1);

    acknowledgeAction(`${deviceA}-lock`);
    expect(loadActionOutbox()).toHaveLength(0);
  });

  it('self-heals a corrupt queue to empty', () => {
    enqueueAction(entry(deviceA, 'lock'));
    localStorage.setItem('afk4.operator.actionOutbox', 'not json');

    expect(loadActionOutbox()).toHaveLength(0);
  });
});

describe('reconcileActionOutbox', () => {
  it('replays actions whose seat and session are unchanged', () => {
    const result = reconcileActionOutbox([entry(deviceA, 'lock')], [seat(deviceA, sessionId)], t);

    expect(result.replay.map((item) => item.commandType)).toEqual(['lock']);
    expect(result.dropped).toHaveLength(0);
  });

  it('drops actions whose session moved on, with an audit note', () => {
    const result = reconcileActionOutbox([entry(deviceA, 'unlock')], [seat(deviceA, null)], t);

    expect(result.replay).toHaveLength(0);
    expect(result.dropped).toHaveLength(1);
    expect(result.dropped[0].entry.commandType).toBe('unlock');
    expect(result.dropped[0].note).toContain('PC-010');
  });

  it('drops actions whose seat is gone from the live map', () => {
    const result = reconcileActionOutbox([entry(deviceA, 'lock')], [seat(deviceB, sessionId)], t);

    expect(result.replay).toHaveLength(0);
    expect(result.dropped).toHaveLength(1);
  });
});
