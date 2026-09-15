import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';

/** Сводка здоровья филиала (BranchDiagnosticsDto). Поля сверяются в `contractParity.test.ts`. */
export interface BranchDiagnosticsDto {
  organizationId: Guid;
  branchId: Guid;
  generatedAtUtc: string;
  deviceSummary: DeviceDiagnosticsSummaryDto;
  commandSummary: CommandDiagnosticsSummaryDto;
  updateSummary: UpdateDiagnosticsSummaryDto;
  staleDevices: StaleDeviceDiagnosticsDto[];
}

export interface DeviceDiagnosticsSummaryDto {
  totalDevices: number;
  onlineDevices: number;
  lockedDevices: number;
  staleDevices: number;
  staleThresholdSeconds: number;
  newestHeartbeatAtUtc: string | null;
}

export interface CommandDiagnosticsSummaryDto {
  pendingCommands: number;
  failedCommands: number;
  recentFailures: FailedCommandDiagnosticsDto[];
}

export interface UpdateDiagnosticsSummaryDto {
  activeRollouts: number;
  installingDevices: number;
  failedDevices: number;
  rollbackDevices: number;
  recentFailures: FailedUpdateDiagnosticsDto[];
}

export interface StaleDeviceDiagnosticsDto {
  deviceId: Guid;
  machineName: string;
  agentVersion: string;
  shellVersion: string;
  isOnline: boolean;
  isLocked: boolean;
  lastHeartbeatAtUtc: string | null;
  lastHeartbeatAgeSeconds: number | null;
}

export interface FailedCommandDiagnosticsDto {
  deviceId: Guid;
  machineName: string;
  commandId: Guid;
  type: string;
  status: string;
  message: string | null;
  updatedAtUtc: string;
}

export interface FailedUpdateDiagnosticsDto {
  deviceId: Guid;
  machineName: string;
  updateRolloutId: Guid;
  component: string;
  targetVersion: string;
  status: string;
  message: string;
  updatedAtUtc: string;
}

export function createDiagnosticsClient(api: PlatformApiClient) {
  return {
    getDiagnostics(branchId: Guid): Promise<BranchDiagnosticsDto> {
      return api.get<BranchDiagnosticsDto>(`branches/${branchId}/diagnostics`);
    }
  };
}
