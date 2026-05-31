# AFK4 Agent Instructions

## Repository

- Root: `D:\afk4.net`.
- Use Windows and PowerShell by default. Do not switch to WSL unless the user
  asks for it.
- Prefer explicit local tools when PATH is unreliable:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' --version
& 'C:\Program Files\dotnet\dotnet.exe' --list-sdks
```

Expected SDK: .NET `10.0.203` or a compatible .NET 10 SDK allowed by
`global.json`.

## Source Of Truth

Load only the context required for the task.

- For repo changes, read `README.md` and
  `docs/progress/2026-05-12-vertical-slice-progress.md`.
- For product scope, user journeys, MVP boundaries, architecture, or module
  boundaries, also read `docs/product/AFK4-MVP-PRD.md` and
  `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`.
- For deployment, CI, packaging, backup/restore, release gates, or production
  readiness, also read `docs/roadmap/production-readiness.md` and the directly
  relevant operations runbook.
- Active implementation plans live in `docs/superpowers/plans/`. Files under
  `docs/archive/` are historical evidence only and are not default context.

Do not rely on chat history when the source files answer the question.

## Fixed Product Decisions

The durable product decisions are maintained in the PRD and architecture spec.
Do not reopen them casually. In short:

- AFK4 is a cloud-first SaaS for computer clubs.
- MVP has no local club server.
- Day-to-day club operations use the native Windows Operator App.
- The platform owner/support surface is an internal browser SaaS Control Plane.
- Gaming PCs are Windows 10/11 in the MVP.
- Backend is a .NET 10 ASP.NET Core modular monolith with PostgreSQL.
- Agent runtime is Windows Agent Service plus Player Shell UI.
- Realtime uses SignalR/WebSockets.
- Offline behavior is grace mode for already active sessions only.
- Billing uses immutable ledger entries.

If one of these changes, update the PRD and architecture spec first, then the
roadmap/progress/plan that depends on it.

## Working Rules

- Inspect `git status` before editing.
- Never revert or overwrite user changes unless explicitly asked.
- Keep changes small and aligned with existing repo patterns.
- Search with your harness's dedicated search tools (e.g. Grep/Glob); `rg` is fine from a shell.
- Edit files with your harness's file-editing tools (e.g. Edit/Write).
- Keep backend module boundaries explicit; modules must not directly mutate
  another module's data.
- Keep shared transport DTOs in `AFK4.Shared.Contracts`.
- Keep domain models separate from transport DTOs.
- Require idempotency for money, session, POS, device, and update commands.
- Use TDD for domain rules, contracts, endpoint behavior, Agent behavior, and
  ViewModels when changing those areas.

## Git

- Use a dedicated topic branch (e.g. `sp3-plan4-billing-ui`) for non-trivial work
  unless the user asks to work directly on the current branch.
- Do not auto-push, auto-merge, or delete task branches unless the user asks or
  the current task explicitly requires it.
- Do not merge a PR until the latest PR head has a green required verification
  check; do not rely on stale, skipped, or older workflow runs.
- Commit coherent units with clear messages after implementation and
  verification.
- Before committing, inspect staged status and staged diff summary.
- Before pushing, run verification appropriate to the blast radius.

## Verification

Use the narrowest fresh verification that proves the change.

- Docs-only changes: `git diff --check`.
- Backend endpoint/service changes: focused affected tests.
- Shared contracts, migrations, auth/tenant/device/session/money boundaries, or
  cross-module changes: affected tests plus the relevant build target.
- Frontend behavior changes: focused frontend tests; production build if
  bundling, routing, environment config, or shared UI wiring changed.
- Packaging, installer, deployment, CI, backup/restore, or release-gate changes:
  run the affected script/workflow/runbook smoke path.
- Full solution build/test is required before push/merge/release validation or
  when shared infrastructure, migrations, packaging, or cross-application
  contracts changed.

## Progress And Docs Hygiene

- Keep `docs/progress/2026-05-12-vertical-slice-progress.md` compact: current
  state, latest verification, known gaps, and next work.
- Put detailed smoke logs, long historical entries, and obsolete plans under
  `docs/archive/`.
- Update progress/roadmap only when durable implementation state, verification,
  release gates, known gaps, or recommended next work changed.
- Do not leave important project state only in chat, branch names, commit
  messages, or PR descriptions.

## Windows App Guidance

- Operator App should feel like dense operator software, not a marketing
  dashboard.
- Main Operator App screen is the floor map.
- Local cache is UI acceleration only, never authority for financial/session
  state.
- Critical operator actions must wait for backend confirmation.
- Player Shell is player-facing UI and is not trusted for authorization,
  billing, or session decisions.

## Definition Of Done

Before ending implementation work:

- requested behavior is implemented, or the blocker is clearly stated;
- final diff was self-reviewed for unintended scope, stale docs, missing tests,
  and obvious product/security regressions;
- focused verification passed, or the reason it could not run is explicit;
- progress/roadmap update needs were checked;
- git status, branch/ahead state, and commit/push status are reported;
- unrelated user changes were not reverted or mixed into the work.
