# Agent And Player Shell Installer Enrollment Runbook

Status: Phase 9 first-slice operational runbook  
Last updated: 2026-05-14

## Purpose

This runbook describes the intended MVP bootstrap flow for a Windows gaming PC.
It does not define a production MSI build pipeline yet. The goal is to keep the
Agent Service, Player Shell, device credentials, and update channel configured
in a repeatable way.

## Preconditions

- The PC runs Windows 10/11.
- The branch exists in the AFK4 backend.
- An authorized technician or manager can create a short-lived PC enrollment
  code from the Operator App or API.
- Agent Service and Player Shell binaries are signed and come from an approved
  AFK4 distribution source.
- Production secrets and signing keys are not stored in the repository.

## Enrollment Flow

1. Install the coordinated gaming-PC package that contains:
   - `AFK4.Agent.Service`;
   - `AFK4.Player.Shell`;
   - default service configuration;
   - recovery/uninstall metadata for rollback.
2. Start the installer in enrollment mode.
3. Enter or scan the branch-scoped PC enrollment code created by authorized
   staff.
4. The installer or first-run Agent calls `POST /api/devices/enroll`.
5. The backend issues:
   - device id;
   - organization id;
   - branch id;
   - device credential secret.
6. Store the device credential using Windows-protected storage or an equivalent
   machine-protected secret location.
7. Write non-secret Agent configuration:
   - `Agent:PlatformBaseUrl`;
   - `Agent:OrganizationId`;
   - `Agent:BranchId`;
   - `Agent:DeviceId`;
   - `Agent:UpdateChannel`;
   - Shell executable path and pipe names.
8. Start or restart the Agent Service.
9. Verify heartbeat succeeds and the device appears in the Operator technician
   workflow.

## Safety Rules

- Never ship a hardcoded device credential in installer files.
- PC enrollment codes must be short-lived and branch-scoped.
- A device credential belongs to exactly one device id.
- If enrollment is repeated for replacement hardware, revoke old credentials
  through the backend.
- The Player Shell is not trusted for enrollment, billing, authorization, or
  update decisions.
- The Agent must remain installed and manageable after a failed Player Shell
  update.

## Manual Recovery

If enrollment or startup fails:

1. Stop the Agent Service.
2. Inspect local Agent logs and backend device command/status records.
3. Remove only the local AFK4 configuration and credential for this device if a
   clean re-enrollment is required.
4. Recreate a fresh PC enrollment code from an authorized technician account.
5. Re-enroll the device.
6. Revoke stale credentials for the previous enrollment.

Do not unlock a gaming PC manually as a substitute for a valid backend-approved
session command.
