import { PlatformApiClient } from '../../platformApi';
import type { OrganizationBrandingDto, UpdateOrganizationBrandingRequest } from '@afk4/contracts';
export type { OrganizationBrandingDto, UpdateOrganizationBrandingRequest } from '@afk4/contracts';

/**
 * Оформление клуба: логотип и цвет, которые видят гости.
 *
 * Живёт на организации, а не на филиале: у сети бренд один, а логотип и обложка конкретного зала
 * настраиваются в профиле филиала и это другое.
 */
export function createBrandingClient(api: PlatformApiClient) {
  return {
    getBranding(): Promise<OrganizationBrandingDto> {
      return api.get<OrganizationBrandingDto>('branding');
    },
    updateBranding(request: UpdateOrganizationBrandingRequest): Promise<OrganizationBrandingDto> {
      return api.patch<OrganizationBrandingDto, UpdateOrganizationBrandingRequest>('branding', request);
    }
  };
}
