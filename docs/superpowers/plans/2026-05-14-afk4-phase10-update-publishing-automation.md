# AFK4 Phase 10 Update Publishing Automation Plan

Status: production artifact hosting and key source boundary implemented
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
- a production-style `http-put` artifact store boundary for presigned
  object-storage upload URLs plus separate public CDN artifact URLs;
- an environment-variable signing key source so key vaults, CI secret
  managers, and controlled release runners can inject the ECDSA PEM without a
  key file on disk;
- focused tests around hash generation, URI generation, JSON output, and
  signature verification.

## Non-Goals

- No cloud-provider-specific object-store SDK integration yet.
- No direct key-vault SDK integration yet; the supported production boundary is
  secret injection through a controlled environment variable.
- No object-store bucket/container or CDN provisioning automation yet.
- No MSI/MSIX authoring toolchain selection yet.
- No GitHub Actions or CI release pipeline yet.
- No Operator App package/rollout management UI.

## Safety Rules

- Signing keys stay outside source control.
- The publisher signs metadata for the exact artifact URI, SHA-256, size,
  component, version, channel, and release notes hash.
- File-system artifacts are copied into deterministic component/channel/version
  paths.
- `http-put` artifacts are uploaded through short-lived presigned URLs while
  package metadata uses the separate public CDN artifact URL.
- The script writes build outputs under ignored `artifacts/`.
- Stable-channel publishing must use a production-controlled signing key and
  production artifact store, not a developer temp directory.

## Follow-Up

1. Add provider-specific object-store/CDN and key-vault SDK adapters only if the
   presigned URL and environment-secret boundary is not enough for production.
2. Decide MSI/MSIX/WiX packaging per Windows client surface.
3. Add CI release jobs only after the signing and hosting provider choices are
   explicit.
