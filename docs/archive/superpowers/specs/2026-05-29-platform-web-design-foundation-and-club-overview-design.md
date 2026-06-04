# Platform.Web Design Foundation, App-Shell, and Club Overview — Design

Date: 2026-05-29
Owner: AFK4 platform
Status: Design (approved in brainstorming; pending written-spec review)

## Why This Exists

The two browser admin surfaces in `src/AFK4.Platform.Web` are functionally
MVP-complete but visually minimal: a single hand-written `styles.css`, system
colours, plain tables, and a hand-rolled `ui.tsx`. The owner asked for the
opposite of MVP — a full, modern, well-built product with good UX and current
best practices — across both admins, *before* moving on to staging smoke.

This is too large for one spec. This document records the program-level
decomposition for context, then specifies **sub-project 1** in detail. Each
later sub-project gets its own spec → plan → implementation cycle.

Live club operations (floor map sessions, POS, shifts, booking, payments,
clients at the counter, logs) remain in the native WebView2 Operator App. The
architecture decision "no browser web admin as the primary club UI" is
unchanged. The web cabinet is the owner/manager **management and analytics**
console, used remotely — not a second operator console.

## Program Decomposition (context, not all in scope here)

The two admins live in one SPA (`AFK4.Platform.Web`) with audience builds
(`VITE_AUDIENCE=admin|club|all`) deployed to two hosts
(`platform.afk4.staging.mubi.dev` for `/admin/*`, `app.afk4.staging.mubi.dev`
for `/club/*`).

1. **Sub-project 1 (this spec): Design foundation + app-shell + Club Overview.**
   Establish the design system and the shell by building one real screen.
2. **Sub-project 2: Full `/club/*` owner console.** Redesign and complete every
   club screen to product quality (analytics, monetization, CRM, etc.). New
   backend contracts as needed.
3. **Sub-project 3: Full `/admin/*` SaaS control plane.** Same treatment for
   Mubi's platform-owner surface.

### Decided in brainstorming (apply to the whole program)

- **Stack:** Tailwind CSS + shadcn/ui (Radix primitives), components vendored
  into the repo. Added to the existing Vite 8 / React 19 / TypeScript project.
- **Visual direction:** "Calm SaaS" — light, airy, restrained, single indigo
  accent, subtle borders/shadows. (Chosen over a vivid-branded and a
  dark-operational direction.)
- **Themes:** both light and dark, **light is the default first paint**, dark
  via toggle and `prefers-color-scheme`; choice persisted locally.
- **i18n from day one:** Russian primary, English secondary. No hard-coded
  user-facing strings — everything goes through a translation layer.
- **Club cabinet scope (whole `/club/*`):** owner/manager management **plus
  analytics**. Live operations stay in the Operator App.

### Club cabinet information architecture (whole `/club/*`, built incrementally)

Sidebar with a branch switcher at the top, then two groups. Sub-areas use
in-page tabs rather than separate top-level nav items.

**Группа «Филиал» (branch-scoped):**

- **Обзор** — home/KPI dashboard.
- **Зал и ПК** — tabs: Карта зала (editor + monitoring) · Устройства
  (inventory, rename, move-seat, remove) · Ожидают подтверждения (pending
  device-approval queue; tab visible only when there are pending devices).
- **Клиенты** — CRM overview.
- **Монетизация** — tabs: Тарифы · Товары (catalog) · Лояльность.
- **Отчёты** — tabs: Аналитика · Журнал событий (audit).
- **Настройки** — tabs: Филиал · Операторы и роли.

**Группа «Аккаунт» (account-scoped):**

- **Все филиалы** — list / add / deactivate branches.
- **Установка (код владельца)** — owner-code generate/rotate + install
  instructions.
- **Биллинг** — subscription/plan (later).
- **Профиль и доступ** — account profile and access.

**Role gating:** Settings, Установка, Биллинг, Профиль are owner-only. A
`branch_manager` role sees the operational areas but not these. Gating is
enforced both in routing and in render.

**Pending-device queue** ("Очередь подключения" in earlier drafts) is the
manual device-approval queue: when a branch has `RequireManualDeviceApproval`
enabled, a freshly enrolled Agent lands in this queue for the owner to
approve/reject. The default is auto-approve, so the queue is usually empty;
therefore it is a tab inside «Зал и ПК», not a top-level item.

## Sub-Project 1 — Detailed Design

### Scope

**In scope:**

- Introduce Tailwind + shadcn/ui into `AFK4.Platform.Web`.
- Design tokens for the "Calm SaaS" direction, light + dark.
- Theme switching (default light, toggle, system preference, persisted).
- i18n scaffolding (RU primary, EN secondary) with the shell + Overview strings
  externalized.
- The club **app-shell**: sidebar (branch switcher, the IA above), top bar
  (breadcrumb + period control), footer (user, role, theme toggle, sign out),
  role gating, responsive collapse to a burger/overlay on narrow widths.
- One fully built screen: **Club Overview** (`/club` → Обзор).

**Out of scope (later sub-projects):**

- Every other `/club/*` screen body and all of `/admin/*`.
- Any new backend endpoint or contract. Sub-project 1 is frontend-only.
- Analytics/monetization/CRM/loyalty/billing features.

### Non-breaking migration constraint

The existing `/club/*` and `/admin/*` screens currently render through the
monolithic `App.tsx` shell, `ClubDashboard`, and `ui.tsx` on the legacy
`styles.css`. Sub-project 1 must not remove working functionality.

- The new design system and Tailwind are introduced **alongside** the existing
  `styles.css`; legacy screens keep working on the old styles.
- The new app-shell becomes the layout for `/club/*`. Existing club screen
  bodies (branches, devices, floor-map editor, operators, install) are
  **reparented into the new shell routes** and remain reachable and functional.
  Their visual redesign is deferred to sub-project 2; until then they may carry
  a small "будет обновлено" marker.
- Only the **Обзор** screen is rebuilt to full product quality in this
  sub-project.

This implies a modest refactor of the current monolithic `ClubDashboard` into
the shell plus per-area screen components — a targeted improvement of code we
are working in, not a speculative rewrite.

### Design system (the foundation)

- **Tokens** as CSS variables: background/surface/border/text/muted, a single
  indigo accent with weak variant, semantic good/warn/danger, radius, shadow,
  spacing scale, typography scale. Two token sets (light, dark) selected by a
  `data-theme` attribute (or `.dark` class) on the root.
- **Tailwind** configured to read these tokens (so utilities and shadcn
  components stay theme-aware). System font stack for now; a webfont decision is
  deferred and not a blocker.
- **shadcn/ui primitives** vendored under `src/components/ui/`: at minimum
  Button, Card, Badge, Tabs, DropdownMenu, Avatar, Skeleton, Tooltip,
  Separator, and a Chart wrapper (Recharts). Added as sub-project 1 needs them;
  the rest arrive with later sub-projects.
- **Theme controller:** reads system preference on first load, defaults to
  light, exposes a toggle, persists the choice (localStorage), applies before
  first paint to avoid a flash.
- **i18n:** a lightweight provider with RU as the default locale and EN as a
  fallback dictionary. Strings are keyed; the shell and Overview ship full RU +
  EN. Numbers/dates/currency formatted per locale (currency code stays
  backend-driven, e.g. TJS).

### App-shell

- **Sidebar:** branch switcher at top (changes the «Филиал» context); the two
  nav groups from the IA above; live count badges (attention / pending) where
  applicable; footer with user, role, theme toggle, sign out.
- **Top bar:** breadcrumb (branch · screen) and a contextual period control on
  screens that need it (Overview does).
- **Role gating:** owner-only items hidden for `branch_manager`, enforced in
  routing and render.
- **Responsive:** sidebar collapses to a burger/overlay below a defined
  breakpoint; layout remains usable at a narrow width and at a typical desktop
  width.
- The shell is composed of small, focused, independently testable components
  (sidebar, branch switcher, nav group, nav item, top bar, theme toggle, user
  menu), not one large file.

### Club Overview screen

- **KPI cards:** devices online (e.g. 28/30), active sessions, revenue today,
  errors-today / attention count. Each shows a label, value (tabular numerals),
  and a small trend/sub-line.
- **Revenue chart:** revenue over the last 7 days, rendered with Recharts via
  the shadcn chart wrapper, theme-aware.
- **Attention list:** devices/items needing attention (offline, missing
  WebView2, shift not opened, …) with status badges, linking to the relevant
  screen.
- **States:** explicit loading (skeletons), authoritative-empty, and
  error-with-retry states for every data region — never silent blanks or fake
  data.
- **Data source:** existing backend contracts the current club dashboard
  already consumes for KPI/overview. No new endpoints. Where the existing
  contract does not yet expose a value shown in the mock, that value is either
  derived from available data or deferred with a clearly-labelled placeholder —
  it must not fabricate backend success.

### Components and boundaries

- `src/theme/` — token definitions, theme provider/controller.
- `src/i18n/` — provider, locale dictionaries (ru, en), formatting helpers.
- `src/components/ui/` — vendored shadcn primitives.
- `src/components/shell/` — sidebar, branch switcher, nav, top bar, user menu,
  theme toggle, responsive container.
- `src/features/club/overview/` — Overview screen and its KPI/chart/attention
  sub-components plus their data hook.
- Existing `App.tsx` becomes composition + routing only; legacy screen bodies
  are mounted as reparented route children pending sub-project 2.

### Error handling

- API errors surface as typed errors projected to localized, actionable copy
  (loading → empty → error/retry), consistent with the existing error-projection
  approach in the codebase. No raw technical strings in the owner-facing path.

### Testing

Vitest + React Testing Library + jsdom (already the project's setup):

- Shell renders with the expected nav groups for an `owner` session.
- Role gating: a `branch_manager` session does not see owner-only items.
- Theme: default is light; toggle switches to dark and persists; system
  preference respected on first load.
- i18n: shell/Overview render in RU by default; switching to EN swaps strings.
- Overview: loading (skeletons), data, authoritative-empty, and error/retry
  states each render correctly.
- Route resolution for `/club` and the reparented club routes inside the new
  shell.
- Gates: `npm run build` (tsc + vite) and `npm test` green.

## Verification Gates

- [ ] Tailwind + shadcn/ui build cleanly in `AFK4.Platform.Web`.
- [ ] Light/dark theming works with no flash; light is default; system honored.
- [ ] Shell renders correct nav per role; responsive collapse works.
- [ ] Overview renders all data states on existing backend contracts.
- [ ] RU/EN strings present for shell + Overview.
- [ ] Existing `/club/*` and `/admin/*` functionality still reachable and
      working (non-breaking).
- [ ] `npm run build` and `npm test` pass.

## Out of Scope / Deferred

- All other `/club/*` screen redesigns and `/admin/*` (sub-projects 2–3).
- New backend contracts for analytics, monetization, loyalty, CRM, billing.
- Webfont selection, marketing landing site (separate plan).

## Related

- `docs/superpowers/plans/2026-05-24-afk4-club-self-service-onboarding.md` —
  the `/club/*` and `/admin/*` IA and backend contracts this builds on.
- `docs/superpowers/plans/2026-05-23-saas-control-plane-tenant-onboarding.md` —
  the `/admin/*` control-plane backend (sub-project 3).
- `docs/roadmap/production-readiness.md` — where staging smoke (deferred until
  after this redesign by owner decision) sits.
- Reference benchmark: SmartShell.gg control panel feature set (aspirational
  completeness bar; AFK4 keeps live ops in the native Operator App).
