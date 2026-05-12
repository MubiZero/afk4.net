# AFK4 Agent Instructions

## Project Root

The repository root is `D:\afk4.net`.

Use Windows/PowerShell by default. Do not switch to WSL unless the user explicitly asks for it.

## Required Context For Every New Session

Before changing code or plans, read:

- `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`
- `docs/superpowers/plans/2026-05-12-afk4-platform-vertical-slice.md`

These files are the source of truth for product scope, architecture, and the current implementation sequence.

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
- POS, inventory, shifts, receipts, audit, and centralized updates are part of the MVP.
- Full event sourcing, kernel drivers, web admin, Linux/macOS agents, and local server are out of MVP scope.

If a user asks to change one of these decisions, update the architecture spec first, then update or create an implementation plan.

## Current Delivery State

Committed docs:

- `12dfb59 docs: add platform architecture spec`
- `d8ab10e docs: add vertical slice implementation plan`
- `f100fbe docs: add mvp delivery roadmap`

Next implementation target:

1. Execute `docs/superpowers/plans/2026-05-12-afk4-platform-vertical-slice.md`.
2. Build the first vertical slice: repository baseline, .NET solution, shared contracts, backend health/floor-map endpoints, SignalR heartbeat, Agent skeleton, Operator App shell, and Player Shell skeleton.
3. Do not jump into billing, POS, updates, identity, or Windows enforcement before the vertical slice is green unless the user explicitly reprioritizes.

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

The current PowerShell process may not always have `dotnet` in `PATH`. Use the full path above if `dotnet` is not recognized.

## Engineering Workflow

- Prefer small, focused changes.
- Follow the existing spec and plan before inventing new structure.
- Use TDD for domain rules, contracts, endpoint behavior, Agent behavior, and ViewModels.
- Keep backend module boundaries explicit.
- Do not let modules directly mutate another module's data.
- Keep shared DTOs in a contracts project.
- Keep domain models separate from transport DTOs.
- Require idempotency for critical money, session, POS, and device commands.
- Verify with fresh commands before claiming build, tests, or completion.
- Commit coherent units of work with clear messages.

## Windows App Guidance

Operator App should feel like dense operator software, not a marketing dashboard.

- Main screen is the floor map.
- Support fast cashier/operator workflows.
- Favor MVVM and testable ViewModels.
- Keep local cache as UI acceleration only, never as financial or session authority.
- Critical actions must wait for backend confirmation.

Player Shell is a controlled player-facing UI and must not be trusted for authorization, billing, or session decisions.

## Documentation Rules

When a major decision changes:

1. Update the architecture spec.
2. Update the delivery plan set if sequencing changes.
3. Create or update the focused implementation plan.
4. Self-review for incomplete markers, contradictions, and missing coverage.
5. Commit the docs before implementing code.
