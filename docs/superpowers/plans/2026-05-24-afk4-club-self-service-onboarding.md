# AFK4 Club Self-Service Onboarding Plan

Status: Draft for implementation
Date: 2026-05-24
Owner: AFK4 platform

## Goal

Replace today's curl-driven club onboarding with a fully UI-driven flow where:

- A platform admin (Mubi) creates a tenant in the Mubi admin SPA and sends one
  invite link to the future club owner.
- The owner opens that link in a browser, sets a password, and lands in their
  own customer dashboard.
- The owner generates a short numeric "owner code" in their dashboard.
- On each Gaming PC the operator runs **one** unsigned MSI, types that owner
  code into a Windows wizard, picks the target branch and seat visually, and
  the device is enrolled, configured, and live.
- Throughout this flow, the manager's dashboard shows devices coming online in
  real time and lets them rename, reassign, or remove devices.

After this plan is implemented, the first real club can be onboarded by Mubi
in minutes from any laptop, with no `curl`, no `psql`, and no manual seat-by-
seat code copying. The `bootstrap.ps1` legacy installer is removed.

## Scope

Four deliverables, in this order. Each lands as its own slice with backend +
SPA + Windows-client changes + docs together. Numbering reflects build order
and dependencies.

### Slice 1 — Backend foundation

1. **Owner code**: short numeric code per branch owner, rotatable, audit-
   logged, usable for installer authentication only (not for SPA sign-in).
2. **Floor map**: per-branch zones + seats data model, CRUD API, plus a
   default "default zone" auto-created with each new branch.
3. **Installer discovery API**: `POST /api/install/discover` taking an owner
   code, returning the owner's branches, each branch's floor map, and the
   subset of seats currently unassigned to any device.
4. **Installer enrollment API**: `POST /api/install/enroll` taking an owner
   code + branch id + seat id + role + a self-supplied PC display name,
   returning device identity + signed bootstrap config for the Agent.
5. **Pending devices API**: `GET /api/club/devices/pending` and approval
   action; the wizard call from step 4 leaves the device in `pending_approval`
   when the owner has set "manual approval" on the branch. Default is
   auto-approve.
6. **Audit events** for owner-code rotation, installer discover, installer
   enroll, and pending-device approval/rejection.

### Slice 2 — Customer dashboard SPA + accept-invite + terminology cleanup

Adds the new customer-area routes to the existing `src/AFK4.Platform.Web`
SPA. The Mubi admin views stay in the same codebase and gain a route prefix.

1. **Route restructure**:
   - `/admin/*` — Mubi admin (platform-admin token required, all current
     Slice 1-6 tenant-management screens move under here).
   - `/club/*` — customer area (staff token with `branch_owner` role
     required).
   - `/auth/*` — public unauthenticated pages.
   - Old root-level admin URLs redirect to `/admin/*` for back-compat in
     bookmarks during the cutover.
2. **Public auth pages** under `/auth/*`:
   - `/auth/accept-invite?code=...` — accept owner invite, set password,
     auto-sign-in, redirect to `/club`.
   - `/auth/sign-in` — staff sign-in (already exists in raw form, gets a
     UI).
   - `/auth/forgot-password` and `/auth/reset-password` — out of scope here
     (see Future Work), but reserve the routes.
3. **Customer dashboard `/club/*` MVP screens**:
   - `/club` — dashboard home (per-branch KPI cards: device count, active
     sessions, errors today).
   - `/club/branches` — list of the owner's branches, "Add branch" wizard
     (admin must approve creation if outside the tenant's `maxBranches`
     limit, otherwise instant).
   - `/club/branches/:id` — branch detail (rename, address, contact,
     deactivate).
   - `/club/branches/:id/floor-map` — drag-and-drop seats grouped into
     zones, save-on-edit with optimistic UI.
   - `/club/branches/:id/devices` — devices in this branch, live-updating
     via SignalR (status, current seat, last heartbeat, role, rename, move
     seat, remove).
   - `/club/branches/:id/devices/pending` — pending-approval queue.
   - `/club/branches/:id/operators` — staff users (invite, roles, deactivate;
     reuses existing platform-admin staff endpoints scoped to branch).
   - `/club/install` — owner code (with rotate button and a "valid until"
     timestamp), prominent **Download AFK4 Setup MSI** button, copy-pasteable
     install command for advanced users, and step-by-step text matching what
     the Windows wizard does.
4. **Terminology cleanup** in UI text and docs:
   - "owner invite code" → just **setup code** everywhere the human-facing
     UI mentions it.
   - "device enrollment code" → **PC enrollment code** (legacy path, hidden
     in the dashboard unless the owner clicks "Advanced install").
   - The "slug pair" concept disappears from the user-facing UI entirely —
     it stays as internal data the Operator App caches transparently.
5. **Audience build flag** in the SPA: `VITE_AUDIENCE=admin` builds an
   image that exposes only `/admin/*` + `/auth/*`, `VITE_AUDIENCE=club` builds
   only `/club/*` + `/auth/*`. Same code, two Coolify applications, two
   hostnames.

### Slice 3 — Windows clients consolidation

Replace the current two-MSI + `bootstrap.ps1` installer flow with a single
unsigned MSI plus a WPF first-run wizard.

1. **Single MSI** `AFK4-Agent.msi` ships only the Agent Service + the WPF
   first-run wizard. Player Shell and Operator App are no longer in the MSI
   payload.
2. **First-run wizard** `AFK4.SetupWizard.exe` is launched by MSI on
   completion (postinstall custom action), opens a 4-step WPF window:
   1. Owner-code entry (6–8 digit numeric input, with paste support).
   2. Branch picker (data from `/api/install/discover`).
   3. Visual seat picker — renders the chosen branch's floor map, free
      seats green-clickable, occupied seats grey, allows creating a new
      seat from inside the picker.
   4. Role radio: **Gaming PC**, **Manager workstation**.
3. **Enroll call** uses `/api/install/enroll` with the chosen values plus
   a self-generated stable device-key pair. Server returns device identity +
   Agent bootstrap config (API host, channel, signed lease key).
4. **Agent role-aware self-installer**: after first enroll, Agent reads its
   role from the bootstrap config and pulls the right component bundle from
   the update channel:
   - `gaming_pc` → installs and supervises Player Shell.
   - `manager_workstation` → installs Operator App (and its WebView2
     dependency check) and registers it as the default machine-wide
     operator app.
5. **Self-update** continues through the existing Agent update-channel
   infrastructure; no separate updater binary.
6. **Uninstall**: MSI uninstall stops the Agent, Agent uninstalls its child
   components, all per-machine config is removed but per-user data is left
   in place.
7. **Removal of `bootstrap.ps1`, `gaming-pc.msi`, `operator-app.msi`**
   from the publishing pipeline once the new MSI has shipped one channel
   release and Mubi has migrated all existing VM-side test devices over.
8. **Signing** stays as a future flip — once SignPath approval comes
   through, the existing `signing_provider` workflow input becomes
   `signpath` and the MSI is signed without code changes.

### Slice 4 — Public landing site

Marketing-facing static site at the staging apex (`afk4.staging.mubi.dev`
during pilot, moves to the final prod domain when one is acquired). Separate
codebase from the SPA so a marketing iteration doesn't trigger an app rebuild.

1. **Tech stack**: Astro for static-site generation. Reasons: tiny output,
   zero JS by default, drop-in React/Vue islands if a section needs it,
   first-class i18n routing, content as Markdown.
2. **Repo layout**: new top-level directory `src/AFK4.Public.Web/` (matches
   the existing `src/AFK4.*` namespace pattern even though it is not .NET).
   Astro project lives inside; `npm` and `astro build` produce a `dist/`
   directory.
3. **Initial pages**:
   - `/` (home) — what AFK4 is in two sentences, three screenshots from
     the Operator App + Customer Dashboard, primary CTA "Talk to us"
     scrolling to the contact form, secondary CTA "Try AFK4" (placeholder
     link to `/club/install` for invited owners — self-signup is in the
     post-onboarding roadmap, not here).
   - `/features` — feature grid pulled from `docs/product/AFK4-MVP-PRD.md`
     trimmed for marketing.
   - `/pricing` — placeholder plan tier cards (Starter / Growth /
     Enterprise) without dollar amounts until pricing is decided; "Contact
     us for pricing" CTAs.
   - `/contact` — short form (name, email, club name, message) that POSTs
     to a small Astro endpoint which forwards to a Telegram bot via the
     existing alert channel + writes to a CSV (or future inbox table).
   - `/legal/privacy` and `/legal/terms` — placeholders with "draft
     pending legal review" banner.
4. **i18n**: Russian primary, English secondary. Astro i18n routing under
   `/`, `/en/`. Content files stored side-by-side
   (`pages/index.md`, `pages/en/index.md`).
5. **SEO basics**: per-page `<title>` + meta description + `og:image`
   per locale. `sitemap.xml` auto-generated via Astro integration.
   `robots.txt` allowing all.
6. **Contact form delivery**: reuse the existing `@afk4alerts_bot`
   Telegram bot with a new chat (group, not the existing private DM) so
   future team members can see inbound leads. Bot token stored in a new
   Coolify secret on the public-web Coolify app. Form submissions also
   logged to a JSON file in a Coolify-mounted volume for later import
   into a CRM.
7. **Coolify deployment**: separate Coolify application using a tiny
   Dockerfile that runs `astro build` then `nginx` serving `dist/` on
   port 8080. Lives at the staging apex
   (`https://afk4.staging.mubi.dev/`) until the final prod domain is
   decided. The Platform API's existing host stays at
   `afk4.staging.mubi.dev/api/...` — public web is the catch-all
   alongside it, **not** in front of it (Traefik priority lower than
   the API router so `/api/*` keeps reaching the API).
8. **Analytics**: privacy-respecting only (Plausible self-hosted if we
   stand it up later, or none for v1). No Google Analytics.
9. **Domain migration story**: when the final prod domain is acquired,
   the only changes are (a) Coolify host rename, (b) DNS, (c) absolute
   URLs in `<head>` (sitemap, canonical, og:url). No content changes.

## Non-Goals

These are real follow-ups but **not** covered in this plan. Tracked in
[`2026-05-24-afk4-roadmap-post-onboarding.md`](./2026-05-24-afk4-roadmap-post-onboarding.md)
as a roadmap; each gets its own implementation plan when its time comes.

- Production environment separation from staging.
- SignPath integration follow-through (already scaffolded in PR #45).
- Self-service signup from the public landing page (the landing **page**
  exists in Slice 4; the self-signup **flow** is roadmap).
- Payment provider integration (Stripe / ЮKassa / Тинькофф / local TJ
  gateway).
- Billing UI in the customer dashboard.
- Mobile companion app for owners.
- Audit log UI in the Mubi admin.
- Password reset flow (`/auth/forgot-password`).
- Multi-region deployment automation.

## Product Decisions

These are agreed and should not be re-litigated during implementation.

1. **Single MSI now, signed later.** Pilot 1-2 ship unsigned. SmartScreen
   "Unverified publisher" warning is accepted as a known issue until
   SignPath Foundation approves. No `bootstrap.ps1` lives past this slice.
2. **One owner-code, not a per-device enrollment code, drives installer
   auth.** Code is short (numeric, 6–8 digits), rotatable, audit-logged,
   and only valid for the install/* endpoints (cannot sign in to the
   SPA).
3. **The owner code is per-owner, not per-branch.** A multi-branch owner
   types one code on every PC in every branch; the wizard's branch picker
   resolves which branch.
4. **Self-naming + visual seat pick during install.** No per-PC code
   typing from the manager. No "type 30 codes one by one" workflow ever.
5. **Single SPA codebase, audience build flag, two Coolify apps.** Mubi
   admin and customer dashboard share code, deploy as two images, two
   hostnames. `admin.afk4.staging.mubi.dev` (current) + new
   `app.afk4.staging.mubi.dev`.
6. **Terminology: one term per concept in user-facing surfaces.**
   - **Setup code** = the link/code that turns a club owner into a branch
     owner. (Internally still `owner_invite_code`; SPA labels say "setup
     code".)
   - **Owner code** = the rotatable numeric code the owner types into the
     Windows installer.
   - **PC enrollment code** = legacy advanced path only, not in default
     UX.
7. **Floor map is per-branch, not per-tenant.** Each branch has its own
   zones and seat grid. Seats are unique within a branch.
8. **Pending-device approval defaults OFF.** New devices auto-enroll into
   the picked seat. The branch owner can flip on "manual approval" if
   they want a queue.
9. **Bootstrap admin and platform-admin user surface are unchanged.**
   This plan does not touch `PlatformAdminBootstrapHostedService` or the
   `/api/platform/auth/*` endpoint group.

## Architecture Shape

### Backend (`src/AFK4.Platform.Api`)

Stay inside the existing modular monolith. Add (or extend) these modules:

- **Identity** module: gains owner-code entity, generator, rotation,
  lookup. Owner codes are stored hashed (SHA-256 over normalized form),
  with `Suffix` (last 4 chars) cached for masked display in the SPA, same
  pattern as `OwnerInviteEntity.CodeSuffix`.
- **Tenancy** module: gains zones and seats entities, both per-branch,
  with display order. A default zone is auto-created when a branch is
  provisioned.
- **Devices** module (new — or under Tenancy if too small): device
  identity, pending-approval flag, role, seat assignment, last heartbeat.
- **Install** module (new): orchestrates the wizard-side flows:
  - `POST /api/install/discover` (owner code → branches + floor map +
    free seats).
  - `POST /api/install/enroll` (owner code + branch + seat + role + PC
    name → device identity + bootstrap config).
  - These endpoints **do not** issue platform-admin or staff tokens;
    they return only the data the installer needs.

Audit events to add:

- `install.owner_code.generated`
- `install.owner_code.rotated`
- `install.discover.invoked`
- `install.enroll.succeeded`
- `install.enroll.rejected` (invalid owner code, exhausted seat limits,
  branch not owned by code)
- `tenancy.floor_map.updated`
- `tenancy.device.approved`
- `tenancy.device.rejected`
- `tenancy.device.removed`

### Data model additions

New tables (snake_case names; columns stay PascalCase to match the existing
convention):

- `owner_codes` — `OwnerCodeId uuid pk`, `PlatformAdminUserId uuid` for
  Mubi-issued codes / `StaffUserId uuid nullable` for branch-owner-issued
  codes (exactly one of the two non-null), `CodeHash text`, `CodeSuffix
  varchar(4)`, `ExpiresAtUtc timestamptz`, `LastUsedAtUtc timestamptz
  nullable`, `RevokedAtUtc timestamptz nullable`, `RevokedReason text
  nullable`, `CreatedAtUtc timestamptz`. Index on `CodeHash`; partial
  index on `(StaffUserId) WHERE RevokedAtUtc IS NULL`.
- `zones` — `ZoneId uuid pk`, `BranchId uuid fk`, `Name varchar(64)`,
  `DisplayOrder int`, `CreatedAtUtc`, `UpdatedAtUtc`. Index on `(BranchId,
  DisplayOrder)`. Default `(BranchId, "Default")` row inserted whenever a
  new branch is provisioned (in the existing tenant-provisioning service,
  not via SQL trigger).
- `seats` — `SeatId uuid pk`, `BranchId uuid fk`, `ZoneId uuid fk`,
  `Label varchar(32)` (e.g. `01`, `seat-04`), `PositionX int`,
  `PositionY int` (free-form coordinates in the editor's grid; clients
  render however they like), `CreatedAtUtc`, `UpdatedAtUtc`. Unique
  index on `(BranchId, Label)`. Index on `(BranchId, ZoneId)`.
- `devices` (likely an extension of an existing entity if any — verify
  during implementation; the smoke script's `device-enrollment-codes`
  flow already implies a `devices` table somewhere): add `SeatId uuid
  nullable fk`, `Role varchar(32)` (`gaming_pc` / `manager_workstation`),
  `DisplayName varchar(64)`, `EnrollmentState varchar(32)` (`pending`,
  `approved`, `rejected`, `removed`), `EnrolledViaOwnerCodeId uuid
  nullable fk`. If a `devices` table does not exist yet, design it from
  scratch with these plus the standard identity columns
  (`DeviceId uuid pk`, `BranchId uuid fk`, key pair material, last
  heartbeat).

EF migrations follow the existing pattern (one migration per slice,
applied via the documented `docs/operations/coolify-staging-deploy.md`
flow gated by `confirm_migrations_applied=true`).

### API contract sketches

These are *sketches*, not final. Match existing
`src/AFK4.Shared.Contracts/Platform/` style during implementation.

```
POST /api/club/owner-code/generate            (staff, branch_owner)
  → { ownerCode, codeSuffix, expiresAtUtc }
POST /api/club/owner-code/rotate              (staff, branch_owner)
  → { ownerCode, codeSuffix, expiresAtUtc }
GET  /api/club/owner-code                     (staff, branch_owner)
  → { codeSuffix, expiresAtUtc, lastUsedAtUtc }     (never returns full code)

GET  /api/club/branches/{branchId}/floor-map  (staff, branch_owner)
  → { zones: [{ zoneId, name, displayOrder,
                seats: [{ seatId, label, positionX, positionY,
                          deviceId nullable, deviceDisplayName nullable }] }] }
PUT  /api/club/branches/{branchId}/floor-map  (staff, branch_owner)
  body: full zones+seats document; server diff-applies and returns the
        canonical post-save document.

POST /api/install/discover                    (unauth, owner code in body)
  body: { ownerCode }
  → { ownerDisplayName, branches: [{ branchId, slug, name, zones, seats,
       freeSeatIds }] }
POST /api/install/enroll                      (unauth, owner code in body)
  body: { ownerCode, branchId, seatId, role, displayName, devicePublicKey }
  → { deviceId, apiBaseUrl, updateChannel, leaseSigningPublicKey,
       initialEnrollmentToken }    (one-shot, used by Agent to fetch first
                                    config bundle)

GET  /api/club/branches/{branchId}/devices    (staff)
  → list with status / seat / role / lastHeartbeat
GET  /api/club/branches/{branchId}/devices/pending
  → pending-approval queue
POST /api/club/devices/{deviceId}/approve
POST /api/club/devices/{deviceId}/reject
POST /api/club/devices/{deviceId}/rename
POST /api/club/devices/{deviceId}/move-seat   { seatId }
POST /api/club/devices/{deviceId}/remove
```

Rate-limit the two `/api/install/*` endpoints with the same Traefik recipe
as `/api/operator-connections/resolve` (see
`deploy/coolify/ingress.md`). The owner-code endpoint is high-leverage
and brute-forceable.

### SPA (`src/AFK4.Platform.Web`)

Route tree after refactor:

```
/auth/accept-invite?code=...     public
/auth/sign-in                    public, returns to ?return=...
/auth/forgot-password            placeholder, future
/auth/reset-password?token=...   placeholder, future

/admin                           platform_admin
/admin/tenants                   platform_admin
/admin/tenants/:orgId            platform_admin
/admin/tenants/:orgId/health     platform_admin
/admin/tenants/:orgId/audit      future
/admin/audit                     future

/club                            staff, branch_owner
/club/branches                   staff, branch_owner
/club/branches/:branchId         staff, branch_owner
/club/branches/:branchId/floor-map
/club/branches/:branchId/devices
/club/branches/:branchId/devices/pending
/club/branches/:branchId/operators
/club/install                    staff, branch_owner
```

Tech notes:

- Reuse the existing `tokenStore` / RTK Query setup; add a second token
  context for staff tokens alongside the platform-admin one.
- Floor-map editor: pick a small drag library (e.g. `dnd-kit`) over
  rolling our own.
- Live device updates: extend the existing SignalR hub or add a new
  `devices` hub group keyed by `branchId`.

### Coolify deployments

Add a second Coolify application for the same SPA Dockerfile with build
arg `VITE_AUDIENCE=club` and host `app.afk4.staging.mubi.dev`. The
existing admin app stays at `platform.afk4.staging.mubi.dev` and gets
`VITE_AUDIENCE=admin`. Both build from the same git ref and the same
Dockerfile (`deploy/coolify/platform-web.Dockerfile`). DNS A/AAAA record
for the new host added before first deploy. Traefik labels mirror the
existing platform-web app.

### Windows clients

- **Single MSI project** in `src/AFK4.Client.SetupBundle` (new) using WiX.
  Authoring tracks the existing `gaming-pc.msi` WiX project; the bundle
  outputs `AFK4-Agent.msi` and includes the Agent Service binary +
  `AFK4.SetupWizard.exe`.
- **`AFK4.SetupWizard.exe`** is a small WPF app, also new, in
  `src/AFK4.Client.SetupWizard`. It depends on the same shared HTTP
  client helpers the Agent uses to call the install/* endpoints.
- **Agent role-aware bootstrap** lives in the existing Agent code under
  `src/AFK4.GamingPc.AgentService`. The Agent gains a startup phase that:
  1. Reads its enrollment state from local protected storage.
  2. If `role=gaming_pc` and Player Shell missing or stale, downloads
     and installs the Player Shell MSI from the update channel.
  3. If `role=manager_workstation` and Operator App missing or stale,
     downloads and installs the Operator App MSI from the update
     channel.
  4. Reports component versions in heartbeat.

Player Shell MSI and Operator App MSI continue to exist as packaging
artifacts in the update channel; they just stop shipping as part of the
top-level installer. The Agent is the only thing the operator installs
by hand.

## Sequencing And Implementation Order

Within each slice, build backend → SPA → docs → demo. Do not skip ahead.

1. **Slice 1.1** — Owner code: entity, migration, generate/rotate/lookup
   service, API endpoints, audit events, xUnit tests using
   `PlatformAdminTestHelper` patterns.
2. **Slice 1.2** — Floor map: entities, migration, CRUD service, API,
   default-zone auto-creation hook in `TenantProvisioningService`,
   xUnit tests.
3. **Slice 1.3** — Install module: discover + enroll endpoints, owner-code
   rate-limit ingress recipe, audit events, xUnit tests (including
   "enrolls into pending when manual approval set", "rejects on revoked
   owner code", etc.).
4. **Slice 1.4** — Devices admin surface API: pending queue + approve /
   reject / rename / move-seat / remove + SignalR notification on state
   change.
5. **Slice 2.1** — SPA route restructure under `/admin/*` + redirects
   from old URLs. Existing screens migrate, **no new screens yet**, so
   this lands cleanly.
6. **Slice 2.2** — `/auth/accept-invite` + `/auth/sign-in` public pages,
   wired to existing accept-invite and sign-in endpoints.
7. **Slice 2.3** — Customer dashboard MVP screens under `/club/*`,
   landing on `/club/install` (owner-code page) and the install
   instructions matching the wizard.
8. **Slice 2.4** — Terminology cleanup pass across SPA strings + the
   public-facing docs in `docs/operations/`.
9. **Slice 2.5** — SPA audience build flag + second Coolify app for the
   customer host.
10. **Slice 3.1** — `AFK4.SetupWizard` WPF app, runs against staging
    install/* APIs without an MSI yet (debug via direct exe).
11. **Slice 3.2** — Single MSI WiX project, postinstall hook launches
    the wizard. Build + ship internal channel release.
12. **Slice 3.3** — Agent role-aware component install. Adds Player
    Shell / Operator App auto-install branches.
13. **Slice 3.4** — Smoke on clean Windows VM end-to-end: real owner
    creates code in `/club/install`, runs MSI, types code in wizard,
    picks seat, Player Shell auto-installs, Operator App on a second
    VM acts as manager workstation.
14. **Slice 3.5** — Deprecate `bootstrap.ps1`, `gaming-pc.msi`,
    `operator-app.msi` once the smoke passes; remove from publishing
    pipeline.
15. **Slice 4.1** — Astro project bootstrap in `src/AFK4.Public.Web/`,
    home page in RU + EN with placeholder content, build + serve
    locally.
16. **Slice 4.2** — Features, pricing, contact, legal placeholder
    pages. Contact form endpoint with Telegram bot wiring and JSON
    log file.
17. **Slice 4.3** — Astro Dockerfile + Coolify application for
    `https://afk4.staging.mubi.dev/` apex routing (Traefik priority
    lower than Platform API to keep `/api/*` reaching the API).
    Telegram bot chat group setup with `@afk4alerts_bot` (new group,
    not the existing private alerts DM).
18. **Slice 4.4** — SEO basics (sitemap.xml, robots.txt, per-page
    meta), final screenshot pass after Slices 1-3 ship so visuals
    match the real product, polish RU + EN copy.

## Testing

- Unit + integration tests in `tests/AFK4.Platform.Api.Tests` for every
  new endpoint, including failure modes (revoked owner code, mismatched
  branch, exhausted seats, brute-force rate-limit).
- Extend `scripts/staging-smoke.py` with a Slice-7 walk:
  generate owner code → call discover → call enroll for a synthetic
  device → list devices → move seat → remove device.
- Manual end-to-end on a clean Windows 11 VM (the existing
  `docs/operations/real-device-windows-pc-smoke.md` rig) for Slice 3.4.
- Floor-map editor needs a Playwright (or equivalent) test for the
  drag-and-drop flow; current SPA test setup TBD during implementation.

## Risk + Open Questions

1. **Owner code brute-force**. 6 digits = 10⁶ keyspace. Mitigation:
   per-IP rate-limit (Traefik), per-code lock-out (e.g., 10 wrong
   attempts → revoke code, notify owner), audit on every failed lookup.
   Decide during Slice 1.3 whether 6 digits is enough or move to 8.
2. **Multiple owners share the same staff user vs one user per owner**.
   The current data model has `StaffUser` with branch role assignments.
   If a single human owns two clubs, they have one `StaffUser` row with
   two role rows. Owner code is per-`StaffUser`, so one human → one
   code that resolves to multiple branches. Confirmed in the discover
   flow.
3. **Cross-tenant data leakage in discover**. `POST /api/install/discover`
   accepts an owner code with no auth header — the code IS the credential.
   The endpoint must scope its response strictly to branches owned by
   the resolved staff user, with an integration test that fails closed
   if a branch from another tenant appears in the response.
4. **Pending-device queue with auto-approve default**. If a device
   enrolls into the wrong seat by mistake, the owner must be able to
   correct it without a full re-install — `move-seat` API handles this.
5. **Floor-map editor coordinate model**. Free-form `PositionX, PositionY`
   keeps backend dumb. Editor decides rendering. Risk: future "snap to
   grid" or "club blueprint" features may need a richer model — leave
   the migration door open with `nullable` coordinates and a `Layout` JSON
   blob reserved for later.
6. **SmartScreen warning for unsigned MSI**. Documented, accepted.
   Re-evaluate if pilot 1 club rejects the warning.
7. **Removing `bootstrap.ps1`** breaks any in-flight VM tests. Slice 3.5
   only runs after Slice 3.4 confirms the new path works on a clean
   VM end-to-end.

## What Is Not In This Plan

Tracked in the post-onboarding roadmap
([`2026-05-24-afk4-roadmap-post-onboarding.md`](./2026-05-24-afk4-roadmap-post-onboarding.md))
as headings; each becomes its own implementation plan when its time
comes:

- Production environment separation (`coolify-prod-deploy.yml`,
  separate Postgres, separate uptime monitors, prod
  session-signing key).
- Self-service signup from the public landing page (page exists in
  Slice 4; backend tenant creation flow is roadmap).
- Payments + billing UI.
- Audit log UI.
- Password reset flow.
- Mobile companion app.
- Multi-tenant white-label.
- SignPath signing rollout (PR #45 scaffolded the workflow input,
  remaining work is application + secrets + cutover).
- Final prod domain acquisition + DNS/Coolify/copy cutover.

## Definition Of Done

All of the following must be true for this plan to be considered shipped:

- A platform admin can create a tenant in `/admin/tenants/new` and email
  a clickable invite link to a real human.
- That human opens the link in a desktop browser, sets a password, and
  lands in `/club` without seeing any GUIDs, slugs, or curl commands.
- In `/club/install` they see a 6-8 digit owner code with a one-click
  rotate button and a one-click MSI download.
- On a clean Windows 11 VM, double-clicking the downloaded MSI installs
  the Agent, the first-run wizard opens, the owner types their code,
  picks a branch + seat + role, clicks Finish, and within a minute the
  PC is visible in their dashboard with a green status indicator.
- On a second clean VM enrolled as `manager_workstation`, the Operator
  App auto-installs, launches, and is signed-in (or prompts for
  staff creds).
- `bootstrap.ps1` and the standalone `gaming-pc.msi` / `operator-app.msi`
  no longer ship from the publishing workflow.
- All existing staging-smoke checks (24/24 as of 2026-05-24) still pass,
  plus the new Slice-7 walk.
- `docs/operations/coolify-staging-deploy.md` reflects the new install
  flow as the documented onboarding path; the old curl-based steps are
  removed.
- A publicly-reachable landing site is live at
  `https://afk4.staging.mubi.dev/` in Russian + English with home,
  features, pricing, contact, and legal placeholder pages; the
  contact form delivers to a Telegram group with @afk4alerts_bot.

## Related

- [[2026-05-23-saas-control-plane-tenant-onboarding]] — preceding plan
  (Slices 1-6 + hardening A-E), now shipped.
- `docs/operations/client-packaging.md`, `client-update-rollout.md`,
  `update-package-publishing.md` — existing Windows client packaging
  doc set, will need updates in Slice 3.5.
- `docs/operations/coolify-staging-deploy.md` — staging deploy runbook,
  will need a section on adding the second SPA application in
  Slice 2.5.
- `scripts/staging-smoke.py` — current smoke harness, extended in
  Slice 1.3 + Slice 3.4.
