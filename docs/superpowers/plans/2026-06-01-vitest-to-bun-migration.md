# Vitest → `bun test` Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace vitest with the native `bun test` runner across both frontend projects (`AFK4.Operator.App.Web`, `AFK4.Platform.Web`), leaving zero vitest dependency, zero `vi.*`/`vitest` imports, and no compatibility shim.

**Architecture:** Each project gets a `bunfig.toml` preload that registers happy-dom + jest-dom matchers (replacing vitest's `environment: 'jsdom'` + `setupFiles`). A shared one-shot codemod rewrites the mechanical `vi.*` surface to `bun:test` (`mock`/`spyOn`/`jest`); four special files (module-mock, fake-timers, `stubGlobal`) are hand-rewritten. Vite stays as dev server/bundler — only the test runner changes. Operator.App.Web is migrated first (smaller, but contains the hardest file) to prove the recipe end-to-end, then Platform.Web.

**Tech Stack:** Bun 1.3.14 (`bun:test`), happy-dom (`@happy-dom/global-registrator`), `@testing-library/react`, `@testing-library/jest-dom`, TypeScript, Vite.

---

## Spec

Design: [docs/superpowers/specs/2026-06-01-vitest-to-bun-migration-design.md](../specs/2026-06-01-vitest-to-bun-migration-design.md)

## Critical environment note

Bun is installed at `C:\Users\mubin\.bun\bin\bun.exe` but is **NOT on PATH** in this
shell. Every bun command below uses the full path. In Bash that is `"$HOME/.bun/bin/bun"`;
in PowerShell `& "$env:USERPROFILE\.bun\bin\bun.exe"`. Commands in this plan use the Bash
form. Do **not** assume a bare `bun` works.

## File map

**Shared (created once, deleted at the end):**
- Create: `scripts/codemod-vitest-to-bun.mjs` — one-shot mechanical rewriter.

**Per project** (`src/AFK4.Operator.App.Web/` and `src/AFK4.Platform.Web/`):
- Create: `bunfig.toml` — test preload.
- Create/rewrite: `src/test/setup.ts` — happy-dom register + jest-dom extend + cleanup.
  (Operator.App.Web has none today — created.)
- Create: `src/test/jest-dom.d.ts` — augments `bun:test` `Matchers` with jest-dom types.
- Modify: `vite.config.ts` — `defineConfig` from `vite`, drop `test` block.
- Modify: `tsconfig.json` — `types` array.
- Modify: `package.json` — `test` script, deps.
- Modify: all `*.test.ts` / `*.test.tsx` (codemod + 2 hand-rewrites each).

**Special files (hand-written, excluded from codemod):**
- `src/AFK4.Operator.App.Web/src/App.test.tsx` — `vi.mock` + `vi.hoisted` + `vi.stubGlobal` + `vi.mocked`.
- `src/AFK4.Operator.App.Web/src/platformApi.test.ts` — `vi.stubGlobal`.
- `src/AFK4.Platform.Web/src/App.test.tsx` — `vi.stubGlobal`.
- `src/AFK4.Platform.Web/src/components/ui/toast.test.tsx` — fake timers.

## Mechanical mapping reference (used by the codemod)

| vitest | bun:test |
| --- | --- |
| `from 'vitest'` | `from 'bun:test'` |
| `vi` (import specifier) | dropped; `mock` / `spyOn` / `jest` added as used |
| `vi.fn` | `mock` |
| `vi.spyOn` | `spyOn` |
| `vi.restoreAllMocks()` | `mock.restore()` |
| `vi.clearAllMocks()` | `jest.clearAllMocks()` |

`mock(...)` keeps the jest-compatible chainable helpers already used in the suite
(`.mockResolvedValue`, `.mockRejectedValue`, `.mockReturnValue`, `.mockImplementation`,
`.mock.calls`) and works with `toHaveBeenCalledWith` / `toHaveBeenCalledTimes`.

---

# Task 1: Add the shared codemod script

**Files:**
- Create: `scripts/codemod-vitest-to-bun.mjs`

- [ ] **Step 1: Write the codemod script**

```js
#!/usr/bin/env bun
// scripts/codemod-vitest-to-bun.mjs
// One-shot: rewrites the mechanical vitest surface in *.test.ts(x) to bun:test.
// Special files are passed via --skip and left untouched for hand-rewriting.
import { readdirSync, readFileSync, writeFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

const [, , srcDir, ...rest] = process.argv;
if (!srcDir) {
  console.error('usage: bun scripts/codemod-vitest-to-bun.mjs <srcDir> [--skip rel/path ...]');
  process.exit(1);
}
const skip = new Set(rest.filter((a) => a !== '--skip').map((p) => p.split('/').join(sep)));

function walk(dir) {
  const out = [];
  for (const name of readdirSync(dir)) {
    if (name === 'node_modules') continue;
    const full = join(dir, name);
    if (statSync(full).isDirectory()) out.push(...walk(full));
    else if (/\.test\.tsx?$/.test(name)) out.push(full);
  }
  return out;
}

function transform(content) {
  let s = content;
  s = s.replace(/\bvi\.fn\b/g, 'mock');
  s = s.replace(/\bvi\.spyOn\b/g, 'spyOn');
  s = s.replace(/\bvi\.restoreAllMocks\b/g, 'mock.restore');
  s = s.replace(/\bvi\.clearAllMocks\b/g, 'jest.clearAllMocks');
  s = s.replace(
    /import\s*\{([^}]*)\}\s*from\s*['"]vitest['"];?/,
    (_full, names) => {
      const kept = names
        .split(',')
        .map((n) => n.trim())
        .filter(Boolean)
        .filter((n) => n !== 'vi');
      for (const helper of ['mock', 'spyOn', 'jest']) {
        if (new RegExp(`\\b${helper}\\b`).test(s) && !kept.includes(helper)) kept.push(helper);
      }
      return `import { ${kept.join(', ')} } from 'bun:test';`;
    }
  );
  return s;
}

let changed = 0;
for (const file of walk(srcDir)) {
  const rel = relative(srcDir, file);
  if (skip.has(rel)) { console.log('skip   ', rel); continue; }
  const before = readFileSync(file, 'utf8');
  if (!before.includes("'vitest'") && !before.includes('"vitest"')) continue;
  const after = transform(before);
  if (after !== before) { writeFileSync(file, after); changed++; console.log('mod    ', rel); }
}
console.log(`\n${changed} files modified.`);
```

- [ ] **Step 2: Sanity-check it parses (no project run yet)**

Run: `"$HOME/.bun/bin/bun" build scripts/codemod-vitest-to-bun.mjs --target=node > /dev/null && echo OK`
Expected: prints `OK` (script is syntactically valid). It is not executed against any
project in this task.

- [ ] **Step 3: Commit**

```bash
git add scripts/codemod-vitest-to-bun.mjs
git commit -m "build: add one-shot vitest->bun:test codemod script"
```

---

# Task 2: Operator.App.Web — bun test infrastructure

**Files:**
- Create: `src/AFK4.Operator.App.Web/bunfig.toml`
- Create: `src/AFK4.Operator.App.Web/src/test/setup.ts`
- Create: `src/AFK4.Operator.App.Web/src/test/jest-dom.d.ts`
- Modify: `src/AFK4.Operator.App.Web/vite.config.ts`
- Modify: `src/AFK4.Operator.App.Web/tsconfig.json`
- Modify: `src/AFK4.Operator.App.Web/package.json`

- [ ] **Step 1: Add `bunfig.toml`**

```toml
[test]
preload = ["./src/test/setup.ts"]
```

- [ ] **Step 2: Create `src/test/setup.ts`**

```ts
import { afterEach, expect } from 'bun:test';
import { GlobalRegistrator } from '@happy-dom/global-registrator';
import * as matchers from '@testing-library/jest-dom/matchers';
import { cleanup } from '@testing-library/react';

GlobalRegistrator.register({ url: 'http://localhost/' });
expect.extend(matchers);

afterEach(() => {
  cleanup();
});
```

- [ ] **Step 3: Create `src/test/jest-dom.d.ts`**

```ts
import type { TestingLibraryMatchers } from '@testing-library/jest-dom/matchers';

declare module 'bun:test' {
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type
  interface Matchers<T> extends TestingLibraryMatchers<typeof expect.stringContaining, T> {}
  interface AsymmetricMatchers extends TestingLibraryMatchers<unknown, unknown> {}
}
```

Note: if `bun run build` in Step 8 reports a generic-arity mismatch on `Matchers`, align
the `interface Matchers<T>` signature with the one declared in
`node_modules/@types/bun` (the compiler error names the exact file and expected shape).

- [ ] **Step 4: Edit `vite.config.ts`** — switch the config import and drop the `test` block.

Replace:
```ts
import { defineConfig } from 'vitest/config';
```
with:
```ts
import { defineConfig } from 'vite';
```
and delete the entire `test: { ... }` block from the config object (keep `base`,
`plugins`, `resolve`). Read the file first to get its exact current shape.

- [ ] **Step 5: Edit `tsconfig.json`** — replace the `types` line.

Replace:
```json
    "types": ["vitest/globals", "@testing-library/jest-dom"]
```
with:
```json
    "types": ["bun", "@testing-library/jest-dom"]
```

- [ ] **Step 6: Edit `package.json`** — set the test script (only this one edit by hand;
deps are changed via `bun` in Step 7):

```json
    "test": "bun test",
```

- [ ] **Step 7: Swap dependencies via bun (resolves correct versions, no guessing)**

```bash
cd src/AFK4.Operator.App.Web
"$HOME/.bun/bin/bun" remove vitest jsdom
"$HOME/.bun/bin/bun" add -d @happy-dom/global-registrator @types/bun
cd ../..
```
Expected: `vitest` + `jsdom` removed from `package.json`/`node_modules`;
`@happy-dom/global-registrator` + `@types/bun` added to `devDependencies` with resolved
versions. This project used npm before — `bun` writes `bun.lock`. Remove the now-stale npm
lockfile so there is a single source of truth:
```bash
git rm src/AFK4.Operator.App.Web/package-lock.json
```

- [ ] **Step 8: Verify the toolchain compiles**

Run: `cd src/AFK4.Operator.App.Web && "$HOME/.bun/bin/bun" run build; cd ../..`
Expected: `tsc -b && vite build` succeeds. Test files still import `'vitest'` at this
point — `tsc` with `moduleResolution: Bundler` will error on the missing `vitest` module.
That is expected and is resolved in Task 3. If the **only** errors are `Cannot find module
'vitest'` (and jsdom type), proceed. If there is a `Matchers` arity error from
`jest-dom.d.ts`, fix per the Step 3 note before moving on.

- [ ] **Step 9: Commit**

```bash
# package-lock.json deletion is already staged via `git rm` in Step 7.
git add src/AFK4.Operator.App.Web/bunfig.toml \
        src/AFK4.Operator.App.Web/src/test/setup.ts \
        src/AFK4.Operator.App.Web/src/test/jest-dom.d.ts \
        src/AFK4.Operator.App.Web/vite.config.ts \
        src/AFK4.Operator.App.Web/tsconfig.json \
        src/AFK4.Operator.App.Web/package.json \
        src/AFK4.Operator.App.Web/bun.lock
git commit -m "build(operator-app-web): bun test infra (happy-dom, jest-dom, bunfig)"
```

---

# Task 3: Operator.App.Web — run the codemod on the non-special files

**Files:**
- Modify: every `src/AFK4.Operator.App.Web/src/**/*.test.ts(x)` except `App.test.tsx`
  and `platformApi.test.ts`.

- [ ] **Step 1: Run the codemod (skipping the two special files)**

```bash
"$HOME/.bun/bin/bun" scripts/codemod-vitest-to-bun.mjs \
  src/AFK4.Operator.App.Web/src \
  --skip App.test.tsx platformApi.test.ts
```
Expected: lists `skip   App.test.tsx`, `skip   platformApi.test.ts`, and `mod   ...` for
the rest, ending with `N files modified.`

- [ ] **Step 2: Spot-check the diff**

Run: `git -C . diff --stat src/AFK4.Operator.App.Web/src`
Expected: only `.test.ts(x)` files changed; each shows the `from 'bun:test'` import swap.
Confirm no file still contains `vi.` outside the two skipped ones:
Run: `grep -rn "vi\\." --include="*.test.ts" --include="*.test.tsx" src/AFK4.Operator.App.Web/src | grep -v -E "App\\.test\\.tsx|platformApi\\.test\\.ts" || echo CLEAN`
Expected: `CLEAN`.

- [ ] **Step 3: Run the codemodded suite (exclude the two special files)**

```bash
cd src/AFK4.Operator.App.Web && "$HOME/.bun/bin/bun" test \
  $(git -C ../.. ls-files 'src/AFK4.Operator.App.Web/src/**/*.test.ts' 'src/AFK4.Operator.App.Web/src/**/*.test.tsx' \
    | grep -v -E 'App\.test\.tsx|platformApi\.test\.ts' | sed 's#^src/AFK4.Operator.App.Web/##')
cd ../..
```
Expected: all listed suites pass. If a happy-dom behavioral difference fails a test
(e.g. an unimplemented DOM API), read the failing source, fix the test minimally, and
re-run. Record any such fix in the commit message.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Operator.App.Web/src
git commit -m "test(operator-app-web): migrate mechanical tests to bun:test"
```

---

# Task 4: Operator.App.Web — hand-rewrite `platformApi.test.ts` (stubGlobal)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/platformApi.test.ts`

- [ ] **Step 1: Read the full file** to see every `vi.fn` / `vi.stubGlobal` /
`vi.unstubAllGlobals` site (the head is known; confirm the rest).

- [ ] **Step 2: Rewrite the import**

Replace:
```ts
import { describe, expect, it, vi } from 'vitest';
```
with:
```ts
import { describe, expect, it, mock } from 'bun:test';
```

- [ ] **Step 3: Capture the original `fetch` once, at module top** (just below imports):

```ts
const originalFetch = globalThis.fetch;
```

- [ ] **Step 4: Replace the `vi.fn` mock and the stubGlobal try/finally**

Replace:
```ts
    const fetchImpl = vi.fn(function (this: unknown) {
      expect(this).toBe(globalThis);
      return Promise.resolve(jsonResponse({ ok: true }));
    });
    vi.stubGlobal('fetch', fetchImpl);

    try {
```
with:
```ts
    const fetchImpl = mock(function (this: unknown) {
      expect(this).toBe(globalThis);
      return Promise.resolve(jsonResponse({ ok: true }));
    });
    globalThis.fetch = fetchImpl as unknown as typeof fetch;

    try {
```
and replace:
```ts
    } finally {
      vi.unstubAllGlobals();
    }
```
with:
```ts
    } finally {
      globalThis.fetch = originalFetch;
    }
```
Apply the same `vi.fn`→`mock` and stubGlobal/unstub treatment to any other occurrences
the Step 1 read surfaced.

- [ ] **Step 5: Run the file**

Run: `cd src/AFK4.Operator.App.Web && "$HOME/.bun/bin/bun" test src/platformApi.test.ts; cd ../..`
Expected: all tests pass. The `this === globalThis` assertion depends on the
`PlatformApiClient` calling the global receiver; if it now fails, inspect how the client
invokes `fetch` (unchanged by this migration) before adjusting.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/platformApi.test.ts
git commit -m "test(operator-app-web): port platformApi stubGlobal to bun:test"
```

---

# Task 5: Operator.App.Web — hand-rewrite `App.test.tsx` (module mock)

This is the hardest file: `vi.mock` + `vi.hoisted` (module mock that must run before the
component import), `vi.stubGlobal`, and 58 `vi.mocked(fetch)` calls.

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx`

- [ ] **Step 1: Replace the file header** (current lines 1–47: imports, `vi.hoisted`,
`vi.mock`, `describe` open, `beforeEach`, `afterEach`) with the following. Everything from
the first `it(` onward is unchanged except the body rules in Steps 2–3.

```tsx
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, mock, type Mock } from 'bun:test';
import type { HostBridgeMessageEvent } from './hostBridge';

const realtimeMock = {
  clients: [] as Array<{
    onConnectionStateChanged?: (state: string) => void;
    onDeviceStatusChanged: (status: unknown) => void;
    onDeviceCommandResult?: (result: unknown) => void;
  }>
};

// bun's mock.module is NOT hoisted above static imports the way vi.mock is, so the
// mock must be registered before the component under test is imported.
const actualRealtime = await import('./operatorRealtime');
mock.module('./operatorRealtime', () => ({
  ...actualRealtime,
  createOperatorRealtimeClient: mock((options: {
    onConnectionStateChanged?: (state: string) => void;
    onDeviceStatusChanged: (status: unknown) => void;
    onDeviceCommandResult?: (result: unknown) => void;
  }) => ({
    start: mock(async () => {
      realtimeMock.clients.push(options);
      options.onConnectionStateChanged?.('connected');
    }),
    stop: mock(async () => options.onConnectionStateChanged?.('disconnected'))
  }))
}));

const { App } = await import('./App');

const originalFetch = globalThis.fetch;
let fetchMock: Mock<typeof fetch>;

describe('App', () => {
  beforeEach(() => {
    realtimeMock.clients.length = 0;
    fetchMock = mock(mockPlatformFetch) as unknown as Mock<typeof fetch>;
    globalThis.fetch = fetchMock as unknown as typeof fetch;
  });

  afterEach(() => {
    cleanup();
    globalThis.fetch = originalFetch;
    delete window.chrome;
    delete window.__AFK4_OPERATOR_CONFIG__;
    localStorage.clear();
    sessionStorage.clear();
    mock.restore();
  });
```

(`mockPlatformFetch` is a hoisted `function` declaration later in the file, so referencing
it in `beforeEach` is fine. The previous `vi.clearAllMocks()` + `vi.restoreAllMocks()` pair
collapses to a single `mock.restore()`.)

- [ ] **Step 2: Replace every `const fetchMock = vi.mocked(fetch);` line** with nothing —
delete the line. The module-level `fetchMock` assigned in `beforeEach` is already in scope,
and each test then calls `fetchMock.mockImplementation(...)` / `fetchMock.mock.calls`
against it.

Run this to confirm none remain:
`grep -n "vi.mocked" src/AFK4.Operator.App.Web/src/App.test.tsx || echo NONE`
Expected eventually: `NONE`.

- [ ] **Step 3: Replace the remaining mechanical calls in the body**

- `vi.fn` → `mock` (every remaining occurrence)
- Confirm no `vi.` token remains:
  `grep -n "vi\\." src/AFK4.Operator.App.Web/src/App.test.tsx || echo NONE` → `NONE`.

- [ ] **Step 4: Run the file**

Run: `cd src/AFK4.Operator.App.Web && "$HOME/.bun/bin/bun" test src/App.test.tsx; cd ../..`
Expected: all `App` suites pass. Likely friction points and fixes:
  - If `App` renders before the realtime mock applies, verify the `await import('./App')`
    sits **after** the `mock.module(...)` call (Step 1 ordering).
  - If a `fetchMock` is `undefined` in a test, that test runs outside the `describe` block's
    `beforeEach` — move it inside, or assign `fetchMock` at its top.
  - happy-dom DOM-API gaps: read the failing assertion, fix minimally.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/App.test.tsx
git commit -m "test(operator-app-web): port App module-mock suite to bun:test"
```

---

# Task 6: Operator.App.Web — full green + final cleanup verification

**Files:** none (verification only).

- [ ] **Step 1: Run the entire suite via the package script**

Run: `cd src/AFK4.Operator.App.Web && "$HOME/.bun/bin/bun" test; cd ../..`
Expected: every suite passes, zero failures.

- [ ] **Step 2: Confirm no vitest residue in the project**

Run: `grep -rn "vitest\|vi\\." --include="*.ts" --include="*.tsx" src/AFK4.Operator.App.Web/src || echo CLEAN`
Expected: `CLEAN`.
Run: `grep -n "vitest\|jsdom" src/AFK4.Operator.App.Web/package.json || echo CLEAN`
Expected: `CLEAN`.

- [ ] **Step 3: Confirm the build still passes**

Run: `cd src/AFK4.Operator.App.Web && "$HOME/.bun/bin/bun" run build; cd ../..`
Expected: `tsc -b && vite build` succeeds with no `vitest`/`vi` errors.

- [ ] **Step 4: Commit (if Steps 1–3 produced any fixups; otherwise skip)**

```bash
git add src/AFK4.Operator.App.Web
git commit -m "test(operator-app-web): finalize bun test migration"
```

---

# Task 7: Platform.Web — bun test infrastructure

Identical recipe to Task 2, applied to `src/AFK4.Platform.Web/`. This project **already
has** `src/test/setup.ts` — it is rewritten, not created.

**Files:**
- Create: `src/AFK4.Platform.Web/bunfig.toml`
- Rewrite: `src/AFK4.Platform.Web/src/test/setup.ts`
- Create: `src/AFK4.Platform.Web/src/test/jest-dom.d.ts`
- Modify: `src/AFK4.Platform.Web/vite.config.ts`
- Modify: `src/AFK4.Platform.Web/tsconfig.json`
- Modify: `src/AFK4.Platform.Web/package.json`

- [ ] **Step 1: Add `bunfig.toml`**

```toml
[test]
preload = ["./src/test/setup.ts"]
```

- [ ] **Step 2: Rewrite `src/test/setup.ts`** to exactly:

```ts
import { afterEach, expect } from 'bun:test';
import { GlobalRegistrator } from '@happy-dom/global-registrator';
import * as matchers from '@testing-library/jest-dom/matchers';
import { cleanup } from '@testing-library/react';

GlobalRegistrator.register({ url: 'http://localhost/' });
expect.extend(matchers);

afterEach(() => {
  cleanup();
});
```

- [ ] **Step 3: Create `src/test/jest-dom.d.ts`** with the same content as Task 2 Step 3:

```ts
import type { TestingLibraryMatchers } from '@testing-library/jest-dom/matchers';

declare module 'bun:test' {
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type
  interface Matchers<T> extends TestingLibraryMatchers<typeof expect.stringContaining, T> {}
  interface AsymmetricMatchers extends TestingLibraryMatchers<unknown, unknown> {}
}
```

- [ ] **Step 4: Edit `vite.config.ts`** — replace `import { defineConfig } from 'vitest/config';`
with `import { defineConfig } from 'vite';` and delete the `test: { environment, setupFiles }`
block (keep `base`, `plugins`, `resolve.alias`).

- [ ] **Step 5: Edit `tsconfig.json`** — replace
`"types": ["vitest/globals", "@testing-library/jest-dom"]` with
`"types": ["bun", "@testing-library/jest-dom"]`.

- [ ] **Step 6: Edit `package.json`** — set `"test": "bun test"` (this edit by hand; deps
via `bun` in Step 7).

- [ ] **Step 7: Swap dependencies via bun**

```bash
cd src/AFK4.Platform.Web
"$HOME/.bun/bin/bun" remove vitest jsdom
"$HOME/.bun/bin/bun" add -d @happy-dom/global-registrator @types/bun
cd ../..
```
Expected: vitest/jsdom removed, happy-dom + @types/bun added with resolved versions.
This project already uses bun, so only `bun.lock` updates (no npm lockfile to remove).

- [ ] **Step 8: Verify toolchain compiles** (vitest import errors in tests are expected
until Task 8)

Run: `cd src/AFK4.Platform.Web && "$HOME/.bun/bin/bun" run build; cd ../..`
Expected: errors only of the form `Cannot find module 'vitest'`. Fix any `Matchers` arity
error per Task 2 Step 3 note.

- [ ] **Step 9: Commit**

```bash
git add src/AFK4.Platform.Web/bunfig.toml \
        src/AFK4.Platform.Web/src/test/setup.ts \
        src/AFK4.Platform.Web/src/test/jest-dom.d.ts \
        src/AFK4.Platform.Web/vite.config.ts \
        src/AFK4.Platform.Web/tsconfig.json \
        src/AFK4.Platform.Web/package.json \
        src/AFK4.Platform.Web/bun.lock
git commit -m "build(platform-web): bun test infra (happy-dom, jest-dom, bunfig)"
```

---

# Task 8: Platform.Web — run the codemod on the non-special files

**Files:**
- Modify: every `src/AFK4.Platform.Web/src/**/*.test.ts(x)` except `App.test.tsx` and
  `components/ui/toast.test.tsx`.

- [ ] **Step 1: Run the codemod (skipping the two special files)**

```bash
"$HOME/.bun/bin/bun" scripts/codemod-vitest-to-bun.mjs \
  src/AFK4.Platform.Web/src \
  --skip App.test.tsx components/ui/toast.test.tsx
```
Expected: `skip   App.test.tsx`, `skip   components/ui/toast.test.tsx`, many `mod   ...`,
then `N files modified.`

- [ ] **Step 2: Confirm no stray `vi.` outside the two skipped files**

Run: `grep -rn "vi\\." --include="*.test.ts" --include="*.test.tsx" src/AFK4.Platform.Web/src | grep -v -E "src/App\\.test\\.tsx|components/ui/toast\\.test\\.tsx" || echo CLEAN`
Expected: `CLEAN`.

- [ ] **Step 3: Run the codemodded suite (everything except the two special files)**

```bash
cd src/AFK4.Platform.Web && "$HOME/.bun/bin/bun" test \
  $(git -C ../.. ls-files 'src/AFK4.Platform.Web/src/**/*.test.ts' 'src/AFK4.Platform.Web/src/**/*.test.tsx' \
    | grep -v -E 'src/App\.test\.tsx|components/ui/toast\.test\.tsx' | sed 's#^src/AFK4.Platform.Web/##')
cd ../..
```
Expected: all pass. For any happy-dom failure, read the source, fix the test minimally,
re-run. Common cases: components relying on layout/measurement APIs happy-dom stubs
differently; `recharts` rendering (ResponsiveContainer) may need a fixed-size wrapper or a
ResizeObserver stub in `src/test/setup.ts` if charts are asserted on.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Platform.Web/src
git commit -m "test(platform-web): migrate mechanical tests to bun:test"
```

---

# Task 9: Platform.Web — hand-rewrite `App.test.tsx` (stubGlobal)

**Files:**
- Modify: `src/AFK4.Platform.Web/src/App.test.tsx`

- [ ] **Step 1: Rewrite the import**

Replace:
```ts
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
```
with:
```ts
import { afterEach, beforeEach, describe, expect, it, mock } from 'bun:test';
```

- [ ] **Step 2: Capture the original `fetch`** — add directly below the import block
(before `renderWithProviders`):

```ts
const originalFetch = globalThis.fetch;
```

- [ ] **Step 3: Replace `vi.fn` → `mock`** everywhere (the `buildClubFetchMock` helper and
the inline `beforeEach` mock both use `vi.fn`).

- [ ] **Step 4: Replace the `beforeEach` stubGlobal** — change:
```ts
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL) => {
```
to:
```ts
    globalThis.fetch = mock(async (input: RequestInfo | URL) => {
```
and change the matching close of that call:
```ts
        return jsonResponse(200, []);
      })
    );
```
to:
```ts
        return jsonResponse(200, []);
      }) as unknown as typeof fetch;
```

- [ ] **Step 5: Replace the `afterEach` unstub** — change `vi.unstubAllGlobals();` to
`globalThis.fetch = originalFetch;`.

- [ ] **Step 6: Replace the per-test stubGlobal calls** — there are several of the form:
```ts
    const fetchMock = buildClubFetchMock();
    vi.stubGlobal('fetch', fetchMock);
```
Change each `vi.stubGlobal('fetch', fetchMock);` to
`globalThis.fetch = fetchMock as unknown as typeof fetch;`. And the one inline form
`vi.stubGlobal('fetch', buildClubFetchMock());` becomes
`globalThis.fetch = buildClubFetchMock() as unknown as typeof fetch;`.

Confirm none remain: `grep -n "vi\\." src/AFK4.Platform.Web/src/App.test.tsx || echo NONE`
→ `NONE`.

- [ ] **Step 7: Run the file**

Run: `cd src/AFK4.Platform.Web && "$HOME/.bun/bin/bun" test src/App.test.tsx; cd ../..`
Expected: all routing/audience/sign-in suites pass. These tests read
`window.location.pathname` and call `window.history.replaceState` — happy-dom was
registered with `url: 'http://localhost/'`, so navigation resolves. If a redirect
assertion fails on pathname, verify the registrator `url` option is set in `setup.ts`.

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Platform.Web/src/App.test.tsx
git commit -m "test(platform-web): port App stubGlobal suite to bun:test"
```

---

# Task 10: Platform.Web — hand-rewrite `toast.test.tsx` (fake timers)

**Files:**
- Modify: `src/AFK4.Platform.Web/src/components/ui/toast.test.tsx`

- [ ] **Step 1: Replace the whole file** with the bun fake-timers version:

```tsx
import { render, screen, fireEvent, act } from '@testing-library/react';
import { it, expect, beforeEach, afterEach, jest } from 'bun:test';
import { ToastProvider, useToast } from './toast';
import { I18nProvider } from '@/i18n/I18nProvider';

beforeEach(() => { jest.useFakeTimers(); });
afterEach(() => { jest.useRealTimers(); });

function Trigger() {
  const { toast } = useToast();
  return <button onClick={() => toast({ title: 'Сохранено', variant: 'success' })}>fire</button>;
}

it('shows a toast when fired and dismisses after the delay', () => {
  render(<I18nProvider><ToastProvider autoDismissMs={1000}><Trigger /></ToastProvider></I18nProvider>);
  expect(screen.queryByText('Сохранено')).toBeNull();
  fireEvent.click(screen.getByText('fire'));
  expect(screen.getByText('Сохранено')).toBeInTheDocument();
  act(() => { jest.advanceTimersByTime(1000); });
  expect(screen.queryByText('Сохранено')).toBeNull();
});

it('throws when useToast is used outside the provider', () => {
  function Orphan() { useToast(); return null; }
  expect(() => render(<Orphan />)).toThrow();
});
```

- [ ] **Step 2: Run the file**

Run: `cd src/AFK4.Platform.Web && "$HOME/.bun/bin/bun" test src/components/ui/toast.test.tsx; cd ../..`
Expected: both tests pass.

- [ ] **Step 3: Fallback if fake timers don't drive the auto-dismiss** — if the first test
fails because the toast does not disappear after `advanceTimersByTime` (bun's fake timers
not intercepting the provider's `setTimeout` under happy-dom), switch that test to real
timers with `waitFor`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect } from 'bun:test';
import { ToastProvider, useToast } from './toast';
import { I18nProvider } from '@/i18n/I18nProvider';

function Trigger() {
  const { toast } = useToast();
  return <button onClick={() => toast({ title: 'Сохранено', variant: 'success' })}>fire</button>;
}

it('shows a toast when fired and dismisses after the delay', async () => {
  render(<I18nProvider><ToastProvider autoDismissMs={50}><Trigger /></ToastProvider></I18nProvider>);
  expect(screen.queryByText('Сохранено')).toBeNull();
  fireEvent.click(screen.getByText('fire'));
  expect(screen.getByText('Сохранено')).toBeInTheDocument();
  await waitFor(() => expect(screen.queryByText('Сохранено')).toBeNull());
});

it('throws when useToast is used outside the provider', () => {
  function Orphan() { useToast(); return null; }
  expect(() => render(<Orphan />)).toThrow();
});
```

Re-run Step 2 and confirm green.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Platform.Web/src/components/ui/toast.test.tsx
git commit -m "test(platform-web): port toast timer suite to bun:test"
```

---

# Task 11: Platform.Web — full green + final cleanup verification

**Files:** none (verification only).

- [ ] **Step 1: Run the entire suite via the package script**

Run: `cd src/AFK4.Platform.Web && "$HOME/.bun/bin/bun" test; cd ../..`
Expected: every suite passes, zero failures. (This is the ~140-file run.)

- [ ] **Step 2: Confirm no vitest residue**

Run: `grep -rn "vitest\|vi\\." --include="*.ts" --include="*.tsx" src/AFK4.Platform.Web/src || echo CLEAN`
Expected: `CLEAN`.
Run: `grep -n "vitest\|jsdom" src/AFK4.Platform.Web/package.json || echo CLEAN`
Expected: `CLEAN`.

- [ ] **Step 3: Confirm the build still passes**

Run: `cd src/AFK4.Platform.Web && "$HOME/.bun/bin/bun" run build; cd ../..`
Expected: `tsc -b && vite build` succeeds.

- [ ] **Step 4: Commit (only if Steps 1–3 produced fixups)**

```bash
git add src/AFK4.Platform.Web
git commit -m "test(platform-web): finalize bun test migration"
```

---

# Task 12: Repo-wide cleanup and memory update

**Files:**
- Delete: `scripts/codemod-vitest-to-bun.mjs`
- Modify: `C:\Users\mubin\.claude\projects\D--afk4-net\memory\platform-web-redesign.md` (and `MEMORY.md` pointer if the hook line needs it)

- [ ] **Step 1: Final repo-wide residue check** across both projects:

Run: `grep -rn "from 'vitest'\|from \"vitest\"\|vitest/config\|vi\\." --include="*.ts" --include="*.tsx" src/AFK4.Operator.App.Web/src src/AFK4.Platform.Web/src || echo CLEAN`
Expected: `CLEAN`.

- [ ] **Step 2: Delete the one-shot codemod script** (it has served its purpose):

```bash
git rm scripts/codemod-vitest-to-bun.mjs
```

- [ ] **Step 3: Update the auto-memory** — append a line to
`platform-web-redesign.md` (or add a short dedicated note) recording that both web
frontends now run `bun test` (happy-dom + jest-dom via `bunfig.toml` preload), vitest/jsdom
removed; run tests with `bun test` (bun not on PATH — `~/.bun/bin/bun`). Keep `MEMORY.md`
to one-line pointers only.

- [ ] **Step 4: Final commit**

```bash
git add -A
git commit -m "build: drop one-shot codemod; both web frontends on bun test"
```

- [ ] **Step 5: Report** the final state to the user: both suites green under `bun test`,
both builds passing, vitest fully removed. Note that CI (`pr-verification.yml`) runs only
`dotnet test` and was unaffected.

---

## Self-review notes

- **Spec coverage:** config (bunfig/setup/vite/tsconfig/package) → Tasks 2,7; codemod of
  the 96 `vi.*` files + import swap of all ~150 → Tasks 3,8; special files A/B/C/D →
  Tasks 5 (Operator App + mocked), 4 (Operator platformApi stubGlobal), 9 (Platform App
  stubGlobal), 10 (toast timers); verification (`bun test` + `bun run build` + residue
  grep) → Tasks 6,11,12. All spec sections map to a task.
- **`vi.mocked` (58×):** entirely inside Operator `App.test.tsx`; handled in Task 5 Step 2,
  never reaches the codemod.
- **Type augmentation:** `@types/bun` added (bun ships no local types; none was installed),
  jest-dom matchers augmented via `bun:test` module declaration, gated by `bun run build`.
- **PATH:** every bun invocation uses `~/.bun/bin/bun` — bun is not on PATH here.
```
