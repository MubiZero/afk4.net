# Platform Control Rebuild Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Execute inline; the user explicitly requested no subagents.

**Goal:** Replace the existing Platform Control frontend with an organization-centered internal application for Platform Owner and Platform Support.

**Architecture:** Keep the existing React/Vite application and typed API boundary, but replace its screen composition, route model, permission-derived navigation, and legacy styling. Implement vertical slices around canonical URL-addressable resources; add backend projections only when existing contracts cannot support the approved workflow without frontend joins or fabricated authority.

**Tech Stack:** React 19, TypeScript, Vite, Bun test, Testing Library, Tailwind CSS, Radix primitives, ASP.NET Core .NET 10, PostgreSQL, xUnit.

## Global Constraints

- Platform Control is internal to afk4.net and must not contain Organization Admin workflows.
- Platform Owner and Platform Support share one app; backend authorization is the security boundary.
- Organizations are the primary resource and have canonical full pages.
- Route, organization, tab, search, filter, and sort state must survive refresh and browser history.
- Sensitive mutations wait for backend confirmation, require reasons where specified, and remain audited.
- Existing API contracts are reused only when they support complete workflows without frontend authority.
- Use real `@afk4/i18n` translations; do not concatenate fragments or copy Russian into other locales.
- Text contrast is at least 4.5:1, non-text contrast at least 3:1, targets at least 24 by 24 CSS pixels, and all flows remain operable at 200% zoom.
- Keep light and dark themes semantically equivalent; color communicates state rather than decoration.
- Do not preserve a parallel legacy Platform Control route or compatibility layer.
- Follow TDD for route behavior, permissions, API contracts, mutations, and state models.

---

### Task 1: Route, permission, and application-shell foundation

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/routing/platformRoute.ts`
- Create: `src/AFK4.PlatformControl.Web/src/routing/platformRoute.test.ts`
- Create: `src/AFK4.PlatformControl.Web/src/auth/platformAccess.ts`
- Create: `src/AFK4.PlatformControl.Web/src/auth/platformAccess.test.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/App.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/App.test.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/nav.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/nav.test.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/components/shell/AppShell.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/components/shell/AppShell.test.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/components/shell/Topbar.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/components/shell/UserMenu.tsx`

**Interfaces:**
- Consumes: `PlatformAdminSession.roles`, `PlatformAdminSession.permissions`, browser `pathname` and `search`.
- Produces: `PlatformRoute`, `resolvePlatformRoute(location)`, `pathForPlatformRoute(route)`, `PlatformCapability`, `can(session, capability)`, and permission-filtered `buildPlatformNav(session)`.

- [x] **Step 1: Write failing route and access tests**

```ts
expect(resolvePlatformRoute('/admin/organizations/org-1?tab=support')).toEqual({
  kind: 'organization', organizationId: 'org-1', tab: 'support'
});
expect(pathForPlatformRoute({ kind: 'organizations', query: 'samarkand', status: 'active', sort: 'name' }))
  .toBe('/admin/organizations?q=samarkand&status=active&sort=name');
expect(can(supportSession, 'billing.manage')).toBe(false);
expect(can(ownerSession, 'billing.manage')).toBe(true);
```

- [x] **Step 2: Run the focused tests and verify the old route model fails them**

Run: `cd src/AFK4.PlatformControl.Web && bun test src/routing/platformRoute.test.ts src/auth/platformAccess.test.ts src/platform/nav.test.ts`

Expected: FAIL because the route/access modules and permission-derived navigation do not exist.

- [x] **Step 3: Implement the route and capability contracts**

```ts
export type OrganizationTab = 'summary' | 'clubs' | 'access' | 'subscription' | 'invoices' | 'support' | 'history';
export type PlatformRoute =
  | { kind: 'overview' }
  | { kind: 'organizations'; query: string; status: string; sort: string }
  | { kind: 'organization'; organizationId: string; tab: OrganizationTab }
  | { kind: 'organizationNew' }
  | { kind: 'billing'; tab: 'plans' | 'subscriptions' | 'invoices' }
  | { kind: 'updates' }
  | { kind: 'audit' }
  | { kind: 'settings' }
  | { kind: 'profile' };

export type PlatformCapability =
  | 'organizations.read' | 'organizations.manage' | 'support.manage'
  | 'billing.read' | 'billing.manage' | 'updates.manage'
  | 'audit.read' | 'settings.manage';
```

Map capabilities from authoritative session permissions first and role names only for stable navigation grouping. Reject direct forbidden routes with a dedicated forbidden screen instead of redirecting silently.

- [x] **Step 4: Replace `App.tsx` conditional routing with the new route model and shell**

Keep account activation outside the authenticated shell. Keep language and theme controls in `UserMenu`. Add the attention and global-search triggers only with their working destinations in Tasks 3 and 6; remove the legacy organization drawer when its canonical replacement lands in Task 4.

- [x] **Step 5: Run shell, route, navigation, and App tests**

Run: `cd src/AFK4.PlatformControl.Web && bun test src/routing src/auth/platformAccess.test.ts src/platform/nav.test.ts src/components/shell src/App.test.tsx`

Expected: PASS, including owner/support navigation differences, forbidden direct routes, browser back, and query restoration.

- [x] **Step 6: Commit the foundation**

```bash
git add src/AFK4.PlatformControl.Web/src/App.tsx src/AFK4.PlatformControl.Web/src/App.test.tsx src/AFK4.PlatformControl.Web/src/routing src/AFK4.PlatformControl.Web/src/auth/platformAccess.ts src/AFK4.PlatformControl.Web/src/auth/platformAccess.test.ts src/AFK4.PlatformControl.Web/src/platform/nav.ts src/AFK4.PlatformControl.Web/src/platform/nav.test.ts src/AFK4.PlatformControl.Web/src/components/shell
git commit -m "feat(platform-control): rebuild routing and access shell"
```

### Task 2: Semantic design system and complete application states

**Files:**
- Modify: `src/AFK4.PlatformControl.Web/src/index.css`
- Delete: `src/AFK4.PlatformControl.Web/src/styles.css`
- Modify: `src/AFK4.PlatformControl.Web/src/main.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/components/layout/PageHeader.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/components/layout/Workspace.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/components/data/StatusBadge.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/components/data/DescriptionList.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/components/ui/states.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/components/ui/states.test.tsx`
- Modify: `packages/i18n/src/messages.ts`
- Modify: relevant locale catalogs under `locales/`

**Interfaces:**
- Consumes: existing UI primitives and `useI18n()`.
- Produces: `PageHeader`, `Workspace`, `StatusBadge`, `DescriptionList`, `PageSkeleton`, `EmptyState`, `ErrorState`, `ForbiddenState`, and `PartialFailure`.

- [x] **Step 1: Write failing state and accessibility tests**

```tsx
render(<EmptyState title="Start by creating an organization" action={<button>Create organization</button>} />);
expect(screen.getByRole('heading', { name: /start by creating/i })).toBeVisible();
expect(screen.getByRole('button', { name: /create organization/i })).toBeEnabled();

render(<PartialFailure title="Invoices are unavailable" onRetry={retry} />);
expect(screen.getByRole('status')).toHaveTextContent('Invoices are unavailable');
```

- [x] **Step 2: Run the state tests and verify failure**

Run: `cd src/AFK4.PlatformControl.Web && bun test src/components/ui/states.test.tsx`

Expected: FAIL for missing complete state primitives and semantics.

- [x] **Step 3: Implement semantic tokens and layout primitives**

Use one neutral surface scale, one action accent, semantic success/warning/danger/info tokens, tabular numeric styles, a 2px `:focus-visible` ring, 100-150 ms micro-feedback, and `prefers-reduced-motion`. Remove global legacy element styling and ensure auth/account-activation use the same primitives.

- [x] **Step 4: Add real ru/en/tg copy for shared states**

Add complete messages for first-use empty, filtered empty, forbidden, expired session, partial failure, retry, and safe fallback actions. Keep every variable sentence as one ICU-ready message rather than JSX fragments.

- [x] **Step 5: Run UI-state, i18n, and theme verification**

Run: `cd src/AFK4.PlatformControl.Web && bun test src/components src/theme && bun run build`

Run the repository's existing i18n catalog parity test identified by `rg -n "catalog.*parity|locales" packages/i18n package.json`.

Expected: all focused tests and production build PASS; no stale import of `styles.css` remains.

- [x] **Step 6: Commit the design foundation**

```bash
git add src/AFK4.PlatformControl.Web/src/index.css src/AFK4.PlatformControl.Web/src/main.tsx src/AFK4.PlatformControl.Web/src/components packages/i18n/src/messages.ts locales
git add -u src/AFK4.PlatformControl.Web/src/styles.css
git commit -m "feat(platform-control): establish operations design system"
```

### Task 3: Operational overview and URL-backed organizations table

**Files:**
- Replace: `src/AFK4.PlatformControl.Web/src/platform/overview/OverviewScreen.tsx`
- Replace: `src/AFK4.PlatformControl.Web/src/platform/overview/OverviewScreen.test.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/overview/metricsModel.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/overview/metricsModel.test.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/overview/AttentionQueue.tsx`
- Replace: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationsScreen.tsx`
- Replace: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationsScreen.test.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationsTable.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationsTable.test.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/organizations/organizationsModel.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/organizations/organizationsModel.test.ts`

**Interfaces:**
- Consumes: `PlatformRoute`, `OrganizationSummary`, billing lists, `navigate(route)`.
- Produces: `AttentionItem`, `buildAttentionQueue()`, and an organizations table controlled entirely by the route query.

- [ ] **Step 1: Write failing behavior tests**

```tsx
expect(screen.getByRole('link', { name: /overdue.*Orion/i })).toHaveAttribute(
  'href', '/admin/organizations/org-orion?tab=invoices'
);
await user.type(screen.getByRole('searchbox'), 'tashkent');
expect(onNavigate).toHaveBeenLastCalledWith(expect.objectContaining({ kind: 'organizations', query: 'tashkent' }));
```

Cover health, overdue invoice, suspended organization, failed rollout, and expiring invite attention rows; distinguish first-use empty from filtered no-results.

- [ ] **Step 2: Run focused overview and organization tests**

Run: `cd src/AFK4.PlatformControl.Web && bun test src/platform/overview src/platform/organizations/organizationsModel.test.ts src/platform/organizations/OrganizationsTable.test.tsx src/platform/organizations/OrganizationsScreen.test.tsx`

Expected: FAIL because current overview is a KPI card grid and filters are component-local.

- [ ] **Step 3: Implement the attention-first overview**

Build one primary attention table grouped by severity, with direct links to the affected organization section. Keep a compact factual summary strip only where it helps interpret the queue. A failed optional source renders `PartialFailure` while available signals remain actionable.

- [ ] **Step 4: Implement the organizations workspace**

Use a dense table with name/status, club and device health, owner, plan/payment, last activity, and attention. Search, status, plan, attention, and sort controls update the URL. Remove the `Sheet` and `OrganizationDrawer` from this screen.

- [ ] **Step 5: Verify the vertical slice**

Run: `cd src/AFK4.PlatformControl.Web && bun test src/platform/overview src/platform/organizations && bun run build`

Expected: PASS; refresh/back tests retain list state and every attention row has a canonical destination.

- [ ] **Step 6: Commit overview and list**

```bash
git add src/AFK4.PlatformControl.Web/src/platform/overview src/AFK4.PlatformControl.Web/src/platform/organizations src/AFK4.PlatformControl.Web/src/routing packages/i18n/src/messages.ts locales
git commit -m "feat(platform-control): add operational organization overview"
```

### Task 4: Canonical organization page and resumable provisioning

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationPage.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationPage.test.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationSummaryTab.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationClubsTab.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationAccessTab.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationSupportTab.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationHistoryTab.tsx`
- Replace: `src/AFK4.PlatformControl.Web/src/platform/organizations/NewOrganizationScreen.tsx`
- Replace: `src/AFK4.PlatformControl.Web/src/platform/organizations/NewOrganizationScreen.test.tsx`
- Delete: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationDrawer.tsx`
- Delete: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationDrawer.test.tsx`
- Modify: organization section components and tests in `src/AFK4.PlatformControl.Web/src/platform/organizations/`
- Modify: `src/AFK4.PlatformControl.Web/src/api/types.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/api/platformClients/organizations.ts`
- Modify if projections are missing: platform organization endpoints and DTOs under `src/AFK4.Platform.Api/` and `src/AFK4.Shared.Contracts/`
- Test if backend changes: `tests/AFK4.Platform.Api.Tests/Platform/PlatformOrganizationEndpointTests.cs`

**Interfaces:**
- Consumes: canonical organization route, typed organization/detail/health/invite/note clients.
- Produces: `OrganizationPage` with independent tab states and `ProvisioningProgress` derived from persisted backend resources.

- [ ] **Step 1: Write failing canonical-page tests**

```tsx
renderOrganizationPage('/admin/organizations/org-1?tab=clubs');
expect(await screen.findByRole('heading', { name: 'Orion Gaming' })).toBeVisible();
expect(screen.getByRole('tab', { name: /clubs/i })).toHaveAttribute('aria-selected', 'true');
expect(screen.queryByText(/invoices are unavailable/i)).not.toBeInTheDocument();
```

Also prove that an invoices failure does not hide Summary or Clubs, direct tab URLs restore, forbidden tabs are absent, and status mutations require a reason and backend confirmation.

- [ ] **Step 2: Run focused frontend tests**

Run: `cd src/AFK4.PlatformControl.Web && bun test src/platform/organizations/OrganizationPage.test.tsx src/platform/organizations/NewOrganizationScreen.test.tsx`

Expected: FAIL because the canonical tabbed page and resumable provisioning do not exist.

- [ ] **Step 3: Add only missing backend projections with integration tests**

If owner identity, branch/device health, or audit history cannot be fetched without client-side joins, first add one organization-workspace projection with explicit read permissions. Write endpoint tests for Platform Owner, Platform Support, unauthenticated access, unknown organization, and partial empty data.

Run when changed: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PlatformOrganization" -p:NuGetAudit=false -p:UseSharedCompilation=false`

Expected: new authorization and projection tests PASS.

- [ ] **Step 4: Implement canonical tabs and independent failure boundaries**

Keep Summary factual; Clubs shows branch and device health; Owners and access owns invites; Support owns notes and support access; History is read-only audit. Subscription and Invoices mount their billing feature components without making the whole page depend on them.

- [ ] **Step 5: Implement resumable provisioning**

Persist the organization and first branch before navigating to the canonical page. Derive the next incomplete step from authoritative organization, invite, and subscription state; never keep the only progress in component memory. A retry resumes at the failed step without recreating completed resources.

- [ ] **Step 6: Verify frontend and any backend changes**

Run: `cd src/AFK4.PlatformControl.Web && bun test src/platform/organizations && bun run build`

If shared contracts or backend changed, also run the focused API tests and affected project build.

- [ ] **Step 7: Commit the organization workspace**

```bash
git add src/AFK4.PlatformControl.Web/src/platform/organizations src/AFK4.PlatformControl.Web/src/api src/AFK4.Shared.Contracts src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests packages/i18n/src/messages.ts locales
git commit -m "feat(platform-control): build canonical organization workspace"
```

### Task 5: Plans, subscriptions, invoices, updates, audit, and settings

**Files:**
- Refactor: `src/AFK4.PlatformControl.Web/src/platform/billing/`
- Refactor: `src/AFK4.PlatformControl.Web/src/platform/updates/`
- Create: `src/AFK4.PlatformControl.Web/src/platform/audit/AuditScreen.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/audit/AuditScreen.test.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/settings/PlatformSettingsScreen.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/settings/PlatformSettingsScreen.test.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/api/platformApi.ts`
- Create: `src/AFK4.PlatformControl.Web/src/api/platformClients/audit.ts`
- Create: `src/AFK4.PlatformControl.Web/src/api/platformClients/settings.ts`
- Modify if missing: relevant platform endpoints/contracts and `tests/AFK4.Platform.Api.Tests/Platform/`

**Interfaces:**
- Consumes: capability-gated routes and existing plans, subscriptions, invoices, packages, and rollouts clients.
- Produces: complete owner-only billing/update/settings workspaces and owner/support-readable audit according to backend permissions.

- [ ] **Step 1: Write failing lifecycle and permission tests**

```tsx
expect(renderRoute(supportSession, '/admin/billing')).toShowForbidden();
expect(renderRoute(ownerSession, '/admin/billing?tab=invoices')).toShowTab('Invoices');
await user.click(screen.getByRole('button', { name: /start rollout/i }));
expect(screen.getByLabelText(/reason/i)).toBeRequired();
```

Cover plan create/edit/archive where supported, subscription assignment/change, invoice inspection/correction policy, package validation/retirement, rollout creation/state, audit filtering, and settings save failures.

- [ ] **Step 2: Run focused tests and inventory missing contracts**

Run: `cd src/AFK4.PlatformControl.Web && bun test src/platform/billing src/platform/updates src/platform/audit src/platform/settings`

Expected: existing billing/update tests reveal the retained behavior; new audit/settings and role-gate tests FAIL until implemented.

- [ ] **Step 3: Add missing backend endpoints only for approved complete lifecycles**

For each new endpoint, add integration tests for authentication, role/permission denial, validation, idempotency where money or release state changes, audit emission, and not-found behavior. Do not add UI controls for lifecycle operations the domain does not support.

- [ ] **Step 4: Implement global workspaces using shared table/form/state primitives**

Keep billing as three URL-backed tabs. Make update publication and rollout owner-only with explicit target and consequence summaries. Audit uses server-side time/range filters when the result set can grow. Platform settings contains only persisted global settings, not profile preferences.

- [ ] **Step 5: Verify global workspaces**

Run: `cd src/AFK4.PlatformControl.Web && bun test src/platform/billing src/platform/updates src/platform/audit src/platform/settings src/App.test.tsx && bun run build`

Run focused API tests for every changed endpoint.

- [ ] **Step 6: Commit the global workspaces**

```bash
git add src/AFK4.PlatformControl.Web/src/platform src/AFK4.PlatformControl.Web/src/api src/AFK4.Shared.Contracts src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests packages/i18n/src/messages.ts locales
git commit -m "feat(platform-control): complete platform administration workspaces"
```

### Task 6: Global search and attention navigation

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/platform/search/GlobalSearch.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/search/GlobalSearch.test.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/search/searchModel.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/search/searchModel.test.ts`
- Create: `src/AFK4.PlatformControl.Web/src/api/platformClients/search.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/api/platformApi.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/components/shell/Topbar.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/components/shell/Topbar.test.tsx`
- Add if required: platform search endpoint/contracts and tests.

**Interfaces:**
- Consumes: query text, authenticated platform client, capability map, canonical route builder.
- Produces: `PlatformSearchResult` with `kind`, `id`, `title`, `context`, and canonical `href`.

- [ ] **Step 1: Write failing search tests**

```tsx
await user.type(screen.getByRole('searchbox', { name: /search platform/i }), 'orion');
expect(await screen.findByRole('option', { name: /Orion Gaming.*organization/i }))
  .toHaveAttribute('data-href', '/admin/organizations/org-orion');
```

Cover organization, club, owner, exact ID, empty query, no results, transport failure, keyboard selection, Escape, and permission-filtered result kinds.

- [ ] **Step 2: Run search tests and verify failure**

Run: `cd src/AFK4.PlatformControl.Web && bun test src/platform/search`

Expected: FAIL because the top bar search is only a shell affordance.

- [ ] **Step 3: Implement a server-backed bounded search contract if current lists are insufficient**

Use a minimum query length, cancellation through `AbortSignal`, bounded result groups, normalized Unicode-insensitive matching where supported by PostgreSQL, and exact-ID lookup. Return only resources visible to the caller.

- [ ] **Step 4: Implement keyboard-first global search**

Results use canonical links so modifier-click, browser history, and copied URLs work. Announce result count through `aria-live`; do not overwrite a newer query with a slower earlier response.

- [ ] **Step 5: Verify search**

Run: `cd src/AFK4.PlatformControl.Web && bun test src/platform/search src/components/shell/Topbar.test.tsx && bun run build`

Run focused endpoint tests if the backend search contract was added.

- [ ] **Step 6: Commit search**

```bash
git add src/AFK4.PlatformControl.Web/src/platform/search src/AFK4.PlatformControl.Web/src/api src/AFK4.PlatformControl.Web/src/components/shell src/AFK4.Shared.Contracts src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests packages/i18n/src/messages.ts locales
git commit -m "feat(platform-control): add global resource search"
```

### Task 7: Remove superseded UI and run the big-bang release gate

**Files:**
- Delete: all superseded Platform Control screens, models, styles, and tests identified by import reachability.
- Modify: `src/AFK4.PlatformControl.Web/src/App.test.tsx`
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Modify: `docs/superpowers/plans/README.md`
- Archive after merge: this plan and `docs/superpowers/specs/2026-07-29-platform-control-rebuild-design.md` according to repository convention.

**Interfaces:**
- Consumes: all completed slices.
- Produces: one reachable Platform Control implementation with no legacy authority or duplicate routes.

- [ ] **Step 1: Add failing big-bang regression tests**

```ts
expect(resolvePlatformRoute('/admin/organizations/org-1?tab=support').kind).toBe('organization');
expect(resolvePlatformRoute('/organizations/org-1').kind).toBe('notFound');
expect(screen.queryByTestId('organization-drawer')).not.toBeInTheDocument();
```

Add route inventory tests covering every supported public/authenticated path and proving legacy `/organizations/*` and duplicate screens are gone.

- [ ] **Step 2: Remove unreachable legacy files and compatibility redirects**

Use `rg` to prove each deletion has no live imports. Keep `/account-activation` because it is a current owner-invitation journey; remove only obsolete Platform Control compatibility paths and UI.

- [ ] **Step 3: Run frontend verification**

Run: `cd src/AFK4.PlatformControl.Web && bun test && bun run build`

Expected: all Platform Control tests PASS and production bundle builds without stale imports.

- [ ] **Step 4: Run affected backend and solution verification**

If any shared contracts or backend endpoints changed:

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj -p:NuGetAudit=false -p:UseSharedCompilation=false
dotnet build AFK4.sln -p:EnableWindowsTargeting=true -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: PASS. If Linux cannot execute WindowsDesktop tests, run the repository's established Windows-runtime gate before push and report the platform limitation explicitly.

- [ ] **Step 5: Perform browser and accessibility smoke**

Run the production build through the repository's local Platform Control host. Verify Platform Owner and Platform Support at desktop and narrow widths, light/dark themes, keyboard-only navigation, 200% zoom, long Unicode names, empty data, partial API failure, forbidden routes, and expired session. Record screenshots or browser assertions for the canonical organization and global update flows.

- [ ] **Step 6: Update durable project state**

Record the replacement, exact verification counts, remaining gaps, and next project-level work in the compact progress snapshot. Do not claim production deployment or physical Windows smoke.

- [ ] **Step 7: Inspect and commit the final gate**

```bash
git diff --check
git status --short
git diff --stat
git add src/AFK4.PlatformControl.Web src/AFK4.Platform.Api src/AFK4.Shared.Contracts tests/AFK4.Platform.Api.Tests packages/i18n locales docs
git diff --cached --check
git diff --cached --stat
git commit -m "feat(platform-control): complete big-bang frontend replacement"
```

Do not push, merge, deploy, or delete the topic branch without explicit user authorization.
