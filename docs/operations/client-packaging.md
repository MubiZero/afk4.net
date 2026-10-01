# Client Packaging Runbook

Status: single client master installer (Burn bundle) carrying everything it needs (self-contained programs + offline WebView2); wizard installs role apps
Last updated: 2026-10-01

## Purpose

This runbook records the approved packaging direction for AFK4 Windows
clients and links the operational package flow to the existing update rollout
system.

Historical design note:

- `docs/archive/superpowers/specs/2026-05-14-afk4-client-packaging-design.md`

Historical implementation plan:

- `docs/archive/superpowers/plans/2026-05-14-afk4-phase13-client-packaging-ci.md`

## Packaging Decision

AFK4 ships **one master installer** — `afk4-client-<version>-<channel>.exe`, a
WiX Burn bundle — that operators run to provision any Windows client machine.

The installer **carries everything a club PC needs**. Nothing is downloaded at
install time and nobody is told to go and install something (owner decision of
2026-10-01; growth of the package is accepted).

1. **.NET travels inside every program.** The Agent Service, Setup Wizard, Player
   Shell and Organization Admin are all published **self-contained** for `win-x64`
   (`scripts/build-client-packages.ps1`, one publish loop, no per-project switch),
   so each program's own folder holds its runtime. No PC needs a .NET install, the
   bundle chains no .NET prerequisite and the component MSIs do not check for one.
   Each MSI harvests the whole publish directory (`Files Include="...\**"`), so the
   few hundred runtime files per program travel without being listed by hand; the
   agent service and the wizard folders are copied to the MSI inputs with their
   subfolders. Nothing launches `dotnet`: the agent starts the Player Shell by
   `PlayerShellExecutablePath`, the wizard starts Organization Admin by its
   installed path, the service is registered with its own `AFK4.Agent.Service.exe`,
   and the update helper scripts call only `msiexec`, `sc.exe` and PowerShell.
2. **WebView2 travels inside the bundle.** Every window a human sees is WebView2,
   so the bundle embeds Microsoft's **Evergreen Standalone Installer (x64)**
   (`MicrosoftEdgeWebView2RuntimeInstallerX64.exe`, about 210 MB, `Compressed`, no
   `DownloadUrl`) and runs it silently (`/silent /install`) first, only when none of
   the three EdgeUpdate registry keys reports a runtime.
3. Then the bundle installs the **Agent MSI**.

The build script downloads the WebView2 installer from Microsoft's stable link
(`https://go.microsoft.com/fwlink/?linkid=2124701`, the "Evergreen Standalone
Installer" row of the WebView2 download page) into
`artifacts/client-packages/runtime-cache/` and reuses it while the file keeps a valid
Microsoft Authenticode signature. The link is evergreen (a different file every
time), so there is no SHA to pin: the signature (`O=Microsoft Corporation`) is the
check, and a file that fails it is deleted and the build stops.

The Visual C++ Redistributable is deliberately not carried: WPF self-contained ships
its own native libraries and the BAFunctions DLL is linked statically (`/MT`).

Rough size: the bundle goes from about 70 MB (shared runtime) to about 450 MB: four
self-contained programs (compressed roughly 37 MB for the service and 65 MB for each
WPF program, measured as zip of a `win-x64` publish) plus about 210 MB for WebView2.
Measure the real `.exe` on the build machine; the numbers above are an estimate.

Standalone, the Panel and Player Shell MSIs still refuse to install when WebView2 is
missing, in Russian, pointing at `afk4-client-<version>-<channel>.exe` and at the
WebView2 download for people who insist on installing the MSI alone. The update helper
(`install-afk4-update-msi.ps1`) installs WebView2 before the Panel MSI when the
runtime is missing (it can only download, an update package cannot carry 210 MB).

The bundle window speaks Russian (`installers/bundle/BundleUi.ru.wxl`, the default) and
Tajik (`BundleUi.tg.wxl`, picked by WixStdBA from the Windows UI languages through the
`1064\thm.wxl` subfolder; force it with `afk4-client.exe /lang 1064`).

The component MSIs are build inputs (written to `artifacts/client-packages/intermediates/`);
the single deliverable handed to operators is the bundle `.exe`. The component MSIs are
also still produced as standalone artifacts for update-pipeline publishing and recovery.

- **Agent MSI.** Installs the Agent Service, the WPF Setup Wizard, update helper
  scripts, a Start Menu shortcut, a per-machine first-run pending marker, and a
  HKLM `RunOnce` entry for the wizard. The MSI also attempts to launch the
  wizard after an interactive install. The service is registered for automatic
  startup but is not started by the MSI before enrollment writes bootstrap
  configuration; the wizard starts it after successful enrollment. Agent MSI
  upgrades skip first-run wizard registration for already enrolled machines.
  The Agent MSI **carries the Player Shell MSI in the wizard payload** at
  `…\Setup Wizard\payload\AFK4.Player.Shell.msi`. It does not carry the
  Organization Admin payload. It is the MSI the bundle installs.
- **Organization Admin MSI.**
- **Player Shell MSI** for `gaming_pc` devices. It installs the Shell and writes
  the Agent machine environment values needed to report `player-shell` version
  and supervise the Shell executable. It is delivered inside the Agent MSI's
  wizard payload (above) rather than handed out separately to operators.
- Agent, Player Shell, and Organization Admin component installs schedule an Agent
  Service restart so machine environment values written by MSIs are reloaded.
- MSIX is deferred as a future optional Organization Admin distribution channel.

The Setup Wizard installs the Player Shell — it is **not** a manual step. During
enrollment, the operator signs in (phone or login) and selects the device role.
For a **`gaming_pc`** role the wizard runs `msiexec /qn` on the bundled
`AFK4.Player.Shell.msi`, then starts the Agent, which supervises the Shell. The
Player Shell is **not** installed on manager (operator) workstations.

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

The packaging model changes the artifact shape from ad hoc published zip
outputs toward MSI artifacts (delivered to operators via the single Burn bundle
for fresh provisioning). It does not change backend rollout authority or Agent
signature verification: the standalone component MSIs remain the source the
update pipeline publishes from.

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
logs and keeps Agent restart scheduling outside the currently running Agent
process. The script must pass the msiexec exit code through unchanged: code
`3010` means the package installed but Windows has to restart before the new
build runs, and the Agent reports that as `pending-restart` rather than
`installed`. Collapsing it to `0` tells the platform a fleet is updated while
it is still running the old build.

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
build script passes `-acceptEula wix7` to each `wix build` invocation after
explicit project approval. Building the Burn bundle needs the WiX **BootstrapperApplications** (the v7 name of
Bal) and **Util** extensions; the build script adds them.

Verify the local package build script parses:

```powershell
powershell -NoProfile -Command "[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path scripts/build-client-packages.ps1), [ref] `$null, [ref] `$null) | Out-Null"
```

Build Windows client package inputs, component MSI artifacts, and the master
installer bundle:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 `
  -Version 0.1.0-ci `
  -Channel internal
```

The script writes publish outputs under ignored
`artifacts/client-packages/publish/`, component MSIs (build inputs) under
ignored `artifacts/client-packages/intermediates/`, and the single deliverable
bundle under ignored `artifacts/client-packages/`.

Expected component MSI artifact names (build inputs, in `intermediates/`):

```text
afk4-organization-admin-<version>-<channel>.msi
afk4-agent-<version>-<channel>.msi
afk4-player-shell-<version>-<channel>.msi
```

Expected master installer (deliverable, in `artifacts/client-packages/`):

```text
afk4-client-<version>-<channel>.exe
```

The bundle wraps the Agent MSI for fresh provisioning and carries the offline
WebView2 runtime embedded inside it (the programs themselves are self-contained). The component MSIs are still produced as
standalone build inputs for recovery and as the source the update pipeline
publishes from.

`afk4-agent-<version>-<channel>.msi` is the Setup Wizard onboarding artifact
(enrollment is via the wizard's phone/login sign-in and role selection, and it
carries the Player Shell MSI in its wizard payload).
`scripts/publish-client-msi-updates.ps1` publishes role-aware update metadata
from the Organization Admin MSI, Agent MSI, and Player Shell MSI.

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
    "publicUri": "https://cdn.afk4.example/organization-admin/stable/1.2.3/afk4-organization-admin-1.2.3-stable.msi"
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
