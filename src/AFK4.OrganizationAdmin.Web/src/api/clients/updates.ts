import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';

export interface DeviceUpdateStatusSnapshotDto {
  deviceId: Guid;
  updateRolloutId: Guid;
  updatePackageId: Guid;
  component: string;
  installedVersion: string;
  targetVersion: string;
  status: string;
  message: string;
  updatedAtUtc: string;
}

export interface UpdateRolloutStatusDto {
  updateRolloutId: Guid;
  organizationId: Guid;
  branchId: Guid;
  updatePackageId: Guid;
  component: string;
  version: string;
  channel: string;
  state: string;
  startsAtUtc: string;
  deviceStatuses: DeviceUpdateStatusSnapshotDto[];
}

export interface OrganizationAdminUpdatePreferenceDto {
  organizationId: Guid;
  branchId: Guid;
  maintenanceWindowStart: string;
  maintenanceWindowEnd: string;
  timeZone: string;
}

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
