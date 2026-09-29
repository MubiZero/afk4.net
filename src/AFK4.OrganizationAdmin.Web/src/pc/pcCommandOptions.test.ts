import { describe, expect, it } from 'bun:test';
import type { SeatSummary } from '../operatorData';
import { pcCommandsFor, pcLockCommandsFor } from './pcCommandOptions';

function seat(overrides: Partial<SeatSummary> = {}): SeatSummary {
  return {
    id: 'seat-1', zone: 'Зал A', name: 'ПК 07', tone: 'ready', stateLabel: 'Свободен', player: '', remaining: '',
    device: '', command: 'Idle', app: '', deviceId: 'dev-1', isDeviceOnline: true, isDeviceLocked: true,
    activeSessionId: null, hasActiveSession: false, ...overrides
  };
}

const everything = { canDispatch: true, canMaintain: true };

describe('команды ПК в карточке места', () => {
  // На свободном ПК никого нет: сообщение и выход из аккаунта отправлять некому (#2 аудита) —
  // раньше эти две команды смотрели только на связь, и «всё можно» врало про пустой ПК.
  it('свободный ПК на связи: питание и обслуживание можно, сообщение и выход — некому', () => {
    expect(pcCommandsFor(seat(), everything)).toEqual([
      { id: 'reboot', blockedReason: null, confirm: 'danger' },
      { id: 'shutdown', blockedReason: null, confirm: 'danger' },
      { id: 'message', blockedReason: 'op.pc.blocked.noPlayer', confirm: 'text' },
      { id: 'sign-out', blockedReason: 'op.pc.blocked.noPlayer', confirm: 'warning' },
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

  // Прошлая команда этого места ещё в полёте (тон «ожидание») — вторая команда подождёт, а не
  // продублирует первую (#3 аудита).
  it('прошлая команда ещё не ответила: новую не отправить, сказано «ждём ответа»', () => {
    const options = pcCommandsFor(seat({ tone: 'pending', command: 'Unlock pending' }), everything);

    for (const id of ['reboot', 'shutdown', 'message', 'sign-out'] as const) {
      expect(options.find((o) => o.id === id)!.blockedReason).toBe('op.pc.blocked.pending');
    }
    expect(options.find((o) => o.id === 'maintenance-on')!.blockedReason).toBe('op.pc.blocked.pending');
  });

  it('пока команда в полёте, даже возврат из обслуживания подождёт', () => {
    const options = pcCommandsFor(
      seat({ tone: 'pending', command: 'Unlock pending', maintenanceSinceUtc: '2026-09-25T10:00:00Z' }),
      everything
    );

    expect(options.at(-1)).toEqual({ id: 'maintenance-off', blockedReason: 'op.pc.blocked.pending', confirm: null });
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

// Блокировка/разблокировка (#1 аудита): раньше «Блокировать» была активна и на заблокированном
// ПК, а «Разблокировать» смотрела на сессию, а не на реальный статус блокировки.
describe('блокировка ПК места', () => {
  it('ПК заблокирован: доступна только «Разблокировать»', () => {
    expect(pcLockCommandsFor(seat({ isDeviceLocked: true }))).toEqual([
      { id: 'lock', disabled: true, hintKey: 'op.pc.blocked.alreadyLocked' },
      { id: 'unlock', disabled: false, hintKey: null }
    ]);
  });

  it('ПК разблокирован: доступна только «Блокировать», сессия ни при чём', () => {
    expect(pcLockCommandsFor(seat({ isDeviceLocked: false, activeSessionId: null, hasActiveSession: false }))).toEqual([
      { id: 'lock', disabled: false, hintKey: null },
      { id: 'unlock', disabled: true, hintKey: 'op.pc.blocked.alreadyUnlocked' }
    ]);
  });

  it('статус блокировки не подтверждён: обе кнопки доступны, но с пометкой', () => {
    const options = pcLockCommandsFor(seat({ isDeviceLocked: undefined }));

    expect(options.every((option) => !option.disabled)).toBe(true);
    expect(options.every((option) => option.hintKey === 'op.pc.blocked.lockUnknown')).toBe(true);
  });

  it('ПК не на связи: обе кнопки закрыты той же причиной, что у соседних команд', () => {
    expect(pcLockCommandsFor(seat({ isDeviceOnline: false, tone: 'offline' }))).toEqual([
      { id: 'lock', disabled: true, hintKey: 'op.pc.blocked.offline' },
      { id: 'unlock', disabled: true, hintKey: 'op.pc.blocked.offline' }
    ]);
  });

  it('прошлая команда в полёте: обе кнопки ждут ответа', () => {
    expect(pcLockCommandsFor(seat({ tone: 'pending', command: 'Unlock pending' }))).toEqual([
      { id: 'lock', disabled: true, hintKey: 'op.pc.blocked.pending' },
      { id: 'unlock', disabled: true, hintKey: 'op.pc.blocked.pending' }
    ]);
  });

  it('место без ПК — блокировать нечего', () => {
    expect(pcLockCommandsFor(seat({ deviceId: null }))).toEqual([]);
  });
});
