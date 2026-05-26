# Cost-Aware GitHub Actions CI Design

Status: Approved for implementation planning
Date: 2026-05-16

## Purpose

AFK4 needs mandatory CI gates before pilot and commercial production, but
GitHub Actions billing must be used carefully. The goal is to add reliable PR
verification without creating unnecessary paid Windows runner usage.

This design replaces the earlier self-hosted CI option for the current stage.
GitHub Actions billing is enabled, and GitHub-hosted runners may be used, but
workflow design must keep paid minutes predictable and low.

## Scope

This design covers repository CI only:

- pull request build and test gates;
- packaging smoke checks for client MSI artifacts;
- manual release packaging controls;
- cost controls for GitHub Actions usage.

It does not cover staging deployment, production infrastructure, signing key
storage, CDN/object storage, or branch protection configuration in the GitHub
UI.

## Constraints

- AFK4 includes WPF/WindowsDesktop projects and WiX/MSI packaging, so the
  authoritative full solution gate must run on a Windows runner.
- GitHub-hosted Windows runners consume paid GitHub Actions minutes after the
  account allowance is exhausted.
- Remote CI should not be triggered manually unless there is a concrete reason.
- Packaging and release jobs should not run automatically for every small PR
  update.
- CI must not require production signing, publishing, or registration secrets
  for normal pull requests.

## Recommended Workflow Set

### PR Verification

Create a pull request workflow for changes targeting `main`.

The workflow should run a single Windows job that:

1. checks out the repository;
2. installs/restores the expected .NET toolchain state;
3. runs `dotnet tool restore`;
4. runs `dotnet build AFK4.sln` with the repository's standard flags;
5. runs `dotnet test AFK4.sln` with the repository's standard flags.

The workflow should use:

- `concurrency` to cancel older runs for the same branch or pull request;
- explicit `timeout-minutes`;
- path filters where safe, especially to avoid expensive Windows jobs for
  documentation-only changes;
- minimal artifact retention.

### Packaging Smoke

Create a separate packaging smoke workflow or separate job that runs less often
than PR verification.

Recommended triggers:

- push to `main`;
- manual `workflow_dispatch`;
- optionally pull requests that touch installer, packaging, update, or release
  automation paths.

The packaging smoke should build local MSI artifacts using the existing script:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 -Version 0.1.0-ci -Channel internal
```

The job should verify that both expected MSI artifacts are produced. It should
upload artifacts only when needed, with short retention.

### Release Packaging

Release packaging remains manual.

Signing, metadata publishing, and package registration should be gated behind
explicit `workflow_dispatch` inputs and required secrets. A normal PR or push
must not publish update metadata, register packages, or require production
credentials.

## Cost Controls

CI implementation must include these controls from the first version:

- no broad matrix unless there is a specific compatibility reason;
- no duplicate Linux and Windows full-solution jobs when the Windows job is the
  authoritative gate;
- `concurrency.cancel-in-progress` for PR updates;
- conservative `timeout-minutes`;
- short artifact retention;
- path filtering for docs-only changes where practical;
- manual release workflows for expensive package, signing, publishing, and
  registration operations.

Developers and agents should treat remote GitHub Actions runs as paid shared
infrastructure. Prefer local verification before pushing repeated trial commits
or manually re-running workflows.

## Failure Handling

PR verification failures should block merge once branch protection is enabled.

Packaging smoke failures should block release preparation and should be fixed
before signed client artifacts are produced.

If GitHub-hosted Windows runner availability, cost, or billing becomes a
problem, the fallback is a self-hosted Windows runner connected to GitHub
Actions. A Linux VPS can host auxiliary services, but it cannot replace the
Windows runner for WPF and MSI coverage.

## Testing Strategy

Before this CI slice is considered complete:

- workflow syntax must be validated by GitHub after push;
- equivalent local commands must be run from `D:\afk4.net`;
- the first pull request run must complete successfully;
- packaging smoke must produce the Operator App MSI and coordinated gaming-PC
  MSI at least once in GitHub Actions or through the same script locally if the
  remote run is intentionally deferred to save minutes.

## Implementation Notes

The implementation plan should prefer a small initial workflow over a large
release automation rewrite. The first merge should establish mandatory build
and test confidence. Packaging smoke can be added in the same branch if it stays
focused and does not require production secrets.
