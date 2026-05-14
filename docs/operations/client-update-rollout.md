# Client Update Rollout Runbook

Status: Phase 9 first-slice operational runbook  
Last updated: 2026-05-14

## Purpose

This runbook describes the MVP-safe rollout path for Operator App, Agent
Service, and Player Shell updates. Phase 9 stores package metadata, rollout
targets, and device status. Binary storage, production signing infrastructure,
and CI installer automation remain future implementation details behind these
contracts.

## Package Registration

Register package metadata only after the artifact is built and signed:

- component: `operator-app`, `agent-service`, or `player-shell`;
- version;
- channel: `internal`, `beta`, or `stable`;
- artifact URI;
- SHA-256 hash;
- signature;
- signature algorithm;
- size in bytes;
- release notes.

Stable-channel packages must not be registered without signature and hash
metadata. Package metadata is immutable after registration except explicit
state transitions such as rejected or retired.

## Rollout Sequence

1. Register the signed package in `internal`.
2. Create an internal rollout targeting one or more test devices.
3. Wait for device status reports:
   - `offered`;
   - `downloading`;
   - `downloaded`;
   - `installing`;
   - `installed`.
4. Investigate any `failed` status before widening scope.
5. Register or promote equivalent metadata for `beta`.
6. Roll out to a limited branch or device set.
7. Register or promote equivalent metadata for `stable`.
8. Roll out gradually by branch or explicit device targets.

## Device Behavior

The Agent calls:

```text
POST /api/devices/{deviceId}/updates/check
POST /api/devices/{deviceId}/updates/status
```

Both requests must use the device credential header. The backend decides whether
an update is eligible for that device. The Agent reports progress but does not
change rollout targeting or package metadata.

## Rollback

Rollback is represented as explicit rollout/status state, not deletion:

- `rollback-requested`;
- `rollback-started`;
- `rolled-back`.

When an Agent update fails, the PC must remain managed:

1. Keep the currently running Agent service in control until the replacement is
   verified.
2. Prefer staged download and prepared install before service restart.
3. Report failure with a useful message.
4. Keep Player Shell locked or session-controlled according to the current
   signed lease/runtime state.
5. Use a rollback package or previous known-good installer metadata.

## Out Of Scope For The First Slice

- package binary hosting provider;
- production signing key storage;
- CI-generated MSI/MSIX artifacts;
- Operator App rollout management UI;
- in-place Agent executable replacement implementation.
