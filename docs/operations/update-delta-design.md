# Delta updates by file manifest

Status: phase 1 (publish manifest + file bodies, contracts, API) is on `main`; phase 2a (agent engine:
verify, plan, fetch — not wired) is in review; phase 2b (apply, rollback, wiring behind a flag) is next.

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

## Phase 2b (not built)

Apply with a helper outside the service (stop service/shell, back up the files it replaces, move the staged
files in, start; restore the backup on any failure), the coordinator choosing delta before downloading the MSI,
and `Agent:UpdateDeltaEnabled` (off by default). Open risk to settle there: the files change behind MSI's back
(Add/Remove Programs version, `msiexec /f` repair), so a full MSI update must keep working as a major upgrade.
