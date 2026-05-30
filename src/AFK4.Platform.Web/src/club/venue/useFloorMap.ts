import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { PlatformApiError } from '@/api/platformApi';
import { buildBulkRequest, toEditorZones, type EditorZone } from './floorMapModel';

type Loadable = Pick<ClubApiClient, 'getFloorMap' | 'updateFloorMap'>;

export type SaveOutcome = 'ok' | 'conflict' | 'error';

export type FloorMapState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; retry: () => void }
  | {
      status: 'ready';
      branchName: string;
      zones: EditorZone[];
      setZones: (updater: (prev: EditorZone[]) => EditorZone[]) => void;
      saving: boolean;
      conflict: boolean;
      save: () => Promise<SaveOutcome>;
      reload: () => void;
    };

export function useFloorMap(client: Loadable, branchId: string, organizationId: string): FloorMapState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [branchName, setBranchName] = useState('');
  const [zones, setZones] = useState<EditorZone[]>([]);
  const [etag, setEtag] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [conflict, setConflict] = useState(false);

  const clientRef = useRef(client);
  clientRef.current = client;
  const zonesRef = useRef(zones);
  zonesRef.current = zones;
  const etagRef = useRef(etag);
  etagRef.current = etag;

  const reload = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    setConflict(false);
    clientRef.current.getFloorMap(branchId)
      .then(result => {
        if (cancelled) return;
        setBranchName(result.floorMap.branchName);
        setZones(toEditorZones(result.floorMap));
        setEtag(result.etag);
        setPhase('ready');
      })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [branchId, tick]);

  const updateZones = useCallback((updater: (prev: EditorZone[]) => EditorZone[]) => {
    setZones(prev => updater(prev));
  }, []);

  const save = useCallback(async (): Promise<SaveOutcome> => {
    setSaving(true);
    setConflict(false);
    try {
      await clientRef.current.updateFloorMap(branchId, buildBulkRequest(organizationId, zonesRef.current), etagRef.current);
      setTick(t => t + 1); // reload server truth (new ids + fresh ETag)
      return 'ok';
    } catch (err) {
      if (err instanceof PlatformApiError && (err.status === 412 || err.status === 428)) {
        setConflict(true);
        return 'conflict';
      }
      return 'error';
    } finally {
      setSaving(false);
    }
  }, [branchId, organizationId]);

  if (phase === 'loading') return { status: 'loading', retry: reload };
  if (phase === 'error') return { status: 'error', retry: reload };
  return { status: 'ready', branchName, zones, setZones: updateZones, saving, conflict, save, reload };
}
