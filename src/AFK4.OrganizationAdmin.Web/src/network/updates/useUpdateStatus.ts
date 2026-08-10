import { useCallback, useEffect, useRef, useState } from 'react';
import type { OrganizationAdminUpdatePreferenceDto, UpdateRolloutStatusDto } from '../../api/clients/updates';

export interface UpdateStatusClient {
  getRolloutStatuses(branchId: string): Promise<UpdateRolloutStatusDto[]>;
  getPreference(branchId: string): Promise<OrganizationAdminUpdatePreferenceDto>;
  updatePreference(
    branchId: string,
    request: Pick<OrganizationAdminUpdatePreferenceDto, 'organizationId' | 'maintenanceWindowStart' | 'maintenanceWindowEnd'>
  ): Promise<OrganizationAdminUpdatePreferenceDto>;
}

export type UpdateStatusState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | {
      status: 'ready';
      rollouts: UpdateRolloutStatusDto[];
      preference: OrganizationAdminUpdatePreferenceDto;
      applyPreference: (next: OrganizationAdminUpdatePreferenceDto) => void;
      retry: () => void;
    };

// Раскатки и окно обслуживания грузятся вместе: экран без любого из них показывать нечего —
// статус без окна не даёт настроить, окно без статуса не объясняет, что сейчас происходит.
export function useUpdateStatus(client: UpdateStatusClient, branchId: string): UpdateStatusState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [rollouts, setRollouts] = useState<UpdateRolloutStatusDto[]>([]);
  const [preference, setPreference] = useState<OrganizationAdminUpdatePreferenceDto | null>(null);
  const clientRef = useRef(client);
  clientRef.current = client;
  const retry = useCallback(() => setTick((value) => value + 1), []);

  useEffect(() => {
    if (branchId === '') return undefined;
    let cancelled = false;
    setPhase('loading');
    Promise.all([clientRef.current.getRolloutStatuses(branchId), clientRef.current.getPreference(branchId)])
      .then(([nextRollouts, nextPreference]) => {
        if (cancelled) return;
        setRollouts(Array.isArray(nextRollouts) ? nextRollouts : []);
        setPreference(nextPreference);
        setPhase('ready');
      })
      .catch(() => {
        if (!cancelled) setPhase('error');
      });
    return () => {
      cancelled = true;
    };
  }, [branchId, tick]);

  // Сохранение окна возвращает свежую запись — принимаем её вместо повторной загрузки экрана.
  const applyPreference = useCallback((next: OrganizationAdminUpdatePreferenceDto) => setPreference(next), []);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading' || preference === null) return { status: 'loading' };
  return { status: 'ready', rollouts, preference, applyPreference, retry };
}
