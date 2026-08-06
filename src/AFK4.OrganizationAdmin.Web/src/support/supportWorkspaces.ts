import { permissionNames } from '../permissionNames';
import type { WorkspaceId } from '../operatorTypes';

// Which rail workspace a support grant's writable area (see PlatformSupportWritableAreas.cs on the
// server) unlocks. This is the ONE place that maps "what support can touch" to "what the rail
// shows" — screens must not grow their own ad-hoc `activeSupportSession && ...` conditions, or the
// mapping drifts out of sync with what the server actually enforces per endpoint.
//
// Money workspaces (cash, booking, players' billing, stock, dashboard reports) have no area here on
// purpose: nothing in `PlatformSupportWritableAreas` grants them, so they simply never appear in the
// map below and stay hidden under support — a missing rail button reads as "not this tool's job",
// a visible one that 403s reads as a broken product.
const AREA_TO_WORKSPACE: Record<string, WorkspaceId> = {
  'floor-map': 'map',
  'branch-settings': 'management',
  'branch-profile': 'management',
  staff: 'management',
  devices: 'network'
};

// Returns the workspaces a support session's `writableAreas` unlock. `null` outside support mode
// means "no restriction" — regular permission-based visibility (canOpenWorkspace) still applies.
export function supportVisibleWorkspaces(writableAreas: readonly string[]): ReadonlySet<WorkspaceId> {
  const workspaces = new Set<WorkspaceId>();
  for (const area of writableAreas) {
    const workspace = AREA_TO_WORKSPACE[area];
    if (workspace) {
      workspaces.add(workspace);
    }
  }
  return workspaces;
}

// Which WRITE permission(s) a writable area unlocks. Server-side, a support grant reaches a write
// endpoint only if that exact endpoint carries `.AllowPlatformSupportAccess(permission)` (see
// PlatformSupportSessionMiddleware.cs — the middleware trusts the endpoint's own tag, never
// anything the client claims to have). This list is the client-side mirror of exactly those tags.
// The two sides can't be cross-checked across languages by a single test, so treat
// AuthenticationDomainEndpointTests.PlatformSupportAllowlist_MatchesDeclaredAreas (in
// AFK4.Platform.Api.Tests) as the source of truth for what's actually tagged on the server, and
// update this map whenever that test's declared areas change. Notably, StaffEndpoints.cs tags
// only ManageBranchStaff (profile/state) — `roles` and `password-reset` are deliberately NOT
// tagged: either one is a route to money permissions (e.g. BranchManager) that a support grant's
// expiry wouldn't revoke, so `manageRoles` must never appear below. A write permission with no
// area here (manageRoles, manageTariffs, managePosCatalog, manageInventoryStock,
// managePaymentGateways, manageLoyaltySettings, manageNews, installDevice, openShift, ...) has no
// tagged write endpoint under support at all — granting it client-side would only dress up a
// button that's guaranteed to 403, which is the exact bug this map exists to avoid.
const AREA_TO_WRITE_PERMISSIONS: Record<string, readonly string[]> = {
  'branch-settings': [permissionNames.manageBranchSettings],
  'branch-profile': [permissionNames.manageBranchSettings],
  staff: [permissionNames.manageBranchStaff],
  'floor-map': [permissionNames.manageLayout],
  devices: [
    permissionNames.createDeviceEnrollmentCode,
    permissionNames.assignDeviceSeat,
    permissionNames.rotateDeviceCredential,
    permissionNames.revokeDeviceCredential,
    permissionNames.dispatchDeviceCommand
  ]
};

// The full client-side permission set a support grant should present as, derived from its
// `writableAreas` — not "everything minus one exception". Every read permission (name ending in
// `.view`, by convention across permissionNames.ts) stays granted: the server opens read access
// broadly for support regardless of writableAreas (see AREA_TO_WRITE_PERMISSIONS above — reads
// aren't areas-gated server-side, only writes are), and a screen support can't act on is still one
// worth seeing (audit trail, diagnosing what club staff is looking at). Write permissions are
// granted ONLY for areas actually present in the grant. `openShift` needs no special case: it
// isn't a `.view` permission and 'shifts' isn't one of the five writable areas, so the same two
// rules exclude it without help — see usePostAuthShiftGate.ts, which still reads its absence as
// "gate not required" exactly as before.
export function supportPermissions(writableAreas: readonly string[]): string[] {
  const readPermissions = Object.values(permissionNames).filter((permission) => permission.endsWith('.view'));
  const writePermissions = writableAreas.flatMap((area) => AREA_TO_WRITE_PERMISSIONS[area] ?? []);
  return [...new Set([...readPermissions, ...writePermissions])];
}
