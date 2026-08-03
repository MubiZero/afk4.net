import { afterEach, describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { THEME_STORAGE_KEY, resolveInitialTheme, applyThemeClass } from './theme';

describe('design tokens', () => {
  it('does not define a private colour palette', () => {
    const css = readFileSync(new URL('../index.css', import.meta.url), 'utf8');
    expect(css).toContain('@afk4/tokens');
    expect(css).not.toMatch(/#1d4ed8/i);
    expect(css).not.toMatch(/#f6f7f9/i);
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
