# Vitest → `bun test` Migration — Design

**Date:** 2026-06-01
**Scope:** Replace vitest with the native `bun test` runner across both frontend projects.

## Goal

Fully remove vitest from the repository's two Vite/React frontends and run their test
suites with `bun test`. Vite remains the dev server and production bundler — only the
**test runner** changes. End state: zero vitest dependency, zero `vi.*` / `vitest`
imports, no compatibility shim.

## Affected projects

| Project | Test files | Notes |
| --- | --- | --- |
| `src/AFK4.Platform.Web` | ~140 | Already on Bun as package manager / script runner. Has `src/test/setup.ts`. |
| `src/AFK4.Operator.App.Web` | ~10 | Has **no** `src/test/setup.ts` today — one is created. |

C# backend tests (`tests/AFK4.*.Tests`, run via `dotnet test`) are out of scope.
CI (`.github/workflows/pr-verification.yml`) runs only `dotnet test`; it does **not**
run the web suites, so no workflow changes are required.

## Toolchain

- Bun **1.3.14** is installed at `C:\Users\mubin\.bun\bin\bun.exe` but is **not** on the
  shell PATH in this environment. All bun commands must be invoked by full path, or
  `~/.bun/bin` added to PATH for the session.

## Decisions

- **DOM environment:** happy-dom via `@happy-dom/global-registrator` (bun's recommended
  path), replacing vitest's `environment: 'jsdom'`. jsdom is dropped.
- **`vi.*` surface:** full native rewrite to the `bun:test` API. No `vi` compat shim.
- **jest-dom matchers:** `expect.extend(matchers)` from `@testing-library/jest-dom/matchers`,
  replacing the `@testing-library/jest-dom/vitest` auto-registration.

## Per-project changes

Applied identically to **both** projects unless noted.

### 1. `bunfig.toml` (new, one per project)

```toml
[test]
preload = ["./src/test/setup.ts"]
```

This wires the DOM and matchers before any test runs (bun's equivalent of vitest
`setupFiles`).

### 2. `src/test/setup.ts` (rewritten; created for Operator.App.Web)

Bun-native setup:

```ts
import { afterEach, expect } from 'bun:test';
import { GlobalRegistrator } from '@happy-dom/global-registrator';
import * as matchers from '@testing-library/jest-dom/matchers';
import { cleanup } from '@testing-library/react';

GlobalRegistrator.register();
expect.extend(matchers);
afterEach(() => { cleanup(); });
```

(Replaces the current `import '@testing-library/jest-dom/vitest'` + `afterEach` from
`'vitest'`.)

### 3. `vite.config.ts`

- Change `defineConfig` import from `'vitest/config'` → `'vite'`.
- Delete the `test: { environment, setupFiles }` block.
- Keep `base`, `plugins` (react, tailwind), and the `@` resolve alias — the build is
  unaffected.

### 4. `tsconfig.json`

- `types: ["vitest/globals", "@testing-library/jest-dom"]`
  → `types: ["bun", "@testing-library/jest-dom"]`.
- The `@/*` path alias is resolved natively by bun from `paths`; no extra test config.

### 5. `package.json`

- Script: `"test": "vitest run"` → `"test": "bun test"`.
- Remove devDeps: `vitest`, `jsdom`.
- Add devDep: `@happy-dom/global-registrator`. (`bun-types` / `@types/bun` for the
  `"bun"` tsconfig type, if not already resolvable.)
- Keep `@testing-library/react`, `@testing-library/jest-dom`.
- Run `bun install` to update the lockfile and prune removed packages.

## Test-file rewrite (mechanical)

A codemod pass over all `*.test.ts` / `*.test.tsx`, verified by running the suite
afterward. The `'vitest'` → `'bun:test'` import swap applies to **every** test file
(~150 across both projects); the `vi.*` mappings apply to the **96** files that use them.
The `vi` identifier is always the vitest import, so token-level replacement is safe:

| vitest | bun:test |
| --- | --- |
| `from 'vitest'` | `from 'bun:test'` |
| `vi.fn(` | `mock(` |
| `vi.spyOn(` | `spyOn(` |
| `vi.clearAllMocks()` | `jest.clearAllMocks()` |
| `vi.restoreAllMocks()` | `mock.restore()` |

- `mock(...)` returned by bun keeps the jest-compatible chainable helpers used in the
  suite: `.mockResolvedValue`, `.mockRejectedValue`, `.mockReturnValue`,
  `.mockImplementation`.
- Import statements are updated to pull `mock`, `spyOn`, and/or `jest` from `bun:test`
  alongside the existing `describe` / `it` / `expect` / lifecycle hooks.

## Special cases (hand-written, not codemod)

### A. `src/AFK4.Operator.App.Web/src/App.test.tsx` — module mock

Uses `vi.mock('./operatorRealtime', importOriginal)` + `vi.hoisted` (85 `vi.*` calls).
Bun's `mock.module` is **not** hoisted above static `import` statements the way `vi.mock`
is, so the module under test must be imported *after* the mock is registered:

- Replace the `vi.hoisted(() => ({...}))` wrapper with a plain `const`.
- Register `mock.module('./operatorRealtime', () => ({ ...actual, createOperatorRealtimeClient: mock(...) }))`
  where `actual` comes from `await import('./operatorRealtime')`.
- Load the component under test via dynamic `import('./App')` after the mock is in place
  (e.g. top-level await, or in a `beforeAll`).

### B. `src/AFK4.Platform.Web/src/components/ui/toast.test.tsx` — fake timers

Uses `vi.useFakeTimers()` / `vi.advanceTimersByTime()` / `vi.useRealTimers()`. Map to
bun's `jest.useFakeTimers()` / `jest.advanceTimersByTime()` / `jest.useRealTimers()`
(supported in bun 1.3). If behavior differs (auto-dismiss `setTimeout` not advancing),
fall back to real timers + `waitFor`.

### C. `vi.stubGlobal` (3 files)

`Operator/App.test.tsx`, `Operator/platformApi.test.ts`, `Platform/App.test.tsx`.
Bun has no `stubGlobal`/`unstubAllGlobals`. Replace with explicit save in `beforeEach`
and restore in `afterEach` of the patched global (e.g. `fetch`).

### D. Config-test files (no special handling beyond the codemod)

`viteConfig.test.ts` imports `../vite.config` and asserts `config.base === '/'`. The
default export still exposes `.base` after switching `defineConfig` to `'vite'`, so the
test stays valid — only its `'vitest'` import is swapped to `'bun:test'`.

## Verification

1. `bun test` passes in **both** projects (full suites green).
2. `bun run build` (`tsc -b && vite build`) passes in both with the updated tsconfig
   `types`.
3. No remaining references to `vitest` or `vi.` in `src/**` of either project
   (lockfiles / node_modules excluded).

## Risks

- **happy-dom vs jsdom** behavioral differences may break a handful of component tests;
  fixed individually as they surface.
- **toast fake timers** under bun (case B) may need the real-timer fallback.
- **module-mock restructure** (case A) is the most intricate single file.
