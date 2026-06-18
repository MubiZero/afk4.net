---
name: afk4-api-client-decomposition
description: "Decision to keep AFK4 a monorepo (not polyrepo) + the domain-sub-client pattern for fat API clients. Decomposition campaign CLOSED — all 3 refactors merged & pushed to main."
metadata:
  node_type: memory
  type: project
  originSessionId: 2bc799dd-22b4-4c03-b6e4-1ce7a2de74c8
---

2026-06-10: user weighed splitting the AFK4.NET monorepo into polyrepos "by best practices" — decided **AGAINST**. It's a cohesive product, small team, heavy `AFK4.Shared.Contracts` axis (284 files everyone depends on); polyrepo would mean NuGet versioning hell + cross-repo PRs, and .NET tooling assumes a monorepo. Cleanup happens INSIDE the monorepo.

**Domain-sub-client pattern** (used for every fat API client): split a god-client into a shared transport (auth/refresh/send) + per-domain client files, and expose them through a thin facade as `client.<domain>.<method>` (e.g. `client.venue.listDevices`). Consumers depend on their domain client via `Pick<DomainApi,...>` (multi-domain consumers take a bundle). Keep the existing convention (factory functions vs classes) — don't convert style.

**Campaign CLOSED — all 3 refactors merged & pushed to main:**
- `Platform.Web` `ClubApiClient` (571-line monolith) → `apiTransport.ts` + 12 domain clients + thin `clubApi.ts` facade; importers 51→6.
- `Platform.Web` `PlatformApiClient` (383 lines) → `platformTransport.ts` + 6 domain clients; importers 24→6. (Both merged via `--no-ff` 5599905.)
- `Operator.App.Web` `operatorApiClients.ts` (1099-line FILE, already factory-factored) → `types.ts` + `queryHelpers.ts` + 20 domain files in `src/api/clients/` + aggregator; `operatorApiClients.ts` left as a 25-line `export *` BARREL so all 31 consumers + the 502-line test import unchanged. Committed `86232ac`. `settings.ts` left intentionally fat (splitting breaks `clients.settings.*` public API).

**Lessons / boundaries:**
- The codebase already had good interface-segregation via `Pick<...>`; graph "god-node" coupling was partly an artifact of type-import edges, not runtime coupling — verify Pick-discipline before assuming a node is truly coupled.
- Rebuilt graph confirmed the ex-god-clients dropped out of the top nodes. Remaining top nodes are NOT targets: `mockPlatformFetch` (test artifact), shared core/util (`ApiTransport`, `readString`), backend `EfPlatformTenantService`, and WPF Operator ViewModels (`ShiftWorkspaceViewModel` 43, `PosWorkspaceViewModel` 41, `SeatContextPanelViewModel` 40). User: **"43 ребра не так уж и много"** — screen-orchestrators at ~40 edges are normal; no more pilling for numbers' sake (YAGNI). WPF ViewModels are explicitly OFF-LIMITS.

graphify graph lives in repo `graphify-out/` (last rebuilt 2026-06-10 over `src`: ~10516 nodes, 17942 edges, 1078 communities) — rebuild before any future trace. Next deferred project chunk: [[afk4-time-handling-audit]]. Payments epic: [[afk4-multitenant-payments-state]].
