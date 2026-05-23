# AFK4 Client Packaging Design

Status: approved Phase 13 design
Last updated: 2026-05-20

## Purpose

This spec resolves the MVP packaging decision for AFK4 Windows client surfaces:
Operator App, Agent Service, and Player Shell.

The decision sits on top of the existing update architecture:

- the backend remains the rollout authority;
- update packages are registered as immutable signed metadata;
- Agents download, verify, install, report status, and recover through the
  already implemented external install, rollback, and restart adapters;
- production signing keys and binary artifacts stay outside source control.

## Decision Summary

AFK4 uses WiX-authored MSI packages as the MVP baseline for Windows clients.

- Operator App gets its own WiX/MSI installer.
- Agent Service and Player Shell share one coordinated gaming-PC WiX/MSI
  installer.
- MSIX is deferred for a future optional Operator App distribution channel.
- Agent Service and Player Shell do not use MSIX in the MVP because the gaming
  PC package needs service installation, elevated machine scope, recovery
  behavior, and controlled shell deployment.

## Package Surfaces

### Operator App Installer

The Operator App installer packages `AFK4.Operator.App` as a Windows desktop
application for cashier, manager, technician, and owner workstations. The
approved UI runtime is a .NET desktop host with WebView2 and built
React/TypeScript assets.

Rules:

- install with a predictable product identity and upgrade code;
- install or bootstrap the required WebView2 Runtime according to Microsoft
  supported redistribution guidance;
- install the built frontend assets alongside the desktop host without
  requiring a separate local web server;
- install without embedding tenant, branch, staff, device, or signing secrets;
- support silent install for managed club workstations;
- support repair, uninstall, and major upgrades through standard MSI behavior;
- remain compatible with the existing update package registration and rollout
  metadata.

The Operator App may later add an MSIX/App Installer channel if the product
needs a store-like or per-user deployment model. That future channel must not
replace the MSI baseline until update rollout, signing, and support workflows
are proven for the same operational requirements.

### Gaming-PC Installer

The gaming-PC installer packages `AFK4.Agent.Service` and
`AFK4.Player.Shell` together.

Rules:

- install per-machine and require administrator elevation;
- install or configure the Agent as a Windows Service;
- place the Player Shell in a stable path supervised by the Agent;
- install local update helper scripts for MSI install, rollback, and Agent
  restart scheduling;
- write only non-secret default configuration;
- never ship a device credential or enrollment token in the package;
- leave the PC manageable if Player Shell update or launch fails.

Device enrollment remains a runtime flow. A technician or authorized operator
creates a short-lived branch-scoped enrollment code, then the installer or
first-run Agent completes device enrollment and stores the issued device
credential in a machine-protected location.

## Update Artifact Model

The update system continues to treat package binaries as externally hosted
artifacts referenced by signed metadata.

The Phase 13 packaging target is:

- local and CI builds produce `.msi` artifacts under ignored `artifacts/`;
- the Update Publisher signs package metadata containing the public artifact
  URI, SHA-256 hash, size, component, version, channel, and release notes;
- Agents verify SHA-256 and ECDSA metadata signatures before invoking any
  installer process;
- Agent update execution calls an external helper or `msiexec.exe` through the
  existing `Agent:UpdateInstallerExecutablePath` and template options.

Recommended Agent install template for direct MSI execution:

```text
/i "{PackagePath}" /qn /norestart
```

Recommended helper-script model:

```text
powershell.exe -NoProfile -ExecutionPolicy Bypass -File install-afk4-update-msi.ps1 -PackagePath "{PackagePath}" -Component "{Component}" -Version "{Version}"
```

The helper-script model is preferred for production because it can normalize
MSI exit code `3010` to success, write deterministic logs, protect argument
quoting, and handle service restart scheduling separately from the Agent
process that invoked the update.

## Signing

AFK4 has two signing layers:

1. Update metadata signing with ECDSA P-256 SHA-256, already implemented by
   `AFK4.Update.Publisher` and verified by the Agent.
2. Windows Authenticode signing for distributable binaries and installers.

The MVP implementation may build unsigned local development MSIs, but stable
production packages must be Authenticode-signed before rollout. Signing
certificates and private keys stay outside source control. CI jobs may receive
short-lived signing material through the release runner environment only.

## CI Release Boundary

GitHub Actions or another release runner should execute the same local scripts
developers use.

Initial CI responsibilities:

- restore .NET and WiX tools;
- restore and build the Operator App frontend toolchain;
- build and test the solution;
- publish Windows client outputs;
- build Operator App MSI and gaming-PC MSI artifacts;
- upload build artifacts for internal validation;
- optionally call the Update Publisher with environment-provided signing key
  and presigned upload URL values.

Provider-specific object storage, CDN provisioning, key vault SDK integration,
and final production release promotion remain separate decisions unless the
presigned URL and environment-secret boundary proves insufficient.

## Rollback And Recovery

Rollback stays explicit and status-tracked:

- the backend marks rollout/package state transitions;
- Agents report `rollback-started`, `rolled-back`, or `failed`;
- rollback uses a previous known-good MSI or recovery package metadata;
- Agent restart is scheduled outside the current Agent process;
- the Agent must not unlock a PC or trust Player Shell state because an update
  completed.

Before production Agent rollout, AFK4 must test:

- successful gaming-PC install;
- interrupted Agent update recovery;
- Player Shell update failure while Agent remains installed;
- rollback to a previous known-good gaming-PC package;
- Operator App repair/uninstall/upgrade.

## Out Of Scope

This decision does not introduce:

- a local club server;
- a web admin panel;
- Linux or macOS clients;
- kernel drivers;
- Windows Store distribution;
- provider-specific object-store/CDN SDKs;
- committed signing keys, certificates, package artifacts, or generated
  release JSON.
