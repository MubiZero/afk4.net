# AFK4 Agent Instructions

## Project Root

The repository root is `D:\projects\afk4.net`.

Use Windows/PowerShell by default. Do not switch to WSL unless the user
explicitly asks for it.

## Required Context For Every New Session

Use tiered context loading so routine work stays fast while product and
architecture decisions still use the right source of truth.

- For any repo task that changes files or durable delivery state, read
  `README.md` and `docs/progress/2026-05-12-vertical-slice-progress.md`.
- Before changing product scope, user journeys, MVP boundaries, architecture,
  module boundaries, or implementation plans, also read
  `docs/product/AFK4-MVP-PRD.md` and
  `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`.
- Before changing release gates, deployment, staging/production sequencing, CI,
  packaging, backup/restore, or operational readiness, also read
  `docs/roadmap/production-readiness.md` and the directly relevant operations
  docs.
- For narrow docs/tooling edits inside already-approved workflow, read the
  directly relevant files and use the full source-of-truth set only if the
  change touches product, architecture, release, or delivery state.

Source-of-truth order:

1. `docs/product/AFK4-MVP-PRD.md` explains what product is being built and why.
2. `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`
   explains architecture, module boundaries, and fixed technical decisions.
3. `docs/progress/2026-05-12-vertical-slice-progress.md` explains the current
   implemented state, known deviations, latest verification, and next work.
4. `docs/roadmap/production-readiness.md` explains what still blocks pilot and
   commercial production.
5. `README.md` is the concise entry point and navigation document.

Do not rely on chat history for product or architecture decisions when these
files answer the question.

Historical phase plans under `docs/superpowers/plans/` and archived progress
logs under `docs/archive/` are reference material. Read them only when the
current task touches that historical slice or the current progress file points
there.

## Product Decisions That Must Not Be Reopened Casually

- AFK4 is a cloud-first SaaS platform for computer clubs.
- The MVP has no local club server.
- The MVP includes an internal browser-based SaaS Control Plane for platform-owner
  tenant onboarding, subscription/status controls, tenant health, and support.
  Customer day-to-day club operations remain in the native Operator App.
- Operator experience is a native Windows desktop app.
- Gaming PCs are Windows 10/11 only in the MVP.
- Operator App is a native .NET Windows desktop shell with WebView2 and a
  React/TypeScript operator UI.
- Backend is ASP.NET Core on .NET 10.
- Backend starts as a modular monolith, not microservices.
- Database target is PostgreSQL.
- Agent runtime is split into Windows Agent Service and Player Shell UI.
- Realtime uses SignalR over WebSockets.
- Offline behavior is grace mode for already active sessions only.
- Billing uses an immutable ledger, not a mutable balance field.
- MVP is multi-tenant from the start.
- POS, inventory, shifts, receipts, audit, reports, and centralized updates are
  part of the MVP.
- Full event sourcing, kernel drivers, customer browser operational admin as
  the primary club UI, Linux/macOS agents, mobile app, country-specific fiscal
  integrations, and local server are out of MVP scope.

If a user asks to change one of these decisions, update the PRD and architecture
spec first, then update or create an implementation plan.

## Progress Tracking

Do not store volatile delivery status in this file.

Use:

- `docs/progress/2026-05-12-vertical-slice-progress.md` for current state,
  verification, known deviations, and recommended next work.
- `docs/roadmap/production-readiness.md` for launch blockers and release gates.
- `docs/archive/` for historical evidence that should not be required context.

Keep progress files short enough to be useful in future agent sessions. Archive
large historical logs instead of making them mandatory reading.

Progress snapshot hygiene:

- Keep new progress entries compact: current state, latest verification, known
  gaps, and next work. Avoid long command transcripts.
- Move detailed smoke logs, historical evidence, one-off incident narratives,
  and bulky output to `docs/archive/` when they are useful but not required
  context for every new session.
- Prefer updating an existing current-state bullet over appending another long
  chronological note when the old detail no longer changes what the next agent
  should do.

Before ending any session that changed code, workflows, deployment/operations
docs, implementation plans, product scope, architecture, CI/release behavior, or
merge state, explicitly check whether the current progress snapshot or
production roadmap must be updated. If the implemented state, latest
verification, known gaps, recommended next work, release gates, or recent
integration notes changed, update the relevant progress documents in the same
branch before the final response and before merge.

Do not leave important project state only in chat, PR descriptions, branch
names, commit messages, or GitHub check output. Persist durable status in:

- `docs/progress/2026-05-12-vertical-slice-progress.md` for what is now true,
  what was verified, what remains broken or missing, and what should happen
  next.
- `docs/roadmap/production-readiness.md` for launch blockers, release gates,
  staging/production sequencing, and operational readiness changes.

When a PR is merged, record notable merged work in the progress snapshot if it
changes current capabilities, verification evidence, gaps, or next work. Include
PR numbers, merge commits, important remote CI results, and any follow-up branch
names when they will matter to the next agent.

If no progress document update is needed after a task, say so briefly in the
final response and explain why.

## Local Tooling

Git is installed here:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' --version
```

.NET SDK is installed here:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' --list-sdks
```

Expected SDK:

```text
10.0.203 [C:\Program Files\dotnet\sdk]
```

The current PowerShell process may not always have `dotnet` in `PATH`. Use the
full path above if `dotnet` is not recognized.

## Engineering Workflow

- Prefer small, focused changes.
- Follow the PRD, architecture spec, current progress, and focused plan before
  inventing new structure.
- Use TDD for domain rules, contracts, endpoint behavior, Agent behavior, and
  ViewModels.
- Keep backend module boundaries explicit.
- Do not let modules directly mutate another module's data.
- Keep shared DTOs in `AFK4.Shared.Contracts`.
- Keep domain models separate from transport DTOs.
- Require idempotency for critical money, session, POS, and device commands.
- Verify with fresh commands before claiming build, tests, or completion.
- Commit coherent units of work with clear messages.
- Do not revert user changes unless explicitly asked.
- When the user asks to review, check, or assess existing work, default to
  findings-only review. Do not fix, stage, commit, or push until the user asks
  for implementation.
- If a task expands or remains unclear after 60-90 minutes, stop and present the
  fastest pragmatic option and the slower more complete option with tradeoffs.

## Verification Policy

Use the narrowest fresh verification that proves the changed behavior, then
scale up only when the blast radius requires it.

- Docs-only changes: run `git diff --check`; no build/test unless the docs
  include generated examples or scripts that changed.
- Backend endpoint/service changes: run focused tests for the affected endpoint,
  service, and policy path.
- Shared contracts, migrations, auth/tenant/device/session/money boundaries, or
  cross-module changes: run affected project tests plus the relevant contract
  tests and build target.
- Frontend behavior changes: run focused frontend tests; run production build
  when bundling, routing, environment config, or shared UI wiring changed.
- Packaging, installer, deployment, CI, backup/restore, or release-gate changes:
  run the affected build/package/runbook smoke path before claiming readiness.
- Full solution build/test is required before push/merge/release validation, or
  when shared infrastructure, migrations, packaging, or cross-application
  contracts changed. Do not use it as the default inner-loop check.

## Definition Of Done

Before ending implementation work, make sure:

- the requested behavior is implemented or the blocker is clearly stated;
- focused verification passed, or the reason it could not run is explicit;
- progress/roadmap/doc update needs were checked;
- git status, branch/ahead state, and commit/push status are reported;
- unrelated user changes were not reverted or mixed into the work.

## Fast Slice Workflow

Use this mode by default for vertical-slice implementation, review, and polish
work unless the user explicitly asks for exhaustive review or full release
validation.

- Start with a short touch-map, not a long investigation: identify the focused
  plan/progress section, affected endpoints/components/services, existing tests,
  and the smallest acceptance checklist that proves the slice works.
- Keep review and implementation separate. When the user asks whether a pushed
  slice is good, default to a 30-45 minute blocker review with findings first;
  do not start fixes until the user asks to implement them.
- During blocker review, prioritize only issues that can break product promises:
  tenant isolation/status, auth/permissions, money/session/device authority,
  idempotency, auditability, public endpoint abuse, state-machine correctness,
  migrations/defaults, contract compatibility, realtime leakage, and compile or
  test failures.
- Treat naming, cosmetics, broader refactors, and nice-to-have test expansion as
  follow-up unless they are needed to make the slice safe or coherent.
- Once the user says to proceed, implement the smallest coherent fix set for the
  selected blockers. Do not broaden the scope unless a new blocker is discovered
  while making those fixes.
- Use verification tiers:
  - **Focused** while iterating: only the directly affected tests/build target.
  - **Affected** before commit: affected project test suites and any shared
    contract tests touched by the change.
  - **Full** only before push/merge/release validation, or when shared
    infrastructure, migrations, packaging, or cross-application contracts changed.
- Prefer one working vertical path before polishing. Make the primary scenario
  pass, run focused verification, then do one blocker-review pass.
- If a decision point is still unclear after 60-90 minutes, stop and present two
  options: the fastest pragmatic fix and the slower more complete fix, with the
  tradeoff.
- Keep progress updates short. Update the progress snapshot only when current
  implementation state, latest verification, known gaps, next work, release
  gates, or integration notes changed; do not expand long historical logs.
- Do not push or trigger remote CI just to get feedback. Push only coherent
  changes that are ready for remote validation or when the user asks.

## Git Branch And Commit Discipline

- Prefer a `codex/<topic>` branch for non-trivial agent implementation work
  unless the user explicitly asks to work on the current branch or the task is a
  small local docs/workflow update.
- Do not switch branches with a dirty worktree until the dirty files are
  inspected and either committed, stashed with user approval, or confirmed
  unrelated and safe.
- Keep review-only work uncommitted. Commit only after the user asks for fixes
  or after an implementation task is complete.
- Before committing broad changes, inspect staged status and a staged diff
  summary so unrelated files are not accidentally included.
- Report whether changes were committed, pushed, or left local. Do not push
  unless the user asks or the workflow explicitly requires remote validation.

## GitHub Actions And PR Merge Discipline

GitHub Actions billing is enabled for this repository, but paid CI minutes must
be used carefully.

- Do not trigger remote GitHub Actions runs casually. Prefer local build/test
  verification while iterating, and push only coherent changes that are ready
  for remote validation.
- Push only after local affected verification has passed, unless the task is to
  reproduce or debug a remote-only CI/deploy failure.
- Do not retry failed GitHub Actions by pushing speculative commits. Inspect the
  failing check/log first, then make a targeted fix.
- Treat `PR Verification Result` as the required PR gate for normal merges.
- Do not merge a PR until the latest commit on the PR branch has a green remote
  `PR Verification Result` check.
- If GitHub branch protection or rulesets are unavailable for the current
  private repository plan, enforce the same rule manually: inspect the PR checks
  and wait for green CI before merge.
- Do not rely only on skipped, cancelled, stale, or earlier workflow runs. The
  green check must belong to the current PR head commit.
- Keep Windows hosted runner use cost-aware: rely on conditional PR workflow
  paths, stale-run cancellation, timeouts, and short artifact retention.

## Windows App Guidance

Operator App should feel like dense operator software, not a marketing
dashboard.

- Main screen is the floor map.
- Support fast cashier/operator workflows.
- Favor MVVM and testable ViewModels.
- Keep local cache as UI acceleration only, never as financial or session
  authority.
- Critical actions must wait for backend confirmation.

Player Shell is a controlled player-facing UI and must not be trusted for
authorization, billing, or session decisions.

## Documentation Rules

When a major product or architecture decision changes:

1. Update `docs/product/AFK4-MVP-PRD.md` if product scope, user goals, journeys,
   or MVP boundaries change.
2. Update the architecture spec if technical architecture, module boundaries, or
   platform decisions change.
3. Update `docs/roadmap/production-readiness.md` if launch sequencing or
   production gates change.
4. Update `docs/progress/2026-05-12-vertical-slice-progress.md` if current
   implementation state, latest verification, or recommended next work changes.
5. Create or update a focused implementation plan only when the work needs one.
6. Self-review for incomplete markers, contradictions, stale links, and missing
   coverage.
7. Commit the docs before implementing dependent code.

When a change only affects implementation detail inside approved scope, update
the focused plan or code without rewriting the PRD.
