import { getOperatorConfig } from './operatorConfig';

export const ORGANIZATION_ADMIN_PRODUCT = 'organization-admin';
export const ORGANIZATION_ADMIN_COMPATIBILITY_EPOCH = 2;

export function organizationAdminHeaders(): Record<string, string> {
  const config = getOperatorConfig();
  return {
    'X-AFK4-Product': config.product ?? ORGANIZATION_ADMIN_PRODUCT,
    'X-AFK4-Compatibility-Epoch': String(config.compatibilityEpoch ?? ORGANIZATION_ADMIN_COMPATIBILITY_EPOCH),
    'X-AFK4-Client-Version': config.appVersion || 'dev'
  };
}
