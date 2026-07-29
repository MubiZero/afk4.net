import { createReportsClient } from '../api/clients/reports';
import { PlatformApiClient } from '../platformApi';
import type { OperatorBackendContext } from '../operatorTypes';

export function createReportClients(backend: OperatorBackendContext) {
  const api = new PlatformApiClient({
    baseUrl: backend.config.platformBaseUrl,
    getAccessToken: () => backend.session.accessToken
  }).forOrganization(backend.session.organizationId);
  return createReportsClient(api);
}
