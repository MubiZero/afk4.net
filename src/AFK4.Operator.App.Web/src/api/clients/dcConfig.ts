import { PlatformApiClient } from '../../platformApi';

export interface DcPayLinkConfigDto {
  cardSet: boolean;
  // Only the last 4 digits are ever returned — the full card number is never sent back to the client.
  cardLast4: string;
  commentTemplate: string;
  isActive: boolean;
}

export interface UpdateDcPayLinkConfigRequest {
  // null/empty keeps the stored card number; a non-empty value replaces it.
  cardNumber: string | null;
  commentTemplate: string;
  isActive: boolean;
}

export function createDcConfigClient(api: PlatformApiClient) {
  return {
    get(): Promise<DcPayLinkConfigDto> {
      return api.get<DcPayLinkConfigDto>('/api/owner/dc-config');
    },
    update(request: UpdateDcPayLinkConfigRequest): Promise<DcPayLinkConfigDto> {
      return api.post<DcPayLinkConfigDto, UpdateDcPayLinkConfigRequest>('/api/owner/dc-config', request);
    }
  };
}
