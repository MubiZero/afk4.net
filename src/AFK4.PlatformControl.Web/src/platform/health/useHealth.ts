import type { HealthApi } from '@/api/platformClients/health';
import type { HealthOverview } from '@/api/types';
import { useLoadable, type Loadable } from '../useLoadable';

export type HealthState = Loadable<HealthOverview>;

type Client = Pick<HealthApi, 'getOverview'>;

/// Здоровье платформы смотрят так же, как обзор сети: открыл и оставил. Инциденты и очереди
/// меняются медленнее клубов, поэтому реже.
const HEALTH_REFRESH_MS = 120_000;

export function useHealth(client: Client): HealthState {
  return useLoadable(() => client.getOverview(), [], { refreshMs: HEALTH_REFRESH_MS });
}
