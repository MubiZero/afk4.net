import { it, expect } from 'bun:test';
import { getSetupMsiUrl } from './installModel';

it('falls back to the default MSI url when env is unset', () => {
  expect(getSetupMsiUrl()).toBe('/downloads/AFK4-Agent.msi');
});
