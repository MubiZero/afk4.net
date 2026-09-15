import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type { BranchDiagnosticsDto } from '@afk4/contracts';
export type {
  BranchDiagnosticsDto,
  CommandDiagnosticsSummaryDto,
  DeviceDiagnosticsSummaryDto,
  FailedCommandDiagnosticsDto,
  FailedUpdateDiagnosticsDto,
  StaleDeviceDiagnosticsDto,
  UpdateDiagnosticsSummaryDto,
} from '@afk4/contracts';

export function createDiagnosticsClient(api: PlatformApiClient) {
  return {
    getDiagnostics(branchId: Guid): Promise<BranchDiagnosticsDto> {
      return api.get<BranchDiagnosticsDto>(`branches/${branchId}/diagnostics`);
    }
  };
}
