# Platform.Web Foundation + App-Shell + Club Overview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a modern design system and app-shell in `AFK4.Platform.Web` (Tailwind v4 + shadcn/ui + Radix), themeable (light default + dark) and localized (RU/EN), and ship one fully-built screen — the club **Overview** — on existing backend contracts.

**Architecture:** Frontend-only, non-breaking. Tailwind v4 and shadcn primitives are added alongside the legacy `styles.css`. A new `AppShell` becomes the layout for `/club/*`; legacy club screen bodies are reparented into it unchanged (redesign deferred). Only Overview is rebuilt. Theme and i18n are small provider modules with pure, testable cores. The Overview reads `ClubApiClient.getDashboardSummary` + device lists and renders explicit loading/empty/error states.

**Tech Stack:** React 19, TypeScript 6, Vite 8, Tailwind CSS v4 (`@tailwindcss/vite`), shadcn/ui (Radix), Recharts, Vitest 4 + React Testing Library + jsdom.

---

## Reference: existing contracts this plan builds on

From `src/AFK4.Platform.Web/src/api/clubApi.ts` and `types.ts` (do not modify the backend):

- `ClubApiClient.getDashboardSummary(branchId): Promise<OperatorDashboardSummary>` — today's window. Fields used: `utilization.{totalSeats,activeSessions,onlineDevices,offlineDevices,utilizationPercent}`, `alertPressure.{offlineDevices,failedCommands,pendingCommands,totalAlerts}`, `revenue.{totalRevenue,gameplayRevenue,posNetSales,posCheckCount,newPlayerCount}` where money is `Money{amount,currencyCode}`.
- `ClubApiClient.listDevices(branchId): Promise<DeviceInventoryItem[]>` and `listPendingDevices(branchId): Promise<DeviceInventoryItem[]>`. `DeviceInventoryItem` fields used: `deviceId, displayName, isOnline, failedCommandCount, enrollmentState`.
- Errors throw `PlatformApiError(status, message, code)` from `src/api/platformApi.ts`.
- Staff session shape: `StaffSession` (see `src/auth/staffTokenStore.ts`) with `permissions: string[]`, `branchIds: string[]`.

Tests follow the existing colocated pattern (e.g. `src/auth/staffTokenStore.test.ts`, `src/App.test.tsx`) and run on the project's Vitest + jsdom config.

---

## File structure (created in this plan)

```
src/lib/utils.ts                         cn() class-merge helper (shadcn standard)
src/index.css                            Tailwind v4 entry + design tokens (light/dark)
src/components/ui/*                       shadcn primitives (generated via CLI)
src/theme/theme.ts                       pure theme core (resolve/apply/persist)
src/theme/ThemeProvider.tsx              React context + useTheme()
src/theme/theme.test.ts
src/i18n/messages.ts                     Locale, Messages, ru + en dictionaries
src/i18n/I18nProvider.tsx                React context + useI18n() (t + formatters)
src/i18n/i18n.test.tsx
src/club/nav.ts                          ClubRole, nav config, visibleNav(role)
src/club/nav.test.ts
src/components/shell/ThemeToggle.tsx
src/components/shell/BranchSwitcher.tsx
src/components/shell/NavList.tsx
src/components/shell/UserMenu.tsx
src/components/shell/Topbar.tsx
src/components/shell/AppShell.tsx
src/components/shell/AppShell.test.tsx
src/club/overview/overviewModel.ts       view-model + pure builder from API DTOs
src/club/overview/overviewModel.test.ts
src/club/overview/useOverview.ts         data hook (loading/ready/error + retry)
src/club/overview/useOverview.test.tsx
src/club/overview/OverviewScreen.tsx     screen: KPI cards, revenue chart, attention
src/club/overview/OverviewScreen.test.tsx
```

Modified: `vite.config.ts` (Tailwind plugin + `@` alias), `tsconfig.json` (`@/*` path), `src/api/clubApi.ts` (range overload), `src/main.tsx` (mount providers), `src/App.tsx` (mount AppShell for `/club/*`, reparent legacy screens).

---

## Task 1: Tailwind v4 + shadcn tooling

**Files:**
- Modify: `src/AFK4.Platform.Web/package.json` (deps), `vite.config.ts`, `tsconfig.json`
- Create: `src/index.css`, `src/lib/utils.ts`, `components.json`

- [ ] **Step 1: Install dependencies**

Run in `src/AFK4.Platform.Web`:
```bash
npm install tailwindcss@^4 @tailwindcss/vite@^4 class-variance-authority clsx tailwind-merge lucide-react recharts
```
Expected: packages added to `dependencies`, no peer errors.

- [ ] **Step 2: Add the `@` path alias to `tsconfig.json`**

In `compilerOptions` add:
```json
"baseUrl": ".",
"paths": { "@/*": ["./src/*"] }
```

- [ ] **Step 3: Wire Tailwind plugin + alias in `vite.config.ts`**

Merge into the existing `defineConfig` (keep existing React/test config):
```ts
import { fileURLToPath, URL } from 'node:url';
import tailwindcss from '@tailwindcss/vite';
// plugins: [react(), tailwindcss()]
// resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } }
```
Ensure the `test` block keeps `environment: 'jsdom'` and the existing setup file.

- [ ] **Step 4: Create `src/lib/utils.ts`**

```ts
import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
```

- [ ] **Step 5: Create `src/index.css` with Tailwind v4 + design tokens**

```css
@import "tailwindcss";

@custom-variant dark (&:is(.dark *));

/* "Calm SaaS" — light (default) */
:root {
  --background: #f6f7f9;
  --foreground: #0f172a;
  --card: #ffffff;
  --card-foreground: #0f172a;
  --muted: #64748b;
  --border: #e7e9ee;
  --input: #e7e9ee;
  --ring: #4f46e5;
  --primary: #4f46e5;
  --primary-foreground: #ffffff;
  --primary-weak: #eef2ff;
  --accent: #eef2ff;
  --accent-foreground: #4f46e5;
  --success: #16a34a;
  --warning: #d97706;
  --danger: #dc2626;
  --radius: 0.625rem;
}

.dark {
  --background: #0f1117;
  --foreground: #e7e9ee;
  --card: #171a21;
  --card-foreground: #e7e9ee;
  --muted: #98a1b2;
  --border: #262b35;
  --input: #262b35;
  --ring: #818cf8;
  --primary: #818cf8;
  --primary-foreground: #0f1117;
  --primary-weak: rgba(129,140,248,0.16);
  --accent: rgba(129,140,248,0.16);
  --accent-foreground: #a5b0ff;
  --success: #34d399;
  --warning: #fbbf24;
  --danger: #f87171;
}

@theme inline {
  --color-background: var(--background);
  --color-foreground: var(--foreground);
  --color-card: var(--card);
  --color-card-foreground: var(--card-foreground);
  --color-muted: var(--muted);
  --color-border: var(--border);
  --color-input: var(--input);
  --color-ring: var(--ring);
  --color-primary: var(--primary);
  --color-primary-foreground: var(--primary-foreground);
  --color-primary-weak: var(--primary-weak);
  --color-accent: var(--accent);
  --color-accent-foreground: var(--accent-foreground);
  --color-success: var(--success);
  --color-warning: var(--warning);
  --color-danger: var(--danger);
  --radius-lg: var(--radius);
  --font-sans: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Inter, sans-serif;
}

body { background: var(--background); color: var(--foreground); font-family: var(--font-sans); }
```

- [ ] **Step 6: Create `components.json` for shadcn (Tailwind v4 / CSS variables)**

```json
{
  "$schema": "https://ui.shadcn.com/schema.json",
  "style": "new-york",
  "rsc": false,
  "tsx": true,
  "tailwind": { "config": "", "css": "src/index.css", "baseColor": "neutral", "cssVariables": true },
  "aliases": { "components": "@/components", "utils": "@/lib/utils", "ui": "@/components/ui" },
  "iconLibrary": "lucide"
}
```

- [ ] **Step 7: Generate the primitives this sub-project needs**

```bash
npx shadcn@latest add button card badge skeleton dropdown-menu avatar separator
```
Expected: files created under `src/components/ui/` (button.tsx, card.tsx, badge.tsx, skeleton.tsx, dropdown-menu.tsx, avatar.tsx, separator.tsx). These are vendored, repo-owned components.

- [ ] **Step 8: Import `index.css` in `src/main.tsx`**

Add at the top of `src/main.tsx` (keep the existing `styles.css` import so legacy screens are unaffected):
```ts
import './index.css';
```

- [ ] **Step 9: Verify build**

Run: `npm run build`
Expected: `tsc -b` + `vite build` succeed, 0 errors.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "build: add Tailwind v4 + shadcn primitives and design tokens to Platform.Web"
```

---

## Task 2: Theme core (pure logic)

**Files:**
- Create: `src/theme/theme.ts`, `src/theme/theme.test.ts`

- [ ] **Step 1: Write the failing test**

`src/theme/theme.test.ts`:
```ts
import { afterEach, describe, expect, it, vi } from 'vitest';
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run src/theme/theme.test.ts`
Expected: FAIL — `./theme` cannot be resolved.

- [ ] **Step 3: Implement `src/theme/theme.ts`**

```ts
export type Theme = 'light' | 'dark';
export const THEME_STORAGE_KEY = 'afk4.platform.theme';

export function resolveInitialTheme(stored: string | null, systemPrefersDark: boolean): Theme {
  if (stored === 'light' || stored === 'dark') return stored;
  return systemPrefersDark ? 'dark' : 'light';
}

export function applyThemeClass(theme: Theme): void {
  const root = document.documentElement;
  root.classList.toggle('dark', theme === 'dark');
}

export function systemPrefersDark(): boolean {
  return typeof window !== 'undefined'
    && typeof window.matchMedia === 'function'
    && window.matchMedia('(prefers-color-scheme: dark)').matches;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx vitest run src/theme/theme.test.ts`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add src/theme/theme.ts src/theme/theme.test.ts
git commit -m "feat(theme): add pure theme resolution + apply core"
```

---

## Task 3: ThemeProvider + useTheme

**Files:**
- Create: `src/theme/ThemeProvider.tsx`
- Test: extend `src/theme/theme.test.ts` is not enough — add provider test in same file or new file `src/theme/ThemeProvider.test.tsx`

- [ ] **Step 1: Write the failing test**

`src/theme/ThemeProvider.test.tsx`:
```tsx
import { describe, expect, it, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ThemeProvider, useTheme } from './ThemeProvider';
import { THEME_STORAGE_KEY } from './theme';

function Probe() {
  const { theme, toggle } = useTheme();
  return <button onClick={toggle}>theme:{theme}</button>;
}

describe('ThemeProvider', () => {
  beforeEach(() => { localStorage.clear(); document.documentElement.classList.remove('dark'); });

  it('defaults to light and applies no dark class', () => {
    render(<ThemeProvider><Probe /></ThemeProvider>);
    expect(screen.getByText('theme:light')).toBeInTheDocument();
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('toggles to dark, applies the class, and persists', () => {
    render(<ThemeProvider><Probe /></ThemeProvider>);
    fireEvent.click(screen.getByRole('button'));
    expect(screen.getByText('theme:dark')).toBeInTheDocument();
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
  });

  it('restores a persisted choice on mount', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');
    render(<ThemeProvider><Probe /></ThemeProvider>);
    expect(screen.getByText('theme:dark')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run src/theme/ThemeProvider.test.tsx`
Expected: FAIL — `./ThemeProvider` not found.

- [ ] **Step 3: Implement `src/theme/ThemeProvider.tsx`**

```tsx
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { applyThemeClass, resolveInitialTheme, systemPrefersDark, THEME_STORAGE_KEY, type Theme } from './theme';

interface ThemeContextValue { theme: Theme; setTheme: (t: Theme) => void; toggle: () => void; }
const ThemeContext = createContext<ThemeContextValue | null>(null);

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(() =>
    resolveInitialTheme(
      typeof localStorage === 'undefined' ? null : localStorage.getItem(THEME_STORAGE_KEY),
      systemPrefersDark()
    )
  );

  useEffect(() => { applyThemeClass(theme); }, [theme]);

  const setTheme = useCallback((t: Theme) => {
    setThemeState(t);
    try { localStorage.setItem(THEME_STORAGE_KEY, t); } catch { /* ignore */ }
  }, []);

  const toggle = useCallback(() => { setTheme(theme === 'dark' ? 'light' : 'dark'); }, [theme, setTheme]);

  const value = useMemo(() => ({ theme, setTheme, toggle }), [theme, setTheme, toggle]);
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (ctx === null) throw new Error('useTheme must be used within ThemeProvider');
  return ctx;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx vitest run src/theme/ThemeProvider.test.tsx`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/theme/ThemeProvider.tsx src/theme/ThemeProvider.test.tsx
git commit -m "feat(theme): add ThemeProvider with persistence and system default"
```

---

## Task 4: i18n (messages + provider)

**Files:**
- Create: `src/i18n/messages.ts`, `src/i18n/I18nProvider.tsx`, `src/i18n/i18n.test.tsx`

- [ ] **Step 1: Write the failing test**

`src/i18n/i18n.test.tsx`:
```tsx
import { describe, expect, it } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider, useI18n } from './I18nProvider';

function Probe() {
  const { t, locale, setLocale, formatCurrency } = useI18n();
  return (
    <div>
      <span>nav:{t('nav.overview')}</span>
      <span>loc:{locale}</span>
      <span>money:{formatCurrency(4250, 'TJS')}</span>
      <button onClick={() => setLocale('en')}>en</button>
    </div>
  );
}

describe('i18n', () => {
  it('defaults to Russian', () => {
    render(<I18nProvider><Probe /></I18nProvider>);
    expect(screen.getByText('nav:Обзор')).toBeInTheDocument();
    expect(screen.getByText('loc:ru')).toBeInTheDocument();
  });
  it('switches to English', () => {
    render(<I18nProvider><Probe /></I18nProvider>);
    fireEvent.click(screen.getByRole('button'));
    expect(screen.getByText('nav:Overview')).toBeInTheDocument();
  });
  it('returns the key when a translation is missing', () => {
    render(<I18nProvider><MissingProbe /></I18nProvider>);
    expect(screen.getByText('out:does.not.exist')).toBeInTheDocument();
  });
});

function MissingProbe() {
  const { t } = useI18n();
  return <span>out:{t('does.not.exist' as never)}</span>;
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run src/i18n/i18n.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement `src/i18n/messages.ts`**

```ts
export type Locale = 'ru' | 'en';

export const messages = {
  ru: {
    'nav.group.branch': 'Филиал',
    'nav.group.account': 'Аккаунт',
    'nav.overview': 'Обзор',
    'nav.venue': 'Зал и ПК',
    'nav.clients': 'Клиенты',
    'nav.monetization': 'Монетизация',
    'nav.reports': 'Отчёты',
    'nav.settings': 'Настройки',
    'nav.branches': 'Все филиалы',
    'nav.install': 'Установка',
    'nav.billing': 'Биллинг',
    'nav.profile': 'Профиль и доступ',
    'shell.signOut': 'Выйти',
    'shell.soon': 'Скоро',
    'shell.theme.toggle': 'Сменить тему',
    'overview.title': 'Обзор',
    'overview.kpi.devicesOnline': 'Устройства онлайн',
    'overview.kpi.activeSessions': 'Активные сессии',
    'overview.kpi.revenueToday': 'Выручка сегодня',
    'overview.kpi.attention': 'Требуют внимания',
    'overview.revenue.title': 'Выручка сегодня',
    'overview.revenue.gameplay': 'Игровое время',
    'overview.revenue.pos': 'Бар и товары',
    'overview.attention.title': 'Требуют внимания',
    'overview.attention.empty': 'Всё в порядке — ничего не требует внимания.',
    'overview.attention.offline': 'офлайн',
    'overview.attention.failed': 'ошибки команд',
    'overview.attention.pending': 'ожидает подтверждения',
    'state.loading': 'Загрузка…',
    'state.error': 'Не удалось загрузить данные.',
    'state.retry': 'Повторить'
  },
  en: {
    'nav.group.branch': 'Branch',
    'nav.group.account': 'Account',
    'nav.overview': 'Overview',
    'nav.venue': 'Floor & PCs',
    'nav.clients': 'Clients',
    'nav.monetization': 'Monetization',
    'nav.reports': 'Reports',
    'nav.settings': 'Settings',
    'nav.branches': 'All branches',
    'nav.install': 'Install',
    'nav.billing': 'Billing',
    'nav.profile': 'Profile & access',
    'shell.signOut': 'Sign out',
    'shell.soon': 'Soon',
    'shell.theme.toggle': 'Toggle theme',
    'overview.title': 'Overview',
    'overview.kpi.devicesOnline': 'Devices online',
    'overview.kpi.activeSessions': 'Active sessions',
    'overview.kpi.revenueToday': 'Revenue today',
    'overview.kpi.attention': 'Need attention',
    'overview.revenue.title': 'Revenue today',
    'overview.revenue.gameplay': 'Gameplay',
    'overview.revenue.pos': 'Shop & bar',
    'overview.attention.title': 'Need attention',
    'overview.attention.empty': 'All good — nothing needs attention.',
    'overview.attention.offline': 'offline',
    'overview.attention.failed': 'command errors',
    'overview.attention.pending': 'pending approval',
    'state.loading': 'Loading…',
    'state.error': 'Failed to load data.',
    'state.retry': 'Retry'
  }
} as const;

export type MessageKey = keyof (typeof messages)['ru'];
```

- [ ] **Step 4: Implement `src/i18n/I18nProvider.tsx`**

```tsx
import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { messages, type Locale, type MessageKey } from './messages';

interface I18nContextValue {
  locale: Locale;
  setLocale: (l: Locale) => void;
  t: (key: MessageKey) => string;
  formatNumber: (n: number) => string;
  formatCurrency: (amount: number, currencyCode: string) => string;
  formatDate: (iso: string) => string;
}
const I18nContext = createContext<I18nContextValue | null>(null);
const LOCALE_TAG: Record<Locale, string> = { ru: 'ru-RU', en: 'en-US' };

export function I18nProvider({ children, initialLocale = 'ru' }: { children: ReactNode; initialLocale?: Locale }) {
  const [locale, setLocale] = useState<Locale>(initialLocale);

  const t = useCallback((key: MessageKey): string => {
    const dict = messages[locale] as Record<string, string>;
    return dict[key] ?? key;
  }, [locale]);

  const formatNumber = useCallback((n: number) => new Intl.NumberFormat(LOCALE_TAG[locale]).format(n), [locale]);
  const formatCurrency = useCallback(
    (amount: number, currencyCode: string) =>
      new Intl.NumberFormat(LOCALE_TAG[locale], { style: 'currency', currency: currencyCode, maximumFractionDigits: 0 }).format(amount),
    [locale]
  );
  const formatDate = useCallback(
    (iso: string) => new Intl.DateTimeFormat(LOCALE_TAG[locale], { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso)),
    [locale]
  );

  const value = useMemo(
    () => ({ locale, setLocale, t, formatNumber, formatCurrency, formatDate }),
    [locale, t, formatNumber, formatCurrency, formatDate]
  );
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18nContextValue {
  const ctx = useContext(I18nContext);
  if (ctx === null) throw new Error('useI18n must be used within I18nProvider');
  return ctx;
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `npx vitest run src/i18n/i18n.test.tsx`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/i18n
git commit -m "feat(i18n): add RU/EN message dictionaries and I18nProvider"
```

---

## Task 5: Club navigation config + role gating

**Files:**
- Create: `src/club/nav.ts`, `src/club/nav.test.ts`

- [ ] **Step 1: Write the failing test**

`src/club/nav.test.ts`:
```ts
import { describe, expect, it } from 'vitest';
import { clubNav, roleFromPermissions, visibleNav } from './nav';

describe('club nav', () => {
  it('owner sees every item', () => {
    const groups = visibleNav('owner');
    const keys = groups.flatMap(g => g.items.map(i => i.key));
    expect(keys).toContain('settings');
    expect(keys).toContain('install');
    expect(keys).toContain('billing');
    expect(keys).toContain('profile');
  });

  it('manager does not see owner-only items', () => {
    const keys = visibleNav('manager').flatMap(g => g.items.map(i => i.key));
    expect(keys).toContain('overview');
    expect(keys).toContain('venue');
    expect(keys).not.toContain('settings');
    expect(keys).not.toContain('install');
    expect(keys).not.toContain('billing');
    expect(keys).not.toContain('profile');
  });

  it('derives owner role from the owner permission', () => {
    expect(roleFromPermissions(['identity.branch_staff.manage'])).toBe('owner');
    expect(roleFromPermissions(['sessions.start'])).toBe('manager');
  });

  it('config is internally consistent (every item has a path and label key)', () => {
    for (const g of clubNav) for (const i of g.items) {
      expect(i.path.startsWith('/club')).toBe(true);
      expect(i.labelKey.startsWith('nav.')).toBe(true);
    }
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run src/club/nav.test.ts`
Expected: FAIL — `./nav` not found.

- [ ] **Step 3: Implement `src/club/nav.ts`**

```ts
import type { MessageKey } from '@/i18n/messages';

export type ClubRole = 'owner' | 'manager';
export type NavGroupKey = 'branch' | 'account';

export interface NavItem {
  key: string;
  labelKey: MessageKey;
  path: string;
  ownerOnly: boolean;
  soon: boolean;
}
export interface NavGroup { key: NavGroupKey; labelKey: MessageKey; items: NavItem[]; }

export const clubNav: NavGroup[] = [
  {
    key: 'branch',
    labelKey: 'nav.group.branch',
    items: [
      { key: 'overview', labelKey: 'nav.overview', path: '/club', ownerOnly: false, soon: false },
      { key: 'venue', labelKey: 'nav.venue', path: '/club/venue', ownerOnly: false, soon: true },
      { key: 'clients', labelKey: 'nav.clients', path: '/club/clients', ownerOnly: false, soon: true },
      { key: 'monetization', labelKey: 'nav.monetization', path: '/club/monetization', ownerOnly: true, soon: true },
      { key: 'reports', labelKey: 'nav.reports', path: '/club/reports', ownerOnly: false, soon: true },
      { key: 'settings', labelKey: 'nav.settings', path: '/club/settings', ownerOnly: true, soon: true }
    ]
  },
  {
    key: 'account',
    labelKey: 'nav.group.account',
    items: [
      { key: 'branches', labelKey: 'nav.branches', path: '/club/branches', ownerOnly: false, soon: true },
      { key: 'install', labelKey: 'nav.install', path: '/club/install', ownerOnly: true, soon: false },
      { key: 'billing', labelKey: 'nav.billing', path: '/club/billing', ownerOnly: true, soon: true },
      { key: 'profile', labelKey: 'nav.profile', path: '/club/profile', ownerOnly: true, soon: true }
    ]
  }
];

const OWNER_PERMISSION = 'identity.branch_staff.manage';

export function roleFromPermissions(permissions: readonly string[]): ClubRole {
  return permissions.includes(OWNER_PERMISSION) ? 'owner' : 'manager';
}

export function visibleNav(role: ClubRole): NavGroup[] {
  return clubNav
    .map(group => ({ ...group, items: group.items.filter(i => role === 'owner' || !i.ownerOnly) }))
    .filter(group => group.items.length > 0);
}
```

> Note: `OWNER_PERMISSION` uses the backend's owner-level branch-staff management permission. If the project standardizes a dedicated owner-role flag later, update this single constant.

- [ ] **Step 4: Run test to verify it passes**

Run: `npx vitest run src/club/nav.test.ts`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/nav.ts src/club/nav.test.ts
git commit -m "feat(club): add navigation config with role gating"
```

---

## Task 6: ThemeToggle component

**Files:**
- Create: `src/components/shell/ThemeToggle.tsx`

- [ ] **Step 1: Implement `src/components/shell/ThemeToggle.tsx`**

```tsx
import { Moon, Sun } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useI18n } from '@/i18n/I18nProvider';
import { useTheme } from '@/theme/ThemeProvider';

export function ThemeToggle() {
  const { theme, toggle } = useTheme();
  const { t } = useI18n();
  return (
    <Button variant="ghost" size="icon" aria-label={t('shell.theme.toggle')} onClick={toggle}>
      {theme === 'dark' ? <Sun className="size-4" /> : <Moon className="size-4" />}
    </Button>
  );
}
```

- [ ] **Step 2: Verify it type-checks within the shell test (covered in Task 9). No standalone test.**

Run: `npx tsc -b`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/components/shell/ThemeToggle.tsx
git commit -m "feat(shell): add theme toggle button"
```

---

## Task 7: BranchSwitcher, NavList, UserMenu, Topbar

**Files:**
- Create: `src/components/shell/BranchSwitcher.tsx`, `NavList.tsx`, `UserMenu.tsx`, `Topbar.tsx`

- [ ] **Step 1: Implement `src/components/shell/NavList.tsx`**

```tsx
import { cn } from '@/lib/utils';
import { useI18n } from '@/i18n/I18nProvider';
import { Badge } from '@/components/ui/badge';
import { visibleNav, type ClubRole } from '@/club/nav';

export interface NavListProps {
  role: ClubRole;
  activePath: string;
  counts?: Record<string, number>;
  onNavigate: (path: string) => void;
}

export function NavList({ role, activePath, counts = {}, onNavigate }: NavListProps) {
  const { t } = useI18n();
  return (
    <nav className="flex flex-col gap-1">
      {visibleNav(role).map(group => (
        <div key={group.key} className="px-2 py-1">
          <div className="px-3 pb-1 pt-3 text-[10px] font-bold uppercase tracking-wide text-muted">
            {t(group.labelKey)}
          </div>
          {group.items.map(item => {
            const active = item.path === activePath;
            const count = counts[item.key];
            return (
              <button
                key={item.key}
                type="button"
                aria-current={active ? 'page' : undefined}
                onClick={() => onNavigate(item.path)}
                className={cn(
                  'flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left text-sm font-medium text-foreground/80 hover:bg-accent',
                  active && 'bg-accent font-semibold text-accent-foreground'
                )}
              >
                <span>{t(item.labelKey)}</span>
                {typeof count === 'number' && count > 0 && (
                  <Badge variant="secondary" className="ml-auto">{count}</Badge>
                )}
                {item.soon && <span className="ml-auto text-[10px] text-muted">{t('shell.soon')}</span>}
              </button>
            );
          })}
        </div>
      ))}
    </nav>
  );
}
```

- [ ] **Step 2: Implement `src/components/shell/BranchSwitcher.tsx`**

```tsx
import { ChevronsUpDown } from 'lucide-react';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';

export interface BranchOption { branchId: string; name: string; }
export interface BranchSwitcherProps {
  orgName: string;
  branches: BranchOption[];
  activeBranchId: string;
  onSelect: (branchId: string) => void;
}

export function BranchSwitcher({ orgName, branches, activeBranchId, onSelect }: BranchSwitcherProps) {
  const active = branches.find(b => b.branchId === activeBranchId) ?? branches[0];
  return (
    <DropdownMenu>
      <DropdownMenuTrigger className="m-3 flex items-center gap-3 rounded-lg border border-border bg-card px-3 py-2 text-left">
        <span className="flex size-7 items-center justify-center rounded-md bg-primary text-xs font-bold text-primary-foreground">
          {orgName.slice(0, 1)}
        </span>
        <span className="min-w-0">
          <span className="block truncate text-sm font-bold">{orgName}</span>
          <span className="block truncate text-[11px] text-muted">{active?.name ?? '—'}</span>
        </span>
        <ChevronsUpDown className="ml-auto size-4 text-muted" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-56">
        {branches.map(b => (
          <DropdownMenuItem key={b.branchId} onSelect={() => onSelect(b.branchId)}>
            {b.name}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
```

- [ ] **Step 3: Implement `src/components/shell/UserMenu.tsx`**

```tsx
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { useI18n } from '@/i18n/I18nProvider';
import { ThemeToggle } from './ThemeToggle';

export interface UserMenuProps { displayName: string; roleLabel: string; onSignOut: () => void; }

export function UserMenu({ displayName, roleLabel, onSignOut }: UserMenuProps) {
  const { t } = useI18n();
  return (
    <div className="mt-auto flex items-center gap-3 border-t border-border px-3 py-3">
      <Avatar className="size-8"><AvatarFallback>{displayName.slice(0, 1)}</AvatarFallback></Avatar>
      <div className="min-w-0">
        <div className="truncate text-sm font-semibold">{displayName}</div>
        <div className="truncate text-[11px] text-muted">{roleLabel}</div>
      </div>
      <div className="ml-auto flex items-center gap-1">
        <ThemeToggle />
        <Button variant="ghost" size="sm" onClick={onSignOut}>{t('shell.signOut')}</Button>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Implement `src/components/shell/Topbar.tsx`**

```tsx
import { Menu } from 'lucide-react';
import { Button } from '@/components/ui/button';

export interface TopbarProps { branchName: string; screenTitle: string; onOpenSidebar: () => void; right?: React.ReactNode; }

export function Topbar({ branchName, screenTitle, onOpenSidebar, right }: TopbarProps) {
  return (
    <header className="flex items-center justify-between border-b border-border bg-card px-5 py-3">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" className="md:hidden" aria-label="menu" onClick={onOpenSidebar}>
          <Menu className="size-4" />
        </Button>
        <div className="text-sm text-muted">
          {branchName} · <b className="text-base text-foreground">{screenTitle}</b>
        </div>
      </div>
      {right}
    </header>
  );
}
```

- [ ] **Step 5: Verify type-check**

Run: `npx tsc -b`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/components/shell/BranchSwitcher.tsx src/components/shell/NavList.tsx src/components/shell/UserMenu.tsx src/components/shell/Topbar.tsx
git commit -m "feat(shell): add branch switcher, nav list, user menu, topbar"
```

---

## Task 8: AppShell (composition + responsive) with tests

**Files:**
- Create: `src/components/shell/AppShell.tsx`, `src/components/shell/AppShell.test.tsx`

- [ ] **Step 1: Write the failing test**

`src/components/shell/AppShell.test.tsx`:
```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { AppShell } from './AppShell';

function renderShell(role: 'owner' | 'manager') {
  return render(
    <ThemeProvider><I18nProvider>
      <AppShell
        role={role}
        orgName="Победа"
        branches={[{ branchId: 'b1', name: 'Центральный' }]}
        activeBranchId="b1"
        activePath="/club"
        screenTitle="Обзор"
        userName="Алишер"
        roleLabel="Владелец"
        counts={{ venue: 2 }}
        onNavigate={vi.fn()}
        onSelectBranch={vi.fn()}
        onSignOut={vi.fn()}
      >
        <div>screen-body</div>
      </AppShell>
    </I18nProvider></ThemeProvider>
  );
}

describe('AppShell', () => {
  it('renders branch + account groups and the body for an owner', () => {
    renderShell('owner');
    expect(screen.getByText('Филиал')).toBeInTheDocument();
    expect(screen.getByText('Аккаунт')).toBeInTheDocument();
    expect(screen.getByText('Настройки')).toBeInTheDocument();
    expect(screen.getByText('screen-body')).toBeInTheDocument();
  });

  it('hides owner-only items for a manager', () => {
    renderShell('manager');
    expect(screen.queryByText('Настройки')).not.toBeInTheDocument();
    expect(screen.queryByText('Биллинг')).not.toBeInTheDocument();
    expect(screen.getByText('Обзор')).toBeInTheDocument();
  });

  it('fires navigation on item click', () => {
    const onNavigate = vi.fn();
    render(
      <ThemeProvider><I18nProvider>
        <AppShell role="owner" orgName="П" branches={[{ branchId: 'b1', name: 'Ц' }]} activeBranchId="b1"
          activePath="/club" screenTitle="Обзор" userName="A" roleLabel="Владелец"
          onNavigate={onNavigate} onSelectBranch={vi.fn()} onSignOut={vi.fn()}>
          <div />
        </AppShell>
      </I18nProvider></ThemeProvider>
    );
    fireEvent.click(screen.getByText('Обзор'));
    expect(onNavigate).toHaveBeenCalledWith('/club');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run src/components/shell/AppShell.test.tsx`
Expected: FAIL — `./AppShell` not found.

- [ ] **Step 3: Implement `src/components/shell/AppShell.tsx`**

```tsx
import { useState, type ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { BranchSwitcher, type BranchOption } from './BranchSwitcher';
import { NavList } from './NavList';
import { UserMenu } from './UserMenu';
import { Topbar } from './Topbar';
import type { ClubRole } from '@/club/nav';

export interface AppShellProps {
  role: ClubRole;
  orgName: string;
  branches: BranchOption[];
  activeBranchId: string;
  activePath: string;
  screenTitle: string;
  userName: string;
  roleLabel: string;
  counts?: Record<string, number>;
  topbarRight?: ReactNode;
  onNavigate: (path: string) => void;
  onSelectBranch: (branchId: string) => void;
  onSignOut: () => void;
  children: ReactNode;
}

export function AppShell(props: AppShellProps) {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <aside
        className={cn(
          'fixed inset-y-0 left-0 z-40 flex w-60 flex-col border-r border-border bg-card transition-transform md:static md:translate-x-0',
          sidebarOpen ? 'translate-x-0' : '-translate-x-full'
        )}
      >
        <BranchSwitcher orgName={props.orgName} branches={props.branches}
          activeBranchId={props.activeBranchId} onSelect={props.onSelectBranch} />
        <div className="flex-1 overflow-auto">
          <NavList role={props.role} activePath={props.activePath} counts={props.counts}
            onNavigate={(p) => { setSidebarOpen(false); props.onNavigate(p); }} />
        </div>
        <UserMenu displayName={props.userName} roleLabel={props.roleLabel} onSignOut={props.onSignOut} />
      </aside>

      {sidebarOpen && (
        <div className="fixed inset-0 z-30 bg-black/40 md:hidden" onClick={() => setSidebarOpen(false)} aria-hidden />
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar branchName={props.branches.find(b => b.branchId === props.activeBranchId)?.name ?? ''}
          screenTitle={props.screenTitle} onOpenSidebar={() => setSidebarOpen(true)} right={props.topbarRight} />
        <main className="flex-1 overflow-auto p-5">{props.children}</main>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx vitest run src/components/shell/AppShell.test.tsx`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/components/shell/AppShell.tsx src/components/shell/AppShell.test.tsx
git commit -m "feat(shell): add AppShell composition with responsive sidebar"
```

---

## Task 9: Overview view-model (pure builder)

**Files:**
- Create: `src/club/overview/overviewModel.ts`, `src/club/overview/overviewModel.test.ts`

- [ ] **Step 1: Write the failing test**

`src/club/overview/overviewModel.test.ts`:
```ts
import { describe, expect, it } from 'vitest';
import { buildOverview } from './overviewModel';
import type { DeviceInventoryItem, OperatorDashboardSummary } from '@/api/types';

const summary: OperatorDashboardSummary = {
  organizationId: 'o', branchId: 'b', fromUtc: '', toUtc: '', generatedAtUtc: '',
  utilization: { totalSeats: 30, activeSessions: 19, endingSessions: 0, onlineDevices: 28, offlineDevices: 2, sessionStarts: 40, utilizationPercent: 63 },
  alertPressure: { pendingCommands: 0, failedCommands: 1, offlineDevices: 2, endingSessions: 0, totalAlerts: 3 },
  revenue: {
    posNetSales: { amount: 1250, currencyCode: 'TJS' },
    gameplayRevenue: { amount: 3000, currencyCode: 'TJS' },
    totalRevenue: { amount: 4250, currencyCode: 'TJS' },
    posCheckCount: 12, newPlayerCount: 4
  }
};

function device(p: Partial<DeviceInventoryItem>): DeviceInventoryItem {
  return {
    organizationId: 'o', branchId: 'b', deviceId: 'd', machineName: 'PC', agentVersion: '1', shellVersion: '1',
    enrolledAtUtc: '', lastHeartbeatAtUtc: null, isOnline: true, isLocked: false, seatId: null, seatName: null,
    zoneId: null, zoneName: null, activeCredentialCount: 0, installedAppCount: 0, pendingCommandCount: 0,
    failedCommandCount: 0, displayName: 'PC', role: 'gaming_pc', enrollmentState: 'approved', ...p
  };
}

describe('buildOverview', () => {
  it('maps KPI values from the summary', () => {
    const vm = buildOverview(summary, [], []);
    expect(vm.kpis.devicesOnline).toEqual({ online: 28, total: 30 });
    expect(vm.kpis.activeSessions).toBe(19);
    expect(vm.kpis.utilizationPercent).toBe(63);
    expect(vm.kpis.revenueToday).toEqual({ amount: 4250, currencyCode: 'TJS' });
    expect(vm.kpis.attention).toBe(3);
    expect(vm.revenueBreakdown).toEqual([
      { key: 'gameplay', amount: 3000 },
      { key: 'pos', amount: 1250 }
    ]);
  });

  it('builds attention rows from offline + failed devices and pending count', () => {
    const vm = buildOverview(
      summary,
      [device({ deviceId: 'd1', displayName: 'ПК-14', isOnline: false }),
       device({ deviceId: 'd2', displayName: 'ПК-07', failedCommandCount: 2 }),
       device({ deviceId: 'd3', displayName: 'OK', isOnline: true })],
      [device({ deviceId: 'd9', displayName: 'Новый', enrollmentState: 'pending' })]
    );
    const ids = vm.attention.map(a => a.deviceId);
    expect(ids).toEqual(expect.arrayContaining(['d1', 'd2', 'd9']));
    expect(ids).not.toContain('d3');
    expect(vm.attention.find(a => a.deviceId === 'd1')?.kind).toBe('offline');
    expect(vm.attention.find(a => a.deviceId === 'd9')?.kind).toBe('pending');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run src/club/overview/overviewModel.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement `src/club/overview/overviewModel.ts`**

```ts
import type { DeviceInventoryItem, Money, OperatorDashboardSummary } from '@/api/types';

export type AttentionKind = 'offline' | 'failed' | 'pending';
export interface AttentionRow { deviceId: string; name: string; kind: AttentionKind; }
export interface RevenueSlice { key: 'gameplay' | 'pos'; amount: number; }

export interface OverviewViewModel {
  kpis: {
    devicesOnline: { online: number; total: number };
    activeSessions: number;
    utilizationPercent: number;
    revenueToday: Money;
    attention: number;
  };
  revenueBreakdown: RevenueSlice[];
  attention: AttentionRow[];
}

export function buildOverview(
  summary: OperatorDashboardSummary,
  devices: DeviceInventoryItem[],
  pending: DeviceInventoryItem[]
): OverviewViewModel {
  const attention: AttentionRow[] = [];
  for (const d of devices) {
    if (!d.isOnline) attention.push({ deviceId: d.deviceId, name: d.displayName, kind: 'offline' });
    else if (d.failedCommandCount > 0) attention.push({ deviceId: d.deviceId, name: d.displayName, kind: 'failed' });
  }
  for (const p of pending) attention.push({ deviceId: p.deviceId, name: p.displayName, kind: 'pending' });

  return {
    kpis: {
      devicesOnline: { online: summary.utilization.onlineDevices, total: summary.utilization.totalSeats },
      activeSessions: summary.utilization.activeSessions,
      utilizationPercent: summary.utilization.utilizationPercent,
      revenueToday: summary.revenue.totalRevenue,
      attention: summary.alertPressure.totalAlerts
    },
    revenueBreakdown: [
      { key: 'gameplay', amount: summary.revenue.gameplayRevenue.amount },
      { key: 'pos', amount: summary.revenue.posNetSales.amount }
    ],
    attention
  };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx vitest run src/club/overview/overviewModel.test.ts`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/overview/overviewModel.ts src/club/overview/overviewModel.test.ts
git commit -m "feat(overview): add pure view-model builder"
```

---

## Task 10: useOverview data hook

**Files:**
- Create: `src/club/overview/useOverview.ts`, `src/club/overview/useOverview.test.tsx`

- [ ] **Step 1: Write the failing test**

`src/club/overview/useOverview.test.tsx`:
```tsx
import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useOverview } from './useOverview';

const okSummary = {
  utilization: { totalSeats: 30, activeSessions: 19, endingSessions: 0, onlineDevices: 28, offlineDevices: 2, sessionStarts: 1, utilizationPercent: 63 },
  alertPressure: { pendingCommands: 0, failedCommands: 0, offlineDevices: 2, endingSessions: 0, totalAlerts: 2 },
  revenue: { posNetSales: { amount: 1, currencyCode: 'TJS' }, gameplayRevenue: { amount: 2, currencyCode: 'TJS' }, totalRevenue: { amount: 3, currencyCode: 'TJS' }, posCheckCount: 0, newPlayerCount: 0 }
};

function fakeClient(over: Partial<Record<'getDashboardSummary' | 'listDevices' | 'listPendingDevices', unknown>> = {}) {
  return {
    getDashboardSummary: vi.fn().mockResolvedValue(okSummary),
    listDevices: vi.fn().mockResolvedValue([]),
    listPendingDevices: vi.fn().mockResolvedValue([]),
    ...over
  } as never;
}

describe('useOverview', () => {
  it('reaches ready with a view-model', async () => {
    const { result } = renderHook(() => useOverview(fakeClient(), 'b1'));
    expect(result.current.status).toBe('loading');
    await waitFor(() => expect(result.current.status).toBe('ready'));
    if (result.current.status === 'ready') {
      expect(result.current.data.kpis.activeSessions).toBe(19);
    }
  });

  it('surfaces an error state and supports retry', async () => {
    const failing = fakeClient({ getDashboardSummary: vi.fn().mockRejectedValue(new Error('boom')) });
    const { result } = renderHook(() => useOverview(failing, 'b1'));
    await waitFor(() => expect(result.current.status).toBe('error'));
    (failing as { getDashboardSummary: ReturnType<typeof vi.fn> }).getDashboardSummary.mockResolvedValue(okSummary);
    result.current.retry();
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run src/club/overview/useOverview.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement `src/club/overview/useOverview.ts`**

```ts
import { useCallback, useEffect, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { buildOverview, type OverviewViewModel } from './overviewModel';

export type OverviewState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: OverviewViewModel; retry: () => void };

type Loadable = Pick<ClubApiClient, 'getDashboardSummary' | 'listDevices' | 'listPendingDevices'>;

export function useOverview(client: Loadable, branchId: string): OverviewState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: OverviewViewModel; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    Promise.all([client.getDashboardSummary(branchId), client.listDevices(branchId), client.listPendingDevices(branchId)])
      .then(([summary, devices, pending]) => {
        if (cancelled) return;
        setState({ status: 'ready', data: buildOverview(summary, devices, pending) });
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setState({ status: 'error', message: err instanceof Error ? err.message : 'error' });
      });
    return () => { cancelled = true; };
  }, [client, branchId, tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx vitest run src/club/overview/useOverview.test.tsx`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/overview/useOverview.ts src/club/overview/useOverview.test.tsx
git commit -m "feat(overview): add data hook with loading/error/retry states"
```

---

## Task 11: OverviewScreen (KPI cards, revenue chart, attention) with states

**Files:**
- Create: `src/club/overview/OverviewScreen.tsx`, `src/club/overview/OverviewScreen.test.tsx`

- [ ] **Step 1: Write the failing test**

`src/club/overview/OverviewScreen.test.tsx`:
```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { OverviewScreen } from './OverviewScreen';
import type { OverviewState } from './useOverview';

function wrap(state: OverviewState) {
  return render(<I18nProvider><OverviewScreen state={state} /></I18nProvider>);
}

const ready: OverviewState = {
  status: 'ready', retry: vi.fn(),
  data: {
    kpis: { devicesOnline: { online: 28, total: 30 }, activeSessions: 19, utilizationPercent: 63, revenueToday: { amount: 4250, currencyCode: 'TJS' }, attention: 3 },
    revenueBreakdown: [{ key: 'gameplay', amount: 3000 }, { key: 'pos', amount: 1250 }],
    attention: [{ deviceId: 'd1', name: 'ПК-14', kind: 'offline' }]
  }
};

describe('OverviewScreen', () => {
  it('renders KPI values when ready', () => {
    wrap(ready);
    expect(screen.getByText('Активные сессии')).toBeInTheDocument();
    expect(screen.getByText('19')).toBeInTheDocument();
    expect(screen.getByText('ПК-14')).toBeInTheDocument();
  });

  it('shows a loading skeleton', () => {
    wrap({ status: 'loading', retry: vi.fn() });
    expect(screen.getByTestId('overview-loading')).toBeInTheDocument();
  });

  it('shows an error with a working retry', () => {
    const retry = vi.fn();
    wrap({ status: 'error', message: 'x', retry });
    fireEvent.click(screen.getByText('Повторить'));
    expect(retry).toHaveBeenCalled();
  });

  it('shows the empty attention message when there is nothing to attend to', () => {
    wrap({ ...ready, data: { ...ready.data!, attention: [] } } as OverviewState);
    expect(screen.getByText('Всё в порядке — ничего не требует внимания.')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run src/club/overview/OverviewScreen.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement `src/club/overview/OverviewScreen.tsx`**

```tsx
import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { useI18n } from '@/i18n/I18nProvider';
import type { MessageKey } from '@/i18n/messages';
import type { AttentionKind } from './overviewModel';
import type { OverviewState } from './useOverview';

const ATTENTION_LABEL: Record<AttentionKind, MessageKey> = {
  offline: 'overview.attention.offline',
  failed: 'overview.attention.failed',
  pending: 'overview.attention.pending'
};
const SLICE_COLOR: Record<string, string> = { gameplay: 'var(--primary)', pos: 'var(--success)' };

export function OverviewScreen({ state }: { state: OverviewState }) {
  const { t, formatNumber, formatCurrency } = useI18n();

  if (state.status === 'loading') {
    return (
      <div data-testid="overview-loading" className="grid grid-cols-1 gap-4 md:grid-cols-4">
        {[0, 1, 2, 3].map(i => <Skeleton key={i} className="h-24 w-full rounded-lg" />)}
      </div>
    );
  }

  if (state.status === 'error') {
    return (
      <Card><CardContent className="flex flex-col items-center gap-3 py-10">
        <p className="text-muted">{t('state.error')}</p>
        <Button onClick={state.retry}>{t('state.retry')}</Button>
      </CardContent></Card>
    );
  }

  const { kpis, revenueBreakdown, attention } = state.data;
  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
        <Kpi label={t('overview.kpi.devicesOnline')} value={`${formatNumber(kpis.devicesOnline.online)} / ${formatNumber(kpis.devicesOnline.total)}`} />
        <Kpi label={t('overview.kpi.activeSessions')} value={formatNumber(kpis.activeSessions)} sub={`${kpis.utilizationPercent}%`} />
        <Kpi label={t('overview.kpi.revenueToday')} value={formatCurrency(kpis.revenueToday.amount, kpis.revenueToday.currencyCode)} />
        <Kpi label={t('overview.kpi.attention')} value={formatNumber(kpis.attention)} />
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        <Card className="md:col-span-2">
          <CardHeader><CardTitle>{t('overview.revenue.title')}</CardTitle></CardHeader>
          <CardContent>
            <div className="h-48">
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie data={revenueBreakdown} dataKey="amount" nameKey="key" innerRadius={50} outerRadius={75}>
                    {revenueBreakdown.map(s => <Cell key={s.key} fill={SLICE_COLOR[s.key]} />)}
                  </Pie>
                  <Tooltip />
                </PieChart>
              </ResponsiveContainer>
            </div>
            <div className="mt-3 flex gap-4 text-sm">
              <span><b>{t('overview.revenue.gameplay')}:</b> {formatCurrency(revenueBreakdown[0].amount, kpis.revenueToday.currencyCode)}</span>
              <span><b>{t('overview.revenue.pos')}:</b> {formatCurrency(revenueBreakdown[1].amount, kpis.revenueToday.currencyCode)}</span>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>{t('overview.attention.title')}</CardTitle></CardHeader>
          <CardContent className="flex flex-col gap-2">
            {attention.length === 0 && <p className="text-sm text-muted">{t('overview.attention.empty')}</p>}
            {attention.map(row => (
              <div key={row.deviceId} className="flex items-center justify-between border-b border-border py-2 last:border-0">
                <span className="text-sm font-medium">{row.name}</span>
                <Badge variant={row.kind === 'offline' ? 'destructive' : 'secondary'}>{t(ATTENTION_LABEL[row.kind])}</Badge>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function Kpi({ label, value, sub }: { label: string; value: string; sub?: string }) {
  return (
    <Card><CardContent className="py-4">
      <div className="text-xs font-medium text-muted">{label}</div>
      <div className="mt-2 text-2xl font-bold tabular-nums">{value}</div>
      {sub && <div className="mt-1 text-xs text-muted">{sub}</div>}
    </CardContent></Card>
  );
}
```

> If the generated `card.tsx` does not export `CardHeader`/`CardTitle` in your shadcn version, re-run `npx shadcn@latest add card` and confirm the exports; the shadcn `card` component provides `Card`, `CardHeader`, `CardTitle`, `CardContent`.

- [ ] **Step 4: Run test to verify it passes**

Run: `npx vitest run src/club/overview/OverviewScreen.test.tsx`
Expected: PASS (4 tests). (Recharts renders inside jsdom; the chart container has zero size but does not throw.)

- [ ] **Step 5: Commit**

```bash
git add src/club/overview/OverviewScreen.tsx src/club/overview/OverviewScreen.test.tsx
git commit -m "feat(overview): add Overview screen with KPI, revenue chart, attention, states"
```

---

## Task 12: Mount providers + wire Overview into the club shell (non-breaking)

**Files:**
- Modify: `src/main.tsx`, `src/App.tsx`

- [ ] **Step 1: Wrap the app with providers in `src/main.tsx`**

Wrap the existing `<App .../>` render with the providers (keep existing imports):
```tsx
import { ThemeProvider } from './theme/ThemeProvider';
import { I18nProvider } from './i18n/I18nProvider';
// ...
//   <ThemeProvider><I18nProvider><App apiBaseUrl={...} /></I18nProvider></ThemeProvider>
```

- [ ] **Step 2: Write the failing test for the club shell route**

Add to `src/App.test.tsx` (or a new `src/App.club.test.tsx`):
```tsx
import { describe, expect, it } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { ThemeProvider } from './theme/ThemeProvider';
import { I18nProvider } from './i18n/I18nProvider';
import App from './App';

// A signed-in staff session must exist; reuse the project's existing helper/mocks
// for staff session + ClubApiClient (see existing App.test.tsx setup).

describe('club shell', () => {
  it('renders the Overview inside the new AppShell at /club', async () => {
    window.history.pushState({}, '', '/club');
    render(<ThemeProvider><I18nProvider><App apiBaseUrl="http://localhost" audience="club" /></I18nProvider></ThemeProvider>);
    // With a signed-in session mocked, the shell + Overview mount:
    await waitFor(() => expect(screen.getByText('Обзор')).toBeInTheDocument());
  });
});
```

> Use the same staff-session/`ClubApiClient` mocking approach already present in `src/App.test.tsx`. If none exists for the club path, mock `readStaffSession` to return a session with the owner permission and stub `ClubApiClient` methods (`getDashboardSummary`, `listDevices`, `listPendingDevices`, `getBranchProfile`) as in `useOverview.test.tsx`.

- [ ] **Step 3: Run test to verify it fails**

Run: `npx vitest run src/App.test.tsx`
Expected: FAIL — `/club` renders the legacy `ClubDashboard`, not the new shell/Overview.

- [ ] **Step 4: Refactor `App.tsx` club rendering to use AppShell**

In the `isClubRoute(route)` branch of `App.tsx`, replace the direct `<ClubDashboard .../>` render with the new shell. Derive role and branches from the staff session and branch profile; render Overview for `/club`, and **reparent the existing `ClubDashboard` content for the other club routes** so nothing breaks:

```tsx
import { AppShell } from './components/shell/AppShell';
import { OverviewScreen } from './club/overview/OverviewScreen';
import { useOverview } from './club/overview/useOverview';
import { roleFromPermissions } from './club/nav';

function ClubArea({ client, route, session, onSignOut, onNavigate }: {
  client: ClubApiClient; route: ClubRoute; session: StaffSession;
  onSignOut: () => void; onNavigate: (path: string) => void;
}) {
  const role = roleFromPermissions(session.permissions);
  const branchId = session.branchIds[0] ?? '';
  const branches = session.branchIds.map((id, i) => ({ branchId: id, name: i === 0 ? 'Филиал' : `Филиал ${i + 1}` }));
  const overview = useOverview(client, branchId);
  const isOverview = route.kind === 'clubDashboard';

  return (
    <AppShell
      role={role}
      orgName={session.displayName}
      branches={branches}
      activeBranchId={branchId}
      activePath={pathForRoute(route)}
      screenTitle={isOverview ? 'Обзор' : ''}
      userName={session.displayName}
      roleLabel={role === 'owner' ? 'Владелец' : 'Менеджер'}
      onNavigate={onNavigate}
      onSelectBranch={() => { /* single-branch pilot: no-op until branch switching ships */ }}
      onSignOut={onSignOut}
    >
      {isOverview
        ? <OverviewScreen state={overview} />
        : <LegacyClubScreen client={client} route={route} session={session} />}
    </AppShell>
  );
}
```

`LegacyClubScreen` renders the *body* of the existing `ClubDashboard` for non-overview routes (install/branches/devices/operators/floor-map) without its old chrome. Extract that body from the current `ClubDashboard` (move the per-route content rendering into `LegacyClubScreen`, leaving sign-in/redirect handling in `App.tsx`). Add a small `pathForRoute(route)` mapping `ClubRoute` → the nav `path` strings in `src/club/nav.ts`. Keep `onNavigate` updating `window.history` + route state exactly as the existing `navigate` does.

- [ ] **Step 5: Run the test to verify it passes**

Run: `npx vitest run src/App.test.tsx`
Expected: PASS — `/club` shows the new shell with Overview; other club routes still render their (legacy) bodies.

- [ ] **Step 6: Manual smoke**

Run: `npm run dev`, open the printed URL at `/club` with a dev staff session. Confirm: shell renders, theme toggle flips light/dark, sidebar collapses on a narrow window, Overview shows data/loading/error correctly, other club menu items still open their existing screens.

- [ ] **Step 7: Commit**

```bash
git add src/main.tsx src/App.tsx src/club src/components
git commit -m "feat(club): mount Overview in new AppShell; reparent legacy club screens (non-breaking)"
```

---

## Task 13: Add range overload to ClubApiClient (used by later Reports; thin + tested now)

> Keeps the chart honest for the future Reports sub-project without a backend change — the existing endpoint already accepts `fromUtc/toUtc`. Small, isolated, no UI consumer yet.

**Files:**
- Modify: `src/api/clubApi.ts`
- Test: `src/api/clubApi.range.test.ts`

- [ ] **Step 1: Write the failing test**

`src/api/clubApi.range.test.ts`:
```ts
import { describe, expect, it, vi } from 'vitest';
import { ClubApiClient } from './clubApi';

describe('getDashboardSummaryForRange', () => {
  it('passes explicit fromUtc/toUtc query params', async () => {
    const fetchImpl = vi.fn().mockResolvedValue(new Response('{}', { status: 200 }));
    const client = new ClubApiClient({ baseUrl: 'http://x', fetchImpl, session: { accessToken: 't', refreshToken: 'r', staffUserId: 's', organizationId: 'o', displayName: 'd', branchIds: ['b'], permissions: [], accessTokenExpiresAtUtc: '', refreshTokenExpiresAtUtc: '' } as never, onSessionChanged: () => {} });
    await client.getDashboardSummaryForRange('b1', '2026-05-20T00:00:00.000Z', '2026-05-20T23:59:59.000Z');
    const url = fetchImpl.mock.calls[0][0] as string;
    expect(url).toContain('/api/branches/b1/dashboard/summary?');
    expect(url).toContain('fromUtc=2026-05-20T00%3A00%3A00.000Z');
    expect(url).toContain('toUtc=2026-05-20T23%3A59%3A59.000Z');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx vitest run src/api/clubApi.range.test.ts`
Expected: FAIL — `getDashboardSummaryForRange` is not a function.

- [ ] **Step 3: Add the method to `ClubApiClient`** (next to `getDashboardSummary`)

```ts
  public getDashboardSummaryForRange(branchId: string, fromUtc: string, toUtc: string): Promise<OperatorDashboardSummary> {
    const query = new URLSearchParams({ fromUtc, toUtc, limit: '3' });
    return this.send<OperatorDashboardSummary>(
      'GET',
      `/api/branches/${encodeURIComponent(branchId)}/dashboard/summary?${query.toString()}`
    );
  }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx vitest run src/api/clubApi.range.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/api/clubApi.ts src/api/clubApi.range.test.ts
git commit -m "feat(api): add dashboard-summary range overload for future reports"
```

---

## Task 14: Final gates

- [ ] **Step 1: Full test run**

Run: `npm test`
Expected: all suites pass (new + existing 12 from prior slices).

- [ ] **Step 2: Type-check + production build**

Run: `npm run build`
Expected: `tsc -b` + `vite build` succeed, 0 errors.

- [ ] **Step 3: i18n completeness check**

Confirm `messages.ru` and `messages.en` have identical key sets (every key used by the shell + Overview is present in both). A quick check:
```bash
node -e "const m=require('./src/i18n/messages.ts');" 2>/dev/null || echo "manual: diff the key lists in src/i18n/messages.ts"
```
Manually verify: both dictionaries list the same keys (the `MessageKey` type is derived from `ru`, so any key missing from `en` would still resolve via fallback — confirm none are missing for shipped screens).

- [ ] **Step 4: Commit any fixups**

```bash
git add -A
git commit -m "chore: final gates for foundation + Overview slice"
```

---

## Self-Review Notes (author)

- **Spec coverage:** tokens+themes (Tasks 1–3), i18n (Task 4), shadcn stack (Task 1), app-shell with role gating + responsive (Tasks 5–8), Overview with loading/empty/error on existing contracts (Tasks 9–12), non-breaking reparenting (Task 12), tests across all (each task), build/test gates (Task 14). The 7-day revenue chart from the mock is intentionally replaced by a today gameplay/POS breakdown (single existing call); the trend chart + range method consumer is deferred to the Reports sub-project (range method seeded in Task 13).
- **Type consistency:** `OverviewViewModel`, `OverviewState`, `AttentionRow.kind`, `ClubRole`, `NavGroup/NavItem`, `MessageKey` are defined once and reused; `useOverview` consumes `buildOverview`; `OverviewScreen` consumes `OverviewState`.
- **No new backend contracts.** All data from existing `ClubApiClient` methods + the param-only range overload.
```
