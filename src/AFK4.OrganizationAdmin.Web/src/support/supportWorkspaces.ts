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
