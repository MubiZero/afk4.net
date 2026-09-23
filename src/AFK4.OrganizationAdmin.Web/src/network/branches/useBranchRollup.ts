import { useCallback, useEffect, useRef, useState } from 'react';
import { buildBranchRollup, type BranchRollupEntry, type BranchRollupViewModel } from './branchRollupModel';
import type { BranchProfileDto, OperatorDashboardSummaryDto } from '../../operatorApiClients';

export interface RollupClient {
  getOwnerBranches(): Promise<{ branchId: string; name: string }[]>;
  getBranchProfile(branchId: string): Promise<BranchProfileDto>;
  getBranchSummary(branchId: string): Promise<OperatorDashboardSummaryDto>;
}

export type BranchRollupState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; error: unknown; retry: () => void }
  // profiles carries the raw profile per branch (null if that branch's profile fetch failed) —
  // BranchesDestination needs the FULL profile, not just name/city, because updateBranchProfile
  // is a full-record PATCH (see branchProfileRequest.ts); the rollup only aggregates KPIs.
  | { status: 'ready'; data: BranchRollupViewModel; profiles: Record<string, BranchProfileDto | null>; retry: () => void };

// Loads the org's branch list, then per-branch profile (name/city) + today's dashboard summary in
// parallel. A branch whose profile/summary call fails still shows up as a row (kpis: null) —
// only a failure of getOwnerBranches itself (the org-wide list) fails the whole screen.
// Клиента может не быть вовсе (сессия ещё не поднялась) — тогда экран честно остаётся в загрузке,
// а не притворяется пустым филиалом через клиент-заглушку.
export function useBranchRollup(client: RollupClient | null, unnamedLabel: string): BranchRollupState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [error, setError] = useState<unknown>(null);
  const [data, setData] = useState<BranchRollupViewModel | null>(null);
  const [profiles, setProfiles] = useState<Record<string, BranchProfileDto | null>>({});
  const clientRef = useRef(client);
  clientRef.current = client;
  const retry = useCallback(() => setTick((t) => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    const c = clientRef.current;
    if (c === null) return () => { cancelled = true; };
    (async () => {
      const branches = await c.getOwnerBranches();
      const fetched = await Promise.all(branches.map(async (b) => {
        const [profile, summary] = await Promise.all([
          c.getBranchProfile(b.branchId).catch(() => null),
          c.getBranchSummary(b.branchId).catch(() => null)
        ]);
        const entry: BranchRollupEntry = {
          branchId: b.branchId,
          name: profile?.name || b.name || unnamedLabel,
          city: profile?.city ?? '',
          summary
        };
        return { entry, profile };
      }));
      if (!cancelled) {
        setData(buildBranchRollup(fetched.map((f) => f.entry)));
        setProfiles(Object.fromEntries(fetched.map((f) => [f.entry.branchId, f.profile])));
        setPhase('ready');
      }
    })().catch((reason: unknown) => {
      if (!cancelled) {
        setError(reason);
        setPhase('error');
      }
    });
    return () => { cancelled = true; };
  }, [tick, unnamedLabel]);

  if (phase === 'error') return { status: 'error', error, retry };
  if (phase === 'loading' || data === null) return { status: 'loading', retry };
  return { status: 'ready', data, profiles, retry };
}
