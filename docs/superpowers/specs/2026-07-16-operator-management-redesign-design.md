# Operator «Управление» Redesign Design

**Status:** approved for implementation
**Date:** 2026-07-16

## Goal

Rebuild the operator `Управление` (management) workspace from scratch. The
current codex draft (the `management/` module on
`feat/operator-reports-workspace-consolidation`) is raw and structurally wrong:
unstyled forms with labels glued to inputs, an unwanted overview landing that
leaks a raw branch GUID, a floating `Настройки загружены` status pill dressed as
a button, audit `События` living inside configuration, duplication with `Склад`
and `Карта`, amber-coloured money, and acres of empty space around forms
huddled in a corner.

This is a redesign of information hierarchy and a rebuild of the shared form
scaffold — not a new visual language. The existing operator shell, rail,
tokens, emerald accent, surface-elevation rules, and toast feedback remain the
design system.

## Scope

In scope: everything reachable under the rail section `Управление` today —
club profile, hall/PC layout, tariffs & packages, staff & roles, product
catalog, payment cards, loyalty, news.

Out of scope, removed or relocated (see below): the overview landing, audit
events, stock movements, and update-package publishing.

## Navigation

`Управление` uses a **left secondary navigation** (settings-nav pattern, like
Stripe/GitHub settings). No third level of navigation. No overview landing —
the workspace opens directly on the first accessible item (`Клуб`).

The eight destinations, in order:

| # | Item | Owns | Does not own |
|---|------|------|--------------|
| 1 | **Клуб** | Branch profile: club name, city, currency, human branch name | — |
| 2 | **Залы и ПК** | Hall/zone and workstation structure and ordering | The live floor view (that is `Карта`) |
| 3 | **Тарифы и пакеты** | Tariffs (price/hour, minimum, rounding step) and time packages | — |
| 4 | **Сотрудники и роли** | Staff, invitations, roles/access, password reset | — |
| 5 | **Товары** | Catalog: product, category, price, SKU, barcodes, shell availability, low-stock threshold | Stock movements (receiving/adjustments) — those belong to `Склад` |
| 6 | **Оплата** | Payment cards/providers for online top-ups | — |
| 7 | **Лояльность** | Cashback rules (top-ups / shop, percentages) | — |
| 8 | **Новости** | Client-facing publications | — |

### Removed or relocated

- **Обзор (overview landing):** removed entirely. Entry lands on the first
  accessible item. No invented cards, no raw branch GUID.
- **События (audit/logs):** leaves `Управление`. Audit is not configuration.
  It becomes its own rail section `События` in a separate small follow-up
  (tracked in backlog so it is not lost). This spec only removes it from
  `Управление`.
- **«Записать движение»** on the goods screen: removed from `Товары`. Stock
  movement already lives in `Склад` (receiving/on-hand/inventory/history).
  `Товары` keeps catalog definition only.
- **Обновления / Интеграции (update-package publishing):** removed from the
  operator app entirely. Publishing signed update packages (versions, channels,
  checksum, signature, rollout) is a platform/owner-web concern, not a club
  manager task. Tracked in backlog as "operator update-publishing → owner web"
  so the capability is not silently dropped. During implementation, verify
  whether the old Интеграции panel carried any still-needed live toggle (e.g. a
  payment-confirmation mode); if so, surface it under `Оплата` rather than
  reviving the panel.

## Shared Screen And Form Scaffold

The root cause of the "raw" look is that each screen lays out its own form with
no spacing. Introduce **one scaffold** shared by all eight screens, built on the
existing operator tokens, surface-elevation rules, and emerald accent.

Screen structure:

```
Section title            (h1 + one-line subtitle)
─────────────────────────────────────────────
Panel section "…"        (card; raised white + shadow on light theme)
   Field       Field     (2-column grid with gap; label ABOVE the input)
   Field       Field
─────────────────────────────────────────────
[Lists: tariffs / products / PCs — row-cards: name bold, meta muted]
─────────────────────────────────────────────
                 Sticky bar: "изменений нет" / [Сохранить]
```

Rules applied as a class, everywhere — not per-screen:

- **Label above the field**, fixed input height, vertical rhythm from tokens.
  Never `label[input]label[input]` crammed on one line.
- **Forms in a grid**: one or two columns with a gap; fields stretch to the
  panel width instead of huddling in a 400px corner.
- **One save affordance**: a sticky bottom bar with `Сохранить` enabled only
  when dirty, plus a quiet `изменений нет / сохранено` status. The floating
  `Настройки загружены` pill is removed everywhere.
- **Money is neutral/white**; amber is reserved for warnings. Fixes tariff and
  package prices currently shown in amber.
- **Lists** (tariffs, products, PCs, staff) use one row-card pattern: name
  bold, meta muted, actions on the right, following operator surface-elevation
  (light theme = raised white panel + `--shadow-card`; no shadow on nested
  card-in-card, inputs, or modals).
- **Unsaved-changes guard**: switching nav items with pending edits shows a
  lightweight inline confirm — not the heavy drawer from the codex draft.
- **Feedback** uses the shared `useFeedbackToasts` (as in every other section),
  not local banners.
- **Date fields** (`Новости`) follow the same input pattern (label above,
  normal width) instead of bare native `дд.мм.гггг` inputs jammed inline.

Concrete spacing, interaction states, and contrast are settled during
implementation via the interface-limb design skill; this scaffold defines the
intent and the invariants.

## States

- **Loading:** skeleton that preserves the final geometry (field/row
  placeholders in place). Deferred spinner (150–300ms) so fast responses do not
  flash. Layout does not jump.
- **Error:** inline, retryable per screen; keep the last successful view where
  safe. Show the concrete backend error, never a generic "что-то пошло не так".
- **Empty:** honest empty states with context (`Новостей пока нет`, `Пока нет
  подключённых карт`), properly styled, no fabricated zeros.

## Permissions And Visibility

- Each nav item is visible only when the staff member holds the relevant
  permission. Reuse the codex permission mapping
  (`allowedManagementDestinations`), minus the removed `overview`, `events`, and
  `integrations` destinations.
- Inaccessible items are **not rendered** — no disabled promises. If nothing is
  accessible, show a clear section-level message.
- The branch identifier is always shown as a human branch name (from the
  session / `floorMap.branchName`), never a UUID.

## Data Flow

Screens reuse the existing operator domain clients (settings, loyalty, news,
payment gateways) and the shared feedback/toast layer. This is a UI and
information-hierarchy rebuild; no new backend write authority and no new
endpoints are introduced by this spec. Money values keep the established
minor-units → major-on-UI-boundary contract.

## Testing And Acceptance

- Navigation renders the eight items in order, lands on the first accessible
  item, and hides items the staff member cannot access.
- No screen renders the removed overview, events, integrations, or the
  `Настройки загружены` pill.
- `Товары` has no stock-movement action; catalog edits still work.
- Forms render with labels above fields and grid spacing at 1920 / 1440 / 1280
  and the narrow stacked breakpoint, dark and light, with no overflow and no
  console/page errors.
- Save is disabled until dirty and enabled after edits; the unsaved-changes
  guard fires when switching items with pending edits.
- Money values render neutral, never amber.
- Loading shows skeletons (no layout jump); errors show the concrete backend
  message with retry; empty states explain context.
- The branch is shown by name, never as a UUID.

Acceptance: a manager opens `Управление`, lands on `Клуб`, moves across the
eight items, edits and saves a setting with clear dirty/saved feedback, sees
only the items they may access, and never encounters raw forms, a GUID, audit
events, stock movements, or update publishing inside this workspace.

## Delivery Slices

Independently verifiable slices for one implementation plan:

1. **Scaffold + navigation:** shared screen/form scaffold, the eight-item left
   nav, removal of overview/events/integrations/movements, permission-derived
   visibility, unsaved-changes guard, save bar. Migrate the simplest screens
   (`Клуб`, `Лояльность`, `Новости`) onto the scaffold as proof.
2. **Remaining screens on the scaffold:** `Залы и ПК`, `Тарифы и пакеты`,
   `Сотрудники и роли`, `Товары`, `Оплата` — each rebuilt on the shared
   scaffold with correct lists, money colour, states, and empty/error handling.
3. **Cleanup + follow-up hooks:** remove dead codex management artifacts
   (overview, drawer, dirty-guard drawer, integrations screen), confirm no
   duplication with `Склад`/`Карта`, and file the backlog items (`События` as
   its own rail section; operator update-publishing → owner web).

## Non-Goals

- No new visual language; the operator design system stays.
- No new backend endpoints or write authority.
- No club-admin web replacement for the native operator app.
- No revival of the overview dashboard, the approvals-style drawer, or
  update-package publishing inside the operator.
