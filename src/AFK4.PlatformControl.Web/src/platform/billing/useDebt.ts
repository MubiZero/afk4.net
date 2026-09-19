import type { DebtApi } from '@/api/platformClients/debt';
import type { DebtRow } from '@/api/types';
import { useLoadable, type Loadable } from '../useLoadable';

export type DebtState = Loadable<DebtRow[]>;

type Client = Pick<DebtApi, 'listDebt'>;

export function useDebt(client: Client): DebtState {
  return useLoadable(() => client.listDebt());
}
