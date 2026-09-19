import type { PlansApi } from '@/api/platformClients/plans';
import type { SubscriptionPlan } from '@/api/types';
import { useLoadable, type Loadable } from '../useLoadable';

export type PlansState = Loadable<SubscriptionPlan[]>;

type Client = Pick<PlansApi, 'listPlans'>;

export function usePlans(client: Client): PlansState {
  return useLoadable(() => client.listPlans(true));
}
