export type Theme = 'light' | 'dark';
export const THEME_STORAGE_KEY = 'afk4.platform.theme';

// Dense internal operations surface, dark by default (matches the shared AFK4 baseline —
// see AFK4.OrganizationAdmin.Web/src/operatorTheme.tsx). Only an explicit stored choice
// overrides it; there is no system-preference fallback.
export function resolveInitialTheme(stored: string | null): Theme {
  if (stored === 'light' || stored === 'dark') return stored;
  return 'dark';
}

export function applyThemeClass(theme: Theme): void {
  const root = document.documentElement;
  // `.dark` drives Tailwind's `dark:` variant (used by the ui/* primitives);
  // `data-theme` is what @afk4/tokens keys its colour values off of. Both must be kept
  // in sync or the shared token palette resolves to nothing.
  root.classList.toggle('dark', theme === 'dark');
  root.setAttribute('data-theme', theme);
}
