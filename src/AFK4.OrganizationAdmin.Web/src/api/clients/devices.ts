import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import { normalizeDeviceCommandQuery } from '../queryHelpers';

/** Отправленная машине команда (DeviceCommandDto). Поля сверяются в `contractParity.test.ts`. */
export interface DeviceCommandDto {
  commandId: Guid;
  type: string;
  createdAtUtc: string;
  payload: Record<string, string>;
}

export interface DeviceCommandStatusDto {
  deviceId: Guid;
  commandId: Guid;
  type: string;
  status: string;
  message: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** Карточка одного ПК. В отличие от DeviceInventoryItemDto несёт хвост последних команд. */
export interface DeviceDetailDto {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  machineName: string;
  agentVersion: string;
  shellVersion: string;
  enrolledAtUtc: string;
  lastHeartbeatAtUtc: string | null;
  isOnline: boolean;
  isLocked: boolean;
  seatId: Guid | null;
  seatName: string | null;
  zoneId: Guid | null;
  zoneName: string | null;
  activeCredentialCount: number;
  installedAppCount: number;
  recentCommands: DeviceCommandStatusDto[];
  displayName?: string;
  role?: string;
  enrollmentState?: string;
}
/**
 * ПК в списке филиала (DeviceInventoryItemDto). Поля перечислены поимённо — совпадение с
 * сервером проверяется в `contractParity.test.ts`, а не держится на памяти.
 */
export interface DeviceInventoryItemDto {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  machineName: string;
  agentVersion: string;
  shellVersion: string;
  enrolledAtUtc: string;
  lastHeartbeatAtUtc: string | null;
  isOnline: boolean;
  isLocked: boolean;
  seatId: Guid | null;
  seatName: string | null;
  zoneId: Guid | null;
  zoneName: string | null;
  activeCredentialCount: number;
  installedAppCount: number;
  pendingCommandCount: number;
  failedCommandCount: number;
  displayName: string;
  role: string;
  enrollmentState: string;
}
export interface DeviceEnrollmentCodeDto {
  organizationId: Guid;
  branchId: Guid;
  code: string;
  expiresAtUtc: string;
}

// `credentialSecret` сервер показывает ровно один раз — в ответе на ротацию. В логи и в состояние
// экрана его класть нельзя: второй раз его не покажут никому, включая владельца.
export interface RotateDeviceCredentialResponse {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  credentialId: Guid;
  credentialSecret: string;
  rotatedAtUtc: string;
}

export interface RevokeDeviceCredentialResponse {
  organizationId: Guid;
  branchId: Guid;
  deviceId: Guid;
  credentialId: Guid;
  revokedAtUtc: string;
}

export interface DispatchDeviceCommandRequest {
  type: string;
  payload: Record<string, string>;
}

export interface DeviceCommandSearchQuery {
  limit?: number | null;
}
export interface RenameDeviceRequest { organizationId: Guid; displayName: string }
export interface RemoveDeviceRequest { organizationId: Guid; reason: string }
// Один и тот же запрос у подтверждения и отказа: причина нужна отказу — по ней потом понимают,
// почему машину не пустили, — а подтверждению не обязательна.
export interface DeviceStateChangeRequest { organizationId: Guid; reason?: string | null }

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
