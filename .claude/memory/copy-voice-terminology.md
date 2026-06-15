---
name: copy-voice-terminology
description: AFK4.NET product copy voice & terminology standard — glossary doc + i18n guard test; catalog normalized; Operator.App.Web full localization deferred (2026-06-04)
metadata: 
  node_type: memory
  type: project
  originSessionId: 9758e85d-c3bf-4ec3-a14b-0e42a3eec377
---

Established 2026-06-04 to de-fragment user-facing text (user: «текст разрозненный»). Grows out of the brand tone-of-voice — see [[afk4-brand-positioning]].

**i18n architecture (already single-source, NOT fragmented):** root `locales/{ru,en,tg}.json` (778+ keys) is the truth → `bun run gen` in `packages/i18n` regenerates `packages/i18n/src/messages.ts`. Edit JSON then ALWAYS regen (a freshness test fails otherwise). Platform.Web's `src/i18n/*` is just a 3-line re-export shim of `@afk4/i18n` (not a duplicate). `.NET` Player.Shell shares the same embedded root locales via `AFK4.Localization`.

**Glossary (canon):** `docs/product/copy-voice-and-terminology.md`. Terms: **ПК** (gaming machine, never «компьютер») · **устройство** (tech/admin device list) · **место** (floor-map seat) · **клуб** = whole business, **филиал** = one location, **сеть** = multi-branch · **киберклуб** = marketing category · **тенант** only in platform admin. Tone: no CAPS, no «!», no emoji. Enforced by guard test `packages/i18n/src/voice.test.ts` (rejects Cyrillic ALL-CAPS word ≥4 letters and the word «компьютер»).

**Done — merged to `main` via PR #53 (commits 4cfb0b1, 1ca3ea1, 0f06711, e0313bf):**
- Catalog normalized: dropped CAPS (`СЕССИЯ АКТИВНА`→`Сессия активна`), `компьютер`→`ПК`, fixed `payments_cards.scope.branch` «Клуб»→«Филиал».
- Platform.Web `App.tsx`: hardcoded screen-title/role maps → `t()` w/ catalog keys (added `platform.tenant.title`, `platform.profile.roleLabel`); unified «Менеджер»→`roles.branch_manager`.
- Customer.Web: ~9 aria-label/sr-only → new `a11y.*` keys via `t()`.
- Operator.App.Web `App.tsx`: `staffRoleLabel` role names aligned to catalog `roles.*` (Кассир-оператор / Управляющий / Бухгалтер).

**Operator.App.Web localization — DONE 2026-06-08 (was "deferred").** The bulk was already migrated in PR #58 (see [[email-identity-parity]]); this session closed the last raw-rendered gaps. **Key architecture fact:** the floor-map *data* layer (`operatorData.ts` seed + `floorMapState.ts` mapping) intentionally stores **English/RU tokens** (`'Lease fresh'`, `'Wallet'`, `'Online · unlocked'`, zones), and localization happens at **render** via label fns in `operatorHelpers.ts` — `commandLabel`/`billingLabel`/`deviceStatusLabel`/`zoneLabel`/`toneLabel`/`appVersionLabel`. So do NOT t()-ify the seed/mapping — it's not a gap. The real raw-string gaps fixed: `apiErrors.projectOperatorError` (now takes `t`, threaded to ~50 call sites), player `status` (now stable keys `vip/active/debt/package/inactive` + `playerStatusLabel`, rendered + filtered on the key), `BackendSettingsWorkspace` ~16 editable prefill defaults → `op.settings.prefill.*`, players note `op.players.note.createdFromCard`, `connectionResolver` default → English sentinel mapped by `localizeResolutionErrorDetail`, and one missed `deviceStatusLabel` wrap in `MapWorkspace.tsx:184`. New keys: `op.error.actionFailed.*`, `op.players.status.*`, `op.settings.prefill.*` (21/locale, real ru/en/tg, tg-guard whitelisted loanwords). Gate: i18n 34/34, operator tsc clean + 181/181. **billingLabel open-tab/cash bug FIXED 2026-06-08 (`a4f21f1`):** it only matched wallet/package/postpaid/guest, so `'Открытый счёт'` and `'Cash'` floor-map tokens fell to "not set". Added `op.helper.billing.openTab`/`.cash` (ru/en/tg) + token matches + table-driven `operatorHelpers.test.ts` asserting every floor-map billing token resolves. Operator 188/188. Minor leftovers: `Customer.Web/components/ui/toast.tsx` aria still hardcoded «Уведомления» (its test renders `ToastProvider` w/o `I18nProvider`); `tg` locale ~98% RU-fallback (only ~16/778 truly translated; parity test doesn't catch RU-identical values); Platform.Web `index.html` `lang="en"` while UI is RU + hardcoded «AFK4 Control Plane» chrome; SetupWizard XAML is English, legacy WPF Operator.App XAML is hardcoded RU (both off the shared catalog).
