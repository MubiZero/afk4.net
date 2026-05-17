# AFK4 Agent Instructions

## Project Root

The repository root is `D:\afk4.net`.

Use Windows/PowerShell by default. Do not switch to WSL unless the user
explicitly asks for it.

## Required Context For Every New Session

Before changing code, product scope, architecture, or plans, read:

- `README.md`
- `docs/product/AFK4-MVP-PRD.md`
- `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`
- `docs/progress/2026-05-12-vertical-slice-progress.md`
- `docs/roadmap/production-readiness.md`

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
- The MVP has no web admin panel.
- Operator experience is a native Windows desktop app.
- Gaming PCs are Windows 10/11 only in the MVP.
- Operator App is WPF + MVVM.
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
- Full event sourcing, kernel drivers, web admin, Linux/macOS agents, mobile
  app, country-specific fiscal integrations, and local server are out of MVP
  scope.

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

## GitHub Actions And PR Merge Discipline

GitHub Actions billing is enabled for this repository, but paid CI minutes must
be used carefully.

- Do not trigger remote GitHub Actions runs casually. Prefer local build/test
  verification while iterating, and push only coherent changes that are ready
  for remote validation.
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
