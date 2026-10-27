import { PlatformApiClient } from '../../platformApi';
import type {
  BranchOfflineLeaseKeyDto,
  OfflineJournalUploadRequest,
  OfflineJournalUploadResponse,
  RegisterOfflineLeaseKeyRequest
} from '@afk4/contracts';

// Офлайн у стойки (docs/roadmap/offline-front-desk.md): ключ этого ПК на сервере и досылка журнала кассы.
export function createOfflineDeskClient(api: PlatformApiClient) {
  return {
    registerKey(branchId: string, request: RegisterOfflineLeaseKeyRequest): Promise<BranchOfflineLeaseKeyDto> {
      return api.post<BranchOfflineLeaseKeyDto, RegisterOfflineLeaseKeyRequest>(`branches/${branchId}/offline-lease-keys`, request);
    },
    uploadJournal(branchId: string, request: OfflineJournalUploadRequest): Promise<OfflineJournalUploadResponse> {
      return api.post<OfflineJournalUploadResponse, OfflineJournalUploadRequest>(`branches/${branchId}/offline-journal`, request);
    }
  };
}
