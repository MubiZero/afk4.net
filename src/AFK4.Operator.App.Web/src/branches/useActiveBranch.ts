import { useCallback, useEffect, useState } from 'react';

const KEY = 'afk4.operator.activeBranchId';

export function useActiveBranch(branchIds: readonly string[]): { activeBranchId: string | null; select: (id: string) => void } {
  const [activeBranchId, setActiveBranchId] = useState<string | null>(() => {
    const stored = localStorage.getItem(KEY);
    if (stored && branchIds.includes(stored)) return stored;
    return branchIds[0] ?? null;
  });

  useEffect(() => {
    if (activeBranchId && branchIds.includes(activeBranchId)) return;
    const next = branchIds[0] ?? null;
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
