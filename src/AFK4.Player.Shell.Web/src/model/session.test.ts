import { describe, expect, it } from 'bun:test';
import { PlayerApiError } from '../api/playerApi';
import { devScenarioState } from '../host/devHost';
import { extendUnavailableKey, sessionActionErrorKey, sessionRole } from './session';

const owner = '00000000-0000-4000-8000-000000000020';
const playing = devScenarioState('session')!;

describe('кто перед экраном сессии', () => {
  it('вошёл тот, чья сессия, — деньги здесь', () => {
    expect(sessionRole(playing, { signedIn: true, displayName: 'Алишер', playerAccountId: owner })).toBe('owner');
  });

  it('сессия игрока, а на ПК он не вошёл — продлить можно, войдя', () => {
    expect(sessionRole(playing, { signedIn: false })).toBe('signInToManage');
  });

  it('вошёл не тот — чужими деньгами не распоряжается', () => {
    expect(sessionRole(playing, { signedIn: true, displayName: 'Другой', playerAccountId: 'other' })).toBe('signInToManage');
  });

  it('посадила стойка без счёта — продлевает администратор', () => {
    expect(sessionRole({ ...playing, sessionOwnerKind: 'guest', sessionOwnerPlayerAccountId: null }, { signedIn: false })).toBe('counter');
  });
});

describe('почему продлить нельзя', () => {
  it('сессия по пакету и сессия у стойки — своими словами', () => {
    expect(extendUnavailableKey('package_session')).toBe('playerShell.extend.unavailable.package');
    expect(extendUnavailableKey('not_prepaid')).toBe('playerShell.extend.unavailable.counter');
    expect(extendUnavailableKey(null)).toBeNull();
  });

  it('отказы действий — по коду сервера', () => {
    expect(sessionActionErrorKey(new PlayerApiError(409, 'insufficient_balance'))).toBe('playerShell.chooseTime.error.insufficient');
    expect(sessionActionErrorKey(new PlayerApiError(404, null))).toBe('playerShell.session.error.gone');
    expect(sessionActionErrorKey(new TypeError('Failed to fetch'))).toBe('playerShell.chooseTime.error.offline');
  });
});
