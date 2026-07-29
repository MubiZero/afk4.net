import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';

export type UpdateRolloutStatusDto = Record<string, unknown>;

export function createUpdateClient(api: PlatformApiClient) {
  return {
    getRolloutStatuses(branchId: Guid): Promise<UpdateRolloutStatusDto[]> {
      return api.get<UpdateRolloutStatusDto[]>(`branches/${branchId}/updates/rollouts`);
    }
  };
}
