# Shared `@afk4/tokens` Color Package — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract the Operator + Setup Wizard color palettes into one shared workspace package (`@afk4/tokens`) so the brand palette has a single source of truth and can't drift again.

**Architecture:** CSS-only package exporting `tokens.css`. Theme-agnostic scales live in `:root`; all colors live under `[data-theme="light"]` / `[data-theme="dark"]`. Both apps already set `data-theme` on `<html>` and default to dark, so the package drops in cleanly. Guard tests (used-vars-defined + WCAG contrast) prevent regressions.

**Tech Stack:** Bun (test + workspace), Vite, raw CSS custom properties.

**Spec:** `docs/superpowers/specs/2026-06-14-shared-color-tokens-design.md`

---

## File Structure

- Create: `packages/tokens/package.json` — workspace package manifest, exports `./tokens.css`.
- Create: `packages/tokens/tokens.css` — the single source of truth (the only runtime artifact).
- Create: `packages/tokens/tokens.test.ts` — guard tests (used-vars-defined + WCAG contrast).
- Create: `packages/tokens/tsconfig.json` — minimal TS config for the test file.
- Modify: `src/AFK4.Operator.App.Web/package.json` — add `@afk4/tokens` dep.
- Modify: `src/AFK4.Operator.App.Web/src/main.tsx` — import package CSS before local styles.
- Modify: `src/AFK4.Operator.App.Web/index.html` — static `data-theme="dark"` + no-flash script.
- Modify: `src/AFK4.Operator.App.Web/src/styles.css` — strip local token blocks.
- Modify: `src/AFK4.SetupWizard.Web/package.json` — add `@afk4/tokens` dep.
- Modify: `src/AFK4.SetupWizard.Web/src/main.tsx` — import package CSS before local styles.
- Modify: `src/AFK4.SetupWizard.Web/index.html` — static `data-theme="dark"` (script already present).
- Modify: `src/AFK4.SetupWizard.Web/src/styles.css` — strip local token blocks.

---

## Task 1: Create the `@afk4/tokens` package files

**Files:**
- Create: `packages/tokens/package.json`
- Create: `packages/tokens/tokens.css`
- Create: `packages/tokens/tsconfig.json`

- [ ] **Step 1: Create `packages/tokens/package.json`**

```json
{
  "name": "@afk4/tokens",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "exports": {
    "./tokens.css": "./tokens.css"
  },
  "scripts": {
    "test": "bun test"
  },
  "devDependencies": {
    "@types/bun": "^1.3.14",
    "typescript": "^6.0.3"
  }
}
```

- [ ] **Step 2: Create `packages/tokens/tsconfig.json`**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "types": ["bun"],
    "strict": true,
    "noEmit": true,
    "skipLibCheck": true
  },
  "include": ["*.ts"]
}
```

- [ ] **Step 3: Create `packages/tokens/tokens.css`** (the full canonical palette)

```css
/* @afk4/tokens — single source of truth for the product palette (Operator + Setup Wizard).
   Colours live under [data-theme]; theme-agnostic scales live in :root. Both consuming apps
   always set data-theme on <html> (incl. a no-flash inline script in index.html), so bare
   :root never needs to carry colour. Brand accent = AFK4 emerald. */

:root {
  /* Radius */
  --radius-xs: 4px;
  --radius-sm: 6px;
  --radius-md: 8px;
  --radius-lg: 12px;
  --radius-pill: 999px;

  /* Motion */
  --duration-fast: 120ms;
  --duration-medium: 200ms;
  --duration-modal: 320ms;
  --ease-out: cubic-bezier(0.16, 1, 0.3, 1);
  --ease-in: cubic-bezier(0.4, 0, 1, 1);
  --ease-spring: cubic-bezier(0.34, 1.4, 0.7, 1);

  /* Focus rings — resolve --accent-ring at the use site, so they pick up the active theme. */
  --focus-ring: 0 0 0 3px var(--accent-ring);
  --focus-ring-danger: 0 0 0 3px rgba(220, 38, 38, 0.22);

  /* Type scale */
  --text-xs: 11px;
  --text-sm: 13px;
  --text-base: 14px;
  --text-md: 16px;
  --text-lg: 20px;
  --text-xl: 26px;
  --text-2xl: 32px;
}

[data-theme="dark"] {
  color-scheme: dark;

  --surface-canvas: #121212;
  --surface-elevated: #1e1e1e;
  --surface-card: #242424;
  --surface-muted: #1a1a1a;
  --surface-sunken: #161616;
  --surface-hover: #232323;
  --surface-accent-soft: #143830;

  --border-soft: #2a2a2a;
  --border-default: #3a3a3a;
  --border-strong: #555555;
  --border-accent: #2f6b58;

  --text-primary: rgba(255, 255, 255, 0.92);
  --text-strong: rgba(255, 255, 255, 0.82);
  --text-secondary: rgba(255, 255, 255, 0.70);
  --text-tertiary: rgba(255, 255, 255, 0.55);
  --text-quaternary: rgba(255, 255, 255, 0.38);
  --text-on-accent: #0a1a14;

  --accent: #2cc592;
  --accent-hover: #3dd9a3;
  --accent-pressed: #1ea877;
  --accent-soft: #143830;
  --accent-ring: rgba(44, 197, 146, 0.32);
  --accent-rgb: 44, 197, 146;
  --accent-glow: rgba(44, 197, 146, 0.30);
  --accent-text: #4dd6a6;
  --accent-on-soft: #c2f0df;
  --accent-bright: #effbf6;

  --danger: #f87171;
  --danger-strong: #c4261d;
  --danger-text: #fca5a5;
  --danger-soft-bg: #2a1414;
  --danger-soft-border: #5a2424;
  --warning: #fbbf24;
  --warning-text: #fcd34d;
  --warning-soft-bg: #2a1f0a;
  --warning-soft-border: #5a3f18;
  --success: #4ade80;
  --success-text: #86efac;
  --success-soft-bg: #15311f;
  --success-soft-border: #1f5135;

  --shadow-card: 0 1px 0 rgba(0, 0, 0, 0.30), 0 8px 24px rgba(0, 0, 0, 0.40);
  --shadow-elevated: 0 1px 0 rgba(0, 0, 0, 0.35), 0 18px 40px rgba(0, 0, 0, 0.50);
  --shadow-press: 0 6px 14px var(--accent-glow);
}

[data-theme="light"] {
  color-scheme: light;

  --surface-canvas: #eef2f7;
  --surface-elevated: #ffffff;
  --surface-card: #f7f9fc;
  --surface-muted: #f8fafc;
  --surface-sunken: #f1f5f9;
  --surface-hover: #f1f7f4;
  --surface-accent-soft: #dcefe6;

  --border-soft: #e2e8f0;
  --border-default: #cbd5e1;
  --border-strong: #94a3b8;
  --border-accent: #8ed0b6;

  --text-primary: #0f172a;
  --text-strong: #334155;
  --text-secondary: #475569;
  --text-tertiary: #64748b;
  --text-quaternary: #94a3b8;
  --text-on-accent: #ffffff;

  --accent: #0b9e74;
  --accent-hover: #0a8a66;
  --accent-pressed: #07664c;
  --accent-soft: #d9e6e1;
  --accent-ring: rgba(11, 158, 116, 0.22);
  --accent-rgb: 11, 158, 116;
  --accent-glow: rgba(11, 158, 116, 0.24);
  --accent-text: #0b9e74;
  --accent-on-soft: #07664c;
  --accent-bright: #06402f;

  --danger: #dc2626;
  --danger-strong: #c4261d;
  --danger-text: #991b1b;
  --danger-soft-bg: #fff1f2;
  --danger-soft-border: #fecaca;
  --warning: #d97706;
  --warning-text: #92400e;
  --warning-soft-bg: #fffbeb;
  --warning-soft-border: #fde68a;
  --success: #16a34a;
  --success-text: #15803d;
  --success-soft-bg: #ecfdf3;
  --success-soft-border: #bbf7d0;

  --shadow-card: 0 1px 0 rgba(15, 23, 42, 0.04), 0 8px 24px rgba(15, 23, 42, 0.08);
  --shadow-elevated: 0 1px 0 rgba(15, 23, 42, 0.04), 0 18px 40px rgba(15, 23, 42, 0.12);
  --shadow-press: 0 6px 14px var(--accent-glow);
}
```

- [ ] **Step 4: Link the workspace**

Run: `~/.bun/bin/bun install`
Expected: completes without error; `@afk4/tokens` now resolvable as a workspace package.

- [ ] **Step 5: Commit**

```bash
git add packages/tokens/package.json packages/tokens/tokens.css packages/tokens/tsconfig.json
git commit -m "feat(tokens): add @afk4/tokens shared palette package"
```

---

## Task 2: Guard tests for the package

**Files:**
- Create: `packages/tokens/tokens.test.ts`

- [ ] **Step 1: Write the test file**

```ts
import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const ROOT = join(import.meta.dir, '..', '..');
const tokensCss = readFileSync(join(import.meta.dir, 'tokens.css'), 'utf8');

const APP_STYLES = [
  'src/AFK4.Operator.App.Web/src/styles.css',
  'src/AFK4.SetupWizard.Web/src/styles.css',
];

function definedVars(css: string): Set<string> {
  const out = new Set<string>();
  for (const m of css.matchAll(/(--[a-z0-9-]+)\s*:/gi)) out.add(m[1]);
  return out;
}

function usedVars(css: string): Set<string> {
  const out = new Set<string>();
  for (const m of css.matchAll(/var\((--[a-z0-9-]+)/gi)) out.add(m[1]);
  return out;
}

// Every var(--x) a consuming app references must be defined either in the shared package
// or locally in that app's own stylesheet (covers app-local aliases like --panel / --chart-*).
describe('used vars are defined', () => {
  const pkgDefs = definedVars(tokensCss);
  for (const rel of APP_STYLES) {
    test(rel, () => {
      const css = readFileSync(join(ROOT, rel), 'utf8');
      const localDefs = definedVars(css);
      const missing = [...usedVars(css)].filter(
        (v) => !pkgDefs.has(v) && !localDefs.has(v) && !v.startsWith('--chart-'),
      );
      expect(missing).toEqual([]);
    });
  }
});

// ── WCAG contrast ──────────────────────────────────────────────────────────────
type RGBA = [number, number, number, number];

function parseColor(c: string): RGBA {
  const s = c.trim();
  if (s.startsWith('#')) {
    const n = parseInt(s.slice(1), 16);
    return [(n >> 16) & 255, (n >> 8) & 255, n & 255, 1];
  }
  const m = s.match(/rgba?\(([^)]+)\)/i);
  if (!m) throw new Error(`unparseable color: ${c}`);
  const p = m[1].split(',').map((x) => parseFloat(x.trim()));
  return [p[0], p[1], p[2], p[3] ?? 1];
}

// Composite a (possibly translucent) foreground over an opaque background.
function over(fg: RGBA, bg: RGBA): RGBA {
  const a = fg[3];
  return [fg[0] * a + bg[0] * (1 - a), fg[1] * a + bg[1] * (1 - a), fg[2] * a + bg[2] * (1 - a), 1];
}

function luminance([r, g, b]: RGBA): number {
  const f = (v: number) => {
    const x = v / 255;
    return x <= 0.03928 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
}

function contrast(fg: string, bg: string): number {
  const bgc = parseColor(bg);
  const l1 = luminance(over(parseColor(fg), bgc));
  const l2 = luminance(bgc);
  const [hi, lo] = l1 > l2 ? [l1, l2] : [l2, l1];
  return (hi + 0.05) / (lo + 0.05);
}

function themeVars(css: string, theme: string): Record<string, string> {
  const block = css.match(new RegExp(`\\[data-theme="${theme}"\\]\\s*\\{([^}]+)\\}`));
  if (!block) throw new Error(`no ${theme} block`);
  const map: Record<string, string> = {};
  for (const m of block[1].matchAll(/(--[a-z0-9-]+)\s*:\s*([^;]+);/gi)) map[m[1]] = m[2].trim();
  return map;
}

describe('WCAG contrast', () => {
  const surfaces = ['--surface-canvas', '--surface-elevated', '--surface-card'];
  for (const theme of ['dark', 'light']) {
    const v = themeVars(tokensCss, theme);
    for (const bg of surfaces) {
      test(`${theme}: text-primary on ${bg} >= 4.5`, () => {
        expect(contrast(v['--text-primary'], v[bg])).toBeGreaterThanOrEqual(4.5);
      });
      test(`${theme}: text-secondary on ${bg} >= 4.5`, () => {
        expect(contrast(v['--text-secondary'], v[bg])).toBeGreaterThanOrEqual(4.5);
      });
      test(`${theme}: text-tertiary on ${bg} >= 3`, () => {
        expect(contrast(v['--text-tertiary'], v[bg])).toBeGreaterThanOrEqual(3);
      });
    }
  }
});
```

- [ ] **Step 2: Run the tests**

Run: `cd packages/tokens && ~/.bun/bin/bun test`
Expected: PASS. The "used vars are defined" tests pass trivially for now (apps still define everything locally); the WCAG tests validate the package values. If any WCAG test fails, the palette value is wrong — fix it in `tokens.css` before continuing.

- [ ] **Step 3: Commit**

```bash
git add packages/tokens/tokens.test.ts
git commit -m "test(tokens): guard used-vars-defined and WCAG contrast"
```

---

## Task 3: Migrate Operator.App.Web onto the package

**Files:**
- Modify: `src/AFK4.Operator.App.Web/package.json`
- Modify: `src/AFK4.Operator.App.Web/src/main.tsx`
- Modify: `src/AFK4.Operator.App.Web/index.html`
- Modify: `src/AFK4.Operator.App.Web/src/styles.css`

- [ ] **Step 1: Add the dependency**

In `src/AFK4.Operator.App.Web/package.json`, add to `"dependencies"` (alphabetical, next to the other `@afk4/*` entries):

```json
    "@afk4/tokens": "workspace:*",
```

- [ ] **Step 2: Import the package CSS first**

In `src/AFK4.Operator.App.Web/src/main.tsx`, replace this line:

```ts
import './styles.css';
```

with:

```ts
import '@afk4/tokens/tokens.css';
import './styles.css';
```

- [ ] **Step 3: Add the no-flash theme script + static default to `index.html`**

In `src/AFK4.Operator.App.Web/index.html`, change `<html lang="ru">` to `<html lang="ru" data-theme="dark">`, and add this `<script>` inside `<head>` right after the `<title>` line:

```html
    <script>
      // No-flash theme init: set data-theme before the first frame so the dark dashboard
      // doesn't flash an unstyled/wrong-theme frame before React mounts. Default dark
      // (cyber-club night context); explicit choice from localStorage wins. Key mirrors
      // STORAGE_KEY in operatorTheme.tsx.
      (function () {
        try {
          var stored = localStorage.getItem('afk4.operator.theme');
          document.documentElement.setAttribute(
            'data-theme',
            stored === 'light' || stored === 'dark' ? stored : 'dark',
          );
        } catch (e) {
          document.documentElement.setAttribute('data-theme', 'dark');
        }
      })();
    </script>
```

- [ ] **Step 4: Strip the local token blocks from `styles.css`**

In `src/AFK4.Operator.App.Web/src/styles.css`:

(a) Reduce the opening `:root { ... }` block so it keeps ONLY the non-token properties — delete every design-token declaration and the `/* ── Design tokens ── */` comment inside it. The block must end up exactly:

```css
:root {
  color-scheme: dark;
  font-family: "Segoe UI", Arial, sans-serif;
  color: var(--text-primary);
  background: var(--surface-canvas);
  font-synthesis: none;
  text-rendering: geometricPrecision;
}
```

(b) Delete the entire `[data-theme="light"] { ... }` block (and its leading `/* Light theme — ... */` comment). All those values now come from `@afk4/tokens`.

Leave everything else in the file untouched (all `.operator-shell`, `.seat-tile`, etc. rules and the `@property --chart-value` / `@keyframes`).

- [ ] **Step 5: Re-link and verify build + typecheck**

Run: `~/.bun/bin/bun install && cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun run build`
Expected: `tsc -b` passes, `vite build` succeeds ("built in ..."). Pre-existing signalr/chunk-size warnings are fine.

- [ ] **Step 6: Verify the guard test still passes**

Run: `cd packages/tokens && ~/.bun/bin/bun test`
Expected: PASS — in particular `src/AFK4.Operator.App.Web/src/styles.css` has no missing vars (proves every token it uses is now provided by the package or kept locally).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/package.json src/AFK4.Operator.App.Web/src/main.tsx src/AFK4.Operator.App.Web/index.html src/AFK4.Operator.App.Web/src/styles.css
git commit -m "refactor(operator): consume shared @afk4/tokens palette"
```

---

## Task 4: Migrate SetupWizard.Web onto the package

**Files:**
- Modify: `src/AFK4.SetupWizard.Web/package.json`
- Modify: `src/AFK4.SetupWizard.Web/src/main.tsx`
- Modify: `src/AFK4.SetupWizard.Web/index.html`
- Modify: `src/AFK4.SetupWizard.Web/src/styles.css`

- [ ] **Step 1: Add the dependency**

In `src/AFK4.SetupWizard.Web/package.json`, add to `"dependencies"` (next to the other `@afk4/*` entries):

```json
    "@afk4/tokens": "workspace:*",
```

- [ ] **Step 2: Import the package CSS first**

In `src/AFK4.SetupWizard.Web/src/main.tsx`, replace:

```ts
import './styles.css';
```

with:

```ts
import '@afk4/tokens/tokens.css';
import './styles.css';
```

- [ ] **Step 3: Add the static default theme to `index.html`**

In `src/AFK4.SetupWizard.Web/index.html`, change `<html lang="ru">` to `<html lang="ru" data-theme="dark">`. (The no-flash `<script>` is already present — leave it as is.)

- [ ] **Step 4: Strip the local token blocks from `styles.css`**

In `src/AFK4.SetupWizard.Web/src/styles.css`:

(a) Reduce the FIRST `:root { ... }` block (the one starting at the top with `color-scheme: light;`) to keep only non-token properties. Change `color-scheme: light;` to `color-scheme: dark;` (the app's default is dark) and delete all `--*` token declarations and the "Light theme defaults" comment. It must end up exactly:

```css
:root {
  color-scheme: dark;
  font-family: "Segoe UI", system-ui, -apple-system, sans-serif;
  font-synthesis: none;
  text-rendering: geometricPrecision;
}
```

(b) Delete the entire `[data-theme="dark"] { ... }` block (and its leading comment). Those values now come from `@afk4/tokens`.

(c) The SECOND `:root { ... }` block (the one holding `--radius-*`, `--shadow-press`, `--focus-ring`, `--duration-*`, `--ease-*`, `--text-*` and `color`/`background`) — delete every `--*` declaration in it (all provided by the package now), keeping only:

```css
:root {
  color: var(--text-primary);
  background: var(--surface-canvas);
}
```

Leave all other rules (`.wizard-*`, `@keyframes`, media queries) untouched.

- [ ] **Step 5: Re-link and verify build + typecheck**

Run: `~/.bun/bin/bun install && cd src/AFK4.SetupWizard.Web && ~/.bun/bin/bun run build`
Expected: `tsc -b` passes, `vite build` succeeds.

- [ ] **Step 6: Verify the guard test still passes**

Run: `cd packages/tokens && ~/.bun/bin/bun test`
Expected: PASS — both app stylesheets report zero missing vars.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.SetupWizard.Web/package.json src/AFK4.SetupWizard.Web/src/main.tsx src/AFK4.SetupWizard.Web/index.html src/AFK4.SetupWizard.Web/src/styles.css
git commit -m "refactor(wizard): consume shared @afk4/tokens palette"
```

---

## Task 5: Full verification + memory

- [ ] **Step 1: Run both apps' unit suites**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test`
Then: `cd src/AFK4.SetupWizard.Web && ~/.bun/bin/bun test`
Expected: both green (no test referenced the old palette; this confirms no breakage).

- [ ] **Step 2: Visual smoke — Operator (mock mode)**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun run dev`, open the printed URL.
Confirm: dashboard + floor map render emerald (no blue), no first-frame flash, the titlebar theme toggle flips dark↔light correctly, light theme is readable. Stop the dev server.

- [ ] **Step 3: Visual smoke — Wizard (preview)**

Run: `cd src/AFK4.SetupWizard.Web && ~/.bun/bin/bun run build && ~/.bun/bin/bun run preview`, open the URL.
Confirm: renders dark by default, emerald accent unchanged, theme toggle works, no flash. Stop preview.

- [ ] **Step 4: Update project memory**

Append a one-line pointer in `C:\Users\mubin\.claude\projects\D--afk4-net\memory\MEMORY.md` and write a memory file `shared-color-tokens.md` recording: `@afk4/tokens` is the single source for the Operator + Wizard palette (CSS-only, theme blocks under `[data-theme]`, both apps default dark with no-flash script); Player.Shell/Platform/Customer are out of scope; guard tests live in `packages/tokens/tokens.test.ts`. Link `[[operator-theme-and-preview]]` and `[[wizard-signin-redesign]]`.

- [ ] **Step 5: Final commit (if memory or any leftover staged)**

```bash
git add -A
git commit -m "chore(tokens): record shared palette in project memory"
```

---

## Notes for the implementer

- Run `bun` via `~/.bun/bin/bun` (per project convention).
- The earlier uncommitted Operator emerald edit in `styles.css` is harmless: Task 3 Step 4 deletes that whole `:root` token block anyway, replacing it with the package. Anchor edits on stable lines (`color-scheme`, `font-family`, the `[data-theme="light"]` selector), not on specific hex values.
- Do NOT touch Platform.Web, Customer.Web, or Player.Shell.Web — out of scope.
- If a WCAG test fails after a palette tweak, adjust the token value, not the threshold.
