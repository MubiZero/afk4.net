# AFK4 Club Self-Service Onboarding Plan

Status: active; Slices 1.1-3.5 implemented locally, Slice 4 next
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

The floor map model + CRUD API already exist in the repo as of migration
`20260513034617_AddClubLayout` (entities `ZoneEntity`, `SeatEntity`,
`DeviceEntity`, `DeviceSeatAssignmentEntity` and endpoints
`/api/branches/{branchId}/floor-map`, `/api/branches/{branchId}/layout/zones`,
`/api/branches/{branchId}/layout/seats`,
`/api/branches/{branchId}/device-enrollment-codes`, `/api/devices/enroll`,
`/api/devices/{deviceId}/heartbeat`). Slice 1 builds on top of them rather
than reinventing.

1. **Owner code**: new short numeric code per `StaffUser`, rotatable, audit-
   logged, usable for installer authentication only (not for SPA sign-in).
   Brute-force protection specified in Risk #1 — minimum 8 digits, per-code
   lockout after 5 failed attempts, lockout revokes the code and notifies
   the owner.
2. **Floor map gaps**: confirm existing endpoints cover the new editor's
   needs (single combined GET that returns zones + seats + assignment
   status; PUT for bulk save with optimistic-concurrency ETag).
   - The existing zone/seat endpoints are per-resource; the editor will
     drive many small saves OR one bulk save. Decide during Slice 1.2:
     either (a) keep per-resource endpoints and have the editor batch
     them, or (b) add a single `PUT /api/branches/{branchId}/floor-map`
     bulk endpoint with `If-Match` ETag header for concurrency. **(b)
     is preferred** — simpler client, single audit row per save, hard
     concurrency guarantee.
   - Add a default "default zone" auto-create hook in the existing
     branch-provisioning service if it isn't already there (verify
     during implementation).
3. **Installer discovery API**: `POST /api/install/discover` taking an owner
   code, returning the owner's branches under the same `StaffUser`'s
   `OrganizationId`, each branch's floor map, and the subset of seats
   currently unassigned to any device (via `DeviceSeatAssignmentEntity`
   where `DetachedAtUtc IS NULL`).
4. **Installer enrollment API**: `POST /api/install/enroll` taking an owner
   code + branch id + seat id + role + a self-supplied PC display name +
   device public key, returning everything Agent needs in one response
   (`deviceId`, `apiBaseUrl`, `updateChannel`, `leaseSigningPublicKey`).
   No second-call/one-shot-token round trip — the enroll response is the
   bootstrap.
5. **Pending devices API**: `GET /api/club/branches/{branchId}/devices/pending`
   and approval action; the wizard call from step 4 leaves the device in
   `pending_approval` when the owner has set "manual approval" on the
   branch. Default is auto-approve. The new `EnrollmentState` column on
   `devices` is the only schema delta from this slice.
6. **Audit events** for owner-code rotation, installer discover (failed
   lookups only — see Risk #16 on audit noise), installer enroll
   (succeeded + rejected), and pending-device approval/rejection.

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
     zones. **Optimistic-concurrency model:** the editor loads the
     full floor map with an ETag, all edits are local, the **Save**
     button bulk-PUTs with `If-Match: <etag>`; the server returns 409
     if the ETag is stale and the editor offers reload-and-replay.
     Auto-save is explicitly out — we don't want silent overwrites
     when two managers edit the same map.
   - `/club/branches/:id/devices` — devices in this branch, live-updating
     via SignalR (status, current seat, last heartbeat, role, rename, move
     seat, remove).
   - `/club/branches/:id/devices/pending` — pending-approval queue.
   - `/club/branches/:id/operators` — staff users (invite, roles, deactivate;
     reuses existing platform-admin staff endpoints scoped to branch).
     **Role boundary inside `/club`**: routes under `/club/*` are
     accessible to any staff user with `branch_owner` OR
     `branch_manager` role (manager has read+device-ops but no
     owner-code generate, no operator-invite, no branch lifecycle).
     Routes for owner-code, operator invites, and branch settings are
     `branch_owner`-only.
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
2. **First-run wizard** `AFK4.SetupWizard.exe` is launched after MSI
   completion. **Important: MSI custom actions run as SYSTEM and cannot
   open an interactive WPF window directly.** The pattern:
   - MSI writes a per-machine "first-run pending" flag + registers
     `AFK4.SetupWizard.exe` as a HKLM `RunOnce` entry so the wizard
     fires on the next interactive login of any admin user.
   - For the manual-install case the operator is already logged in;
     the postinstall action explicitly **launches the wizard in the
     interactive desktop session** via `WTSQueryUserToken` + `CreateProcessAsUser`,
     OR (simpler fallback) launches it via the existing user's
     `explorer.exe` token using a small launcher helper.
   - If no interactive session exists (silent MDM/SCCM deploy), the
     wizard does NOT auto-launch; the MSI completes successfully, the
     `RunOnce` entry waits, and the operator runs the wizard from the
     Start Menu shortcut. Device stays unenrolled until then.
   The wizard itself is a 4-step WPF window:
   1. Owner-code entry (8-digit numeric input, paste support, no
      ambiguous chars).
   2. Branch picker (data from `/api/install/discover`).
   3. Visual seat picker — renders the chosen branch's floor map, free
      seats green-clickable, occupied seats grey, allows creating a new
      seat from inside the picker.
   4. Role radio: **Gaming PC**, **Manager workstation**.
3. **Enroll call** uses `/api/install/enroll` with the chosen values plus
   a self-generated stable device-key pair. Server returns device identity +
   Agent bootstrap config (API host, channel, signed lease key) in a single
   response.
4. **Agent role-aware self-installer**: after first enroll, Agent reads its
   role from the bootstrap config and pulls the right component bundle from
   the update channel:
   - `gaming_pc` → installs and supervises Player Shell.
   - `manager_workstation` → checks for **Microsoft Edge WebView2
     Runtime** first (registry probe at
     `HKLM\Software\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}`),
     downloads + runs `MicrosoftEdgeWebView2Setup.exe /silent /install`
     if missing, then installs Operator App and registers it as the
     default machine-wide operator app. If WebView2 install fails
     (offline, corporate proxy, etc.) Agent surfaces the error to the
     dashboard via heartbeat and keeps retrying on a backoff.
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

Marketing-facing static site on a dedicated subdomain
`www.afk4.staging.mubi.dev` during pilot, moves to the final prod domain
apex when one is acquired. **Not** co-hosted on the API host — the
Platform API owns `afk4.staging.mubi.dev` apex and we don't want
Traefik-priority hacks or reserved-prefix lists between marketing and
API. Separate codebase from the SPA so a marketing iteration doesn't
trigger an app rebuild.

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
   port 8080. Lives at `https://www.afk4.staging.mubi.dev/` (own
   subdomain, dedicated DNS A/AAAA, dedicated Let's Encrypt cert,
   dedicated Traefik labels). The Platform API stays untouched on
   `afk4.staging.mubi.dev` apex; no Traefik priority juggling needed.
8. **Analytics**: privacy-respecting only (Plausible self-hosted if we
   stand it up later, or none for v1). No Google Analytics.
9. **Domain migration story**: when the final prod domain is acquired,
   the changes are (a) add new Coolify host for the prod landing, (b)
   DNS, (c) absolute URLs in `<head>` (sitemap, canonical, og:url),
   (d) a 301 redirect from `www.afk4.staging.mubi.dev` → the new prod
   host for any inbound links. No content changes.

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
   SignPath Foundation approves. The single Agent MSI path has Windows 11 VM
   evidence through Agent `0.1.29`; `bootstrap.ps1` and the coordinated legacy
   MSI path are retired from the default publishing/onboarding flow, with
   explicit fallback switches and migration notes for existing test devices.
2. **One owner-code, not a per-device enrollment code, drives installer
   auth.** Code is 8 digits numeric (decision anchored — see Risk #1),
   rotatable, audit-logged, and only valid for the install/* endpoints
   (cannot sign in to the SPA).
3. **Owner-code scope = one per `StaffUser`, which is per-organization.**
   - A single-club owner has one code that resolves to the branches of
     their one organization.
   - A multi-club human who legally owns several separate organizations
     has one `StaffUser` row per organization (current data-model
     reality, not a regression) and therefore one owner code per
     organization. The wizard's branch picker resolves which branch
     within that organization.
   - "One human, one code, any number of clubs" is **not** delivered by
     this slice — it requires a new identity-level abstraction above
     `StaffUser` and is tracked in the post-onboarding roadmap.
4. **Self-naming + visual seat pick during install.** No per-PC code
   typing from the manager. No "type 30 codes one by one" workflow ever.
5. **Single SPA codebase, audience build flag, two Coolify apps.** Mubi
   admin and customer dashboard share code, deploy as two images, two
   hostnames. **Existing admin host stays at
   `platform.afk4.staging.mubi.dev`** (current uptime monitor + ingress
   reference this name — renaming it is operationally expensive and
   pointless). **New customer host = `app.afk4.staging.mubi.dev`.**
6. **Terminology: one term per concept in user-facing surfaces.**
   - **Setup code** = the link/code that turns a club owner into a branch
     owner. (Internally still `owner_invite_code`; SPA labels say "setup
     code".)
   - **Owner code** = the rotatable numeric code the owner types into the
     Windows installer.
   - **PC enrollment code** = legacy advanced path only, not in default
     UX.
7. **Floor map is per-branch, not per-tenant.** Each branch has its own
   zones and seat grid. Seats are unique within a branch. Floor map
   schema already exists (`ZoneEntity`, `SeatEntity`,
   `DeviceSeatAssignmentEntity` since migration `AddClubLayout`); this
   slice adds editor APIs on top, not the data model.
8. **Pending-device approval defaults OFF.** New devices auto-enroll into
   the picked seat. The branch owner can flip on "manual approval" per
   branch (new `BranchEntity.RequireManualDeviceApproval bool`) if they
   want a queue. If a device enrolls into the wrong seat, the owner uses
   `move-seat` — no re-install.
9. **Floor-map saves use optimistic-concurrency ETag.** No silent
   last-write-wins. Bulk PUT with `If-Match`; 409 on stale ETag, editor
   offers reload-and-replay.
10. **Bootstrap admin and platform-admin user surface are unchanged.**
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

### Data model — diff from existing schema

Existing schema as of migration `20260513034617_AddClubLayout`:

- `devices` — `DeviceEntity`: `DeviceId uuid pk`, `OrganizationId`,
  `BranchId`, `MachineName`, `AgentVersion`, `ShellVersion`,
  `EnrolledAtUtc`, `LastHeartbeatAtUtc`, `IsOnline`, `IsLocked`.
- `zones` — `ZoneEntity`: `ZoneId uuid pk`, `OrganizationId`, `BranchId`,
  `Name varchar(120)`, `SortOrder int`, `CreatedAtUtc`.
- `seats` — `SeatEntity`: `SeatId uuid pk`, `OrganizationId`, `BranchId`,
  `ZoneId`, `Name varchar(80)`, `SortOrder int`, `CreatedAtUtc`.
- `device_seat_assignments` — `DeviceSeatAssignmentEntity`:
  `DeviceSeatAssignmentId pk`, `OrganizationId`, `BranchId`, `SeatId`,
  `DeviceId`, `AttachedAtUtc`, `DetachedAtUtc nullable`. **Many-to-many
  with history** — a device can move between seats and the history is
  preserved; the "current seat" of a device is the row where
  `DetachedAtUtc IS NULL`.
- `device_enrollment_codes` — `DeviceEnrollmentCodeEntity`: legacy
  per-device enrollment code flow. Stays in the schema; new flow uses
  owner-codes instead.

**What this slice adds:**

- **New table `owner_codes`** — `OwnerCodeId uuid pk`, `StaffUserId uuid
  fk`, `CodeHash text` (SHA-256 over normalized digits), `CodeSuffix
  varchar(4)` (last 4 digits for masked display), `ExpiresAtUtc
  timestamptz`, `LastUsedAtUtc timestamptz nullable`, `FailedAttemptCount
  int default 0`, `RevokedAtUtc timestamptz nullable`, `RevokedReason
  text nullable`, `CreatedAtUtc timestamptz`. Indices: `CodeHash`
  (lookup), partial `(StaffUserId) WHERE RevokedAtUtc IS NULL` (one
  active code per StaffUser invariant — enforced at service layer, not
  unique constraint, because rotation creates a new row before
  revoking the old).
- **New columns on `devices`** —
  - `Role varchar(32)` not null, default `'gaming_pc'` for migration
    backfill (`gaming_pc` / `manager_workstation`).
  - `DisplayName varchar(80)` not null, default copies `MachineName` on
    backfill, then becomes editable in the dashboard.
  - `EnrollmentState varchar(32)` not null, default `'approved'` for
    migration backfill (existing devices are already approved); new
    values `pending` and `rejected` and `removed` come from the new
    install flow.
  - `EnrolledViaOwnerCodeId uuid nullable fk` — null for legacy
    `device-enrollment-codes` flow, set for new install/* flow.
- **New column on `branches`** — `RequireManualDeviceApproval bool` not
  null default `false`. When true, `/api/install/enroll` writes
  `EnrollmentState = 'pending'` instead of `'approved'`.
- **No changes to zones/seats** beyond what's already there. The new
  editor uses a single bulk endpoint (see below) but the row shape is
  unchanged.

**Migration ordering**: one migration per slice. Applied via the
documented `docs/operations/coolify-staging-deploy.md` flow gated by
`confirm_migrations_applied=true`.

### API contract sketches

These are *sketches*, not final. Match existing
`src/AFK4.Shared.Contracts/Platform/` style during implementation. Path
naming follows the existing pattern (`/api/branches/{branchId}/...` for
branch-scoped staff endpoints, no `/club` prefix in the URL itself — the
prefix lives in the SPA route tree, the API stays flat).

```
POST /api/staff/me/owner-code/generate                (staff, branch_owner)
  → { ownerCode, codeSuffix, expiresAtUtc }
POST /api/staff/me/owner-code/rotate                  (staff, branch_owner)
  → { ownerCode, codeSuffix, expiresAtUtc }
GET  /api/staff/me/owner-code                         (staff, branch_owner)
  → { codeSuffix, expiresAtUtc, lastUsedAtUtc, failedAttemptCount }
                                                      (never returns full code)
  (No branchId in URL — owner code is per-StaffUser per Decision #3.
   The SPA renders the owner-code widget on every /club/branches/:id/install
   page for UX convenience, but all three buttons call this one endpoint;
   scope is implicit-by-token.)

GET  /api/branches/{branchId}/floor-map               (staff)
  → { etag, zones: [{ zoneId, name, sortOrder,
                       seats: [{ seatId, name, sortOrder,
                                 deviceId nullable,
                                 deviceDisplayName nullable }] }] }
  (Endpoint exists today; we extend it to return ETag header + assignment
   status by joining device_seat_assignments where DetachedAtUtc IS NULL.)
PUT  /api/branches/{branchId}/floor-map               (staff, branch_owner)
  headers: If-Match: <etag>
  body: full zones+seats document
  → 200 with new canonical document + new ETag, OR 409 with current
    canonical document if If-Match is stale.
  (New bulk endpoint. The existing per-zone / per-seat POST/PATCH
   endpoints stay for now and may be deprecated in a later cycle.)

POST /api/install/discover                            (unauth, owner code in body)
  body: { ownerCode }
  → { ownerDisplayName, branches: [{ branchId, slug, name,
                                      floorMap: { zones: [...] },
                                      freeSeatIds: [...] }] }
  (Scopes response to branches under the resolved StaffUser's
   OrganizationId only. Integration test fails closed if a branch from
   another OrganizationId appears.)
POST /api/install/enroll                              (unauth, owner code in body)
  body: { ownerCode, branchId, seatId, role, displayName, devicePublicKey }
  → { deviceId, apiBaseUrl, updateChannel, leaseSigningPublicKey,
       enrollmentState }
  (Single response. No follow-up one-shot token call — the response IS
   the bootstrap. enrollmentState is "approved" or "pending" depending
   on the branch's RequireManualDeviceApproval flag.)

GET  /api/branches/{branchId}/devices                 (staff)
  → list with status / seat / role / lastHeartbeat / enrollmentState
GET  /api/branches/{branchId}/devices/pending         (staff, branch_owner)
  → pending-approval queue
POST /api/devices/{deviceId}/approve                  (staff, branch_owner)
POST /api/devices/{deviceId}/reject                   (staff, branch_owner)
POST /api/devices/{deviceId}/rename                   (staff)
POST /api/devices/{deviceId}/move-seat                (staff)  { seatId }
POST /api/devices/{deviceId}/remove                   (staff, branch_owner)
```

**Rate-limit + brute-force protection** for the two `/api/install/*`
endpoints — see Risk #1 for the anchored spec. Traefik recipe is
necessary but not sufficient on its own:
- Reuse the existing Traefik per-source-IP recipe from
  `deploy/coolify/ingress.md` (30 req/min average, burst 10).
- **In addition**, application-layer per-code failed-attempt counter
  on `owner_codes.FailedAttemptCount`. 5 failures → code revoked
  automatically (`RevokedAtUtc = now`, `RevokedReason = 'brute_force_detected'`)
  and the owner is notified via the SPA + (when email infra exists,
  see roadmap) email.
- Application-layer per-IP exponential backoff that lives in the
  install module — Traefik handles steady-state, app handles
  per-attacker burst.

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

- Reuse the existing `tokenStore` setup; add a second token context for
  staff tokens alongside the platform-admin one. (Verify the existing
  SPA actually has an RTK Query layer — the current
  `src/AFK4.Platform.Web/package.json` only lists React/Vite/Vitest,
  no `@reduxjs/toolkit`, so RTK Query may not be in use yet; pick the
  simplest data-fetching approach already established by the existing
  admin pages.)
- Floor-map editor: pick a small drag library (e.g. `dnd-kit`) over
  rolling our own.
- Live device updates: extend the existing SignalR hub or add a new
  `devices` hub. **Briefly:** one hub at `/hubs/devices`, clients
  join group `branch:{branchId}` on subscribe, server broadcasts
  `DeviceStatusChanged` (deviceId, branchId, status, seatId,
  lastHeartbeatAtUtc) on state change. Discovered in Slice 1.4 spec.
- Tests: the SPA uses **Vitest + React Testing Library + jsdom**
  (current `devDependencies`). Use the same for component tests on
  the new screens. For the floor-map drag-and-drop interaction add a
  Vitest+JSDOM unit-style test on the editor's pure state-transition
  reducer first; only add a Playwright (or equivalent) end-to-end
  test if the JSDOM test cannot cover a real bug class. No
  Playwright dependency is added unless that test is actually
  needed.

### Coolify deployments

Two SPA Coolify apps from the same Dockerfile + git ref, distinguished
by build arg:

| Host | Build arg | Audience |
|---|---|---|
| `platform.afk4.staging.mubi.dev` (existing) | `VITE_AUDIENCE=admin` | Mubi admin only |
| `app.afk4.staging.mubi.dev` (new) | `VITE_AUDIENCE=club` | Customer dashboard only |

Both run on `deploy/coolify/platform-web.Dockerfile`. DNS A/AAAA record
for the new `app.*` host added before first deploy. Traefik labels
mirror the existing platform-web app. The existing
`platform.afk4.staging.mubi.dev` keeps its current uptime monitor and
ingress configuration unchanged.

Plus a third unrelated Coolify app for Slice 4 (public landing) on
`www.afk4.staging.mubi.dev` — own Dockerfile, own DNS, own cert. Not
sharing anything with the SPA apps.

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
2. **Slice 1.2** — Floor-map editor API gap: ETag-aware
   `GET /api/branches/{branchId}/floor-map` (extend existing
   endpoint) + new bulk
   `PUT /api/branches/{branchId}/floor-map` with `If-Match`. Add
   `RequireManualDeviceApproval` to `BranchEntity` + branch settings
   endpoint to toggle it. Add default-zone auto-create hook in the
   existing branch-provisioning service (verify it isn't already
   there). Migration is small — schema delta is one bool column +
   any nullable `PositionX/Y` columns on `seats` if decision (b) of
   Risk #5 is chosen.
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

### Slice Status

- **Slice 1.1** — completed on `main`: owner-code entity, generate/rotate/
  summary endpoints, active lookup service, hashed storage, expiry/last-used,
  audit, and DB one-active-code-per-staff invariant.
- **Slice 1.2** — completed on `main`: floor-map ETag reads and bulk PUT,
  branch `RequireManualDeviceApproval` settings, default-zone provisioning, and
  bulk-delete protection for seats with active device assignments or session
  history.
- **Slice 1.3** — completed on `main`: install discover/enroll endpoints,
  device role/display/enrollment-state persistence, owner-code scoped branch
  discovery, auto-approved vs pending enrollment, rejected/success audit,
  per-code five-strike owner-code revocation for resolved install failures,
  app-layer install backoff, and Coolify ingress rate-limit recipe updates.
- **Slice 1.4** - completed on `main` (original branch
  `codex/slice-1.4-devices-admin`):
  branch device inventory now carries display name, role, and enrollment
  state; pending-device queue plus approve/reject/rename/move-seat/remove
  staff APIs are implemented with audit records, credential revocation on
  reject/remove, active-session remove protection, removed-device list hiding,
  and `deviceStatusChanged` notifications for admin state changes.
- **Slice 2.1** - completed on `main` (original branch
  `codex/slice-2.1-platform-web-routes`): the existing `Platform.Web`
  platform-admin tenant list/create/detail screens now resolve under
  `/admin`, `/admin/tenants`, `/admin/tenants/new`, and
  `/admin/tenants/{organizationId}`. Legacy root-level admin bookmarks
  (`/`, `/tenants`, `/tenants/new`, `/tenants/{organizationId}`) redirect with
  `history.replaceState`; in-app transitions use admin-prefixed URLs and
  `popstate` handles browser back/forward. No new `/auth` or `/club` screens
  are included in this slice.
- **Slice 2.2** - completed on `main` (original branch
  `codex/slice-2.2-auth-pages`):
  `Platform.Web` now has public `/auth/accept-invite` and `/auth/sign-in`
  pages wired to the existing accept-invite and staff sign-in endpoints.
  Staff auth uses a separate session store from platform-admin auth, successful
  accept/sign-in redirects to `/club`, `/auth/forgot-password` and
  `/auth/reset-password` are reserved, and `/club` is only a minimal signed-in
  handoff until the Slice 2.3 customer dashboard screens land.
- **Slice 2.3** - completed on `main` (original branch
  `codex/slice-2.3-club-dashboard`): `Platform.Web` now resolves the first
  customer dashboard MVP routes under `/club/*`. Successful accept-invite and
  staff sign-in redirect to `/club/install`; the signed-in club shell has
  install/owner-code generate-rotate, overview KPI, branch list/detail,
  branch settings, ETag floor-map editor, devices/pending-device actions, and
  operators screens backed by the existing staff APIs. This slice does not add
  new backend branch-creation/deactivation contracts or SignalR in the
  customer SPA.
  Follow-up UX/contract gap: `/auth/sign-in` still exposes a raw Organization
  GUID field because the current staff sign-in API requires `OrganizationId`.
  This violates the PRD requirement that normal club users must not copy
  organization/branch GUIDs. Replace it with a tenant resolution flow before
  pilot evidence: resolve by invite/session/host/slug or by unique staff login,
  and show a controlled tenant-picker or support error only when a staff login
  is legitimately ambiguous across organizations.
- **Slice 2.4** - completed on `main` (original branch
  `codex/slice-2.4-terminology`): Platform Web visible copy now uses setup
  codes for owner onboarding, owner codes for the Windows setup wizard, and
  tenant/branch keys instead of visible slug labels. Public
  operations docs now call the legacy `/device-enrollment-codes` path PC
  enrollment codes while preserving endpoint names.
- **Slice 2.5** - completed on `main` (original branch
  `codex/slice-2.5-platform-web-audience`): Platform Web now reads
  `VITE_AUDIENCE=admin|club|all`, gates route resolution/rendering so the admin
  build exposes `/admin/*` + `/auth/*` and the customer build exposes
  `/club/*` + `/auth/*`, redirects the customer host root to `/club/install`,
  and builds both audience variants. The shared Dockerfile accepts
  `VITE_AUDIENCE`, and Coolify ingress/runbook docs define the existing
  `platform.afk4.staging.mubi.dev` admin app plus the new
  `app.afk4.staging.mubi.dev` customer app; the customer app is now deployed in
  staging.
- **Slice 3.1** - completed on `main` (original branch
  `codex/slice-3.1-setup-wizard`): added `AFK4.SetupWizard.exe` as a direct-
  debug WPF first-run wizard plus a tested `AFK4.SetupWizard.Core`. The wizard
  uses the staging install APIs to discover branches/floor maps by owner code,
  pick a free seat, add a missing seat through the new owner-code-scoped
  `/api/install/seats` endpoint, choose `gaming_pc` or
  `manager_workstation`, enroll with a stable local device public key, and
  write Agent bootstrap environment values.
- **Slice 3.2** - completed on `main` (original branch `codex/slice-3-2`):
  added the single
  `afk4-agent-<version>-<channel>.msi` WiX package. The package publishes
  `AFK4.SetupWizard`, installs Agent Service + Setup Wizard + update helper
  scripts, adds a Start Menu shortcut, writes a first-run pending marker and
  HKLM `RunOnce` entry, attempts interactive postinstall wizard launch, starts
  the Agent Service after successful wizard enrollment, and is verified by the
  local package build script. The legacy coordinated
  `afk4-gaming-pc` MSI has since been removed from the default package/update
  smoke path and remains only as an explicit staging fallback.
- **Slice 3.3** - completed on `main` (original branch `codex/slice-3-3`):
  Agent component version reporting and Platform API update checks are
  now role-aware, so
  `gaming_pc` devices pull `player-shell` and `manager_workstation` devices
  pull `operator-app` from the existing update channel while `agent-service`
  stays common. The build now produces a standalone
  `afk4-player-shell-<version>-<channel>.msi`; update metadata publishing uses
  separate Operator App, Agent, and Player Shell MSI artifacts; the Setup
  Wizard writes update-helper configuration after owner-code enrollment; and
  the MSI helper installs WebView2 before applying the Operator App MSI when
  needed. Agent restarts after Agent, Player Shell, and Operator App component
  installs so it reloads machine environment values written by the MSIs.
- **Slice 3.4** - current staging evidence: the single Agent MSI owner-code
  path reached internal Agent `0.1.29` on Windows 11 VM2. The run covered
  enrollment, update rollout, service restart/reboot recovery, automatic
  service startup after reboot, and no Setup Wizard rerun after upgrade. If
  strict slice sign-off requires the second `manager_workstation` role path,
  collect that from `0.1.29` or a newer green package; otherwise proceed to
  Slice 3.5 legacy installer retirement.
- **Slice 3.5** - implemented locally on
  `codex/slice-3-5-legacy-retirement`: the default client package build and
  `Package Smoke` workflow now produce/publish only the role-aware Operator
  App, Agent, and Player Shell MSI artifacts. The legacy coordinated
  `afk4-gaming-pc` MSI and staging bootstrapper stay behind explicit fallback
  switches for old staging device recovery and are no longer current pass
  evidence.

## Testing

- Unit + integration tests in `tests/AFK4.Platform.Api.Tests` for every
  new endpoint, including failure modes (revoked owner code, mismatched
  branch, exhausted seats, brute-force rate-limit).
- Extend `scripts/staging-smoke.py` with a Slice 1.4 walk:
  generate owner code → call discover → call enroll for a synthetic
  device → list pending devices → approve → rename → move seat →
  remove device.
- Manual end-to-end on a clean Windows 11 VM (the existing
  `docs/operations/real-device-windows-pc-smoke.md` rig) for Slice 3.4.
- Floor-map editor needs a Playwright (or equivalent) test for the
  drag-and-drop flow; current SPA test setup TBD during implementation.

## Risk + Open Questions

1. **Owner code brute-force — anchored spec.** 8 digits = 10⁸ keyspace.
   Defense in depth:
   - **Code length:** 8 digits, numeric only, no ambiguous chars
     (still pure digits for paste UX).
   - **Traefik per-source-IP rate-limit:** 30/min average, burst 10,
     reusing `deploy/coolify/ingress.md` recipe.
   - **Application per-code lock-out:** `FailedAttemptCount`
     incremented on every wrong-code lookup that resolves to a
     `StaffUser` (so each code's 5-strike budget is independent).
     Code auto-revoked at 5 with `RevokedReason='brute_force_detected'`.
     Owner is notified in the SPA immediately; via email when email
     infra ships (roadmap).
   - **Application per-IP exponential backoff:** sliding-window
     counter per source-IP across all `/api/install/*` calls; 50
     calls in 60s → 1s sleep on the response, 100 calls → 5s, 200
     calls → connection reset. Lives in the install module.
   - **Audit:** failed install.discover / install.enroll calls are
     audited with source IP (success is **not** audited per call —
     see Risk #11 on noise).
2. **Multi-organization owners — known limitation.** `StaffUser` is
   per-organization (one row per `OrganizationId`). A multi-club human
   gets one owner code per organization, not one master code. Captured
   in Product Decision #3. Cross-org single-code support is roadmap
   work and explicitly out of scope here.
3. **Club staff sign-in tenant resolution.** The current browser staff
   sign-in form asks for a raw `OrganizationId`, which is unacceptable for
   real club users and conflicts with the PRD. Before pilot, add a staff
   sign-in path that resolves tenant context from a non-GUID user-facing
   input or known context. If `UserName + Password` resolves to exactly one
   active organization, sign in directly. If it resolves to multiple
   organizations for a multi-club human, return a non-secret disambiguation
   challenge and let the user choose by organization/branch display name or
   stable slug, never by GUID. Keep all tenant resolution server-side and
   preserve the existing tenant-isolation checks on issued staff tokens.
4. **Cross-tenant data leakage in discover.** `POST /api/install/discover`
   accepts an owner code with no auth header — the code IS the credential.
   The endpoint must scope its response strictly to branches under the
   resolved `StaffUser`'s `OrganizationId`. Add an integration test that
   seeds two orgs, generates an owner code for org A's owner, and
   asserts org B's branches never appear in the response — must fail
   closed if the scoping changes later.
5. **Pending-device queue with auto-approve default.** If a device
   enrolls into the wrong seat by mistake, the owner uses `move-seat`
   to fix it — no re-install. The `RequireManualDeviceApproval` branch
   flag is OFF by default per Product Decision #8.
6. **Floor-map editor — coordinate vs SortOrder.** Existing schema has
   `SortOrder int` only (1-D). The new drag-and-drop editor wants 2-D
   layout. Two approaches:
   - (a) Keep `SortOrder` only — editor renders zones top-to-bottom,
     seats inside each zone wrap left-to-right. Lossy for irregular
     club layouts but no schema change.
   - (b) Add nullable `PositionX, PositionY int` to `seats` for
     editor-driven free positioning, fall back to `SortOrder` when
     null. Requires migration.
   **Decide during Slice 1.2.** Recommendation: (a) for the pilot,
   (b) only if a real pilot club asks for a non-linear seat layout.
6. **Floor-map concurrent edits — handled.** ETag/If-Match on bulk PUT
   (Product Decision #9). 409 on stale ETag → editor reloads and
   replays. No silent last-write-wins.
7. **SmartScreen warning for unsigned MSI.** Documented, accepted for
   pilot 1-2. Re-evaluate if a pilot club rejects the warning.
8. **MSI postinstall WPF launch — non-trivial.** SYSTEM context can't
   open an interactive WPF window. Wizard launches via `RunOnce` HKLM
   + `CreateProcessAsUser` against the interactive desktop session for
   the active install; silently no-ops on headless MDM/SCCM deploys,
   waits for the next interactive login. Operator can also start the
   wizard manually from the Start Menu shortcut. Specified in Slice 3
   step 2.
9. **WebView2 Runtime dependency for Operator App.** Operator App is
   WPF + WebView2 and fails on start without the Edge WebView2 Runtime.
   Agent probes for the runtime before installing Operator App, runs
   `MicrosoftEdgeWebView2Setup.exe /silent /install` if missing,
   surfaces install failure via heartbeat for the dashboard to show.
   Specified in Slice 3 step 4.
10. **Removing `bootstrap.ps1`** can break any in-flight legacy VM tests.
    Slice 3.5 preserves migration/recovery notes and explicit fallback
    switches for existing staging devices while removing the legacy bootstrap/
    coordinated MSI path from the default flow.
11. **`install.discover` audit noise.** Each wizard launch (30 PCs in
    a single onboarding) produces a discover call. Decision: audit
    **failed** discover lookups only (security signal), do not audit
    successful ones (operational noise with no investigative value).
    Successful enroll IS audited — that's the actual state change.
12. **Email infrastructure** is upstream of owner-notification on
    brute-force (#1), accept-invite email delivery, password reset
    (roadmap), and self-signup verification (roadmap). Until it
    exists, brute-force notifications surface only in the SPA. See
    roadmap section "Email sending infrastructure".
13. **Apex hostname collision** for landing avoided by putting Slice 4
    on `www.afk4.staging.mubi.dev`. API stays on apex, landing on
    `www.*` — no Traefik priority hacks.

## What Is Not In This Plan

Tracked in the post-onboarding roadmap
([`2026-05-24-afk4-roadmap-post-onboarding.md`](./2026-05-24-afk4-roadmap-post-onboarding.md))
as headings; each becomes its own implementation plan when its time
comes:

- Production environment separation (`coolify-prod-deploy.yml`,
  separate Postgres, separate uptime monitors, prod
  session-signing key).
- SignPath signing rollout (PR #45 scaffolded the workflow input,
  remaining work is application + secrets + cutover).
- Self-service signup from the public landing page (page exists in
  Slice 4; backend tenant creation flow is roadmap).
- Payments + billing UI.
- Audit log UI.
- Password reset flow.
- Mobile companion app.
- Multi-tenant white-label.
- **Email sending infrastructure** — upstream dependency for
  brute-force owner-notification (Risk #1) and several roadmap items.
  Until it ships, owner brute-force notifications surface only in the
  SPA.
- Cross-organization single owner-code (one human, one code, any
  number of clubs). Captured in Product Decision #3 — requires a
  new identity abstraction above `StaffUser`.
- Final prod domain acquisition + DNS/Coolify/copy cutover.

## Definition Of Done

All of the following must be true for this plan to be considered shipped:

- A platform admin can create a tenant in `/admin/tenants/new` (on
  `platform.afk4.staging.mubi.dev`) and email a clickable invite link
  to a real human.
- That human opens the link in a desktop browser, sets a password, and
  lands in `/club` (on `app.afk4.staging.mubi.dev`) without seeing any
  GUIDs, slugs, or curl commands.
- In `/club/install` they see an 8-digit owner code (with the
  rotate / failed-attempt-count UI) and a one-click MSI download.
- Both `platform.afk4.staging.mubi.dev` and `app.afk4.staging.mubi.dev`
  serve only their own routes — opening `/admin/*` on the customer
  host returns the public sign-in page or 404, and `/club/*` on the
  admin host does the same. Audience build flag verified.
- On a clean Windows 11 VM, double-clicking the downloaded MSI installs
  the Agent, the first-run wizard opens, the owner types their code,
  picks a branch + seat + role, clicks Finish, and within a minute the
  PC is visible in their dashboard with a green status indicator.
- On a second clean VM enrolled as `manager_workstation`, the WebView2
  Runtime is installed automatically if missing, Operator App
  auto-installs, launches, and shows a sign-in screen with the staging
  API host pre-configured. Operator signs in with staff creds and the
  app loads.
- `/api/install/discover` and `/api/install/enroll` are protected by
  the documented brute-force defense (8-digit codes, Traefik
  rate-limit, per-code 5-strike lock-out with auto-revoke, per-IP
  exponential backoff in the install module). An integration test
  exists for each layer.
- Floor-map editor uses ETag/If-Match — two concurrent edits in two
  browser tabs produce a 409 on the second save (not silent
  overwrite), and the editor prompts to reload-and-replay.
- `bootstrap.ps1` and the standalone `gaming-pc.msi` / `operator-app.msi`
  no longer ship from the publishing workflow.
- All existing staging-smoke checks (24/24 as of 2026-05-24) still pass,
  plus the new Slice-7 walk (generate owner code → discover → enroll
  synthetic device → list devices → move seat → remove device).
- `docs/operations/coolify-staging-deploy.md` reflects the new install
  flow as the documented onboarding path; the old curl-based steps are
  removed.
- A publicly-reachable landing site is live at
  `https://www.afk4.staging.mubi.dev/` in Russian + English with home,
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
