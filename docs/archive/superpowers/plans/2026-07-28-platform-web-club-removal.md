# Platform.Web `/club` Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the obsolete browser club workspace from `AFK4.Platform.Web` while preserving the internal platform-owner/support Control Plane.

**Architecture:** Make Platform Web an admin-only SPA rooted at `/admin`. Remove the club route union, staff-auth bootstrap, club API client, club feature tree, and club-only shell components. Keep platform UI primitives and move the money package re-export to a neutral shared module before deleting `src/club`.

**Tech Stack:** React 19, TypeScript, Bun test, Vite, `@afk4/money`.

## Global Constraints

- Preserve all `/admin`, tenant, platform billing, platform profile, and platform-admin authentication behavior.
- `/club` and every `/club/*` URL resolve to the existing `notFound` route; do not silently redirect club users into Control Plane.
- Do not change or remove backend branch endpoints or shared contracts used by Operator App.
- Do not push, deploy, or mix the unrelated `.claude/memory/*` edits into commits.

---

### Task 1: Lock the admin-only route contract

**Files:**
- Modify: `src/AFK4.Platform.Web/src/App.test.tsx`
- Delete: `src/AFK4.Platform.Web/src/App.routing.test.ts`
- Delete: `src/AFK4.Platform.Web/src/App.branches.test.tsx`
- Delete: `src/AFK4.Platform.Web/src/App.settings.test.tsx`

**Interfaces:**
- Consumes: `resolvePlatformRoute(pathname, historyState?, search?)`.
- Produces: regression proof that `/club` and `/club/install` are unsupported while `/admin` remains available.

- [x] **Step 1: Replace club-positive routing assertions with admin-only assertions**

```ts
it('rejects removed club routes', () => {
  expect(resolvePlatformRoute('/club').route).toEqual({ kind: 'notFound', path: '/club' });
  expect(resolvePlatformRoute('/club/install').route).toEqual({ kind: 'notFound', path: '/club/install' });
});
```

- [x] **Step 2: Run `bun test src/App.test.tsx` and verify RED because `/club` still resolves**

- [x] **Step 3: Keep all existing admin route, sign-in, tenant navigation, and rendering assertions**

### Task 2: Convert App to admin-only routing

**Files:**
- Modify: `src/AFK4.Platform.Web/src/App.tsx`
- Modify: `src/AFK4.Platform.Web/src/main.tsx`
- Modify: `src/AFK4.Platform.Web/src/vite-env.d.ts`

**Interfaces:**
- Consumes: platform-admin `PlatformApiClient`, `PlatformAdminSession`, and `AdminRoute`.
- Produces: `App` and `resolvePlatformRoute` without audience or club route support.

- [x] **Step 1: Remove audience, club routes, staff session/client state, and club navigation callbacks**

- [x] **Step 2: Remove `ClubArea`, club route mapping, audience filtering, and `VITE_AUDIENCE` normalization**

- [x] **Step 3: Keep `/` -> `/admin`, legacy `/tenants*`, `/auth/sign-in`, and `/admin*` behavior**

- [x] **Step 4: Run `bun test src/App.test.tsx` and verify GREEN**

- [x] **Step 5: Commit the admin-only route slice**

### Task 3: Remove the club feature and private transport trees

**Files:**
- Delete: `src/AFK4.Platform.Web/src/club/**`
- Delete: `src/AFK4.Platform.Web/src/api/clubApi.ts` and tests
- Delete: `src/AFK4.Platform.Web/src/api/apiTransport.ts`
- Delete: `src/AFK4.Platform.Web/src/api/clients/**`
- Delete: `src/AFK4.Platform.Web/src/api/staffAuthApi.ts` and test
- Delete: `src/AFK4.Platform.Web/src/auth/staffTokenStore.ts` and test
- Replace: the session-persisting invite acceptance with stateless
  `AcceptOwnerInvite` + `OwnerInviteAcceptanceApi`
- Delete: `src/AFK4.Platform.Web/src/components/StaffSignIn.tsx`,
  `ForgotPassword.tsx`, their tests, and club-only shell files

**Interfaces:**
- Consumes: the green admin-only App from Task 2.
- Produces: no runtime import path from Platform Web to branch-scoped club
  code; public first-owner onboarding remains without persisting staff tokens.

- [x] **Step 1: Delete club-only source and colocated tests**

- [x] **Step 2: Search with `rg -n "@/club|/club|clubApi|staffAuthApi|staffTokenStore|apiTransport" src/AFK4.Platform.Web/src`**

- [x] **Step 3: Run `bun test`; missing-module failures identify accidental Control Plane dependencies**

### Task 4: Preserve shared platform utilities and clean configuration

**Files:**
- Create: `src/AFK4.Platform.Web/src/lib/money.ts`
- Modify: platform imports currently targeting `@/club/money`
- Modify: `src/AFK4.Platform.Web/src/api/types.ts` only for proven-unused club-only declarations
- Modify: `src/AFK4.Platform.Web/src/styles.css` to remove club-only selectors

**Interfaces:**
- Produces: `minorToMajor` and `majorToMinor` re-exported from `@afk4/money` at `@/lib/money`.

- [x] **Step 1: Add `export { minorToMajor, majorToMinor } from '@afk4/money';` and update platform imports**

- [x] **Step 2: Remove proven-dead club-only types/styles/config without changing platform contracts**

- [x] **Step 3: Run `bun test` and `bun run build`**

- [x] **Step 4: Commit club source removal and utility cleanup**

### Task 5: Record durable state and certify removal

**Files:**
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Modify: `docs/superpowers/plans/README.md`
- Modify: `docs/superpowers/specs/README.md`
- Modify: `docs/superpowers/notes/2026-07-28-operator-parity-certificate.md`

**Interfaces:**
- Produces: current project docs stating Platform Web is Control Plane only and recording fresh verification.

- [x] **Step 1: Replace the recommended removal item with the next project-level work**

- [x] **Step 2: Mark the certificate removal gate fulfilled without rewriting historical parity evidence**

- [x] **Step 3: Run `git diff --check`**

- [x] **Step 4: Run `bun test && bun test src/App.test.tsx && bun run build`**

- [x] **Step 5: Self-review scope and commit docs**
