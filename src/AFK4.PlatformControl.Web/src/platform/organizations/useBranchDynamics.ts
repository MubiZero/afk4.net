import type { BranchDynamicsApi } from '@/api/platformClients/branchDynamics';
import type { BranchDynamics } from '@/api/types';
import { useLoadable, type Loadable } from '../useLoadable';

export type BranchDynamicsState = Loadable<BranchDynamics>;

type Client = Pick<BranchDynamicsApi, 'getBranchDynamics'>;

export function useBranchDynamics(
  client: Client,
  organizationId: string,
  branchId: string,
  days = 30
): BranchDynamicsState {
  // Клуба, о котором спрашивать, может не быть вовсе (организация без филиалов): вызывающий
  // рисует в этом случае своё пустое состояние и результат не читает, а запрос по кривому адресу
  // всё равно незачем отправлять — он бы только сказал «не удалось».
  return useLoadable(
    () => branchId === '' ? new Promise<BranchDynamics>(() => {}) : client.getBranchDynamics(organizationId, branchId, days),
    [organizationId, branchId, days]);
}
