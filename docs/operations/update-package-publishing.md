# Update Package Publishing Runbook

Status: Phase 10 production-hosting boundary runbook
Last updated: 2026-05-18

## Purpose

This runbook describes the publishing path for AFK4 client update artifacts.
It prepares the metadata required by the existing Platform API package
registration endpoint without storing signing keys or binary artifacts in the
repository.

The publisher supports three artifact stores:

- `file-system` for local/dev hosting directories;
- `http-put` for production-style presigned object-storage upload URLs with a
  separate public CDN artifact URL;
- `s3` for S3-compatible object storage such as MinIO.

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
$env:AFK4_UPDATE_SIGNING_KEY_PEM = '-----BEGIN EC PRIVATE KEY-----...example release runner secret...-----END EC PRIVATE KEY-----'

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

For staging MinIO publishing, use `s3`. The staging bucket is expected to be
publicly readable by Agents through:

```text
https://updates.afk4.staging.mubi.dev/afk4-updates-staging/
```

The publisher signs S3 `PUT` requests with AWS Signature Version 4 and writes
the same public URL shape that Agents later download:

```powershell
$env:AFK4_UPDATE_SIGNING_KEY_PEM = '-----BEGIN EC PRIVATE KEY-----...release runner secret...-----END EC PRIVATE KEY-----'
$env:AFK4_UPDATE_ARTIFACTS_S3_ACCESS_KEY = '<minio-access-key>'
$env:AFK4_UPDATE_ARTIFACTS_S3_SECRET_KEY = '<minio-secret-key>'

& 'C:\Program Files\dotnet\dotnet.exe' run --project src/AFK4.Update.Publisher/AFK4.Update.Publisher.csproj -- `
  --organization-id 0c04d6c0-bfa8-4e26-9263-fc0d307d0f08 `
  --component agent-service `
  --version 1.2.3 `
  --channel internal `
  --artifact C:\builds\afk4-gaming-pc-1.2.3-internal.msi `
  --artifact-store s3 `
  --s3-endpoint https://updates.afk4.staging.mubi.dev `
  --s3-bucket afk4-updates-staging `
  --s3-public-base-uri https://updates.afk4.staging.mubi.dev/afk4-updates-staging/ `
  --s3-access-key-env-var AFK4_UPDATE_ARTIFACTS_S3_ACCESS_KEY `
  --s3-secret-key-env-var AFK4_UPDATE_ARTIFACTS_S3_SECRET_KEY `
  --signing-key-env-var AFK4_UPDATE_SIGNING_KEY_PEM `
  --release-notes "Internal Agent Service validation build." `
  --output artifacts/update-packages/agent-service-1.2.3-internal-request.json
```

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

For MinIO/S3 through the wrapper, use `-ArtifactStore s3` and pass the endpoint,
bucket, public base URI, and secret environment-variable names:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-client-update.ps1 `
  -Component agent-service `
  -ProjectPath src/AFK4.Agent.Service/AFK4.Agent.Service.csproj `
  -Version 1.2.3 `
  -Channel internal `
  -OrganizationId 0c04d6c0-bfa8-4e26-9263-fc0d307d0f08 `
  -ArtifactStore s3 `
  -S3Endpoint https://updates.afk4.staging.mubi.dev `
  -S3Bucket afk4-updates-staging `
  -S3PublicBaseUri https://updates.afk4.staging.mubi.dev/afk4-updates-staging/ `
  -S3AccessKeyEnvVar AFK4_UPDATE_ARTIFACTS_S3_ACCESS_KEY `
  -S3SecretKeyEnvVar AFK4_UPDATE_ARTIFACTS_S3_SECRET_KEY `
  -SigningKeyEnvVar AFK4_UPDATE_SIGNING_KEY_PEM `
  -ReleaseNotes "Internal Agent Service validation build."
```

## Publish Ready MSI Packages

After `scripts/build-client-packages.ps1` has created MSI artifacts, publish
signed update metadata without republishing the projects:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-client-msi-updates.ps1 `
  -Version 1.2.3 `
  -Channel internal `
  -OrganizationId 0c04d6c0-bfa8-4e26-9263-fc0d307d0f08 `
  -PackageDirectory artifacts/client-packages `
  -OutputDirectory artifacts/update-packages `
  -ArtifactStore file-system `
  -HostingRoot C:\afk4-updates `
  -PublicBaseUri https://updates.afk4.test/packages/ `
  -SigningKeyPath C:\afk4-secrets\update-signing-key.pem `
  -ReleaseNotes "Internal MSI validation build."
```

The Operator App MSI generates one request JSON for `operator-app`. The
coordinated gaming-PC MSI generates two request JSON files, one for
`agent-service` and one for `player-shell`, both pointing at the same MSI
artifact.

For production-style object storage/CDN publishing:

```powershell
$env:AFK4_UPDATE_SIGNING_KEY_PEM = '-----BEGIN EC PRIVATE KEY-----...example release runner secret...-----END EC PRIVATE KEY-----'

powershell -ExecutionPolicy Bypass -File scripts/publish-client-msi-updates.ps1 `
  -Version 1.2.3 `
  -Channel stable `
  -OrganizationId 0c04d6c0-bfa8-4e26-9263-fc0d307d0f08 `
  -PackageDirectory artifacts/client-packages `
  -OutputDirectory artifacts/update-packages `
  -ArtifactStore http-put `
  -OperatorArtifactUploadUri "https://storage-provider.example/operator-upload-token" `
  -OperatorArtifactPublicUri "https://cdn.afk4.example/operator-app/stable/1.2.3/afk4-operator-app-1.2.3-stable.msi" `
  -GamingPcArtifactUploadUri "https://storage-provider.example/gaming-pc-upload-token" `
  -GamingPcArtifactPublicUri "https://cdn.afk4.example/gaming-pc/stable/1.2.3/afk4-gaming-pc-1.2.3-stable.msi" `
  -SigningKeyEnvVar AFK4_UPDATE_SIGNING_KEY_PEM `
  -ReleaseNotes "Stable Windows client release."
```

For staging MinIO, publish both MSI artifacts with:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-client-msi-updates.ps1 `
  -Version 1.2.3 `
  -Channel internal `
  -OrganizationId 0c04d6c0-bfa8-4e26-9263-fc0d307d0f08 `
  -PackageDirectory artifacts/client-packages `
  -OutputDirectory artifacts/update-packages `
  -ArtifactStore s3 `
  -S3Endpoint https://updates.afk4.staging.mubi.dev `
  -S3Bucket afk4-updates-staging `
  -S3PublicBaseUri https://updates.afk4.staging.mubi.dev/afk4-updates-staging/ `
  -S3AccessKeyEnvVar AFK4_UPDATE_ARTIFACTS_S3_ACCESS_KEY `
  -S3SecretKeyEnvVar AFK4_UPDATE_ARTIFACTS_S3_SECRET_KEY `
  -SigningKeyEnvVar AFK4_UPDATE_SIGNING_KEY_PEM `
  -ReleaseNotes "Internal MSI validation build."
```

## Register The Package

POST the generated JSON to:

```text
POST /api/branches/{branchId}/updates/packages
```

The staff token must include `updates.packages.manage`. Register generated
request JSON files with a short-lived token supplied outside the repository:

```powershell
$env:AFK4_UPDATE_REGISTRATION_TOKEN = 'example-short-lived-staff-access-token'

powershell -ExecutionPolicy Bypass -File scripts/register-update-package-requests.ps1 `
  -PlatformBaseUrl https://platform.afk4.example `
  -BranchId acfc0212-967f-4d84-94be-9003387b09c2 `
  -RequestDirectory artifacts/update-packages `
  -AccessTokenEnvVar AFK4_UPDATE_REGISTRATION_TOKEN
```

By default, registration leaves package state as `registered`. A human or
Operator App workflow can validate packages and create rollouts using
`docs/operations/client-update-rollout.md`.

For staging/internal smoke, the script can create a rollout immediately after
registration:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/register-update-package-requests.ps1 `
  -PlatformBaseUrl https://afk4.staging.mubi.dev `
  -BranchId acfc0212-967f-4d84-94be-9003387b09c2 `
  -RequestDirectory artifacts/update-packages `
  -AccessTokenEnvVar AFK4_UPDATE_REGISTRATION_TOKEN `
  -CreateRollouts `
  -RolloutComponent agent-service `
  -RolloutTargetKind device `
  -RolloutTargetDeviceId '<device-id>' `
  -RolloutReason "Internal Agent Service smoke rollout."
```

When GitHub Actions registers packages, the repository or environment variable
`AFK4_ALLOWED_PLATFORM_BASE_URLS` must contain the allowed Platform API base
URLs. The workflow compares the dispatch `platform_base_url` input with that
allowlist before using `AFK4_UPDATE_REGISTRATION_TOKEN`, so the token cannot be
sent to arbitrary dispatch input hosts.

The `Package Smoke` workflow can publish staging artifacts to MinIO and create
an internal device rollout when these repository variables/secrets are present:

- variables: `AFK4_STAGING_PLATFORM_BASE_URL`, `AFK4_STAGING_ORGANIZATION_ID`,
  `AFK4_STAGING_BRANCH_ID`, `AFK4_STAGING_MINIO_ENDPOINT`,
  `AFK4_STAGING_MINIO_BUCKET`, `AFK4_STAGING_UPDATE_PUBLIC_BASE_URI`,
  `AFK4_STAGING_UPDATE_TARGET_DEVICE_ID`, `AFK4_ALLOWED_PLATFORM_BASE_URLS`;
- secrets: `AFK4_UPDATE_SIGNING_KEY_PEM`,
  `AFK4_STAGING_MINIO_ACCESS_KEY`, `AFK4_STAGING_MINIO_SECRET_KEY`,
  `AFK4_STAGING_UPDATE_STAFF_USERNAME`,
  `AFK4_STAGING_UPDATE_STAFF_PASSWORD`.

Use exact MSI-compatible versions for automatic MSI smoke, for example
`0.1.3`. The backend and Agent tolerate prerelease metadata suffixes where
possible, but Windows Installer exposes `ProductVersion` as numeric fields.

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

The staging Gaming PC setup executable embeds
`deploy/coolify/staging-update-signing-public.pem` when
`scripts/build-client-packages.ps1` is called with
`-StagingUpdateSigningPublicKeyPath`. Keep the matching private key outside the
repository and use it only from a release workstation or secret-injected
runner.

## Verification

Before widening a rollout:

- confirm the generated artifact URI is reachable by Agent devices;
- compare the generated SHA-256 with the hosted artifact;
- keep the generated request JSON as release evidence outside source control;
- confirm the presigned upload URL has expired after publishing;
- rotate any staging object-store or staff credentials that were copied through
  chat, terminals, or ad hoc setup shells during smoke;
- clear the signing-key environment variable from the release runner after use;
- register first in `internal`, then widen to `beta` and `stable`.
