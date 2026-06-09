import { describe, expect, it } from 'bun:test';
import { PlayerShellStateNames } from './shellContracts';

describe('shellContracts', () => {
  it('mirrors the C# PlayerShellStateNames constants exactly', () => {
    expect(PlayerShellStateNames).toEqual({
      Locked: 'locked',
      Active: 'active',
      Grace: 'grace',
      Ending: 'ending',
      Maintenance: 'maintenance',
      Offline: 'offline',
      Error: 'error'
    });
  });
});
