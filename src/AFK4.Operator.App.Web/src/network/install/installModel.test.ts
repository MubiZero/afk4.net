import { describe, it, expect } from 'bun:test';
import { getInstallerUrl } from './installModel';

describe('getInstallerUrl', () => {
  it('returns the configured url when present', () => {
    expect(getInstallerUrl({ setupInstallerUrl: 'https://dl.example/afk4-client.exe' } as never)).toBe('https://dl.example/afk4-client.exe');
  });
  it('returns null when unset (no broken fallback link)', () => {
    expect(getInstallerUrl({} as never)).toBeNull();
    expect(getInstallerUrl({ setupInstallerUrl: '   ' } as never)).toBeNull();
  });
});
