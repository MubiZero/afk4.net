import { afterEach, describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { THEME_STORAGE_KEY, resolveInitialTheme, applyThemeClass } from './theme';

describe('design tokens', () => {
  // Панель не имеет права заводить собственные цвета и собственные примитивы: и то и другое
  // уже есть в @afk4/tokens и @afk4/ui. Именно параллельный набор делал её «чужим шаблоном».
  it('builds on the shared token and UI packages, with no private palette', () => {
    const css = readFileSync(new URL('../styles.css', import.meta.url), 'utf8');
    expect(css).toContain('@afk4/ui/kit.css');
    expect(css).toContain('@afk4/ui/rail.css');
    expect(css).toContain('@afk4/ui/auth.css');
  });

  it('declares no colour values of its own in the local layers', () => {
    for (const layer of ['01-base', '02-shell', '10-clubs', '11-organization', '12-money', '13-forms']) {
      const css = readFileSync(new URL(`../styles/${layer}.css`, import.meta.url), 'utf8');
      expect(css).not.toMatch(/#[0-9a-f]{3,8}\b/i);
    }
  });
});

describe('theme core', () => {
  afterEach(() => {
    document.documentElement.classList.remove('dark');
    document.documentElement.removeAttribute('data-theme');
  });

  it('defaults to dark when nothing is stored', () => {
    expect(resolveInitialTheme(null)).toBe('dark');
  });
  it('honors a stored choice', () => {
    expect(resolveInitialTheme('light')).toBe('light');
    expect(resolveInitialTheme('dark')).toBe('dark');
  });
  it('ignores invalid stored values and falls back to dark', () => {
    expect(resolveInitialTheme('purple')).toBe('dark');
  });
  it('applyThemeClass toggles the dark class and data-theme attribute on the root', () => {
    applyThemeClass('dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    applyThemeClass('light');
    expect(document.documentElement.classList.contains('dark')).toBe(false);
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
  });
  it('exposes the storage key', () => {
    expect(THEME_STORAGE_KEY).toBe('afk4.platform.theme');
  });
});
