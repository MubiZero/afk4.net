# AFK4 Authenticode And CI Registration Design

Status: proposed for implementation
Last updated: 2026-05-16

## Purpose

This spec defines the next release-hardening slice after the Phase 13 WiX/MSI
client packaging work. The goal is to make AFK4 Windows client release jobs able
to Authenticode-sign MSI artifacts and optionally register signed update package
metadata with the backend, without binding the MVP to a specific cloud signing
or object-storage provider.

The approved first slice is a provider-neutral, secret-driven boundary:

- local and CI package builds remain possible without production secrets;
- Authenticode signing is enabled only when signing inputs are supplied;
- update metadata publishing and backend registration are enabled only when
  artifact hosting, ECDSA metadata signing, and backend credentials are supplied;
- no certificate, private key, ECDSA signing key, presigned URL, package
  request JSON, or MSI artifact is committed to source control.

## Current Baseline

AFK4 already has the following release pieces:

- `scripts/build-client-packages.ps1` publishes the Operator App, Agent
  Service, and Player Shell outputs and builds two WiX MSI artifacts:
  Operator App MSI and coordinated gaming-PC MSI.
- `.github/workflows/client-packages.yml` manually builds, tests, packages, and
  uploads unsigned MSI artifacts from a Windows runner.
- `AFK4.Update.Publisher` can publish any artifact path, including `.msi`,
  through either local file-system hosting or presigned HTTP PUT upload, then
  emit a signed `CreateUpdatePackageRequest` JSON file.
- `POST /api/branches/{branchId}/updates/packages` registers signed package
  metadata with staff bearer-token authorization and `updates.packages.manage`.
- Operator App update management can validate/reject/retire registered packages
  and create staged rollouts after package metadata is present.

The current gap is that CI only produces unsigned MSI artifacts and does not
offer a guarded path to create/register update package metadata for those MSI
artifacts.

## Goals

1. Add an Authenticode signing entrypoint for MSI artifacts.
2. Keep signing provider-neutral by using Windows `signtool.exe` and
   environment-provided certificate material or certificate-store selection.
3. Add a release publishing entrypoint that takes already-built MSI artifacts,
   calls `AFK4.Update.Publisher`, and writes per-package registration request
   JSON files.
4. Add an optional backend registration entrypoint that posts generated
   `CreateUpdatePackageRequest` JSON files to the existing Platform API.
5. Extend the GitHub Actions client package workflow with guarded signing,
   publishing, and registration steps that run only when required inputs and
   secrets are present.
6. Update runbooks so local, internal CI, and production release operators can
   understand which steps are artifact-only and which steps require secrets.

## Non-Goals

- No provider-specific Azure Trusted Signing, AWS, GCP, Key Vault, or object
  store SDK integration in this slice.
- No certificate procurement, renewal, or policy automation.
- No committed certificates, private keys, ECDSA update signing keys, presigned
  URLs, generated request JSON, or built package artifacts.
- No automatic stable-channel rollout creation.
- No change to Agent update validation, metadata signature verification,
  rollback, installer execution, or Player Shell trust boundaries.
- No replacement of the existing zip-based `scripts/publish-client-update.ps1`
  developer path.

## Design Decisions

### Authenticode Signing Boundary

Add `scripts/sign-client-packages.ps1`.

The script signs the MSI artifacts produced by `scripts/build-client-packages.ps1`.
It accepts either:

- `-CertificatePath` plus `-CertificatePasswordEnvVar` for a PFX file supplied
  by a release runner; or
- `-CertificateSha1` plus optional certificate store options for signing with a
  certificate already installed on the Windows runner.

The script also accepts:

- `-PackagePath` repeated for explicit artifact paths, or
  `-PackageDirectory` to sign all matching `*.msi` files under
  `artifacts/client-packages/`;
- `-TimestampUrl` for RFC 3161 timestamping;
- `-SigntoolPath` for deterministic local tests and unusual runner layouts.

The script does not discover or download certificates. It fails closed if
signing is requested but certificate inputs are incomplete. If signing is not
requested in CI, the workflow logs a clear skip message and still uploads
unsigned internal artifacts.

### Signed Package Metadata Publishing

Add `scripts/publish-client-msi-updates.ps1`.

This script consumes already-built MSI artifacts instead of republishing
projects. It maps:

- `afk4-operator-app-<version>-<channel>.msi` to one update package request
  for component `operator-app`;
- `afk4-gaming-pc-<version>-<channel>.msi` to two update package requests,
  one for component `agent-service` and one for component `player-shell`.

The coordinated gaming-PC MSI intentionally does not introduce a new
`gaming-pc` update component in this slice. The backend and Agent already share
the `agent-service` and `player-shell` vocabulary. Publishing two metadata
records for the same MSI keeps that contract stable while still allowing the
existing coordinated installer to update both runtime pieces together. The two
records may share artifact URI, size, and SHA-256; each still signs its own
canonical metadata payload, so signatures differ by component.

For each generated package request, the script invokes `AFK4.Update.Publisher`
with:

- organization id;
- component;
- version;
- channel;
- artifact path;
- artifact store kind (`file-system` or `http-put`);
- artifact hosting inputs;
- ECDSA metadata signing key source from file path or environment variable;
- release notes;
- output request JSON path under ignored `artifacts/update-packages/`.

The expected request filenames are:

- `operator-app-<version>-<channel>-request.json`;
- `agent-service-<version>-<channel>-request.json`;
- `player-shell-<version>-<channel>-request.json`.

The script reuses the existing publisher rather than duplicating SHA-256,
upload, signing, or JSON serialization behavior.

### Backend Registration Boundary

Add `scripts/register-update-package-requests.ps1`.

This script posts one or more generated request JSON files to:

```text
POST /api/branches/{branchId}/updates/packages
```

It accepts:

- `-PlatformBaseUrl`;
- `-BranchId`;
- `-RequestPath` repeated or `-RequestDirectory`;
- either `-AccessToken` or `-AccessTokenEnvVar`.

The script does not sign in, mint staff tokens, or manage credentials. CI must
receive a short-lived staff access token from a protected release environment
or a future purpose-built service credential flow. The current backend endpoint
and audit trail remain unchanged.

### CI Workflow Shape

Extend `.github/workflows/client-packages.yml` with optional inputs:

- `sign_packages`: boolean, default `false`;
- `publish_update_metadata`: boolean, default `false`;
- `register_update_packages`: boolean, default `false`;
- `organization_id`;
- `branch_id`;
- `artifact_store`;
- `hosting_root`;
- `public_base_uri`;
- `operator_artifact_upload_uri`;
- `operator_artifact_public_uri`;
- `gaming_pc_artifact_upload_uri`;
- `gaming_pc_artifact_public_uri`;
- `release_notes`.

The workflow remains useful in three modes:

1. **Artifact-only internal build:** build/test/package/upload MSI artifacts.
   No secrets are required.
2. **Signed artifact build:** additionally sign MSIs when Authenticode inputs
   are present.
3. **Release registration build:** sign, publish update metadata through
   `AFK4.Update.Publisher`, upload request JSON artifacts, and optionally post
   them to the Platform API when a staff access token is supplied.

Stable-channel runs should require signing and metadata publishing. The
implementation plan should encode that guard in scripts or workflow checks so a
stable release cannot silently proceed as unsigned artifact-only output.

### Testing Strategy

Tests should stay deterministic and should not require a real code-signing
certificate.

Recommended coverage:

- parser tests for all new PowerShell scripts;
- argument-construction tests for `sign-client-packages.ps1` using a fake
  `signtool` script path that records arguments;
- validation tests that signing fails when both PFX and store selector inputs
  are missing or mixed incorrectly;
- publish script tests that verify MSI file-to-component mapping and generated
  Update Publisher arguments with a fake dotnet executable;
- registration script tests that post request JSON to a local test listener or
  mocked HTTP endpoint and require bearer authorization;
- workflow text tests that require guarded signing/publishing/registration
  steps and upload of generated request JSON artifacts.

Full release verification still requires a real Windows signing certificate and
provider-specific artifact hosting in a protected environment, but that is a
production operations task outside this implementation slice.

## Security Rules

- Production Authenticode certificate material must come from CI secrets,
  secure runner storage, or a future signing provider.
- ECDSA update metadata signing key material must continue to use the existing
  file or environment-variable boundary; stable production publishing must use
  protected release secrets, not developer temp files.
- Backend registration must use an access token with
  `updates.packages.manage` for the target branch.
- Generated request JSON files may contain public artifact URIs and signatures;
  they are still build artifacts and stay under ignored `artifacts/` unless an
  operator explicitly downloads them from CI.
- Presigned upload URLs must never be printed in full in docs, committed files,
  or persistent logs beyond the release runner's protected execution logs.

## Rollout And Failure Behavior

- Signing failure fails the job when `sign_packages = true`.
- Metadata publishing failure fails the job when `publish_update_metadata =
  true`.
- Backend registration failure fails the job when `register_update_packages =
  true`.
- If optional flags are false, the workflow should log that the step was
  skipped and continue.
- The package state after registration remains `registered`; human or operator
  workflow validation is still required before rollout.

## Documentation Updates

The implementation should update:

- `docs/operations/client-packaging.md` with signing and registration commands;
- `docs/operations/update-package-publishing.md` with MSI release examples;
- `README.md` release command summary;
- `docs/progress/2026-05-12-vertical-slice-progress.md` with verification
  results and remaining production caveats.

## Open Production Decisions

These remain intentionally deferred:

1. Final Authenticode certificate authority and storage policy.
2. Whether production uses PFX injection, certificate store installation, Azure
   Trusted Signing, or another provider.
3. Final object storage/CDN provider and presigned URL generation automation.
4. Whether backend package registration should later use a first-class service
   credential instead of a short-lived staff access token.
