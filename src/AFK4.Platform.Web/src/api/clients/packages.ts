import type { ApiTransport } from '../apiTransport';
import type {
  CreatePackageDefinitionRequest,
  PackageDefinition,
  PackageOption,
  UpdatePackageDefinitionRequest
} from '../types';

export class PackagesApi {
  public constructor(private readonly transport: ApiTransport) {}

  public getPackageOptions(branchId: string): Promise<PackageOption[]> {
    return this.transport.send<PackageOption[]>('GET', `/api/branches/${encodeURIComponent(branchId)}/packages/options`);
  }

  public createPackageDefinition(branchId: string, request: CreatePackageDefinitionRequest): Promise<PackageDefinition> {
    return this.transport.send<PackageDefinition>('POST', `/api/branches/${encodeURIComponent(branchId)}/packages`, request);
  }

  public updatePackageDefinition(branchId: string, packageDefinitionId: string, request: UpdatePackageDefinitionRequest): Promise<PackageDefinition> {
    return this.transport.send<PackageDefinition>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/packages/${encodeURIComponent(packageDefinitionId)}`,
      request
    );
  }
}
