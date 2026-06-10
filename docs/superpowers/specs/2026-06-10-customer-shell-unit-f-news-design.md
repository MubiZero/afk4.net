# Unit F — News / Banners Design

**Date:** 2026-06-10
**Status:** Approved (design), pending implementation plan
**Epic:** Customer-shell WebView2 pivot — Unit F (engagement/commerce layer). Unit F = three independent subsystems (shop, loyalty, news). Shop shipped (cycle 1), loyalty shipped (cycle 2). This spec covers **News/banners only** — the last Unit F cycle.

## Goal

Let the network owner publish news/announcements (optionally with a banner image) that a seated player reads in the WebView2 React shell. News can target the whole network or a single branch, and can be time-boxed with an optional show window. The server is authoritative for content and visibility; the shell and the owner UI are non-authoritative faces. No money is involved.

## Decisions (locked during brainstorming)

1. **Level & authorship:** **organization-wide, authored by the network owner only** (`/api/owner/*`, like loyalty). Each news item has an optional `BranchId`: null = visible to all branches, set = visible only to that branch. One authoring surface covers both network-wide promos and branch-local notices. Branch managers do not author news this cycle.
2. **Content:** title + body text + an **optional image URL** (external address, like `OrganizationEntity.LogoUrl` — no file upload, no self-hosted storage). No clickable links / CTA (kiosk-escape risk + YAGNI).
3. **Lifecycle:** an `IsPublished` flag plus an **optional show window** (`PublishAtUtc` / `ExpiresAtUtc`). Visibility is computed **at query time** (published AND now within window) — no background job / cron. Covers "show now", "show until Friday", "show from Monday".
4. **Player surface:** a **"Новости" entry in `SelfServiceMenu`** opening a `NewsScreen` list of cards (image + title + body + date), newest first. No read/unread tracking, no home-screen carousel.

## Architecture & Boundary

Thin CRUD layer over established patterns; nothing changes in enforcement (lock/kiosk/lease stay in `Agent.Service`). Structurally this is "shop minus money/ledger/SignalR/status-transitions": a dedicated collection entity with owner CRUD and one player read query. Three sides:

- **Server (`AFK4.Platform.Api`)** — source of truth: the `news_items` collection, validation, visibility filtering. Owner CRUD under `/api/owner/news`; player read under `/api/me/news`. A thin `EfNewsService` holds validation + queries; endpoints stay thin.
- **Player shell (`AFK4.Player.Shell.Web`)** — `NewsScreen`: a read-only list of currently-visible news. Offline-cached via `idbCache`.
- **Owner web (`AFK4.Operator.App.Web`)** — a `NewsWorkspace`: list + create/edit/delete form (title, body, image URL, branch target, published toggle, optional window), alongside the existing owner-scoped surfaces (loyalty, payment gateways, owner code).

## Data Model

Follows project conventions: sealed class, `Guid` key, `DateTimeOffset *Utc`, snake_case table configured in `PlatformDbContext.OnModelCreating`, migration `YYYYMMDDHHMMSS_AddNewsItems`.

**`NewsItemEntity`** — a collection (many rows per org), table `news_items`:
- `Id: Guid` — PK
- `OrganizationId: Guid` — owning org (indexed; FK to org)
- `BranchId: Guid?` — null = all branches; set = only that branch
- `Title: string` — required, ≤ 200 chars
- `Body: string` — required, ≤ 4000 chars
- `ImageUrl: string?` — optional, ≤ 2048 chars, must be `http`/`https`
- `IsPublished: bool`
- `PublishAtUtc: DateTimeOffset?` — optional window start
- `ExpiresAtUtc: DateTimeOffset?` — optional window end
- `CreatedAtUtc: DateTimeOffset`
- `UpdatedAtUtc: DateTimeOffset`

**Player visibility query:** `OrganizationId == player.OrganizationId` AND `IsPublished` AND (`BranchId == null` OR `BranchId == player.HomeBranchId`) AND (`PublishAtUtc == null` OR `PublishAtUtc <= now`) AND (`ExpiresAtUtc == null` OR `ExpiresAtUtc > now`); ordered by `PublishAtUtc ?? CreatedAtUtc` descending; `Take(50)`. The player's `HomeBranchId` is loaded from `PlayerAccountEntity` (the player token carries only `OrganizationId`, same as the shop flow). A player with no `HomeBranchId` sees only org-wide (`BranchId == null`) news.

## Contracts (`AFK4.Shared.Contracts/News/`)

- `NewsItemDto` — owner-facing, full: `Id`, `BranchId?`, `Title`, `Body`, `ImageUrl?`, `IsPublished`, `PublishAtUtc?`, `ExpiresAtUtc?`, `CreatedAtUtc`, `UpdatedAtUtc`.
- `CreateNewsItemRequest` — `BranchId?`, `Title`, `Body`, `ImageUrl?`, `IsPublished`, `PublishAtUtc?`, `ExpiresAtUtc?`.
- `UpdateNewsItemRequest` — same fields as create (full replace).
- `PlayerNewsItemDto` — player-facing minimal: `Id`, `Title`, `Body`, `ImageUrl?`, `PublishedAtUtc` (the effective sort date, `PublishAtUtc ?? CreatedAtUtc`).
- `Identity/StaffPermissionNames.ManageNews = "news.manage"` — granted to **Owner only** in `PermissionCatalog`.
- Audit action names (in `AFK4.Platform.Api.Audit`): `CreateNews`, `UpdateNews`, `DeleteNews`.

## Endpoints

**Owner** — all require `RequireOrganizationPermission(StaffPermissionNames.ManageNews)`, `orgId` from `authorization.StaffContext!.OrganizationId`, audit on writes via `IAuditRecordWriter`:

- `GET /api/owner/news` — list all news for the org (published and not), newest first.
- `POST /api/owner/news` — create. Audit `CreateNews`.
- `PATCH /api/owner/news/{id}` — full-replace update. Audit `UpdateNews`. (`PlatformApiClient` has `patch`/`delete` but no `put`; owner mutations therefore use POST/PATCH, matching the loyalty cycle.)
- `DELETE /api/owner/news/{id}` — hard delete (no soft-delete/archive — YAGNI). Audit `DeleteNews`.

**Player:**

- `GET /api/me/news` — `.RequireRateLimiting("player-me")`, `IPlayerContextAccessor`-guarded, returns `PlayerNewsItemDto[]` per the visibility query above.

## Validation & Edge Cases

- Empty/whitespace `Title` or `Body` → `400`.
- `Title` > 200 or `Body` > 4000 or `ImageUrl` > 2048 chars → `400`.
- `ImageUrl` set but not `http`/`https` → `400`.
- Both window dates set and `PublishAtUtc >= ExpiresAtUtc` → `400`.
- `BranchId` set but not a branch of the caller's org → `400`.
- `PATCH`/`DELETE` on an id not in the caller's org → `404` (org-scoped lookup).
- Owner endpoints without `ManageNews` permission → `401/403`.
- Player with no `HomeBranchId` → sees only org-wide news (no error).
- Broken/unreachable `ImageUrl` in the shell → image hidden via `onError`, card still renders text.
- Offline shell → news served from `idbCache`, marked last-known.
- Server-authoritative throughout: the shell never decides visibility locally.

## Player Shell (`AFK4.Player.Shell.Web`)

- `apiTypes.ts`: add `PlayerNewsItemDto` (camelCase mirror).
- `shellApi.ts`: add `getNews: () => call<PlayerNewsItemDto[]>('/api/me/news')`.
- `screens/NewsScreen.tsx`: props `{ api, onDone }`; loads on mount; renders a list of cards (optional image with `onError` fallback, title, body, formatted date); empty state "Новостей пока нет"; offline path via `idbCache`. Raw Russian strings (the shell has no i18n hook).
- `screens/SelfServiceMenu.tsx`: `view` union += `'news'`; a "Новости" button **not gated on an active session** (like loyalty); renders `<NewsScreen api={api} onDone={() => setView('menu')} />`.

## Owner Web (`AFK4.Operator.App.Web`)

- `operatorApiClients.ts`: `NewsItemDto` interface + `createNewsClient(api)` with `list()`, `create(req)`, `update(id, req)` (`api.patch`), `remove(id)` (`api.delete`); registered in `createOperatorApiClients` as `news`.
- `NewsWorkspace.tsx`: list of news + create/edit form (title, body, image URL, branch target dropdown ["Все филиалы" / a specific branch], published toggle, optional start/end datetime inputs) + delete. Client memoized via the `backend` prop (loyalty re-render trap); `useI18n()`. The branch dropdown reuses the existing org branch list available to the operator app.
- Nav wiring: `operatorTypes.ts` (`WorkspaceId` += `'news'`), `operatorData.ts` (navItem `{ labelKey: 'op.news.nav', icon: Newspaper }`), `operatorPermissions.ts` (`workspaceIds` entry + `workspacePermissionRules.news = [permissionNames.manageNews]` + `permissionNames.manageNews = 'news.manage'`), `App.tsx` (render block + side-panel exclusion), `SummarySidePanel.tsx` if needed.
- Permission: `PermissionCatalog` grants `ManageNews` to Owner.
- i18n: `op.news.*` keys in `locales/{ru,en,tg}.json`, then `bun run gen` → `packages/i18n/src/messages.ts`.

## Testing Strategy

- **Server (xUnit):** visibility filter (published-only; window start/end boundaries; branch-targeted vs org-wide; player with/without `HomeBranchId`); create/update/delete happy paths write audit; validation rejects (empty title/body, over-length, bad image scheme, inverted window, foreign branch); owner endpoints require `ManageNews` (non-owner → 403); org scoping (cannot patch/delete another org's item → 404); player endpoint ordering + `Take(50)`.
- **Player shell (`bun test` + happy-dom):** renders news cards; empty state; image `onError` fallback; offline path from cache.
- **Owner web (`bun test` + happy-dom):** renders list; create/edit/delete; branch-target dropdown; validation (empty fields, inverted window).

## Out of Scope (this cycle)

- Read/unread tracking and unread badges.
- Home-screen banner carousel (list screen only).
- Clickable links / CTA buttons.
- Self-hosted image upload (external URL only).
- Per-branch authorship (owner authors only).
- Drafts / scheduled-publish workflow (the show window already gives a deferred start).
- Localization of news content per language (single-language free text authored by the owner).
