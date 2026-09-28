import { createReportsClient } from '../api/clients/reports';
import { createShiftClient } from '../api/clients/shifts';
import { createFloorMapClient } from '../api/clients/floorMap';
import { authenticatedPlatformApi } from '../operatorHelpers';
import type { OperatorBackendContext } from '../operatorTypes';

function organizationApi(backend: OperatorBackendContext) {
  return authenticatedPlatformApi(backend.config.platformBaseUrl, backend.session)
    .forOrganization(backend.session.organizationId);
}

export function createReportClients(backend: OperatorBackendContext) {
  return createReportsClient(organizationApi(backend));
}

/**
 * Подробные отчёты живут не в «витринных» эндпоинтах рабочего места, а в общих: время игры и
 * действия сотрудников отдаёт клиент смен. План зала рядом — чтобы у места в отчёте было имя,
 * а не идентификатор.
 */
export function createDetailReportClients(backend: OperatorBackendContext) {
  const api = organizationApi(backend);
  return { shifts: createShiftClient(api), floorMap: createFloorMapClient(api) };
}
