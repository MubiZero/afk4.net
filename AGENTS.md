# AFK4 Agent Instructions

## Project Root

The repository root is `D:\afk4.net`.

Use Windows/PowerShell by default. Do not switch to WSL unless the user explicitly asks for it.

## Required Context For Every New Session

Before changing code or plans, read:

- `README.md`
- `docs/product/AFK4-MVP-PRD.md`
- `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`
- `docs/superpowers/plans/2026-05-12-afk4-platform-vertical-slice.md`

Source-of-truth order:

1. `docs/product/AFK4-MVP-PRD.md` explains what product is being built and why.
2. `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md` explains how the platform is architected.
3. `docs/superpowers/plans/2026-05-12-afk4-platform-vertical-slice.md` explains the first implemented technical slice.
4. `README.md` is the concise entry point and navigation document.

Do not rely on chat history for product decisions when these files answer the question.

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

The first vertical slice foundation has been implemented on branch `feature/vertical-slice`.

Implemented foundation:

- repository baseline with `global.json`, `Directory.Build.props`, `.editorconfig`, `.gitignore`, and expanded `README.md`;
- `AFK4.sln` with backend, shared contracts, building blocks, Agent Service, Operator App, Player Shell, and tests;
- strongly typed Guid ID primitives in `AFK4.BuildingBlocks`;
- shared DTO contracts for device heartbeat and floor map;
- Platform API endpoints:
  - `GET /api/health`
  - `GET /api/branches/{branchId}/floor-map`
  - `POST /api/devices/{deviceId}/heartbeat`
- SignalR hub at `/hubs/devices` and server broadcast event `deviceStatusChanged`;
- Agent Service options, heartbeat payload factory, and HTTP heartbeat worker loop;
- WPF Operator App shell with static floor map ViewModel;
- WPF Player Shell fullscreen locked-state skeleton;
- Master MVP PRD at `docs/product/AFK4-MVP-PRD.md`.

Recent key commits on `feature/vertical-slice`:

- `12dfb59 docs: add platform architecture spec`
- `d8ab10e docs: add vertical slice implementation plan`
- `f100fbe docs: add mvp delivery roadmap`
- `57b2763 docs: add mvp product requirements`

Known implementation deviations or adaptations:

- The current Agent Service sends heartbeat through an HTTP POST loop to `/api/devices/{deviceId}/heartbeat`.
- The backend then broadcasts device status through SignalR.
- The full architecture still requires an outgoing Agent SignalR/WebSocket connection for realtime command and state flow. That is not implemented yet and should be a focused follow-up plan.
- `dotnet new sln` on this .NET 10 SDK defaulted to `.slnx`, so the solution was created with `--format sln`.
- `Microsoft.Extensions.Http` was added to `AFK4.Agent.Service` because `AddHttpClient`/`IHttpClientFactory` are required by the planned Worker.

Latest verified state:

- `dotnet build AFK4.sln --no-restore` succeeds with 0 warnings and 0 errors.
- `dotnet test AFK4.sln --no-restore` succeeds with 14 passing tests.

Recommended next implementation target:

1. Decide whether to merge `feature/vertical-slice` back into `main`.
2. Create a focused plan for Agent SignalR/WebSocket connection, command channel, and Operator App realtime subscription.
3. Do not jump into billing, POS, updates, identity, or Windows enforcement before the realtime foundation decision is resolved unless the user explicitly reprioritizes.

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

The project currently targets .NET SDK `10.0.203`.

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

1. Update `docs/product/AFK4-MVP-PRD.md` if product scope, user goals, journeys, or MVP boundaries change.
2. Update the architecture spec if technical architecture, module boundaries, or platform decisions change.
3. Update the delivery plan set if sequencing changes.
4. Create or update the focused implementation plan.
5. Self-review for incomplete markers, contradictions, and missing coverage.
6. Commit the docs before implementing code.

When a change only affects implementation detail inside an approved scope, update the focused plan or code without rewriting the PRD.
