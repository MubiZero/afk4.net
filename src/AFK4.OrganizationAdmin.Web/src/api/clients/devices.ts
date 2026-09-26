import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import { normalizeDeviceCommandQuery } from '../queryHelpers';
import type {
  DeviceAssistanceStateDto,
  DeviceCommandDto,
  DeviceCommandStatusDto,
  DeviceDetailDto,
  DeviceHardwareDto,
  DeviceInventoryItemDto,
  DeviceStateChangeRequest,
  DispatchDeviceCommandRequest,
  RenameDeviceRequest,
  RevokeDeviceCredentialResponse,
  RotateDeviceCredentialResponse,
} from '@afk4/contracts';
export type {
  DeviceAssistanceStateDto,
  DeviceCommandDto,
  DeviceCommandStatusDto,
  DeviceDetailDto,
  DeviceHardwareDto,
  DeviceInventoryItemDto,
  DeviceStateChangeRequest,
  DispatchDeviceCommandRequest,
  RenameDeviceRequest,
  RevokeDeviceCredentialResponse,
  RotateDeviceCredentialResponse,
} from '@afk4/contracts';

export interface DeviceCommandSearchQuery {
  limit?: number | null;
}
export interface RemoveDeviceRequest { organizationId: Guid; reason: string }

export function createDeviceClient(api: PlatformApiClient) {
  return {
    listDevices(branchId: Guid): Promise<DeviceInventoryItemDto[]> {
      return api.get<DeviceInventoryItemDto[]>(`branches/${branchId}/devices`);
    },
    // Оператор подошёл к месту — вызов снят.
    resolveAssistanceRequest(deviceId: Guid, request: DeviceStateChangeRequest): Promise<DeviceAssistanceStateDto> {
      return api.post<DeviceAssistanceStateDto, DeviceStateChangeRequest>(
        `devices/${deviceId}/assistance-request/resolve`, request);
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
    // Железо ПК: сейчас, принятое и что поменялось (P9).
    getHardware(deviceId: Guid): Promise<DeviceHardwareDto> {
      return api.get<DeviceHardwareDto>(`devices/${deviceId}/hardware`);
    },
    acceptHardware(deviceId: Guid): Promise<DeviceHardwareDto> {
      return api.post<DeviceHardwareDto>(`devices/${deviceId}/hardware/accept`);
    },
    renameDevice(deviceId: Guid, request: RenameDeviceRequest): Promise<DeviceInventoryItemDto> {
      return api.post<DeviceInventoryItemDto, RenameDeviceRequest>(`devices/${deviceId}/rename`, request);
    },
    removeDevice(deviceId: Guid, request: RemoveDeviceRequest): Promise<DeviceInventoryItemDto> {
      return api.post<DeviceInventoryItemDto, RemoveDeviceRequest>(`devices/${deviceId}/remove`, request);
    },
    // Машины, ждущие решения человека. Появляются, только когда в филиале включено ручное
    // подтверждение: иначе ПК встаёт в строй сам, и очередь всегда пуста.
    listPendingDevices(branchId: Guid): Promise<DeviceInventoryItemDto[]> {
      return api.get<DeviceInventoryItemDto[]>(`branches/${branchId}/devices/pending`);
    },
    approveDevice(deviceId: Guid, request: DeviceStateChangeRequest): Promise<DeviceInventoryItemDto> {
      return api.post<DeviceInventoryItemDto, DeviceStateChangeRequest>(`devices/${deviceId}/approve`, request);
    },
    rejectDevice(deviceId: Guid, request: DeviceStateChangeRequest): Promise<DeviceInventoryItemDto> {
      return api.post<DeviceInventoryItemDto, DeviceStateChangeRequest>(`devices/${deviceId}/reject`, request);
    },
    rotateDeviceCredential(deviceId: Guid): Promise<RotateDeviceCredentialResponse> {
      return api.post<RotateDeviceCredentialResponse>(`devices/${deviceId}/credentials/rotate`);
    },
    /// Попросить машину сменить ключ самой: она сделает это ближайшим сердцебиением и не
    /// потеряет связь. В отличие от rotateDeviceCredential, который отрезает её немедленно.
    requestDeviceCredentialRotation(deviceId: Guid): Promise<void> {
      return api.post<void>(`devices/${deviceId}/credentials/request-rotation`);
    },
    revokeDeviceCredential(deviceId: Guid, credentialId: Guid): Promise<RevokeDeviceCredentialResponse> {
      return api.post<RevokeDeviceCredentialResponse>(`devices/${deviceId}/credentials/${credentialId}/revoke`);
    }
  };
}
