import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';

export type UpdatePackageDto = Record<string, unknown>;
export type UpdateRolloutDto = Record<string, unknown>;
export type UpdateRolloutStatusDto = Record<string, unknown>;

export interface CreateUpdatePackageRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface UpdatePackageStateChangeRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface CreateUpdateRolloutRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface UpdateRolloutStateChangeRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export function createUpdateClient(api: PlatformApiClient) {
  return {
    getRolloutStatuses(branchId: Guid): Promise<UpdateRolloutStatusDto[]> {
      return api.get<UpdateRolloutStatusDto[]>(`/api/branches/${branchId}/updates/rollouts`);
    },
    registerPackage(branchId: Guid, request: CreateUpdatePackageRequest): Promise<UpdatePackageDto> {
      return api.post<UpdatePackageDto, CreateUpdatePackageRequest>(`/api/branches/${branchId}/updates/packages`, request);
    },
    changePackageState(branchId: Guid, updatePackageId: Guid, request: UpdatePackageStateChangeRequest): Promise<UpdatePackageDto> {
      return api.post<UpdatePackageDto, UpdatePackageStateChangeRequest>(`/api/branches/${branchId}/updates/packages/${updatePackageId}/state`, request);
    },
    createRollout(branchId: Guid, request: CreateUpdateRolloutRequest): Promise<UpdateRolloutDto> {
      return api.post<UpdateRolloutDto, CreateUpdateRolloutRequest>(`/api/branches/${branchId}/updates/rollouts`, request);
    },
    changeRolloutState(branchId: Guid, updateRolloutId: Guid, request: UpdateRolloutStateChangeRequest): Promise<UpdateRolloutDto> {
      return api.post<UpdateRolloutDto, UpdateRolloutStateChangeRequest>(`/api/branches/${branchId}/updates/rollouts/${updateRolloutId}/state`, request);
    }
  };
}
