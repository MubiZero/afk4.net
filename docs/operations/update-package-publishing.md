# Update Package Publishing Runbook

Status: Phase 10 production-hosting boundary runbook
Last updated: 2026-05-14

## Purpose

This runbook describes the publishing path for AFK4 client update artifacts.
It prepares the metadata required by the existing Platform API package
registration endpoint without storing signing keys or binary artifacts in the
repository.

The publisher supports two artifact stores:

- `file-system` for local/dev hosting directories;
- `http-put` for production-style presigned object-storage upload URLs with a
  separate public CDN artifact URL.

The publisher supports two signing key sources:

- `--signing-key <path>` for local files outside the repository;
- `--signing-key-env-var <name>` for a PEM secret injected by a key vault,
  secret manager, CI environment, or deployment runner.

## Signing Key

Use an ECDSA P-256 private key in PEM format. Keep it outside the repository.
For local internal-channel experiments only, generate a temporary key with:

```powershell
openssl ecparam -name prime256v1 -genkey -noout -out C:\afk4-secrets\update-signing-key.pem
```

Production and stable-channel publishing must use a controlled production key
source. Prefer a key vault or secret manager that injects the PEM into a
short-lived environment variable on the release runner. Do not commit the key,
generated request JSON, or update artifacts.

## Publish A Ready Artifact

Use the publisher directly when an installer or zip artifact already exists.
For local/dev filesystem hosting:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project src/AFK4.Update.Publisher/AFK4.Update.Publisher.csproj -- `
  --organization-id 0c04d6c0-bfa8-4e26-9263-fc0d307d0f08 `
  --component agent-service `
  --version 1.2.3 `
  --channel internal `
  --artifact C:\builds\afk4-agent-service-1.2.3.zip `
  --hosting-root C:\afk4-updates `
  --public-base-uri https://updates.afk4.test/packages/ `
  --signing-key C:\afk4-secrets\update-signing-key.pem `
  --release-notes "Internal Agent Service validation build." `
  --output artifacts/update-packages/agent-service-1.2.3-internal-request.json
```

The tool:

- copies the artifact to `{hosting-root}/{component}/{channel}/{version}/`;
- computes the SHA-256 hash and size from the copied artifact;
- signs canonical package metadata with ECDSA P-256 SHA-256;
- writes a `CreateUpdatePackageRequest` JSON payload.

For production-style object storage/CDN publishing, first obtain a short-lived
presigned upload URL from the object-storage provider and decide the public CDN
URL that Agents will download from. Then publish with `http-put`:

```powershell
$env:AFK4_UPDATE_SIGNING_KEY_PEM = '<PEM supplied by key vault at runtime>'

& 'C:\Program Files\dotnet\dotnet.exe' run --project src/AFK4.Update.Publisher/AFK4.Update.Publisher.csproj -- `
  --organization-id 0c04d6c0-bfa8-4e26-9263-fc0d307d0f08 `
  --component agent-service `
  --version 1.2.3 `
  --channel stable `
  --artifact C:\builds\afk4-agent-service-1.2.3.zip `
  --artifact-store http-put `
  --artifact-upload-uri "https://storage-provider.example/upload-token" `
  --artifact-public-uri "https://cdn.afk4.example/packages/agent-service/stable/1.2.3/afk4-agent-service-1.2.3.zip" `
  --signing-key-env-var AFK4_UPDATE_SIGNING_KEY_PEM `
  --release-notes "Stable Agent Service release." `
  --output artifacts/update-packages/agent-service-1.2.3-stable-request.json
```

In `http-put` mode, the tool:

- computes SHA-256 and size from the exact local artifact bytes;
- uploads those bytes with HTTP `PUT` to the presigned upload URL;
- writes the public CDN URL into `CreateUpdatePackageRequest.ArtifactUri`;
- signs metadata containing the public CDN URL, hash, size, component, version,
  channel, and release notes.

## Build And Publish A Client Project

Use the wrapper when the artifact should be created from a project publish
output first:

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

The wrapper writes intermediate publish output and generated request JSON under
ignored `artifacts/update-packages/`.

For production-style object storage/CDN publishing through the wrapper:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-client-update.ps1 `
  -Component agent-service `
  -ProjectPath src/AFK4.Agent.Service/AFK4.Agent.Service.csproj `
  -Version 1.2.3 `
  -Channel stable `
  -OrganizationId 0c04d6c0-bfa8-4e26-9263-fc0d307d0f08 `
  -ArtifactStore http-put `
  -ArtifactUploadUri "https://storage-provider.example/upload-token" `
  -ArtifactPublicUri "https://cdn.afk4.example/packages/agent-service/stable/1.2.3/agent-service.zip" `
  -SigningKeyEnvVar AFK4_UPDATE_SIGNING_KEY_PEM `
  -ReleaseNotes "Stable Agent Service release."
```

## Register The Package

POST the generated JSON to:

```text
POST /api/branches/{branchId}/updates/packages
```

The staff token must include `updates.packages.manage`. After registration,
move the package through validation and rollout using
`docs/operations/client-update-rollout.md`.

## Configure Agent Verification

Export the matching public key and configure it on every Agent that can receive
the rollout:

```powershell
openssl ec -in C:\afk4-secrets\update-signing-key.pem -pubout -out C:\afk4-secrets\update-signing-public-key.pem
```

Set `Agent:UpdatePackageSigningPublicKeyPem` to the PEM content. The Agent
verifies the artifact SHA-256 and the ECDSA P-256 signature before invoking
the configured installer adapter. If the public key is missing, the Agent
reports the update as failed and does not install the package.

## Verification

Before widening a rollout:

- confirm the generated artifact URI is reachable by Agent devices;
- compare the generated SHA-256 with the hosted artifact;
- keep the generated request JSON as release evidence outside source control;
- confirm the presigned upload URL has expired after publishing;
- clear the signing-key environment variable from the release runner after use;
- register first in `internal`, then widen to `beta` and `stable`.
