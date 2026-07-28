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
export interface RenameDeviceRequest { organizationId: Guid; displayName: string }
export interface RemoveDeviceRequest { organizationId: Guid; reason: string }

export function createDeviceClient(api: PlatformApiClient) {
  return {
    listDevices(branchId: Guid): Promise<DeviceInventoryItemDto[]> {
      return api.get<DeviceInventoryItemDto[]>(`branches/${branchId}/devices`);
    },
    createEnrollmentCode(branchId: Guid, organizationId: Guid, expiresInSeconds: number): Promise<DeviceEnrollmentCodeDto> {
      return api.post<DeviceEnrollmentCodeDto>(`branches/${branchId}/device-enrollment-codes`, {
        organizationId,
        expiresInSeconds
      });
    },
    dispatchDeviceCommand(deviceId: Guid, request: DispatchDeviceCommandRequest): Promise<DeviceCommandDto> {
      return api.post<DeviceCommandDto, DispatchDeviceCommandRequest>(`devices/${deviceId}/commands`, request);
    },
    listDeviceCommands(deviceId: Guid, query?: DeviceCommandSearchQuery): Promise<DeviceCommandStatusDto[]> {
      return api.get<DeviceCommandStatusDto[]>(`devices/${deviceId}/commands`, normalizeDeviceCommandQuery(query));
    },
    listBranchDeviceCommands(branchId: Guid, query?: DeviceCommandSearchQuery): Promise<DeviceCommandStatusDto[]> {
      return api.get<DeviceCommandStatusDto[]>(`branches/${branchId}/device-commands`, normalizeDeviceCommandQuery(query));
    },
    getDeviceCommandStatus(deviceId: Guid, commandId: Guid): Promise<DeviceCommandStatusDto> {
      return api.get<DeviceCommandStatusDto>(`devices/${deviceId}/commands/${commandId}/status`);
    },
    getDeviceDetail(deviceId: Guid): Promise<DeviceDetailDto> {
      return api.get<DeviceDetailDto>(`devices/${deviceId}`);
    },
    renameDevice(deviceId: Guid, request: RenameDeviceRequest): Promise<DeviceInventoryItemDto> {
      return api.post<DeviceInventoryItemDto, RenameDeviceRequest>(`devices/${deviceId}/rename`, request);
    },
    removeDevice(deviceId: Guid, request: RemoveDeviceRequest): Promise<DeviceInventoryItemDto> {
      return api.post<DeviceInventoryItemDto, RemoveDeviceRequest>(`devices/${deviceId}/remove`, request);
    },
    rotateDeviceCredential(deviceId: Guid): Promise<RotateDeviceCredentialResponse> {
      return api.post<RotateDeviceCredentialResponse>(`devices/${deviceId}/credentials/rotate`);
    },
    revokeDeviceCredential(deviceId: Guid, credentialId: Guid): Promise<RevokeDeviceCredentialResponse> {
      return api.post<RevokeDeviceCredentialResponse>(`devices/${deviceId}/credentials/${credentialId}/revoke`);
    }
  };
}
