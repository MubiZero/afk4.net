# Operator UI History Consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a clean branch rooted at the latest recorded `origin/main` whose tracked tree is identical to a fully verified merge of the current Operator UI feature branch and that `origin/main` tip.

**Architecture:** Use a two-worktree proof pipeline. A proof branch first performs Git's normal three-way merge and passes all verification; a clean branch then squash-applies that verified tree onto the same `origin/main` base and is accepted only when both tree IDs match exactly.

**Tech Stack:** Git worktrees, React 19, TypeScript, Bun 1.3.14, Vite, CSS custom properties, .NET 10 (`global.json` permits the installed 10.0.300 latest-feature SDK), xUnit, browser-preview QA.

## Global Constraints

- Follow `docs/superpowers/specs/2026-07-13-operator-ui-history-consolidation-design.md` exactly.
- Preserve the original feature branch, all existing branches, and both dirty `.claude/worktrees/*` worktrees until the user explicitly approves cleanup.
- Do not push, force-push, merge to `main`, delete branches, clean worktrees, or mutate the two legacy dirty worktrees.
- Do not add the POS Recent receipts panel, port legacy clients master-detail code, remove anti-fraud, or remove `ReviewWorkspace`.
- Preserve the final Clients table/drawer, Stock redesign, shared UI-kit, toast channel, navigation order, and theme-aware Operator/Setup Wizard icons.
- Preserve the five recorded `origin/main` QA-hardening commits and their semantic intent.
- Use `/home/fedya/.bun/bin/bun` explicitly and `dotnet` from the installed .NET 10 SDK.
- Stop on a dirty original checkout, a red baseline, an ambiguous product conflict, a tree mismatch, or a verification failure.
- Use `superpowers:using-git-worktrees` before creating either execution worktree.
- Use `superpowers:frontend-testing-debugging` for browser-preview QA; the Browser plugin is preferred when callable, otherwise use regular Playwright and record that reason.

## File And Branch Map

- `feat/operator-clients-center-redesign` — immutable source branch during execution.
- `backup/operator-ui-pre-consolidation` — immutable local recovery pointer created from the recorded source commit.
- `backup/origin-main-at-consolidation` — immutable local pointer to the recorded `origin/main` commit.
- `.worktrees/operator-ui-proof` / `integration/operator-ui-proof` — normal proof merge and first verification gate.
- `.worktrees/operator-ui-consolidated` / `feat/operator-ui-consolidated` — clean squash result and final verification gate.
- `docs/superpowers/specs/README.md` — keep both the QA-hardening and consolidation entries.
- `docs/superpowers/plans/README.md` — keep both active-plan entries until each plan ships.
- `src/AFK4.Operator.App.Web/src/styles/06-map-grid.css` — retain current ownership boundaries and merge the newer map-label contrast changes.
- `src/AFK4.Operator.App.Web/src/styles/11-pos.css` — retain the current POS/UI-kit redesign and merge the newer SKU contrast token.
- `src/AFK4.Operator.App.Web/src/styles/12-players.css` — retain the current table/drawer implementation; do not restore deleted legacy `.client-row` presentation rules.
- `src/AFK4.Operator.App.Web/src/styles/16-workspace-primitives.css` — authoritative owner of shared critical-confirmation actions, including the merged primary CTA rule.
- `src/AFK4.Operator.App.Web/src/styles/qaContrast.test.ts` — point the CTA guard at `16-workspace-primitives.css`, not the superseded map-local location.
- `src/AFK4.Operator.App.Web/src/devMockBackend.ts` and realtime files — accept the non-conflicting QA-hardening merge from `origin/main`.

---

### Task 1: Freeze Source State And Create The Proof Worktree

**Files:**
- Read: `README.md`
- Read: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Read: `docs/superpowers/specs/2026-07-13-operator-ui-history-consolidation-design.md`
- Create outside tracked source: `.worktrees/operator-ui-proof/`

**Interfaces:**
- Consumes: clean `feat/operator-clients-center-redesign`, current `origin/main`, and unchanged legacy worktrees.
- Produces: immutable backup refs plus isolated `integration/operator-ui-proof` at the exact source commit.

- [ ] **Step 1: Refresh remote refs and prove the original checkout is clean**

Run from `/home/fedya/projects/afk4.net`:

```bash
git fetch --prune origin
git status --short --branch
test -z "$(git status --porcelain=v1)"
```

Expected: the branch is `feat/operator-clients-center-redesign`; the final command exits 0. Stop before branch creation if it does not.

- [ ] **Step 2: Record immutable source, base, and legacy-worktree evidence**

Run:

```bash
SOURCE_SHA=$(git rev-parse feat/operator-clients-center-redesign^{commit})
BASE_SHA=$(git rev-parse origin/main^{commit})
printf 'SOURCE_SHA=%s\nBASE_SHA=%s\n' "$SOURCE_SHA" "$BASE_SHA"
git -C .claude/worktrees/agent-a22a7e4fa8a8723c7 status --porcelain=v1
git -C .claude/worktrees/agent-a22a7e4fa8a8723c7 diff --binary | git hash-object --stdin
git -C .claude/worktrees/agent-a5e4a6d9dc0d15f70 status --porcelain=v1
git -C .claude/worktrees/agent-a5e4a6d9dc0d15f70 diff --binary | git hash-object --stdin
```

Expected: two commit IDs and two stable dirty-worktree diff hashes are captured in the execution log. The first worktree lists 17 changed paths and the second lists 10 deleted brand assets unless the user changed them after this plan was written; any difference is reported before proceeding.

- [ ] **Step 3: Create immutable recovery refs**

Run after re-establishing `SOURCE_SHA` and `BASE_SHA` in the same shell:

```bash
git show-ref --verify --quiet refs/heads/backup/operator-ui-pre-consolidation && exit 1 || true
git show-ref --verify --quiet refs/heads/backup/origin-main-at-consolidation && exit 1 || true
git branch backup/operator-ui-pre-consolidation "$SOURCE_SHA"
git branch backup/origin-main-at-consolidation "$BASE_SHA"
git show --no-patch --format='%H %s' backup/operator-ui-pre-consolidation
git show --no-patch --format='%H %s' backup/origin-main-at-consolidation
```

Expected: both backup refs resolve to the IDs printed in Step 2. If a ref already exists, stop rather than moving it.

- [ ] **Step 4: Verify the worktree location is ignored and create the proof worktree**

Run:

```bash
git check-ignore -q .worktrees/probe
git show-ref --verify --quiet refs/heads/integration/operator-ui-proof && exit 1 || true
git worktree add .worktrees/operator-ui-proof -b integration/operator-ui-proof backup/operator-ui-pre-consolidation
git -C .worktrees/operator-ui-proof status --short --branch
```

Expected: `.worktrees/` is ignored, the new branch is clean, and its HEAD equals `backup/operator-ui-pre-consolidation`.

- [ ] **Step 5: Install locked workspace dependencies in the proof worktree**

Run:

```bash
cd /home/fedya/projects/afk4.net/.worktrees/operator-ui-proof
/home/fedya/.bun/bin/bun install --frozen-lockfile
dotnet tool restore
```

Expected: both commands exit 0 without modifying tracked files.

- [ ] **Step 6: Run the pre-merge baseline and stop if it is red**

Run:

```bash
cd /home/fedya/projects/afk4.net/.worktrees/operator-ui-proof/src/AFK4.Operator.App.Web
/home/fedya/.bun/bin/bun run test
/home/fedya/.bun/bin/bun run build
cd /home/fedya/projects/afk4.net/.worktrees/operator-ui-proof
dotnet build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
dotnet test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: all four commands exit 0. This is the baseline for attributing later failures to merge resolution.

---

### Task 2: Merge `origin/main` And Reconcile The Four Predicted Conflicts

**Files:**
- Modify: `docs/superpowers/specs/README.md`
- Modify: `docs/superpowers/plans/README.md` when the new plan entry collides with the QA plan entry
- Modify: `src/AFK4.Operator.App.Web/src/styles/06-map-grid.css`
- Modify: `src/AFK4.Operator.App.Web/src/styles/11-pos.css`
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css`
- Modify: `src/AFK4.Operator.App.Web/src/styles/16-workspace-primitives.css`
- Modify: `src/AFK4.Operator.App.Web/src/styles/qaContrast.test.ts`
- Verify auto-merge: `src/AFK4.Operator.App.Web/src/devMockBackend.ts`
- Verify auto-merge: `src/AFK4.Operator.App.Web/src/devHostBridge.ts`
- Verify auto-merge: `src/AFK4.Operator.App.Web/src/operatorRealtime.ts`
- Verify auto-merge: `src/AFK4.Operator.App.Web/src/useOperatorRealtime.ts`

**Interfaces:**
- Consumes: proof branch at the recorded source commit and `backup/origin-main-at-consolidation`.
- Produces: a normal two-parent proof merge with current final UI and all five QA commits.

- [ ] **Step 1: Start the normal merge and confirm the predicted conflict set**

Run:

```bash
cd /home/fedya/projects/afk4.net/.worktrees/operator-ui-proof
git merge --no-ff backup/origin-main-at-consolidation -m "merge: reconcile operator UI with current main"
git diff --name-only --diff-filter=U
```

Expected: the merge stops for conflicts. Before the plan/README commit, `git merge-tree` predicted `docs/superpowers/specs/README.md`, `06-map-grid.css`, `11-pos.css`, and `12-players.css`; `docs/superpowers/plans/README.md` may join that set because this plan adds an adjacent active-plan entry. Stop if any other source file conflicts and inspect its base/ours/theirs intent before editing.

- [ ] **Step 2: Resolve both navigation indexes by retaining both active entries**

First restore the current branch's versions as the structural base:

```bash
git checkout --ours docs/superpowers/specs/README.md docs/superpowers/plans/README.md
```

Use `apply_patch` so the specs index contains these two bullets in the open-epic list:

```markdown
- `2026-07-13-operator-qa-hardening-design.md` — Operator App CTA and contrast
  remediation plus reliable browser-preview data and realtime behaviour.
- `2026-07-13-operator-ui-history-consolidation-design.md` — preserve the
  verified final Operator UI tree while consolidating its iterative history
  onto the latest `origin/main` without touching legacy dirty worktrees.
```

Use `apply_patch` so the plans index contains both:

```markdown
- `2026-07-13-operator-qa-hardening.md` — Operator App CTA/a11y remediation
  and reliable browser-preview state/realtime QA.
- `2026-07-13-operator-ui-history-consolidation.md` — verify a normal merge of
  the final Operator UI tree, then reproduce that exact tree on current main
  as one clean consolidation commit.
```

Expected: no conflict markers remain in either README and neither active item is lost.

- [ ] **Step 3: Resolve `06-map-grid.css` without moving shared primitives back into the map layer**

Restore the current branch's file as the structural base:

```bash
git checkout --ours src/AFK4.Operator.App.Web/src/styles/06-map-grid.css
```

Keep the current branch's removal of `.state-flag*` and `.critical-confirmation*` declarations from `06-map-grid.css`. Apply only the QA map-label changes:

```css
.seat-clock-label {
  flex: none;
  color: var(--text-secondary);
  font-size: 11px;
  font-weight: 600;
}

.seat-open-tab {
  color: var(--accent-bright);
  font-size: 12px;
  font-weight: 600;
}
```

Expected: `rg -n "critical-confirmation-actions" src/AFK4.Operator.App.Web/src/styles/06-map-grid.css` prints nothing; map elevation and the two QA color changes remain.

- [ ] **Step 4: Retarget the QA guard to the shared CSS owner and verify RED**

Change `qaContrast.test.ts` to read the shared file:

```ts
const workspacePrimitivesCss = readFileSync(join(import.meta.dir, '16-workspace-primitives.css'), 'utf8');

describe('operator QA visual guards', () => {
  it('keeps the enabled session-start confirmation CTA visually primary', () => {
    expect(workspacePrimitivesCss).toContain('.critical-confirmation-actions .cta-primary:not(:disabled)');
  });
});
```

Expected: the test protects the selector's new canonical location and does not force shared CSS back into `06-map-grid.css`.

Run:

```bash
cd src/AFK4.Operator.App.Web
/home/fedya/.bun/bin/bun test src/styles/qaContrast.test.ts
```

Expected: FAIL because `16-workspace-primitives.css` does not yet contain the explicit primary CTA selector.

- [ ] **Step 5: Put the QA CTA rule in its current authoritative owner and verify GREEN**

After the generic hover/focus rule in `16-workspace-primitives.css`, add:

```css
.critical-confirmation-actions .cta-primary:not(:disabled) {
  border-color: var(--accent);
  background: var(--accent);
  color: var(--text-on-accent);
}

.critical-confirmation-actions .cta-primary:hover:not(:disabled),
.critical-confirmation-actions .cta-primary:focus-visible:not(:disabled) {
  border-color: var(--accent-hover);
  background: var(--accent-hover);
}
```

Run:

```bash
/home/fedya/.bun/bin/bun test src/styles/qaContrast.test.ts
```

Expected: PASS. The current shared-layer ownership remains intact and the enabled session-start CTA overrides the more-specific generic footer button.

- [ ] **Step 6: Resolve POS and Clients CSS in favour of current structures plus applicable QA tokens**

Restore both current final files as the structural base:

```bash
git checkout --ours src/AFK4.Operator.App.Web/src/styles/11-pos.css \
  src/AFK4.Operator.App.Web/src/styles/12-players.css
```

In `11-pos.css`, retain the current UI-kit/card/search/scanner refactor and set the current direct-child SKU selector to:

```css
.pos-product-card > em {
  color: var(--text-tertiary);
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
  font-size: 10px;
  font-style: normal;
}
```

In `12-players.css`, retain the current `.ctable-*`, `.cc-*`, and `.drawer-*` table/drawer styles. Do not restore removed legacy `.client-row em` or `.client-row-badge.is-inactive` rules from `origin/main`; their components were superseded. Keep all non-conflicting shared token updates from `origin/main`.

Expected: `rg -n "^\.client-row( |\.|$)|client-row-badge" src/AFK4.Operator.App.Web/src/styles/12-players.css` prints nothing, while `rg -n "^\.ctable-row|^\.drawer-panel"` finds the current implementation.

- [ ] **Step 7: Verify the non-conflicting QA code survived auto-merge**

Run:

```bash
rg -n "currentPreviewFloorMap|/sessions/start|previewLayoutZones|previewStaff" src/AFK4.Operator.App.Web/src/devMockBackend.ts
rg -n "vite-dev-preview" src/AFK4.Operator.App.Web/src/devHostBridge.ts src/AFK4.Operator.App.Web/src/useOperatorRealtime.ts
rg -n "createPreviewOperatorRealtimeClient" src/AFK4.Operator.App.Web/src/operatorRealtime.ts src/AFK4.Operator.App.Web/src/operatorRealtime.test.ts
rg -n "^<<<<<<<|^=======|^>>>>>>>" docs/superpowers src packages locales
```

Expected: all QA symbols are found and no conflict marker is found.

- [ ] **Step 8: Run focused merge-resolution tests before committing**

Run:

```bash
cd src/AFK4.Operator.App.Web
/home/fedya/.bun/bin/bun test src/MapSidePanel.test.tsx src/devMockBackend.test.ts src/operatorRealtime.test.ts src/styles/qaContrast.test.ts
cd ../../packages/tokens
/home/fedya/.bun/bin/bun test
```

Expected: all focused tests pass, including CTA ownership, preview state, preview realtime, and shared contrast tokens.

- [ ] **Step 9: Inspect and commit the proof merge**

Run:

```bash
cd /home/fedya/projects/afk4.net/.worktrees/operator-ui-proof
git add docs/superpowers/specs/README.md docs/superpowers/plans/README.md \
  src/AFK4.Operator.App.Web/src/styles/06-map-grid.css \
  src/AFK4.Operator.App.Web/src/styles/11-pos.css \
  src/AFK4.Operator.App.Web/src/styles/12-players.css \
  src/AFK4.Operator.App.Web/src/styles/16-workspace-primitives.css \
  src/AFK4.Operator.App.Web/src/styles/qaContrast.test.ts
git add -u
git status --short
git diff --cached --check
git diff --cached --stat
git commit --no-edit
git show --no-patch --format='%H %P %s' HEAD
```

Expected: the commit has two parents, the staged diff has no whitespace errors, and the worktree becomes clean.

---

### Task 3: Verify The Proof Merge

**Files:**
- Test: `packages/i18n/**/*.test.ts*`
- Test: `packages/tokens/**/*.test.ts*`
- Test: `src/AFK4.Operator.App.Web/src/**/*.test.ts*`
- Build: `src/AFK4.Operator.App.Web`
- Build/test: `AFK4.sln`
- Runtime QA: Operator browser preview at `http://127.0.0.1:5174/`

**Interfaces:**
- Consumes: committed proof merge.
- Produces: fresh verification evidence authorizing creation of the clean branch.

- [ ] **Step 1: Run shared package and full Operator Web gates**

Run:

```bash
cd /home/fedya/projects/afk4.net/.worktrees/operator-ui-proof/packages/i18n
/home/fedya/.bun/bin/bun run gen
/home/fedya/.bun/bin/bun test
cd ../tokens
/home/fedya/.bun/bin/bun test
cd ../../src/AFK4.Operator.App.Web
/home/fedya/.bun/bin/bun run test
/home/fedya/.bun/bin/bun run build
```

Expected: generation leaves no tracked diff; every test passes; `tsc -b` and Vite production build exit 0.

- [ ] **Step 2: Run full solution build and test**

Run:

```bash
cd /home/fedya/projects/afk4.net/.worktrees/operator-ui-proof
dotnet build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
dotnet test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: both commands exit 0 under the compatible .NET 10 SDK.

- [ ] **Step 3: Run browser-preview QA through the frontend testing skill**

Start the dev server in a persistent exec session:

```bash
cd /home/fedya/projects/afk4.net/.worktrees/operator-ui-proof/src/AFK4.Operator.App.Web
/home/fedya/.bun/bin/bun run dev
```

At `1440x960`, verify dark and light themes for sign-in, floor map, Clients, Stock, and POS. Start an open-tab session on a free preview seat and confirm the subsequent floor-map read renders it active. Open Settings and confirm seeded seats/staff are non-zero. Record browser console errors and network requests; `/hubs/devices` must not occur in preview mode. Run axe WCAG 2 A/AA checks on sign-in, floor map in both themes, POS, and Clients.

Expected: no blocking console error, no real hub request, coherent preview mutation, and no axe A/AA violation in the audited states.

- [ ] **Step 4: Confirm proof cleanliness**

Run:

```bash
cd /home/fedya/projects/afk4.net/.worktrees/operator-ui-proof
git diff --check
git status --short --branch
```

Expected: clean `integration/operator-ui-proof`. If any verification command generated a tracked change, inspect and remove only that generated change before continuing.

---

### Task 4: Create The Clean Tree-Identical Consolidation Branch

**Files:**
- Create outside tracked source: `.worktrees/operator-ui-consolidated/`
- Stage: the proof branch's net tracked diff relative to `backup/origin-main-at-consolidation`

**Interfaces:**
- Consumes: green `integration/operator-ui-proof` and immutable recorded base.
- Produces: one clean consolidation commit on `feat/operator-ui-consolidated`.

- [ ] **Step 1: Create the clean worktree from the recorded base**

Run from the original checkout:

```bash
cd /home/fedya/projects/afk4.net
git show-ref --verify --quiet refs/heads/feat/operator-ui-consolidated && exit 1 || true
git worktree add .worktrees/operator-ui-consolidated -b feat/operator-ui-consolidated backup/origin-main-at-consolidation
git -C .worktrees/operator-ui-consolidated status --short --branch
```

Expected: the worktree is clean and HEAD equals `backup/origin-main-at-consolidation`.

- [ ] **Step 2: Install locked dependencies**

Run:

```bash
cd /home/fedya/projects/afk4.net/.worktrees/operator-ui-consolidated
/home/fedya/.bun/bin/bun install --frozen-lockfile
dotnet tool restore
```

Expected: both commands exit 0 and tracked status stays clean.

- [ ] **Step 3: Squash-apply the verified proof tree**

Run:

```bash
git merge --squash integration/operator-ui-proof
git status --short
git diff --cached --check
git diff --cached --stat
git diff --cached --name-status
```

Expected: the staged change is the proof branch's net tracked difference from the recorded main base. No `.claude/worktrees`, ignored dependency, untracked, or dirty-worktree-only path is staged.

- [ ] **Step 4: Commit the consolidation**

Run:

```bash
git commit -m "feat(operator): consolidate UI redesign on current main"
git show --no-patch --format='%H %P %s' HEAD
git rev-list --left-right --count backup/origin-main-at-consolidation...HEAD
```

Expected: the commit has exactly one parent, that parent is the recorded main base, and the count is `0 1`.

---

### Task 5: Prove Tree Identity And Repeat Verification

**Files:**
- Test/build: same tracked tree as `integration/operator-ui-proof`
- No source modifications expected

**Interfaces:**
- Consumes: committed proof and clean branches.
- Produces: cryptographic tree equality plus fresh clean-branch verification.

- [ ] **Step 1: Prove tracked tree identity before running tests**

Run:

```bash
cd /home/fedya/projects/afk4.net
git diff --exit-code integration/operator-ui-proof feat/operator-ui-consolidated
PROOF_TREE=$(git rev-parse integration/operator-ui-proof^{tree})
CLEAN_TREE=$(git rev-parse feat/operator-ui-consolidated^{tree})
printf 'PROOF_TREE=%s\nCLEAN_TREE=%s\n' "$PROOF_TREE" "$CLEAN_TREE"
test "$PROOF_TREE" = "$CLEAN_TREE"
```

Expected: no diff output, equal tree IDs, and a zero exit code. Stop and diagnose any mismatch.

- [ ] **Step 2: Run the clean branch's shared and frontend gates**

Run:

```bash
cd /home/fedya/projects/afk4.net/.worktrees/operator-ui-consolidated/packages/i18n
/home/fedya/.bun/bin/bun run gen
/home/fedya/.bun/bin/bun test
cd ../tokens
/home/fedya/.bun/bin/bun test
cd ../../src/AFK4.Operator.App.Web
/home/fedya/.bun/bin/bun run test
/home/fedya/.bun/bin/bun run build
```

Expected: all tests and the production build pass with no tracked generated diff.

- [ ] **Step 3: Run the clean branch's full solution gate**

Run:

```bash
cd /home/fedya/projects/afk4.net/.worktrees/operator-ui-consolidated
dotnet build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
dotnet test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: both commands exit 0.

- [ ] **Step 4: Repeat the browser-preview smoke on the clean branch**

Use the same viewport, routes, session mutation, Settings checks, console/network capture, and axe checks from Task 3 Step 3 against the clean worktree's dev server.

Expected: results match the proof branch. A difference despite equal tree IDs is treated as environment or nondeterminism and investigated before completion.

---

### Task 6: Audit Recovery State And Hand Off Without Publishing

**Files:**
- Read only: all worktree and branch state
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md` only if consolidation exposes a new durable verified gap; otherwise leave unchanged

**Interfaces:**
- Consumes: green tree-identical branches.
- Produces: evidence-backed handoff with no push, merge, deletion, or cleanup.

- [ ] **Step 1: Recheck original and legacy worktree state**

Run:

```bash
cd /home/fedya/projects/afk4.net
git status --short --branch
git -C .claude/worktrees/agent-a22a7e4fa8a8723c7 status --porcelain=v1
git -C .claude/worktrees/agent-a22a7e4fa8a8723c7 diff --binary | git hash-object --stdin
git -C .claude/worktrees/agent-a5e4a6d9dc0d15f70 status --porcelain=v1
git -C .claude/worktrees/agent-a5e4a6d9dc0d15f70 diff --binary | git hash-object --stdin
```

Expected: the original checkout is still clean and the two diff hashes match Task 1 Step 2 exactly.

- [ ] **Step 2: Audit branch topology and publication state**

Run:

```bash
git branch -vv
git worktree list --porcelain
git rev-list --left-right --count origin/main...feat/operator-ui-consolidated
git log --oneline --decorate --graph --max-count=12 feat/operator-ui-consolidated
```

Expected: the clean branch is one consolidation commit on the recorded main base; proof and backup refs remain local; nothing was pushed or merged.

- [ ] **Step 3: Decide progress-document impact**

Compare verification outcomes with `docs/progress/2026-05-12-vertical-slice-progress.md`. Because the branch is not yet merged or published, leave progress unchanged unless verification exposed a new durable gap. If a new gap exists, add only that compact gap, run `git diff --check`, and commit it separately on the clean branch as:

```bash
git add docs/progress/2026-05-12-vertical-slice-progress.md
git commit -m "docs(progress): record operator consolidation verification gap"
```

Expected in the normal path: no progress commit and tree identity remains intact. If a gap commit is required, report that tree identity applied immediately before the evidence-only progress update and include the extra commit explicitly in handoff.

- [ ] **Step 4: Report completion evidence**

Report the source/base/proof/clean commit IDs, both tree IDs, conflict resolutions, all verification results, original and legacy worktree hashes, branch ahead/behind state, and explicit `not pushed / not merged / not cleaned` status.

## Plan Self-Review

- Tasks 1 and 6 preserve and re-audit every recovery point required by the spec.
- Task 2 names every currently predicted conflict and gives exact semantic/CSS resolution, including moving the CTA guard to the current shared owner.
- Task 3 prevents a bad merge from becoming the clean branch.
- Task 4 creates exactly one consolidation commit on the immutable recorded main base.
- Task 5 proves tracked tree identity before repeating all required verification.
- POS Recent receipts, legacy clients master-detail, anti-fraud removal, branch cleanup, push, and merge remain outside scope.
- No placeholder requirement or deferred implementation choice remains.
