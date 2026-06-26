---
name: frontends-on-bun-test
description: Both web frontends run bun test (not vitest) — how to run tests and the gotchas
metadata: 
  node_type: memory
  type: reference
  originSessionId: cd3d93a7-ca01-4d00-abb8-ac33761d21cd
---

Both Vite/React frontends — `src/AFK4.Platform.Web` and `src/AFK4.Operator.App.Web` — were migrated off vitest to the native **`bun test`** runner (2026-06-01). vitest + jsdom are fully removed.

**Run tests:** bun is NOT on PATH here — use the full path. `cd` into a project and run `"$HOME/.bun/bin/bun" test` (Bash) / `& "$env:USERPROFILE\.bun\bin\bun.exe" test` (PowerShell). `bun run build` = `tsc -b && vite build`.

**Setup wiring:** each project has `bunfig.toml` with `[test] preload = ["./src/test/setup.ts"]`. `setup.ts` registers happy-dom (`@happy-dom/global-registrator`) + extends `expect` with `@testing-library/jest-dom/matchers`. CRITICAL: it must `await import('@testing-library/react')` for `cleanup` AFTER `GlobalRegistrator.register()` — a static testing-library import binds `document` too early and every render test throws "a global document has to be available". jest-dom matcher types are augmented onto `bun:test` in `src/test/jest-dom.d.ts`.

**Gotchas learned:**
- **`tsc -b` ТАЙПЧЕКАЕТ тест-файлы** (они в проекте), хотя сам `bun test` runner — нет. Значит зелёный `bun test` ещё НЕ гарантирует зелёную сборку: типовые косяки в `*.test.tsx` валят `bun run build`. Конкретные грабли: bun-мок `mock(async () => ...)` инферится как zero-arg → `.mock.calls[0]` типизируется пустым кортежем (нельзя `const [, req] = …`), а 2-аргументный `.mockImplementation((a,b)=>…)` не подходит. Фикс: дать мок-фабрике сигнатуру — `mock(async (_branchId: string, _request: Record<string, unknown>) => ({…}))` (тогда и `calls`, и `mockImplementation` типобезопасны); либо проектный идиом — каст `mock.calls[0] as unknown as [..]`. ВСЕГДА гонять `bun run build` в финале слайса, не только `bun test` (поймано на Складе S1 — финальный гейт вскрыл красный tsc при зелёных тестах).
- `@types/bun` (needed for `bun:test` types) redefines the global `fetch` type with a required `preconnect` member. `bun test` doesn't typecheck so it stays green, but `tsc -b` breaks where a mock/plain fn is used as `typeof fetch`. Fix: api clients expose `fetchImpl?: FetchLike` (a browser-fetch alias in `@/api/types`), not `typeof fetch`.
- bun's `mock.module` is NOT hoisted above static imports (unlike vitest `vi.mock`) and leaks process-wide / can't be reliably restored. Operator `App.test.tsx` registers the operatorRealtime mock then `await import('./App')`; the preload snapshots the real module onto globalThis so `operatorRealtime.test.ts` still sees the real client.
- `vi.stubGlobal` → manual `globalThis.fetch = … as unknown as typeof fetch` with capture/restore; `vi.fn`→`mock`, `vi.spyOn`→`spyOn`, `vi.useFakeTimers`→`jest.useFakeTimers` (bun's fake timers work under happy-dom).

Related: [[platform-web-redesign]]
