# AFK4 Phase 10 Update Publishing Automation Plan

Status: first slice in progress  
Last updated: 2026-05-14

## Goal

Reduce manual risk in the update pipeline by adding a repeatable package
publishing path for already-built Operator App, Agent Service, and Player Shell
artifacts.

## Scope

This slice adds:

- a local `AFK4.Update.Publisher` CLI that copies an update artifact into a
  hosting directory;
- SHA-256 calculation over the exact hosted artifact;
- ECDSA P-256 signing of a canonical package metadata payload;
- generated `CreateUpdatePackageRequest` JSON ready for the existing Platform
  API package registration endpoint;
- a PowerShell wrapper that publishes a Windows client project, zips the
  publish output, and passes the artifact to the signing/publishing CLI;
- focused tests around hash generation, URI generation, JSON output, and
  signature verification.

## Non-Goals

- No production key vault integration yet.
- No cloud object-store SDK integration yet.
- No MSI/MSIX authoring toolchain selection yet.
- No GitHub Actions or CI release pipeline yet.
- No Operator App package/rollout management UI.

## Safety Rules

- Signing keys stay outside source control.
- The publisher signs metadata for the exact artifact URI, SHA-256, size,
  component, version, channel, and release notes hash.
- Published artifacts are copied into deterministic component/channel/version
  paths.
- The script writes build outputs under ignored `artifacts/`.
- Stable-channel publishing must use a production-controlled signing key and
  hosting root, not a developer temp directory.

## Follow-Up

1. Add a real production hosting provider adapter, likely object storage plus a
   CDN URL.
2. Decide MSI/MSIX/WiX packaging per Windows client surface.
3. Add CI release jobs only after the signing and hosting provider choices are
   explicit.
