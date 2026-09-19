import type { AnalyticsApi } from '@/api/platformClients/analytics';
import type { AnalyticsOverview } from '@/api/types';
import { useLoadable, type Loadable } from '../useLoadable';

export type AnalyticsState = Loadable<AnalyticsOverview>;

type Client = Pick<AnalyticsApi, 'getOverview'>;

export function useAnalytics(client: Client, months = 12): AnalyticsState {
  return useLoadable(() => client.getOverview(months), [months]);
}
