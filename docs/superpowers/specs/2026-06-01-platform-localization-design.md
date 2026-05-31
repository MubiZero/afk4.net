# Localization / i18n Consistency Across All Surfaces

- **Date:** 2026-06-01
- **Status:** Design (decisions proposed; pending founder review)
- **Scope owner:** Platform (AFK4)
- **Related:** [[platform-web-redesign]], full-product UX audit (2026-06-01); notifications backbone (`2026-06-01-platform-notifications-backbone-design.md`); customer shell experience (`2026-06-01-platform-customer-shell-experience-design.md`); counter loop / postpaid checkout (`2026-06-01-platform-counter-loop-postpaid-checkout-design.md`)

## 1. Context & Problem

AFK4 serves a Russian-speaking / Tajikistan market (currency Somoni, `TJS`). The product has
five surfaces and **no shared localization story** — each surface handled language independently,
and most never handled it at all. The result is an inconsistent, partly-English UI in a RU/TJ
market, with no path to the local language (Tajik).

Ground truth, verified in the tree:

- **AFK4.Platform.Web (React) — has full i18n, but it is invisible and incomplete.**
  `src/AFK4.Platform.Web/src/i18n/` holds a hand-rolled provider (`I18nProvider.tsx`), a
  `messages.ts` catalog of **1292 keys** across two locales (`ru`, `en`), and `Intl`-based
  number/currency/date formatting. **But:**
  - `setLocale` exists on the context (`I18nProvider.tsx:35`) yet is **never called from any UI** —
    a tree-wide search for `setLocale` outside `i18n/` returns nothing. There is **no language
    switcher**.
  - The locale is seeded only from `initialLocale = 'ru'` (`I18nProvider.tsx:15`) held in `useState`.
    There is **no persistence** — no `localStorage`, no cookie, no profile field. A switch would not
    survive reload.
  - `LOCALE_TAG` maps only `{ ru: 'ru-RU', en: 'en-US' }` (`I18nProvider.tsx:13`) — no Tajik.
- **AFK4.Operator.App.Web (React) — no i18n at all.** `src/AFK4.Operator.App.Web/src/App.tsx`
  hard-codes Russian throughout (hundreds of Cyrillic literals; e.g. `"Управление выбранным ПК"`,
  `"Фильтр ПК"`, `"Сводка продаж"`). There is no provider, no catalog, no formatter.
- **AFK4.Operator.App (WPF) — no i18n at all.** Hard-coded Russian is spread across
  `MainWindow.xaml` (~82 Cyrillic literals) and view-models. Examples: seat-state labels in
  `FloorMap/FloorMapSeatViewModel.cs:101-109` (`"В сессии"`, `"Свободно"`, `"Готов"`, `"Пауза"`,
  `"Ошибка"`, `"Офлайн"`, `"Сервис"` …) and sign-in errors in `Auth/SignInViewModel.cs:85,91,113`.
  Currency is hard-coded `"TJS"` in **at least eight** files (`Configuration/OperatorAppOptions.cs:12`,
  `FloorMap/SeatContextPanelViewModel.cs:16`, `Players/PlayerSearchViewModel.cs:18,610,612`,
  `Pos/PosWorkspaceViewModel.cs:19`, `Shifts/ShiftWorkspaceViewModel.cs:21`, …). Money is formatted
  `(<minor>/100m).ToString("0.00", CultureInfo.InvariantCulture)` (`Players/PlayerSearchViewModel.cs:592,645`)
  — **invariant**, not locale-aware.
- **AFK4.Player.Shell (WPF) — hard-coded ENGLISH, in a RU/TJ market.** `MainWindow.xaml:57` shows
  `"Session time or connectivity needs attention"`, and `Shell/PlayerShellViewModel.cs:15,134-140`
  hard-codes the entire customer-facing state vocabulary in English: `"This PC is locked."`,
  `"Session is active."`, `"Connection lost…"`, `"This PC is under maintenance."`, `"Agent is
  offline."`. This is the surface the paying customer stares at, and it is in the wrong language.
- **No Tajik (`tg`) locale anywhere.** No per-club or per-customer locale setting; `BranchEntity`
  (`src/AFK4.Platform.Api/Data/BranchEntity.cs`) has no locale field, and `ReceiptEntity` carries a
  `CurrencyCode` but no locale.

The product is therefore three different localization stories (full-but-hidden, RU-hardcoded,
EN-hardcoded) when it should be **one**. This spec defines a single localization approach that all
five surfaces share, adds Tajik, makes language a first-class, persisted, switchable setting, and
makes formatting (number/currency/date) locale-aware everywhere — including receipts and
(coordinating with the notifications spec) email/notification templates.

## 2. Goals

1. **One shared localization model** across all five surfaces: the same locale set, the same key
   namespace conventions, the same fallback rule, the same formatting semantics.
2. **Add Tajik (`tg`)** as a first-class locale alongside `ru` and `en`.
3. **A language switcher with persistence** on the surfaces where an end user picks language;
   a **club-admin default** on the surfaces where the club picks for the customer.
4. **Per-branch `PreferredLocale`** on `BranchEntity`, driving the customer-facing shell language.
5. **Locale-aware number / currency / date formatting on the WPF surfaces** (replace
   `InvariantCulture` + hard-coded `"TJS"`), at parity with Platform.Web's `Intl`-based formatting.
6. **String extraction** of the Operator.App.Web, Operator.App (WPF) and Player.Shell (WPF)
   hard-coded strings into shared key catalogs.
7. **Localized receipts** (locale stamped on the receipt; rendered in that locale).
8. **Localized notification / email templates**, coordinated with the notifications backbone spec.

### Non-goals (explicitly deferred or owned elsewhere)

- **Translation *content* quality / professional `tg` and `en` copy.** This spec lands the
  *mechanism* and the key inventory; filling every `tg`/`en` string with reviewed translations is a
  content task that follows, tracked per-surface (it does not block the plumbing).
- **Right-to-left (RTL) layout.** `ru`, `en`, `tg` are all LTR; no RTL work here.
- **Pluralization beyond `Intl.PluralRules` / .NET equivalents** — we use platform plural rules,
  not a bespoke ICU MessageFormat engine, in v1 (see Future).
- **Per-customer (player-account) UI locale persistence on the PC shell** — the shell follows the
  **branch** `PreferredLocale` in v1; per-player override is Future (ties to customer-shell spec's
  self-login).
- **The notification *delivery* backbone itself** (channels, outbox, retries) — owned by the
  notifications spec; here we only define how its templates are keyed and selected by locale.
- **Currency *conversion*.** Money stays `long` minor units; locale changes *display*, never the
  stored currency. Each org/branch keeps its single currency (`TJS`).

## 3. Proposed Decisions

These are the founder-review forks. Defaults are pre-selected to best practice.

| # | Decision | **Proposed choice** | Rationale / alternative |
|---|----------|---------------------|--------------------------|
| P1 | **Default locale** | **`ru`** for all surfaces | Matches the existing `initialLocale='ru'` and the RU-speaking operator base. Customer shell defaults to the **branch** `PreferredLocale` (itself defaulting to `ru`). |
| P2 | **Which locales ship first** | **`ru`, `en`, `tg`** — plumbing for all three immediately; `ru` fully translated day one, `en` already largely present in Platform.Web, `tg` lands as keys + progressive translation. | Shipping `tg` keys now (even partially filled) avoids a second migration later; fallback rule (P6) keeps untranslated `tg` usable. |
| P3 | **WPF localization mechanism** | **Mirror Platform.Web's JSON key catalog** — a shared `messages.{ru,en,tg}.json` consumed by a small WPF `ILocalizationService` + a `{loc:T Key=...}` XAML markup extension — **rather than .resx**. | Parity & single source of truth: one key namespace and one translation pipeline across React + WPF. `.resx` would fork the catalog format, the tooling, and the translator workflow, and resists hot/runtime locale switch. The catalog is plain JSON either way. |
| P4 | **Switcher vs club-admin setting (per surface)** | **Platform.Web:** user switcher (per-account, persisted). **Operator.App.Web + Operator.App (WPF):** user switcher (per-operator, persisted on device/profile). **Player.Shell:** **no** user switcher — follows **branch `PreferredLocale`** set by the club admin. | Staff pick their own working language; the customer-facing kiosk language is a club decision, not a per-walk-in toggle (a stranger should not be able to flip the kiosk and leave it wrong for the next customer). Optional per-player override is Future. |
| P5 | **Where the catalog lives** | A **single shared catalog source** (`locales/` at repo root or a shared package) generating the per-surface artifacts: TS module for React, JSON resource for WPF. | One inventory, one diff per string change. Avoids the current drift where Platform.Web has 1292 keys and the others have zero. |
| P6 | **Missing-key / missing-locale fallback** | **Key fallback to the key string** (today's behavior: `dict[key] ?? key`, `I18nProvider.tsx:20`) **and locale fallback `tg → ru → key`, `en → key`.** | Keeps the app usable while `tg` fills in; `ru` is the most-complete catalog so untranslated `tg` degrades to `ru`, not to English, in this market. |
| P7 | **Locale persistence transport** | **`localStorage` (web) / local app settings (WPF) for the immediate choice, mirrored to a server-side `PreferredLocale` on the user/staff profile when authenticated** so it follows the account across devices. | Survives reload (today it does not) and roams with the account; anonymous/login screens still honor the local value. |

## 4. Architecture Overview

A single **catalog source of truth** feeds every surface. The runtime locale is resolved per
surface from a small precedence chain, and all human-visible formatting routes through one
locale-aware formatter per platform.

```
                         locales/  (single source of truth)
                  ru.json   en.json   tg.json   (one key namespace)
                         │           │
            ┌────────────┴───┐   ┌───┴───────────────┐
   build:   TS module        │   │   JSON resource (WPF)
            (messages.ts)     │   │   (embedded/asset)
            │                 │   │            │
   ┌────────▼─────┐  ┌────────▼───▼──┐  ┌──────▼────────────┐  ┌──────────────┐
   │ Platform.Web │  │ Operator.App  │  │ Operator.App (WPF)│  │ Player.Shell │
   │  React i18n  │  │  .Web React   │  │ ILocalizationSvc  │  │  (WPF) same  │
   │  + switcher  │  │  + switcher   │  │ + {loc:T} XAML    │  │  svc; NO     │
   └──────┬───────┘  └──────┬────────┘  └────────┬──────────┘  │  switcher    │
          │ user choice     │ user choice        │ user choice └──────┬───────┘
          │                 │                    │                    │ branch
          ▼                 ▼                    ▼                    ▼ PreferredLocale
   localStorage ─┐   localStorage ─┐     app settings ─┐      reads BranchEntity
                 └──────────────────┴───────────────────┴─► PATCH profile.PreferredLocale
                                                            (server, when authenticated)

   Platform.Api ── BranchEntity.PreferredLocale ──► customer shell language
                 ── ReceiptEntity.Locale ─────────► receipt rendered in locale
                 ── notification template keyed by (templateId, locale) ─► email/SMS
                    (locale = recipient profile locale ?? branch PreferredLocale ?? ru)
```

Locale **precedence** when resolving the active locale on a surface:

1. **Staff surfaces** (Platform.Web, both Operator apps): explicit user choice (persisted) →
   profile `PreferredLocale` → default `ru`.
2. **Customer shell** (Player.Shell): branch `PreferredLocale` → default `ru`. (Per-player override
   is Future.)
3. **Server-rendered artifacts** (receipts, notifications): the stamped/selected locale → branch
   `PreferredLocale` → `ru`.

Six components, each independently testable:

1. **Shared catalog & locale set** (add `tg`; one key namespace; build artifacts).
2. **Platform.Web switcher + persistence + `tg`** (close the existing gap).
3. **Operator.App.Web i18n** (introduce provider + extract strings).
4. **WPF localization service + XAML markup extension** (Operator.App + Player.Shell share it).
5. **Locale-aware formatting on WPF** (currency/number/date; kill `InvariantCulture` + hard `"TJS"`).
6. **Server-side locale: `BranchEntity.PreferredLocale`, receipts, notification templates.**

## 5. Components

### 5.1 Shared catalog & locale set

**Current state (verified):** `messages.ts` already defines `export type Locale = 'ru' | 'en'`
and a `messages` object with **1292 keys** per locale (`messages.ts:1`). Formatting is `Intl`-based
(`I18nProvider.tsx:23-31`).

**Changes:**

- Extend the locale union to **`'ru' | 'en' | 'tg'`** and add `tg: 'tg-TJ'` to `LOCALE_TAG`
  (`I18nProvider.tsx:13`). `Intl` supports `tg-TJ`/`tg-Cyrl-TJ`; where a runtime lacks data it falls
  back gracefully (still produces output), so number/date formatting degrades to the closest
  available CLDR data rather than throwing.
- Introduce a **single catalog source** (`locales/{ru,en,tg}.json`, P5) with one key namespace
  (the existing dotted convention: `auth.*`, `nav.*`, `platform.*`, plus new `operator.*`,
  `shell.*`). Platform.Web's `messages.ts` becomes a **generated artifact** of `ru.json`/`en.json`
  (no behavior change to existing keys); `tg.json` starts as a copy of `ru.json`'s key set with
  values to be translated, relying on the P6 fallback meanwhile.
- A **lint/CI check**: every key present in `ru.json` must exist (key-wise) in `en.json` and
  `tg.json`; missing keys fail CI (prevents silent drift as the other surfaces add keys).

### 5.2 Platform.Web — switcher, persistence, `tg`

**Current state (verified):** `setLocale` is exposed on the context but **called nowhere** in the
UI; locale lives in `useState(initialLocale='ru')` with **no persistence**.

**Changes:**

- Add a **language switcher** control (a compact `ru / en / tg` selector) in the app chrome —
  in the existing nav/account area (`nav.group.account` / platform `nav.group.platformAccount`
  already exist as anchors). It calls the existing `setLocale`.
- **Persist** (P7): seed `initialLocale` from `localStorage('afk4.locale')` (falling back to `ru`),
  write on every `setLocale`, and — when authenticated — `PATCH` the staff/user profile's
  `PreferredLocale` so the choice roams. On next login the server value seeds the initial locale.
- Add `tg` to the catalog and selector. No change to the `t`/formatter API — only the locale set,
  the seed, and the write-back.

### 5.3 Operator.App.Web — introduce i18n

**Current state (verified):** no provider; Russian hard-coded across `App.tsx`.

**Changes:**

- Reuse the **same provider shape** as Platform.Web (ideally the literal shared `I18nProvider` +
  `useI18n`, promoted to a shared location per P5) so the two React apps don't fork.
- **Extract** the hard-coded Cyrillic in `App.tsx` (and `ConnectionResolutionScreen.tsx`) into
  `operator.*` keys in `ru.json`, with `en`/`tg` filled progressively under the P6 fallback.
- Add the same **switcher + persistence** as §5.2 (operator picks their own language, P4).

### 5.4 WPF localization service + XAML markup extension (Operator.App + Player.Shell)

**Current state (verified):** both WPF apps hard-code strings (RU in Operator.App, **EN** in
Player.Shell). No localization layer exists.

**Changes (P3 — JSON catalog, not `.resx`):**

- A shared **`ILocalizationService`** in a common WPF library:
  - loads `{ru,en,tg}.json` (the same artifacts as §5.1),
  - exposes `string T(string key)` with the **same fallback as P6**,
  - holds the **current locale** and raises a change event so the UI updates live,
  - exposes locale-aware formatters (§5.5).
- A **`{loc:T Key=...}` XAML markup extension** that binds a `TextBlock`/control text to the service
  and re-resolves on locale change — so XAML literals like `MainWindow.xaml:57` and the seat labels
  become `{loc:T Key=...}`.
- **String extraction:**
  - **Operator.App:** `MainWindow.xaml` (~82 literals), `FloorMap/FloorMapSeatViewModel.cs:101-109`
    state labels, `Auth/SignInViewModel.cs:85,91,113` errors, and the rest of the VMs →
    `operator.*` keys (shared namespace with §5.3 where the concept is identical, e.g. seat states).
  - **Player.Shell:** `MainWindow.xaml:57` and the **English** vocabulary in
    `Shell/PlayerShellViewModel.cs:15,134-140` → `shell.*` keys. This is the highest-priority
    extraction because it is customer-facing **and** currently in the wrong language.
- **Switcher policy (P4):** Operator.App gets a per-operator switcher (persisted to app settings,
  mirrored to profile per P7). **Player.Shell gets no switcher** — it sets the service locale from
  the **branch `PreferredLocale`** (§5.6) at startup and on config refresh.

### 5.5 Locale-aware formatting on WPF

**Current state (verified):** money formatted `(<minor>/100m).ToString("0.00", CultureInfo.InvariantCulture)`
(`Players/PlayerSearchViewModel.cs:592,645`); currency hard-coded `"TJS"` in ≥8 files.

**Changes:**

- Add to `ILocalizationService`:
  - `string FormatCurrency(long minorUnits, string currencyCode)` — uses the current locale's
    `CultureInfo` and the **passed** currency code (no hard-coded `"TJS"`), mirroring Platform.Web's
    `Intl.NumberFormat({style:'currency'})` with `maximumFractionDigits` aligned to the currency.
  - `string FormatNumber(decimal n)` and `string FormatDate(DateTimeOffset d)` using the locale's
    `CultureInfo` (replacing `InvariantCulture`).
- The currency code is **threaded from config/DTO** (the branch/org currency), not constant. The
  eight hard-coded `"TJS"` literals collapse to a single configured default used only as a last
  resort. **Note:** money remains `long` minor units end-to-end; only *display* changes (consistent
  with the platform-wide convention).
- Keep the `(minor/100m)` → major-unit conversion at the **UI boundary only** (matches the
  Platform.Web `minorToMajor` convention).

### 5.6 Server-side locale: branch, receipts, notifications

**Current state (verified):** `BranchEntity` has no locale field; `ReceiptEntity` has `CurrencyCode`
but no locale; notification templates are owned by the notifications spec.

**Changes:**

- **`BranchEntity.PreferredLocale`** (`string`, default `"ru"`) — the customer-shell language and
  the default locale for that branch's receipts and notifications. Editable in the club-admin
  branch settings (Platform.Web). Drives Player.Shell per §5.4.
- **`ReceiptEntity.Locale`** (`string`) — stamp the locale at receipt creation
  (= branch `PreferredLocale`, or the checkout operator's choice if we later expose one) so a
  re-print reproduces the original language. Receipt **rendering** uses the shared catalog +
  formatter so the time-and-snacks receipt from the counter-loop spec prints localized labels and a
  locale-formatted total.
- **Notification / email templates** (coordinating with the notifications spec): templates are keyed
  `(templateId, locale)`; the backbone selects locale as **recipient profile `PreferredLocale` →
  branch `PreferredLocale` → `ru`**. This spec owns the *keying and selection rule and the catalog*;
  the notifications spec owns *delivery*. Staff invites / password-reset / billing emails referenced
  there resolve their language through this rule.

## 6. Data Model Changes (summary)

| Entity / contract | Change |
|---|---|
| `Locale` type (`messages.ts`) | `'ru' | 'en'` → `'ru' | 'en' | 'tg'` |
| `LOCALE_TAG` (`I18nProvider.tsx`) | add `tg: 'tg-TJ'` |
| catalog | new shared `locales/{ru,en,tg}.json`; `messages.ts` becomes generated; CI key-parity check |
| Platform.Web | language switcher UI; `localStorage('afk4.locale')` seed/write; profile write-back |
| Operator.App.Web | new `I18nProvider`/`useI18n` (shared); `operator.*` keys; switcher + persistence |
| WPF shared lib | new `ILocalizationService` + `{loc:T}` XAML markup extension (JSON catalog) |
| Operator.App (WPF) | extract `MainWindow.xaml` + VM strings → `operator.*`; per-operator switcher |
| Player.Shell (WPF) | extract `MainWindow.xaml:57` + `PlayerShellViewModel.cs:134-140` → `shell.*`; locale from branch |
| WPF formatting | `FormatCurrency/FormatNumber/FormatDate` (locale-aware); drop hard-coded `"TJS"` (≥8 files) & `InvariantCulture` |
| `BranchEntity` | add `PreferredLocale` (`string`, default `"ru"`) + EF migration |
| `ReceiptEntity` | add `Locale` (`string`) + EF migration; render via shared catalog |
| user/staff profile | add `PreferredLocale` (`string?`) for roaming choice (per P7) |
| Notification templates | keyed `(templateId, locale)`; locale-selection rule (with notifications spec) |

Each schema change carries an EF migration. Locale changes **display only** — money stays `long`
minor units, the stored currency is unchanged.

## 7. Error Handling & Edge Cases

- **Missing key:** falls back to the key string (existing `dict[key] ?? key`); the same rule is
  implemented in the WPF service so behavior matches across platforms.
- **Missing locale value:** locale fallback per P6 (`tg → ru → key`, `en → key`) — an untranslated
  `tg` string shows `ru` (this market's lingua franca), never raw English.
- **Unknown/invalid persisted locale** (e.g. stale `localStorage` value not in the set): clamp to
  default `ru`; do not throw.
- **`Intl`/`CultureInfo` lacking full `tg-TJ` data:** formatting degrades to the nearest CLDR data
  and still produces output; we never block rendering on locale data. (Flagged so QA expects
  possibly-`ru`-styled grouping for `tg` numbers until CLDR coverage improves.)
- **Currency vs locale independence:** changing UI language must **not** change the currency symbol
  away from the branch currency; `FormatCurrency` always takes the explicit currency code.
- **Receipt re-print:** uses the **stamped** `ReceiptEntity.Locale`, so a re-print months later
  reproduces the original language even if the branch later switches `PreferredLocale`.
- **Player.Shell mid-session locale change:** if a club changes branch `PreferredLocale` while a
  session is live, the shell re-resolves on the next config refresh; in-flight warning/lock text is
  re-rendered from the catalog (no stale literal because they are now keyed).
- **Switcher with no auth (login screens):** local value is honored; profile write-back happens once
  authenticated.

## 8. Testing Strategy

- **Catalog parity (CI):** assert every `ru.json` key exists in `en.json` and `tg.json`; assert no
  surface ships an un-keyed user-visible literal (lint for Cyrillic/long string literals in
  `Operator.App.Web/App.tsx`, WPF XAML, and the shell VM as a regression guard).
- **Fallback rules:** unit-test `tg → ru → key` and `en → key`; unit-test unknown-locale clamp to
  `ru`. Mirror the same tests in the WPF service so both implementations agree (extends the existing
  `i18n.test.tsx` / `messages.test.ts`).
- **Platform.Web switcher + persistence:** switching updates the UI, writes `localStorage`, survives
  reload, and (authenticated) issues the profile `PATCH`; reload seeds from the persisted value.
- **Operator.App.Web:** after extraction, the rendered UI matches the previous Russian output for
  `ru` (snapshot/parity), and switches to `en`/`tg`.
- **WPF formatting:** `FormatCurrency(minor, "TJS")` matches Platform.Web's `Intl` output for the
  same locale/amount (cross-surface parity test on representative values); `FormatNumber`/`FormatDate`
  are locale-aware (no `InvariantCulture` regression).
- **Player.Shell:** the customer vocabulary (locked / active / connection-lost / maintenance /
  offline) renders from `shell.*` in the **branch** locale; no English appears for a `ru`/`tg`
  branch.
- **Receipts:** a receipt created under a `tg` branch stamps `Locale='tg'` and re-prints in `tg`.
- **Notifications (with notifications spec):** template selection resolves
  recipient → branch → `ru`; a recipient with `PreferredLocale='en'` gets the `en` template.

## 9. Decomposition & Sequencing

One coherent localization effort, six separable units. Suggested build order:

1. **Shared catalog & locale set** — add `tg`, establish `locales/` as source of truth, make
   `messages.ts` generated, add the CI parity check (foundation; unblocks everyone).
2. **Platform.Web switcher + persistence + `tg`** — smallest, highest-leverage (closes the existing
   gap on a surface that already has the machinery). Establishes the persistence pattern (P7).
3. **`BranchEntity.PreferredLocale` (+ migration) and the club-admin setting** — needed before the
   shell can follow it; small backend + Platform.Web settings field.
4. **WPF localization service + `{loc:T}` extension** (shared lib) — the reusable substrate for both
   WPF apps.
5. **Player.Shell extraction + branch-locale wiring + WPF formatting** — top customer-facing
   priority once 3 & 4 exist (fixes the wrong-language kiosk).
6. **Operator.App.Web i18n** and **Operator.App (WPF) extraction + formatting** — bulk string
   extraction; can parallel each other once 1 & 4 are in.
7. **Receipts (`ReceiptEntity.Locale` + localized render)** and **notification template keying**
   (with the notifications spec) — depend on 1 & 3.

Translation-content fill for `tg` (and any missing `en`) runs continuously alongside, protected by
the P6 fallback so no step is blocked on complete translations.

## 10. Future (v2 / other tracks)

- **Per-player UI locale** on the customer shell after self-login (ties to the customer-shell
  experience spec) — override branch `PreferredLocale` per signed-in customer.
- **Richer pluralization / gendered forms** via an ICU-MessageFormat-style layer if `Intl.PluralRules`
  / .NET plural support proves insufficient for `ru`/`tg` cases.
- **Additional locales** (e.g. Uzbek) — the catalog + CI-parity machinery makes adding one a data
  task, not a code change.
- **Translation-management workflow** (TMS/export-import round-trip for translators) once the catalog
  stabilizes.
- **RTL** support, if a future market requires it.
- **Auto-detect** initial locale from browser/OS as a smarter default than `ru` for new staff
  accounts (still overridable; persisted per P7).
