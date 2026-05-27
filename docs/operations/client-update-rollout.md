# Client Update Rollout Runbook

Status: Staging-verified role-aware internal MSI rollout runbook
Last updated: 2026-05-27

## Purpose

This runbook describes the MVP-safe rollout path for Operator App, Agent
Service, and Player Shell updates. The staging internal path now has working
MinIO-backed artifact hosting, signed package metadata, package registration,
branch-targeted Operator App rollouts, device-targeted Agent Service rollouts,
Agent download/install/recovery, and backend status tracking. Production
signing custody, production storage/CDN policy, and physical-device release
validation remain release gates rather than active update-epic development
work.

For the current pilot/dev cycle, do not start new update branches unless a
fresh smoke exposes a regression. Fold remaining physical update and rollback
evidence into the real-device release validation loop.

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

Rollback has automated coverage at the Agent installer/recovery boundary and
must still be exercised on physical Windows hardware before commercial release.
It is not a separate near-term development branch unless real-device validation
finds a product defect.

## Remaining Release Gates

- production signing key storage;
- production object storage/CDN policy;
- service credential policy for package registration;
- physical Windows update and rollback evidence;
- Operator App rollout management UI polish for non-developer operation.
