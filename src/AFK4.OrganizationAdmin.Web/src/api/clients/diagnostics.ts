import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';

export type BranchDiagnosticsDto = Record<string, unknown>;

export function createDiagnosticsClient(api: PlatformApiClient) {
  return {
    getDiagnostics(branchId: Guid): Promise<BranchDiagnosticsDto> {
      return api.get<BranchDiagnosticsDto>(`branches/${branchId}/diagnostics`);
    }
  };
}
