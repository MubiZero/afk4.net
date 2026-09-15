import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type { OrganizationAdminUpdatePreferenceDto, UpdateRolloutStatusDto } from '@afk4/contracts';
export type {
  DeviceUpdateStatusSnapshotDto,
  OrganizationAdminUpdatePreferenceDto,
  UpdateRolloutStatusDto,
} from '@afk4/contracts';

export function createUpdateClient(api: PlatformApiClient) {
  return {
    getRolloutStatuses(branchId: Guid): Promise<UpdateRolloutStatusDto[]> {
      return api.get<UpdateRolloutStatusDto[]>(`branches/${branchId}/updates/rollouts`);
    },
    getPreference(branchId: Guid): Promise<OrganizationAdminUpdatePreferenceDto> {
      return api.get<OrganizationAdminUpdatePreferenceDto>(`branches/${branchId}/updates/preferences`);
    },
    updatePreference(branchId: Guid, request: Pick<OrganizationAdminUpdatePreferenceDto, 'organizationId' | 'maintenanceWindowStart' | 'maintenanceWindowEnd'>): Promise<OrganizationAdminUpdatePreferenceDto> {
      return api.put<OrganizationAdminUpdatePreferenceDto, typeof request>(`branches/${branchId}/updates/preferences`, request);
    }
  };
}
