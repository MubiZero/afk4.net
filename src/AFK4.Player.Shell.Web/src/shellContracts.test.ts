import { describe, expect, it } from 'bun:test';
import { PlayerShellStateNames } from './shellContracts';

describe('shellContracts', () => {
  it('mirrors the C# PlayerShellStateNames constants exactly', () => {
    expect(PlayerShellStateNames).toEqual({
      Locked: 'Locked',
      Active: 'Active',
      Grace: 'Grace',
      Ending: 'Ending',
      Maintenance: 'Maintenance',
      Offline: 'Offline',
      Error: 'Error'
    });
  });
});
