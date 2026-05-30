import { useCallback, useEffect, useState } from 'react';

const STORAGE_KEY = 'afk4.club.activeBranchId';

function readStored(): string | null {
  try {
    return typeof localStorage === 'undefined' ? null : localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function writeStored(branchId: string): void {
  try {
    if (typeof localStorage !== 'undefined') localStorage.setItem(STORAGE_KEY, branchId);
  } catch {
    /* ignore */
  }
}

export interface ActiveBranch {
  activeBranchId: string;
  select: (branchId: string) => void;
}

export function useActiveBranch(branchIds: readonly string[]): ActiveBranch {
  const [activeBranchId, setActiveBranchId] = useState<string>(() => {
    const stored = readStored();
    if (stored !== null && branchIds.includes(stored)) return stored;
    return branchIds[0] ?? '';
  });

  // Keep the selection valid if the set of available branches changes.
  useEffect(() => {
    if (activeBranchId !== '' && branchIds.includes(activeBranchId)) return;
    setActiveBranchId(branchIds[0] ?? '');
  }, [branchIds, activeBranchId]);

  const select = useCallback((branchId: string) => {
    if (!branchIds.includes(branchId)) return;
    setActiveBranchId(branchId);
    writeStored(branchId);
  }, [branchIds]);

  return { activeBranchId, select };
}
