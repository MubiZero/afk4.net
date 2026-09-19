import type { HealthApi } from '@/api/platformClients/health';
import type { HealthOverview } from '@/api/types';
import { useLoadable, type Loadable } from '../useLoadable';

export type HealthState = Loadable<HealthOverview>;

type Client = Pick<HealthApi, 'getOverview'>;

export function useHealth(client: Client): HealthState {
  return useLoadable(() => client.getOverview());
}
