---
name: frontends-on-bun-test
description: Both web frontends run bun test (not vitest) — how to run tests and the gotchas
metadata: 
  node_type: memory
  type: reference
  originSessionId: cd3d93a7-ca01-4d00-abb8-ac33761d21cd
---

Both Vite/React frontends — `src/AFK4.PlatformControl.Web` and `src/AFK4.OrganizationAdmin.Web` — were migrated off vitest to the native **`bun test`** runner (2026-06-01). vitest + jsdom are fully removed.

**Run tests:** bun is NOT on PATH here — use the full path. `cd` into a project and run `"$HOME/.bun/bin/bun" test` (Bash) / `& "$env:USERPROFILE\.bun\bin\bun.exe" test` (PowerShell). `bun run build` = `tsc -b && vite build`.

**Setup wiring:** each project has `bunfig.toml` with `[test] preload = ["./src/test/setup.ts"]`. `setup.ts` registers happy-dom (`@happy-dom/global-registrator`) + extends `expect` with `@testing-library/jest-dom/matchers`. CRITICAL: it must `await import('@testing-library/react')` for `cleanup` AFTER `GlobalRegistrator.register()` — a static testing-library import binds `document` too early and every render test throws "a global document has to be available". jest-dom matcher types are augmented onto `bun:test` in `src/test/jest-dom.d.ts`.

**Gotchas learned:**
- **`tsc -b` ТАЙПЧЕКАЕТ тест-файлы** (они в проекте), хотя сам `bun test` runner — нет. Значит зелёный `bun test` ещё НЕ гарантирует зелёную сборку: типовые косяки в `*.test.tsx` валят `bun run build`. Конкретные грабли: bun-мок `mock(async () => ...)` инферится как zero-arg → `.mock.calls[0]` типизируется пустым кортежем (нельзя `const [, req] = …`), а 2-аргументный `.mockImplementation((a,b)=>…)` не подходит. Фикс: дать мок-фабрике сигнатуру — `mock(async (_branchId: string, _request: Record<string, unknown>) => ({…}))` (тогда и `calls`, и `mockImplementation` типобезопасны); либо проектный идиом — каст `mock.calls[0] as unknown as [..]`. ВСЕГДА гонять `bun run build` в финале слайса, не только `bun test` (поймано на Складе S1 — финальный гейт вскрыл красный tsc при зелёных тестах).
- `@types/bun` (needed for `bun:test` types) redefines the global `fetch` type with a required `preconnect` member. `bun test` doesn't typecheck so it stays green, but `tsc -b` breaks where a mock/plain fn is used as `typeof fetch`. Fix: api clients expose `fetchImpl?: FetchLike` (a browser-fetch alias in `@/api/types`), not `typeof fetch`.
- **Финальный гейт слайса обязан гонять и `packages/i18n` тесты, не только приложение.** tg-honesty guard (`tg===ru` → fake-перевод, кроме whitelist `TG_IDENTICAL_TO_RU_ALLOWED`) и прочие i18n-guard живут в `packages/i18n/src/messages.test.ts`. `bun test` из `src/AFK4.OrganizationAdmin.Web` их НЕ видит → можно словить красный CI при «зелёных 558 pass» (поймано на Складе S2: новый `op.stock.journal.csv.sku` имел tg="SKU"===ru → нужно добавить акроним в whitelist). Запускать `bun test` в КАЖДОМ затронутом пакете (или из корня), а не только в приложении.
- **Превью (dev-сервер) протухает на i18n-правках.** Vite ПРЕД-БАНДЛИТ workspace-пакет `@afk4/i18n` в dep-кэш при старте; правки `messages.ts` (после `bun run gen`) НЕ подхватываются по HMR — старый запущенный `bun run dev` показывает ГОЛЫЕ ключи (`op.stock.title` вместо «Склад») для всех ключей, добавленных после старта сервера. Фикс: перезапустить dev-сервер (`rm -rf node_modules/.vite` + `bun run dev -- --force`). Каталог/CI при этом correct — это только протухший preview. (Поймано S2: пользователь увидел голые `op.stock.*` в живом превью.)
- bun's `mock.module` is NOT hoisted above static imports (unlike vitest `vi.mock`) and leaks process-wide / can't be reliably restored. Operator `App.test.tsx` registers the operatorRealtime mock then `await import('./App')`; the preload snapshots the real module onto globalThis so `operatorRealtime.test.ts` still sees the real client.
- `vi.stubGlobal` → manual `globalThis.fetch = … as unknown as typeof fetch` with capture/restore; `vi.fn`→`mock`, `vi.spyOn`→`spyOn`, `vi.useFakeTimers`→`jest.useFakeTimers` (bun's fake timers work under happy-dom).

Related: [[platform-web-redesign]]
