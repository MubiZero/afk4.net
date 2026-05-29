import { afterEach, describe, expect, it } from 'vitest';
import { THEME_STORAGE_KEY, resolveInitialTheme, applyThemeClass } from './theme';

describe('theme core', () => {
  afterEach(() => { document.documentElement.classList.remove('dark'); });

  it('defaults to light when nothing stored and system is light', () => {
    expect(resolveInitialTheme(null, false)).toBe('light');
  });
  it('uses system dark when nothing stored', () => {
    expect(resolveInitialTheme(null, true)).toBe('dark');
  });
  it('honors a stored choice over system', () => {
    expect(resolveInitialTheme('light', true)).toBe('light');
    expect(resolveInitialTheme('dark', false)).toBe('dark');
  });
  it('ignores invalid stored values', () => {
    expect(resolveInitialTheme('purple', false)).toBe('light');
  });
  it('applyThemeClass toggles the dark class on the root', () => {
    applyThemeClass('dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    applyThemeClass('light');
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });
  it('exposes the storage key', () => {
    expect(THEME_STORAGE_KEY).toBe('afk4.platform.theme');
  });
});
