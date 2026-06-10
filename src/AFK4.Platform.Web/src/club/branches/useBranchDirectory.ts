import { useEffect, useRef, useState } from 'react';
import type { BranchApi } from '@/api/clients/branches';

export type BranchDirectory = Record<string, { name: string; city: string }>;

type Loadable = Pick<BranchApi, 'getBranchProfile'>;

export function useBranchDirectory(client: Loadable, branchIds: readonly string[]): BranchDirectory {
  const [directory, setDirectory] = useState<BranchDirectory>({});
  const clientRef = useRef(client);
  clientRef.current = client;
  const key = branchIds.join(',');

  useEffect(() => {
    let cancelled = false;
    const c = clientRef.current;
    const ids = key === '' ? [] : key.split(',');
    void Promise.all(ids.map(async (branchId) => {
      const profile = await c.getBranchProfile(branchId).catch(() => null);
      return profile === null ? null : { branchId, name: profile.name, city: profile.city };
    })).then(results => {
      if (cancelled) return;
      const next: BranchDirectory = {};
      for (const r of results) {
        if (r !== null) next[r.branchId] = { name: r.name, city: r.city };
      }
      setDirectory(next);
    });
    return () => { cancelled = true; };
  }, [key]);

  return directory;
}
