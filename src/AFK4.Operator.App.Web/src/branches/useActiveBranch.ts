import { useCallback, useEffect, useState } from 'react';

const KEY = 'afk4.operator.activeBranchId';

export function useActiveBranch(branchIds: readonly string[]): { activeBranchId: string | null; select: (id: string) => void } {
  const [activeBranchId, setActiveBranchId] = useState<string | null>(() => {
    const stored = localStorage.getItem(KEY);
    if (stored && branchIds.includes(stored)) return stored;
    return branchIds[0] ?? null;
  });

  useEffect(() => {
    // branchIds arrives empty for a beat while the session is still loading (App passes
    // `authSession?.branchIds ?? []`, and authSession starts null) — that's "not known yet", not
    // "this session has zero branches". Treating it as the latter used to wipe the persisted
    // choice on every app boot, right before the real branchIds landed a render later.
    if (branchIds.length === 0) return;
    if (activeBranchId && branchIds.includes(activeBranchId)) return;
    const stored = localStorage.getItem(KEY);
    const next = (stored && branchIds.includes(stored)) ? stored : (branchIds[0] ?? null);
    setActiveBranchId(next);
    if (next) localStorage.setItem(KEY, next); else localStorage.removeItem(KEY);
  }, [branchIds, activeBranchId]);

  const select = useCallback((id: string) => {
    if (!branchIds.includes(id)) return;
    setActiveBranchId(id);
    localStorage.setItem(KEY, id);
  }, [branchIds]);

  return { activeBranchId, select };
}
