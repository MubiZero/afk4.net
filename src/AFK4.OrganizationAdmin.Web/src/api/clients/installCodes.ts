import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type { CreateInstallCodeRequest, InstallCodeDto } from '@afk4/contracts';
export type { CreateInstallCodeRequest, InstallCodeDto } from '@afk4/contracts';

// Коды тихой установки ПК филиала: сам код приходит один раз — в ответе на выдачу.
export function createInstallCodeClient(api: PlatformApiClient) {
  return {
    list(branchId: Guid): Promise<InstallCodeDto[]> {
      return api.get<InstallCodeDto[]>(`branches/${branchId}/install-codes`);
    },
    issue(branchId: Guid, request: CreateInstallCodeRequest): Promise<InstallCodeDto> {
      return api.post<InstallCodeDto, CreateInstallCodeRequest>(`branches/${branchId}/install-codes`, request);
    },
    revoke(branchId: Guid, installCodeId: Guid): Promise<void> {
      return api.delete<void>(`branches/${branchId}/install-codes/${installCodeId}`);
    }
  };
}
