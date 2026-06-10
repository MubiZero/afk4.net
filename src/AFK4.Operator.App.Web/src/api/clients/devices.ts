import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import { normalizeDeviceCommandQuery } from '../queryHelpers';

export type DeviceCommandDto = Record<string, unknown>;
export type DeviceCommandStatusDto = Record<string, unknown>;
export type DeviceDetailDto = Record<string, unknown>;
export type DeviceInventoryItemDto = Record<string, unknown>;
export type DeviceEnrollmentCodeDto = Record<string, unknown>;
export type RotateDeviceCredentialResponse = Record<string, unknown>;
export type RevokeDeviceCredentialResponse = Record<string, unknown>;

export interface DispatchDeviceCommandRequest {
  type: string;
  payload: Record<string, string>;
}

export interface DeviceCommandSearchQuery {
  limit?: number | null;
}

export function createDeviceClient(api: PlatformApiClient) {
  return {
    listDevices(branchId: Guid): Promise<DeviceInventoryItemDto[]> {
      return api.get<DeviceInventoryItemDto[]>(`/api/branches/${branchId}/devices`);
    },
    createEnrollmentCode(branchId: Guid, organizationId: Guid, expiresInSeconds: number): Promise<DeviceEnrollmentCodeDto> {
      return api.post<DeviceEnrollmentCodeDto>(`/api/branches/${branchId}/device-enrollment-codes`, {
        organizationId,
        expiresInSeconds
      });
    },
    dispatchDeviceCommand(deviceId: Guid, request: DispatchDeviceCommandRequest): Promise<DeviceCommandDto> {
      return api.post<DeviceCommandDto, DispatchDeviceCommandRequest>(`/api/devices/${deviceId}/commands`, request);
    },
    listDeviceCommands(deviceId: Guid, query?: DeviceCommandSearchQuery): Promise<DeviceCommandStatusDto[]> {
      return api.get<DeviceCommandStatusDto[]>(`/api/devices/${deviceId}/commands`, normalizeDeviceCommandQuery(query));
    },
    listBranchDeviceCommands(branchId: Guid, query?: DeviceCommandSearchQuery): Promise<DeviceCommandStatusDto[]> {
      return api.get<DeviceCommandStatusDto[]>(`/api/branches/${branchId}/device-commands`, normalizeDeviceCommandQuery(query));
    },
    getDeviceCommandStatus(deviceId: Guid, commandId: Guid): Promise<DeviceCommandStatusDto> {
      return api.get<DeviceCommandStatusDto>(`/api/devices/${deviceId}/commands/${commandId}/status`);
    },
    getDeviceDetail(deviceId: Guid): Promise<DeviceDetailDto> {
      return api.get<DeviceDetailDto>(`/api/devices/${deviceId}`);
    },
    rotateDeviceCredential(deviceId: Guid): Promise<RotateDeviceCredentialResponse> {
      return api.post<RotateDeviceCredentialResponse>(`/api/devices/${deviceId}/credentials/rotate`);
    },
    revokeDeviceCredential(deviceId: Guid, credentialId: Guid): Promise<RevokeDeviceCredentialResponse> {
      return api.post<RevokeDeviceCredentialResponse>(`/api/devices/${deviceId}/credentials/${credentialId}/revoke`);
    }
  };
}
