import type { PlatformTransport } from '../platformTransport';
import type { OrganizationFeatureState } from '../types';

export class FeaturesApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public listFeatures(organizationId: string): Promise<OrganizationFeatureState[]> {
    return this.transport.send<OrganizationFeatureState[]>(
      'GET',
      `/api/platform/organizations/${encodeURIComponent(organizationId)}/features`
    );
  }

  // Оба мутирующих метода возвращают свежий полный список состояний — панель ничего не
  // досчитывает сама на клиенте, всегда показывает то, что вернул сервер.
  public setOverride(
    organizationId: string,
    featureKey: string,
    request: { isEnabled: boolean; reason: string }
  ): Promise<OrganizationFeatureState[]> {
    return this.transport.send<OrganizationFeatureState[]>(
      'PUT',
      `/api/platform/organizations/${encodeURIComponent(organizationId)}/features/${encodeURIComponent(featureKey)}`,
      request
    );
  }

  public clearOverride(organizationId: string, featureKey: string): Promise<OrganizationFeatureState[]> {
    return this.transport.send<OrganizationFeatureState[]>(
      'DELETE',
      `/api/platform/organizations/${encodeURIComponent(organizationId)}/features/${encodeURIComponent(featureKey)}`
    );
  }
}
