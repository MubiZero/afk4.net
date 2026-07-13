# Operator UI History Consolidation Design

**Status:** approved in conversation; pending written-spec review
**Date:** 2026-07-13

## Goal

Consolidate the current Operator UI feature work onto the latest `origin/main`
without changing the verified final repository tree, while retaining the
existing branches and dirty worktrees as recovery points until the clean branch
has passed all required verification.

## Context

The current feature branch contains the desired final Operator UI state but has
accumulated a long, iterative history. It is 102 commits ahead of and 5 commits
behind the latest `origin/main`; 33 of those commits have not been pushed to the
feature branch's upstream. Separate legacy worktrees contain uncommitted edits,
and one older POS branch contains potentially useful work that is intentionally
outside this consolidation.

The five newer `origin/main` commits add Operator QA hardening: CTA and contrast
fixes, coherent browser-preview session state, and preview realtime isolation.
Those changes must remain present in the consolidated result.

## Scope

1. Record immutable commit IDs for the current feature tip and `origin/main` at
   the start of execution.
2. Preserve the current branch, its upstream, all local branches, and both dirty
   legacy worktrees unchanged as recovery points.
3. Create an isolated proof worktree and branch from the recorded current
   feature tip.
4. Merge the recorded `origin/main` tip into the proof branch and resolve only
   genuine overlaps between the current Operator UI state and the newer QA
   hardening work.
5. Verify the proof merge with the required frontend, production-build, and
   .NET checks.
6. Create a separate clean branch from the recorded `origin/main` tip and apply
   the verified proof result as one consolidation commit.
7. Prove that the clean branch and proof branch have identical tracked trees,
   then repeat the required verification on the clean branch.

## Non-goals

- Do not add the compact POS "Recent receipts" panel in this change.
- Do not port or cherry-pick the legacy clients master-detail implementation.
- Do not remove anti-fraud behaviour, `ReviewWorkspace`, or the full cash
  receipts journal.
- Do not delete, reset, clean, stash, or otherwise mutate the existing dirty
  worktrees.
- Do not rewrite or force-push any existing local or remote branch.
- Do not push, merge to `main`, delete branches, or clean worktrees without a
  later explicit user request.
- Do not perform unrelated product, visual, architecture, or documentation
  cleanup while resolving the merge.

## Approach Decision

### Selected: proof merge followed by tree-identical consolidation

First produce and verify a normal merge of the current feature tip with the
latest `origin/main`. This keeps Git's three-way conflict information and gives
reviewers an explicit place to reconcile overlapping changes. Only after that
merge passes verification is its tracked tree copied onto a clean branch rooted
at `origin/main`.

The clean branch is accepted only if `git diff --exit-code` reports no tracked
file difference between it and the verified proof branch. History cleanup
therefore cannot silently change application behaviour.

### Rejected: merge `origin/main` directly into the current branch

This is mechanically safe but retains the full noisy history and mutates the
only branch containing the 33 unpushed commits before a clean result exists.

### Rejected: interactive rebase of the full feature history

Rebasing and squashing more than one hundred iterative commits would repeatedly
replay superseded UI states and conflict resolutions. It has the highest risk
of dropping a later correction or reintroducing an earlier design.

## Branch And Worktree Model

Execution uses two new isolated worktrees:

- `integration/operator-ui-proof` starts at the recorded current feature tip
  and receives a normal merge of the recorded `origin/main` tip.
- `feat/operator-ui-consolidated` starts at the recorded `origin/main` tip and
  receives the verified proof tree as one consolidation commit.

Branch names may receive a numeric suffix if either name already exists. The
recorded source commit IDs, not movable branch names, are authoritative for the
operation.

The original checkout remains on
`feat/operator-clients-center-redesign`. Existing `.claude/worktrees/*`
directories are read-only inputs for this operation and must not be reused for
either new branch.

## Merge Policy

Resolve conflicts according to the final product behaviour rather than commit
age:

1. Preserve the current feature branch's final Clients table/drawer, Stock
   redesign, shared UI-kit, toast channel, navigation order, and theme-aware
   Operator/Setup Wizard icons.
2. Preserve the newer `origin/main` QA fixes for CTA priority, WCAG contrast,
   coherent preview state, and preview-only realtime.
3. When both sides modify the same CSS or fixture file, combine the semantic
   intent from both sides; never select an entire side solely with `--ours` or
   `--theirs`.
4. Keep anti-fraud and review capabilities intact.
5. Do not restore intermediate UI states merely because they appear earlier in
   history.
6. Every manual conflict resolution must be listed in the proof merge commit
   message or an adjacent verification note with the retained behaviour.

## Consolidation Commit

The clean branch is rooted directly at the recorded `origin/main` tip. Apply the
proof branch's net changes relative to that same base as a squash, inspect the
staged diff, and create one coherent consolidation commit. The commit preserves
the final tracked repository state, not the superseded sequence of intermediate
commits.

No generated artifact, ignored file, untracked file, or dirty-worktree-only
change is allowed to enter the consolidation commit.

## Regression Controls

### Source protection

- Record the source and base commit IDs before creating worktrees.
- Confirm the original checkout is clean.
- Confirm the two existing legacy worktrees retain exactly their initial status
  after consolidation.
- Do not delete any recovery branch or worktree during this plan.

### Tree identity

After committing the clean branch:

```text
git diff --exit-code integration/operator-ui-proof feat/operator-ui-consolidated
```

must exit successfully with no output. Also compare `git rev-parse
<branch>^{tree}` for both branches; the tree IDs must match.

### Verification layers

Run the narrowest useful checks during conflict resolution, then run the full
required gate on both the proof and clean branches:

1. Focused Bun tests for every manually resolved Operator Web file.
2. Full Operator Web test suite.
3. Operator Web production build.
4. Relevant Operator App and Setup Wizard builds for WPF asset and host-bridge
   changes.
5. Full solution build and test because the consolidated tree crosses shared
   localization, multiple applications, and native/Web boundaries.
6. Browser-preview smoke for the floor map, Clients, Stock, POS, theme changes,
   preview session state, and preview realtime isolation.

Verification must be fresh. A stale success from an earlier source commit is
not evidence for either new branch.

## Failure Handling

- If the starting checkout is dirty, stop before creating any branch.
- If a required baseline fails before the merge, record the failure and stop
  for user direction; do not attribute it to consolidation.
- If a conflict has ambiguous product behaviour, stop that resolution and
  compare the current feature state, `origin/main`, and the relevant active
  spec before choosing.
- If the proof merge fails verification, keep the proof branch for diagnosis
  and do not create the clean branch.
- If tracked trees differ, do not waive the mismatch. Identify and correct the
  exact file difference, then repeat tree comparison and verification.
- If the clean branch fails a check that passed on the tree-identical proof
  branch, treat the result as an environment or nondeterminism problem and
  investigate before claiming success.

## Documentation And Evidence

The implementation plan records exact commands and expected outcomes. The
final handoff reports:

- recorded source and `origin/main` commit IDs;
- proof and clean branch names and commit IDs;
- manual conflict resolutions;
- tree IDs and tree-diff result;
- verification commands and outcomes;
- original checkout and legacy worktree status before and after;
- commit, push, and merge status.

The compact progress snapshot changes only if the consolidated branch becomes
the durable recommended development state or exposes a new verified gap.

## Acceptance Criteria

- The original feature branch and both legacy dirty worktrees are unchanged.
- The proof branch contains the current final Operator UI behaviour and all
  five recorded `origin/main` QA-hardening commits.
- The clean branch has the recorded `origin/main` tip as its base and one
  consolidation commit containing the proof branch's net tracked changes.
- Proof and clean branch tree IDs are identical and `git diff --exit-code`
  reports no differences.
- Focused tests, full Operator Web tests, production build, relevant native
  builds, full solution build/test, and browser-preview smoke pass on the clean
  branch.
- No POS Recent receipts work, legacy master-detail work, anti-fraud removal,
  dirty-worktree-only edits, or unrelated cleanup enters the clean branch.
- Nothing is pushed, merged, force-updated, or deleted without explicit user
  approval.
