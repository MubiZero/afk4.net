# Update Package Publishing Runbook

Status: Phase 10 first-slice operational runbook  
Last updated: 2026-05-14

## Purpose

This runbook describes the local publishing path for AFK4 client update
artifacts. It prepares the metadata required by the existing Platform API
package registration endpoint without storing signing keys or binary artifacts
in the repository.

## Signing Key

Use an ECDSA P-256 private key in PEM format. Keep it outside the repository.
For local internal-channel experiments only, generate a temporary key with:

```powershell
openssl ecparam -name prime256v1 -genkey -noout -out C:\afk4-secrets\update-signing-key.pem
```

Production and stable-channel publishing must use a controlled production key
location. Do not commit the key or generated update artifacts.

## Publish A Ready Artifact

Use the publisher directly when an installer or zip artifact already exists:

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
- register first in `internal`, then widen to `beta` and `stable`.
