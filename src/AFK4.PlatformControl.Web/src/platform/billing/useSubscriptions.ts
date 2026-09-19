import type { SubscriptionsApi } from '@/api/platformClients/subscriptions';
import type { SubscriptionListItem } from '@/api/types';
import { useLoadable, type Loadable } from '../useLoadable';

export type SubscriptionsState = Loadable<SubscriptionListItem[]>;

type Client = Pick<SubscriptionsApi, 'listSubscriptions'>;

export function useSubscriptions(client: Client): SubscriptionsState {
  return useLoadable(() => client.listSubscriptions());
}
