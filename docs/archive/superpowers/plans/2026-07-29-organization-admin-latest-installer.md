# Organization Admin Latest Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish a verified stable Organization Admin installer URL from staging package CI without restarting or deploying the Platform API.

**Architecture:** Extend the existing S3 publisher with an optional stable alias object key. The immutable versioned upload and signed request remain unchanged; only Organization Admin publishing performs a second atomic S3 object replacement with `Cache-Control: no-store`, then Package Smoke verifies the public bytes and compatibility response. Configure the API once with the stable URL and never mutate Coolify from package CI.

**Tech Stack:** .NET 10, xUnit, PowerShell, GitHub Actions, S3-compatible MinIO, ASP.NET Core.

## Global Constraints

- Versioned package URIs remain immutable and authoritative for signatures, rollouts, audit, and rollback.
- The stable alias is only a compatibility-gate download target.
- Package publication must not call Coolify, restart the API, or deploy application code.
- Staging automation can write only the staging MinIO bucket and `internal` alias.
- Secrets must not appear in logs or artifacts.
- A failed alias upload or public-byte verification fails Package Smoke without exposing a partial object.

---

### Task 1: Optional Stable S3 Alias

**Files:**
- Modify: `src/AFK4.Update.Publisher/UpdatePackagePublishOptions.cs`
- Modify: `src/AFK4.Update.Publisher/UpdatePackagePublishCommand.cs`
- Modify: `src/AFK4.Update.Publisher/FileSystemUpdatePackagePublisher.cs`
- Test: `tests/AFK4.Update.Publisher.Tests/FileSystemUpdatePackagePublisherTests.cs`

**Interfaces:**
- Consumes: existing S3 settings and immutable artifact upload.
- Produces: optional `S3StableAliasObjectKey` / `--s3-stable-alias-object-key`; it sends the same bytes to that key with `Cache-Control: no-store`, while the signed request URI stays versioned.

- [ ] **Step 1: Write failing tests** for immutable plus stable PUTs, cache headers, unchanged request URI, non-S3 rejection, and unsafe-key rejection.
- [ ] **Step 2: Run RED:** `dotnet test tests/AFK4.Update.Publisher.Tests/AFK4.Update.Publisher.Tests.csproj --filter "FullyQualifiedName~StableAlias|FullyQualifiedName~Parse_ReadsS3Arguments"`; expect compile/assertion failure because the option is absent.
- [ ] **Step 3: Implement minimally:** add the option and CLI pair, validate a relative safe object key, refactor authenticated S3 PUT into one helper, open a new stream per PUT, and include `Cache-Control` in stable-request AWS signed headers.
- [ ] **Step 4: Run GREEN:** `dotnet test tests/AFK4.Update.Publisher.Tests/AFK4.Update.Publisher.Tests.csproj`; expect all pass.
- [ ] **Step 5: Commit:** `git add src/AFK4.Update.Publisher tests/AFK4.Update.Publisher.Tests && git commit -m "feat(updates): publish stable installer alias"`.

### Task 2: Package Smoke Wiring And Verification

**Files:**
- Modify: `scripts/publish-client-msi-updates.ps1`
- Modify: `.github/workflows/package-smoke.yml`
- Modify: `tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs`

**Interfaces:**
- Consumes: `-OrganizationAdminStableAliasObjectKey`, generated Organization Admin request JSON, staging public base URI, and Platform API base URL.
- Produces: stable URI `<public-base>/organization-admin/internal/latest/afk4-organization-admin-internal.msi` plus workflow proof that its bytes and the compatibility response match.

- [ ] **Step 1: Write failing tests** requiring the wrapper parameter, Organization Admin-only forwarding, stable verification step, and absence of Coolify/restart/deploy calls.
- [ ] **Step 2: Run RED:** `dotnet test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~ClientReleaseAutomationTests"`; expect missing-wiring assertions.
- [ ] **Step 3: Implement wiring:** pass the fixed internal alias key only to Organization Admin publishing. After registration, parse the request JSON, enforce HTTPS and configured-host containment, download the stable URI, compare length and SHA-256, then require HTTP 426 and an exact stable `downloadUrl`. Do not add Coolify credentials or calls.
- [ ] **Step 4: Run GREEN:** repeat the focused Agent tests; expect all pass.
- [ ] **Step 5: Commit:** stage only the workflow, wrapper, and focused tests, then commit `ci(updates): verify stable Organization Admin installer`.

### Task 3: Documentation And Staging Proof

**Files:**
- Modify: `docs/operations/update-package-publishing.md`
- Modify: `docs/operations/platform-organization-big-bang-cutover.md`
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Modify: `docs/superpowers/specs/2026-07-28-organization-admin-latest-installer-design.md`

**Interfaces:**
- Consumes: tested stable publishing from Task 2.
- Produces: publish/rollback runbook, production approval boundary, one-time API configuration, and staging evidence.

- [ ] **Step 1: Clarify S3 atomic semantics** in the spec: a completed PUT replaces the object atomically; failed PUTs expose no partial object, and public hash verification remains mandatory.
- [ ] **Step 2: Update runbooks** with stable versus immutable responsibilities, no-cache behavior, rollback by re-publishing verified immutable bytes, no-restart invariant, and separate signed production promotion.
- [ ] **Step 3: Run the relevant gate:** full Update Publisher tests, full Agent Service tests, and `git diff --check`; expect green.
- [ ] **Step 4: Commit documentation:** stage only the four listed documents and commit `docs(updates): document stable installer promotion`.
- [ ] **Step 5: Publish and prove staging:** push task commits to `main`, wait for Package Smoke, set the compatibility URL once to the stable URI through Coolify, perform one coordinated API restart, and verify health, exact 426 URL, stable hash/size, immutable request URI, deployed SHA/image identity, and that Package Smoke caused no API restart. Record the run evidence in progress documentation.
