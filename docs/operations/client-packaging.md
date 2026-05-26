# Client Packaging Runbook

Status: current Windows client packaging runbook after Slice 3.4 VM evidence
Last updated: 2026-05-26

## Purpose

This runbook records the approved MVP packaging direction for AFK4 Windows
clients and links the operational package flow to the existing update rollout
system.

Detailed design:

- `docs/superpowers/specs/2026-05-14-afk4-client-packaging-design.md`

Implementation plan:

- `docs/superpowers/plans/2026-05-14-afk4-phase13-client-packaging-ci.md`

## Packaging Decision

AFK4 uses WiX-authored MSI packages as the MVP packaging baseline:

- Agent onboarding has a single `AFK4 Agent` MSI. It installs the Agent
  Service, the WPF Setup Wizard, update helper scripts, a Start Menu shortcut,
  a per-machine first-run pending marker, and a HKLM `RunOnce` entry for the
  wizard. The MSI also attempts to launch the wizard after an interactive
  install. The service is installed demand-start so it does not start before
  owner-code enrollment writes bootstrap configuration; the wizard switches it
  to automatic startup and starts it after successful enrollment. It does not
  carry Player Shell or Operator App payloads.
- Operator App has its own MSI.
- Player Shell has its own MSI for `gaming_pc` devices. It installs the Shell
  and writes the Agent machine environment values needed to report
  `player-shell` version and supervise the Shell executable.
- Agent, Player Shell, and Operator App component installs schedule an Agent
  Service restart so machine environment values written by MSIs are reloaded.
- The older coordinated gaming-PC MSI that contains Agent Service and Player
  Shell remains in the package build only as a legacy staging fallback until
  Slice 3.5 retires it from the default publishing/onboarding flow.
- MSIX is deferred as a future optional Operator App distribution channel.

This matches the current runtime model: the Agent is an elevated Windows
Service, the Shell is supervised by the Agent, and updates are centrally
managed through signed package metadata and staged rollout status.

## Update Pipeline Relationship

The existing update pipeline remains authoritative:

1. Build a client artifact.
2. Publish the artifact to file-system hosting or a production object store/CDN.
3. Sign package metadata with `AFK4.Update.Publisher`.
4. Register the generated package metadata in the backend.
5. Validate the package and create internal, beta, or stable rollouts.
6. Agents download, verify, install, report status, and recover through their
   configured install, rollback, and restart adapters.

Phase 13 changes the artifact shape from ad hoc published zip outputs toward
MSI artifacts. It does not change backend rollout authority or Agent signature
verification.

## Agent MSI Adapter Shape

The Agent already supports external update commands through:

- `Agent:UpdateInstallerExecutablePath`
- `Agent:UpdateInstallerArgumentsTemplate`
- `Agent:UpdateRollbackExecutablePath`
- `Agent:UpdateRollbackArgumentsTemplate`
- `Agent:UpdateRestartExecutablePath`
- `Agent:UpdateRestartArgumentsTemplate`

For local MSI experiments, the install command can point directly at
`msiexec.exe` with:

```text
/i "{PackagePath}" /qn /norestart
```

For production, prefer a helper script that wraps `msiexec.exe`, writes update
logs, treats MSI exit code `3010` as successful restart-required behavior, and
keeps Agent restart scheduling outside the currently running Agent process.

## Safety Rules

- Do not commit MSI artifacts.
- Do not commit Authenticode certificates, private keys, ECDSA update signing
  keys, generated package request JSON, or presigned upload URLs.
- Do not ship durable device credentials or enrollment tokens inside an
  installer.
- Register and roll out internal packages before beta or stable packages.
- Test rollback before production Agent rollout.

## Current Commands

Restore repository-local tools before packaging work:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
& 'C:\Program Files\dotnet\dotnet.exe' wix --version
```

Expected WiX version:

```text
7.0.0
```

WiX v7 requires explicit OSMF EULA acceptance for build and CI usage. The
Phase 13 build script passes `-acceptEula wix7` to each `wix build` invocation
after explicit project approval.

Verify the local package build script parses:

```powershell
powershell -NoProfile -Command "[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path scripts/build-client-packages.ps1), [ref] `$null, [ref] `$null) | Out-Null"
```

Build Windows client package inputs and MSI artifacts:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 `
  -Version 0.1.0-ci `
  -Channel internal
```

The script writes publish outputs under ignored
`artifacts/client-packages/publish/` and MSI artifacts under ignored
`artifacts/client-packages/`.

Expected MSI artifact names:

```text
afk4-operator-app-<version>-<channel>.msi
afk4-agent-<version>-<channel>.msi
afk4-player-shell-<version>-<channel>.msi
afk4-gaming-pc-<version>-<channel>.msi
```

`afk4-agent-<version>-<channel>.msi` is the owner-code Setup Wizard
onboarding artifact. `scripts/publish-client-msi-updates.ps1` now publishes
role-aware update metadata from the Operator App MSI, Agent MSI, and Player
Shell MSI. The legacy `afk4-gaming-pc` MSI is still built only for the staging
fallback path until Slice 3.5 removes it, and it is no longer used for
generated update package metadata.

## Authenticode Signing

Internal package builds may remain unsigned. Stable production package builds
must be Authenticode-signed before update metadata is published.

`scripts/sign-client-packages.ps1` supports two signing providers:

- **signtool (default)** - local PFX file or certificate store.
- **SignPath (cloud)** - submit artifacts to https://signpath.io for
  cloud-managed Authenticode signing. AFK4 qualifies for SignPath
  Foundation's free open-source EV signing tier because the repository is
  AGPL-3.0-or-later licensed.

### Option 1 - signtool with a local PFX

```powershell
$env:AFK4_AUTHENTICODE_PFX_PASSWORD = 'example-pfx-password-from-release-runner'

powershell -ExecutionPolicy Bypass -File scripts/sign-client-packages.ps1 `
  -PackageDirectory artifacts/client-packages `
  -CertificatePath C:\afk4-secrets\afk4-authenticode.pfx `
  -CertificatePasswordEnvVar AFK4_AUTHENTICODE_PFX_PASSWORD
```

### Option 2 - signtool with a certificate already in a Windows cert store

```powershell
powershell -ExecutionPolicy Bypass -File scripts/sign-client-packages.ps1 `
  -PackageDirectory artifacts/client-packages `
  -CertificateSha1 0123456789abcdef0123456789abcdef01234567 `
  -CertificateStoreLocation LocalMachine `
  -CertificateStoreName My
```

Both signtool options use `signtool.exe` from the Windows SDK and fail when
no signing source is configured. They do not download certificates or read
secrets from repository files.

### Option 3 - SignPath cloud signing

Once the SignPath Foundation (or commercial) project is approved, supply the
SignPath organization id, project slug, signing policy slug, and a CI user
token. The script auto-installs the `SignPath` PowerShell module on first
use, submits each MSI as a signing request, waits for completion, and
replaces the unsigned artifact with the signed version returned by
SignPath.

```powershell
$env:AFK4_SIGNPATH_API_TOKEN = 'example-ci-user-api-token'

powershell -ExecutionPolicy Bypass -File scripts/sign-client-packages.ps1 `
  -PackageDirectory artifacts/client-packages `
  -UseSignPath `
  -SignPathOrganizationId 11111111-1111-1111-1111-111111111111 `
  -SignPathProjectSlug afk4-client-packages `
  -SignPathSigningPolicySlug release-signing `
  -SignPathApiTokenEnvVar AFK4_SIGNPATH_API_TOKEN
```

Pass `-SignPathArtifactConfigurationSlug` only when the SignPath project
uses a non-default artifact configuration.

### CI

CI uses the same command through the manual GitHub Actions workflow:

```text
.github/workflows/client-packages.yml
```

The workflow restores .NET tools, builds and tests the solution, runs
`scripts/build-client-packages.ps1`, and uploads the generated MSI artifacts.
Guarded workflow switches can also sign MSI artifacts, publish update metadata,
upload generated request JSON, and register requests with the Platform API when
release inputs and protected secrets are supplied.

GitHub validates `workflow_dispatch` files on push and currently allows at most
25 top-level inputs. The manual release workflow stays below that limit by
using one JSON input for production-style HTTP PUT artifact URIs instead of six
separate upload/public URI fields. When `artifact_store=http-put`, pass
`http_put_artifact_uris_json` in this shape:

```json
{
  "operator": {
    "uploadUri": "https://storage-provider.example/operator-upload-token",
    "publicUri": "https://cdn.afk4.example/operator-app/stable/1.2.3/afk4-operator-app-1.2.3-stable.msi"
  },
  "agent": {
    "uploadUri": "https://storage-provider.example/agent-upload-token",
    "publicUri": "https://cdn.afk4.example/agent/stable/1.2.3/afk4-agent-1.2.3-stable.msi"
  },
  "playerShell": {
    "uploadUri": "https://storage-provider.example/player-shell-upload-token",
    "publicUri": "https://cdn.afk4.example/player-shell/stable/1.2.3/afk4-player-shell-1.2.3-stable.msi"
  }
}
```

The `signing_provider` workflow input selects between `authenticode-pfx` and
`signpath`. The provider-specific repository settings the guard step checks
for are:

- `authenticode-pfx`: secrets `AFK4_AUTHENTICODE_PFX_BASE64` and
  `AFK4_AUTHENTICODE_PFX_PASSWORD`.
- `signpath`: secret `AFK4_SIGNPATH_API_TOKEN` and repository variables
  `AFK4_SIGNPATH_ORGANIZATION_ID`, `AFK4_SIGNPATH_PROJECT_SLUG`,
  `AFK4_SIGNPATH_SIGNING_POLICY_SLUG` (and optional
  `AFK4_SIGNPATH_ARTIFACT_CONFIGURATION_SLUG`).

For older zip-based internal signed update experiments, the existing update
artifact publishing wrapper remains available:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-client-update.ps1 `
  -Component agent-service `
  -ProjectPath src/AFK4.Agent.Service/AFK4.Agent.Service.csproj `
  -Version 1.2.3 `
  -Channel internal `
  -OrganizationId 0c04d6c0-bfa8-4e26-9263-fc0d307d0f08 `
  -HostingRoot C:\afk4-updates `
  -PublicBaseUri https://updates.afk4.test/packages/ `
  -SigningKeyPath C:\afk4-secrets\update-signing-key.pem `
  -ReleaseNotes "Internal Agent Service validation build."
```

Production release jobs still need explicit certificate authority/storage and
object-store/CDN decisions before stable-channel rollout.
