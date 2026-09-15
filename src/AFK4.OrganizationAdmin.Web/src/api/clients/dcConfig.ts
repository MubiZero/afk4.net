import { PlatformApiClient } from '../../platformApi';
import type { DcPayLinkConfigDto, UpdateDcPayLinkConfigRequest } from '@afk4/contracts';
export type { DcPayLinkConfigDto, UpdateDcPayLinkConfigRequest } from '@afk4/contracts';

export function createDcConfigClient(api: PlatformApiClient) {
  return {
    get(): Promise<DcPayLinkConfigDto> {
      return api.get<DcPayLinkConfigDto>('payments/dc-config');
    },
    update(request: UpdateDcPayLinkConfigRequest): Promise<DcPayLinkConfigDto> {
      return api.post<DcPayLinkConfigDto, UpdateDcPayLinkConfigRequest>('payments/dc-config', request);
    }
  };
}
