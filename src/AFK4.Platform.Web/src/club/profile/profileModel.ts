export interface PermissionGroup { key: string; permissions: string[]; }

export function groupPermissions(permissions: readonly string[]): PermissionGroup[] {
  const map = new Map<string, string[]>();
  for (const permission of [...permissions].sort()) {
    const dot = permission.indexOf('.');
    const key = dot === -1 ? permission : permission.slice(0, dot);
    const list = map.get(key) ?? [];
    list.push(permission);
    map.set(key, list);
  }
  return [...map.entries()].map(([key, perms]) => ({ key, permissions: perms }));
}

export interface BranchName { branchId: string; name: string; }

export function resolveBranchNames(
  branchIds: readonly string[],
  directory: Record<string, { name: string }>,
  fallback: string
): BranchName[] {
  return branchIds.map(branchId => ({ branchId, name: directory[branchId]?.name ?? fallback }));
}
