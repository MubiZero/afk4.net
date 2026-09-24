import { describe, expect, it } from 'bun:test';
import { PlayerShellStateNames } from '@afk4/contracts';
import { devScenarioState } from '../host/devHost';
import { selectScreen, type ShellScreen } from './screen';

const state = (name: string) => ({ ...devScenarioState('idle')!, state: name });

describe('selectScreen — таблица экранов §3', () => {
  const cases: [string, Parameters<typeof selectScreen>[0], ShellScreen][] = [
    ['агент ещё молчит', { state: null, signedIn: false, approached: false }, 'connecting'],
    ['свободный ПК, никого', { state: state(PlayerShellStateNames.Locked), signedIn: false, approached: false }, 'idle'],
    ['к свободному ПК подошли', { state: state(PlayerShellStateNames.Locked), signedIn: false, approached: true }, 'approach'],
    ['вошли на свободном ПК', { state: state(PlayerShellStateNames.Locked), signedIn: true, approached: true }, 'chooseTime'],
    ['идёт сессия', { state: state(PlayerShellStateNames.Active), signedIn: false, approached: false }, 'session'],
    ['последняя минута', { state: state(PlayerShellStateNames.Ending), signedIn: true, approached: false }, 'ending'],
    ['связь пропала посреди сессии', { state: state(PlayerShellStateNames.Grace), signedIn: false, approached: false }, 'grace'],
    ['заперт и без связи', { state: state(PlayerShellStateNames.Offline), signedIn: false, approached: true }, 'offline'],
    ['обслуживание', { state: state(PlayerShellStateNames.Maintenance), signedIn: true, approached: true }, 'maintenance'],
    ['сбой', { state: state(PlayerShellStateNames.Error), signedIn: false, approached: false }, 'error']
  ];

  for (const [name, input, expected] of cases) {
    it(name, () => {
      expect(selectScreen(input)).toBe(expected);
    });
  }

  it('без связи на запертом ПК войти нельзя, даже если вход уже был', () => {
    // Сервер коду не поверит, и звать человека к экрану выбора — обещать то, что не выполнить.
    expect(selectScreen({ state: state(PlayerShellStateNames.Offline), signedIn: true, approached: true })).toBe('offline');
  });
});
