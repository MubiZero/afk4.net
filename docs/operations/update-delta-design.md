# Delta updates by file manifest

Status: phase 1 (publish), 2a (agent engine) and 2b (apply, rollback, wiring) are built. The agent path is
controlled by `Agent:UpdateDeltaEnabled`, on by default since the Windows 11 VM acceptance on 2026-10-27: Agent,
Organization Admin and a running Player Shell updated 0.1.361 → 0.1.362 with 0.8–1.2 MB instead of 55–62 MB. Any
delta failure installs the full MSI. Known cosmetic gap: the Windows "Installed apps" version stays at the last
full MSI until the next one.

## Why a file manifest and not a binary diff of the MSI

Measured on two real Agent MSIs (0.1.342 → 0.1.345): a binary patch of the MSI saves ~9% (`zstd --patch-from`:
149 MB of 162.8 MB) because the files sit in a compressed cab and a change shifts everything after it. MSI
patches (`.msp`) do not apply either: every build gets a new ProductCode. A manifest is independent of the
container: between two builds on the same .NET runtime only the application files differ (the SDK that decides
the runtime is pinned in the packaging workflows, `AFK4_SHIPPED_DOTNET_SDK`).

## Shape

- Publisher (`--manifest-roots`): per component a signed manifest (every file: root, path, SHA-256, size,
  compressed size) and the file bodies, Brotli-compressed, content-addressed at `blobs/<aa>/<sha256>.br`.
  Roots: `agent-service` (+ `setup-wizard`), `player-shell`, `organization-admin`.
- The package signature (`AFK4-UPDATE-PACKAGE-V1`) is unchanged; the manifest has its own
  (`AFK4-UPDATE-MANIFEST-V1`). An agent that does not know the field installs the MSI as before.
- The full MSI stays: it is the fallback, the rollback anchor and the first-install path.

## Agent (phase 2a, `Updates/Delta`)

- `UpdateManifestVerifier`: size + SHA-256, ECDSA signature with the same public key as packages, then
  content: schema, component/version/channel equal to the instruction, blob base is HTTPS on the package host,
  known roots, **safe relative paths** (no `..`, no rooted or `:`/`\` paths: writes run as SYSTEM), valid hashes,
  no duplicates.
- `UpdateManifestClient`: fetches the manifest (≤ 8 MB, stall timeout) and returns it only if verified.
- `UpdateDeltaPlanner`: hashes the installed files per root and lists what to fetch. It never deletes
  anything. A plan with nothing to fetch, or whose download is not clearly smaller than the full MSI, is not
  applied: the MSI installs it (this also keeps the recorded version moving).
- `UpdateBlobFetcher`: downloads each distinct body once (4 in parallel), inflates it while hashing, rejects a
  wrong hash or a body that inflates past its declared size, and stages it outside the install folders.

## Phase 2b: apply, rollback, wiring

- `UpdateDeltaApplier`: Windows will not overwrite a DLL that a process has loaded, but it will rename it. Each
  file is copied next to its target as `.afk4new`, the old one is renamed to `.afk4old`, the new one takes its
  place; the running program keeps the old mapping until restart. Any error puts every replaced file back and
  deletes what was added. A journal is written before the first file: at service start, `applying` (power cut
  mid-swap) is rolled back, `applied` has its `.afk4old` copies deleted.
- `UpdateDeltaInstaller`: prepare (manifest → plan → fetch) and apply. Then it sets the same machine variable the
  component MSI writes (`Agent__AgentVersion`, `Agent__ShellVersion`, `Agent__OrganizationAdminVersion`) and
  schedules the agent restart, so the platform sees the new version. Install state goes through the usual store,
  so the existing recovery treats an interrupted delta like an interrupted MSI. The last-known-good MSI is not
  replaced: rollback after a later failure still has a full package.
- Coordinator: tries delta before downloading the MSI when the flag is on and the instruction has a manifest;
  `null` from any step means "install the MSI". The Player Shell gate wraps the swap like it wraps msiexec.
- `UpdateDeltaMaxDownloadRatio` (0.6): delta is used only when it downloads at most this share of the MSI.

## Open risks (live PC acceptance)

- Renaming loaded images works on NTFS, but antivirus or a file opened without `FILE_SHARE_DELETE` (a log in the
  install folder) makes the swap fail: it rolls back and installs the MSI. Watch for this on the pilot.
- Files change behind Windows Installer: Add/Remove Programs keeps the older version, and `msiexec /f` repair
  would restore MSI files. A later full MSI update still works as a major upgrade.
- A crash between swap and restart leaves the agent's own folder mixed until the next start, where the journal
  rolls it back. If the new agent cannot start at all, Windows service recovery restarts it; the journal is read
  by the new code — keep that code path dependency-light.
