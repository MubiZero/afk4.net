# Epic: email-identity-parity (M3 — Operator i18n + email/SMS reset)

**Status: M3 COMPLETE — PR #58 open** (`feature/email-identity-parity` → `main`), HEAD `ee41166`.
Goal of epic: email is a co-equal alternative to phone for staff identity (login / register / reset). M1+M2 merged earlier (PR #57). M3 = Operator desktop app.

## What M3 delivered
- **ICU i18n engine** (`@afk4/i18n`): `t(key, values?)` via `intl-messageformat` (interpolation + per-locale plurals), backward-compatible. Added `createTranslator(locale)` for non-React modules / unit tests.
- **.NET host (Phase C, earlier session)**: 4 reset bridge ops + structured errors (`code`/`remainingAttempts`).
- **Operator web**: `<I18nProvider>`, channel-aware Forgot/Reset screens, login relabel + «Забыли пароль?», email login free from M1.
- **Full Operator localization (ru/en/tg, honest)**: all 9 workspace screens, App shell + nav + signals strip, the entire `operatorHelpers` label layer, and `floorMapState` / `checkoutState` / `actionOutbox`. `pluralRu` deleted.

## Translation policy
Real ru/en/tg everywhere (parity + voice guards). ru byte-exact to old literals (keeps ru-rendered tests green). tg = real Tajik (Cyrillic), not ru copies — except true loanwords (Платформа, оператор, ПК…).

## Deliberate scope cuts (product owner approved)
- `apiErrors.projectOperatorError` left raw: `.title` never rendered (dead), `.detail` is pass-through; 55+ call-site cascade not worth one generic fallback.
- `operatorData.seats` demo fixtures (~110) left raw: seed data shown only before backend loads; backend map path IS localized.
- `connectionResolver` default error left raw (consistent with apiErrors); `BackendPlayersWorkspace` API note 'Создано из карточки клиента' raw (pre-existing).

## Sentinels kept raw on purpose (compared, not displayed-raw): `'нет смены'`, `'Неактивен'`, billing tokens fed to `billingLabel`, English version tokens fed to `appVersionLabel`/`deviceStatusLabel`, `matchesLogSource` substring heuristics (`'касс'`/`'чек'`/`'оператор'`).

## Verification (this machine)
i18n 32/32 · Operator tsc clean + 181/181 + vite build ✓ · Platform.Web 392/392 + tsc ✓ · Customer.Web 66/67 (the 1 fail = pre-existing flaky `toast auto-dismiss` timer test, passes in isolation, unrelated to i18n).
**NOT run here (no .NET SDK on this machine):** `AFK4.Operator.App.Tests` (host) + `Platform.Api.Tests`. This session changed ZERO .cs files (only .tsx/.ts/.json), so they're unaffected — host was 237/237 when Phase C landed. **Confirm in CI.**

## Incidents caught in review & fixed (subagent-introduced)
1. **Fake-green**: a subagent made `t` optional in `operatorHelpers` with Russian fallbacks → ~50 UI call sites showed Russian under en/tg (tsc + ru-tests don't catch). Fixed: `t` made required, all call sites threaded.
2. **Mojibake** from PowerShell file writes: `—`→`вЂ"`, `…`→`вЂ¦`, `×`→`Г—`, tg `муваққатан`→latin `qq`, plus stray BOMs on 4 .tsx. All repaired via Edit/bun. **Lesson: subagents writing files via PowerShell Set-Content corrupt UTF-8 — have them use Edit/Write tools only.**

## Out-of-scope finding (future epic, owner = native speaker)
~79% of the PRE-EXISTING catalog has `tg === ru` (legacy fake-Tajik across ALL frontends). M3's new keys are honest; no new fake tg introduced.

## Environment note (this machine = C:\projects\afk4.net)
bun on PATH (`C:\Users\mubin\.bun\bin\bun`); no .NET SDK; the external superpowers memory graph + skill repos (D:\claude-working-style #35–38, D:\interface-limb) are NOT synced here — repo-tracked `.claude/memory` is the reliable cross-device channel.
