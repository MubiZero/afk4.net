import type { ApiTransport } from '../apiTransport';
import type {
  CreateTariffRequest,
  CreateTariffVersionRequest,
  Tariff,
  TariffOption,
  TariffVersion,
  UpdateTariffRequest,
  UpdateTariffVersionRequest
} from '../types';

export class TariffsApi {
  public constructor(private readonly transport: ApiTransport) {}

  public getTariffOptions(branchId: string): Promise<TariffOption[]> {
    return this.transport.send<TariffOption[]>('GET', `/api/branches/${encodeURIComponent(branchId)}/tariffs/options`);
  }

  public createTariff(branchId: string, request: CreateTariffRequest): Promise<Tariff> {
    return this.transport.send<Tariff>('POST', `/api/branches/${encodeURIComponent(branchId)}/tariffs`, request);
  }

  public createTariffVersion(branchId: string, tariffId: string, request: CreateTariffVersionRequest): Promise<TariffVersion> {
    return this.transport.send<TariffVersion>(
      'POST',
      `/api/branches/${encodeURIComponent(branchId)}/tariffs/${encodeURIComponent(tariffId)}/versions`,
      request
    );
  }

  public updateTariff(branchId: string, tariffId: string, request: UpdateTariffRequest): Promise<Tariff> {
    return this.transport.send<Tariff>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/tariffs/${encodeURIComponent(tariffId)}`,
      request
    );
  }

  public updateTariffVersion(branchId: string, tariffId: string, tariffVersionId: string, request: UpdateTariffVersionRequest): Promise<TariffVersion> {
    return this.transport.send<TariffVersion>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/tariffs/${encodeURIComponent(tariffId)}/versions/${encodeURIComponent(tariffVersionId)}`,
      request
    );
  }
}
