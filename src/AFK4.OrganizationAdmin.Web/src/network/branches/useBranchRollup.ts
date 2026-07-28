import { useCallback, useEffect, useRef, useState } from 'react';
import { readString } from '../../operatorHelpers';
import { buildBranchRollup, type BranchRollupEntry, type BranchRollupViewModel } from './branchRollupModel';

export interface RollupClient {
  getOwnerBranches(): Promise<{ branchId: string; name: string }[]>;
  // BranchProfileDto is loosely typed on the client (Record<string, unknown>), same as the
  // dashboard-summary DTO — read fields defensively (readString etc.), don't assume shape.
  getBranchProfile(branchId: string): Promise<Record<string, unknown>>;
  getBranchSummary(branchId: string): Promise<Record<string, unknown>>;
}

export type BranchRollupState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; retry: () => void }
  // profiles carries the raw profile per branch (null if that branch's profile fetch failed) —
  // BranchesDestination needs the FULL profile, not just name/city, because updateBranchProfile
  // is a full-record PATCH (see branchProfileRequest.ts); the rollup only aggregates KPIs.
  | { status: 'ready'; data: BranchRollupViewModel; profiles: Record<string, Record<string, unknown> | null>; retry: () => void };

// Loads the org's branch list, then per-branch profile (name/city) + today's dashboard summary in
// parallel. A branch whose profile/summary call fails still shows up as a row (kpis: null) —
// only a failure of getOwnerBranches itself (the org-wide list) fails the whole screen.
export function useBranchRollup(client: RollupClient, unnamedLabel: string): BranchRollupState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [data, setData] = useState<BranchRollupViewModel | null>(null);
  const [profiles, setProfiles] = useState<Record<string, Record<string, unknown> | null>>({});
  const clientRef = useRef(client);
  clientRef.current = client;
  const retry = useCallback(() => setTick((t) => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    const c = clientRef.current;
    (async () => {
      const branches = await c.getOwnerBranches();
      const fetched = await Promise.all(branches.map(async (b) => {
        const [profile, summary] = await Promise.all([
          c.getBranchProfile(b.branchId).catch(() => null),
          c.getBranchSummary(b.branchId).catch(() => null)
        ]);
        const entry: BranchRollupEntry = {
          branchId: b.branchId,
          name: profile ? readString(profile, 'name', b.name || unnamedLabel) : (b.name || unnamedLabel),
          city: profile ? readString(profile, 'city', '') : '',
          summary
        };
        return { entry, profile };
      }));
      if (!cancelled) {
        setData(buildBranchRollup(fetched.map((f) => f.entry)));
        setProfiles(Object.fromEntries(fetched.map((f) => [f.entry.branchId, f.profile])));
        setPhase('ready');
      }
    })().catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [tick, unnamedLabel]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading' || data === null) return { status: 'loading', retry };
  return { status: 'ready', data, profiles, retry };
}
