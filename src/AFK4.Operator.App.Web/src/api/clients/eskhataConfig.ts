import { PlatformApiClient } from '../../platformApi';

export interface EskhataConfigDto {
  baseUrl: string;
  companyId: string;
  posId: number;
  // The Hash key secret is never returned by the server — only whether one is stored.
  hashKeySet: boolean;
  status: string;
}

export interface UpdateEskhataConfigRequest {
  baseUrl: string;
  companyId: string;
  posId: number;
  // null/empty keeps the stored secret; a non-empty value replaces it.
  hashKey: string | null;
}

export function createEskhataConfigClient(api: PlatformApiClient) {
  return {
    get(): Promise<EskhataConfigDto> {
      return api.get<EskhataConfigDto>('/api/owner/eskhata-config');
    },
    update(request: UpdateEskhataConfigRequest): Promise<EskhataConfigDto> {
      return api.post<EskhataConfigDto, UpdateEskhataConfigRequest>('/api/owner/eskhata-config', request);
    }
  };
}
