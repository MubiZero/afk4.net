import {
  clearStaffSession,
  staffSessionFromSignInResponse,
  writeStaffSession,
  type StaffSession
} from '../auth/staffTokenStore';
import { PlatformApiError } from './platformApi';
import type {
  BranchProfile,
  BranchSettings,
  CreatePackageDefinitionRequest,
  CreateProductCategoryRequest,
  CreateProductRequest,
  CreateStaffUserRequest,
  CreateTariffRequest,
  CreateTariffVersionRequest,
  DeviceInventoryItem,
  FloorMapBulkUpdateRequest,
  FloorMapBulkUpdateResponse,
  FloorMapRead,
  OperatorDashboardSummary,
  OwnerCodeIssued,
  OwnerCodeSummary,
  PackageDefinition,
  PackageOption,
  PosProduct,
  PosProductCategory,
  ResetStaffUserPasswordRequest,
  StaffSignInResponse,
  StaffUser,
  Tariff,
  TariffOption,
  TariffVersion,
  UpdateBranchProfileRequest,
  UpdateBranchSettingsRequest,
  UpdatePackageDefinitionRequest,
  UpdateProductRequest,
  UpdateStaffUserProfileRequest,
  UpdateStaffUserRolesRequest,
  UpdateStaffUserStateRequest,
  UpdateTariffRequest,
  UpdateTariffVersionRequest
} from './types';

export interface ClubApiClientOptions {
  baseUrl: string;
  fetchImpl?: typeof fetch;
  session: StaffSession | null;
  onSessionChanged: (session: StaffSession | null) => void;
}

export class ClubApiClient {
  private session: StaffSession | null;
  private readonly baseUrl: string;
  private readonly fetchImpl: typeof fetch;
  private readonly onSessionChanged: (session: StaffSession | null) => void;
  private inflightRefresh: Promise<StaffSession | null> | null = null;

  public constructor(options: ClubApiClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/+$/u, '');
    this.fetchImpl = options.fetchImpl ?? fetch.bind(globalThis);
    this.session = options.session;
    this.onSessionChanged = options.onSessionChanged;
  }

  public getSession(): StaffSession | null {
    return this.session;
  }

  public async getOwnerCode(): Promise<OwnerCodeSummary | null> {
    const response = await this.sendRaw('GET', '/api/staff/me/owner-code');
    if (response.status === 204) {
      return null;
    }
    return this.readJson<OwnerCodeSummary>(response);
  }

  public generateOwnerCode(): Promise<OwnerCodeIssued> {
    return this.send<OwnerCodeIssued>('POST', '/api/staff/me/owner-code/generate');
  }

  public rotateOwnerCode(reason: string): Promise<OwnerCodeIssued> {
    return this.send<OwnerCodeIssued>('POST', '/api/staff/me/owner-code/rotate', { reason });
  }

  public getBranchProfile(branchId: string): Promise<BranchProfile> {
    return this.send<BranchProfile>('GET', `/api/branches/${encodeURIComponent(branchId)}/profile`);
  }

  public updateBranchProfile(branchId: string, request: UpdateBranchProfileRequest): Promise<BranchProfile> {
    return this.send<BranchProfile>('PATCH', `/api/branches/${encodeURIComponent(branchId)}/profile`, request);
  }

  public getBranchSettings(branchId: string): Promise<BranchSettings> {
    return this.send<BranchSettings>('GET', `/api/branches/${encodeURIComponent(branchId)}/settings`);
  }

  public updateBranchSettings(branchId: string, request: UpdateBranchSettingsRequest): Promise<BranchSettings> {
    return this.send<BranchSettings>('PUT', `/api/branches/${encodeURIComponent(branchId)}/settings`, request);
  }

  public async getFloorMap(branchId: string): Promise<FloorMapRead> {
    const response = await this.sendRaw('GET', `/api/branches/${encodeURIComponent(branchId)}/floor-map`);
    const floorMap = await this.readJson<FloorMapRead['floorMap']>(response);
    return {
      etag: response.headers.get('ETag'),
      floorMap
    };
  }

  public updateFloorMap(
    branchId: string,
    request: FloorMapBulkUpdateRequest,
    etag: string | null
  ): Promise<FloorMapBulkUpdateResponse> {
    return this.send<FloorMapBulkUpdateResponse>(
      'PUT',
      `/api/branches/${encodeURIComponent(branchId)}/floor-map`,
      request,
      etag === null ? undefined : { 'If-Match': etag }
    );
  }

  public getDashboardSummary(branchId: string): Promise<OperatorDashboardSummary> {
    const now = new Date();
    const from = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), 0, 0, 0));
    const to = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), 23, 59, 59));
    const query = new URLSearchParams({
      fromUtc: from.toISOString(),
      toUtc: to.toISOString(),
      limit: '3'
    });
    return this.send<OperatorDashboardSummary>(
      'GET',
      `/api/branches/${encodeURIComponent(branchId)}/dashboard/summary?${query.toString()}`
    );
  }

  public getDashboardSummaryForRange(branchId: string, fromUtc: string, toUtc: string): Promise<OperatorDashboardSummary> {
    const query = new URLSearchParams({ fromUtc, toUtc, limit: '3' });
    return this.send<OperatorDashboardSummary>(
      'GET',
      `/api/branches/${encodeURIComponent(branchId)}/dashboard/summary?${query.toString()}`
    );
  }

  public listDevices(branchId: string): Promise<DeviceInventoryItem[]> {
    return this.send<DeviceInventoryItem[]>('GET', `/api/branches/${encodeURIComponent(branchId)}/devices`);
  }

  public listPendingDevices(branchId: string): Promise<DeviceInventoryItem[]> {
    return this.send<DeviceInventoryItem[]>('GET', `/api/branches/${encodeURIComponent(branchId)}/devices/pending`);
  }

  public approveDevice(deviceId: string, organizationId: string, reason: string): Promise<DeviceInventoryItem> {
    return this.send<DeviceInventoryItem>(
      'POST',
      `/api/devices/${encodeURIComponent(deviceId)}/approve`,
      { organizationId, reason }
    );
  }

  public rejectDevice(deviceId: string, organizationId: string, reason: string): Promise<DeviceInventoryItem> {
    return this.send<DeviceInventoryItem>(
      'POST',
      `/api/devices/${encodeURIComponent(deviceId)}/reject`,
      { organizationId, reason }
    );
  }

  public renameDevice(deviceId: string, organizationId: string, displayName: string): Promise<DeviceInventoryItem> {
    return this.send<DeviceInventoryItem>(
      'POST',
      `/api/devices/${encodeURIComponent(deviceId)}/rename`,
      { organizationId, displayName }
    );
  }

  public moveDeviceSeat(deviceId: string, organizationId: string, seatId: string): Promise<DeviceInventoryItem> {
    return this.send<DeviceInventoryItem>(
      'POST',
      `/api/devices/${encodeURIComponent(deviceId)}/move-seat`,
      { organizationId, seatId }
    );
  }

  public removeDevice(deviceId: string, organizationId: string, reason: string): Promise<DeviceInventoryItem> {
    return this.send<DeviceInventoryItem>(
      'POST',
      `/api/devices/${encodeURIComponent(deviceId)}/remove`,
      { organizationId, reason }
    );
  }

  public listStaff(branchId: string): Promise<StaffUser[]> {
    return this.send<StaffUser[]>('GET', `/api/branches/${encodeURIComponent(branchId)}/staff`);
  }

  public createStaff(branchId: string, request: CreateStaffUserRequest): Promise<StaffUser> {
    return this.send<StaffUser>('POST', `/api/branches/${encodeURIComponent(branchId)}/staff`, request);
  }

  public updateStaffRoles(
    branchId: string,
    staffUserId: string,
    request: UpdateStaffUserRolesRequest
  ): Promise<StaffUser> {
    return this.send<StaffUser>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/staff/${encodeURIComponent(staffUserId)}/roles`,
      request
    );
  }

  public updateStaffProfile(
    branchId: string,
    staffUserId: string,
    request: UpdateStaffUserProfileRequest
  ): Promise<StaffUser> {
    return this.send<StaffUser>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/staff/${encodeURIComponent(staffUserId)}/profile`,
      request
    );
  }

  public updateStaffState(
    branchId: string,
    staffUserId: string,
    request: UpdateStaffUserStateRequest
  ): Promise<StaffUser> {
    return this.send<StaffUser>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/staff/${encodeURIComponent(staffUserId)}/state`,
      request
    );
  }

  public resetStaffPassword(
    branchId: string,
    staffUserId: string,
    request: ResetStaffUserPasswordRequest
  ): Promise<StaffUser> {
    return this.send<StaffUser>(
      'POST',
      `/api/branches/${encodeURIComponent(branchId)}/staff/${encodeURIComponent(staffUserId)}/password-reset`,
      request
    );
  }

  public getTariffOptions(branchId: string): Promise<TariffOption[]> {
    return this.send<TariffOption[]>('GET', `/api/branches/${encodeURIComponent(branchId)}/tariffs/options`);
  }

  public createTariff(branchId: string, request: CreateTariffRequest): Promise<Tariff> {
    return this.send<Tariff>('POST', `/api/branches/${encodeURIComponent(branchId)}/tariffs`, request);
  }

  public createTariffVersion(branchId: string, tariffId: string, request: CreateTariffVersionRequest): Promise<TariffVersion> {
    return this.send<TariffVersion>(
      'POST',
      `/api/branches/${encodeURIComponent(branchId)}/tariffs/${encodeURIComponent(tariffId)}/versions`,
      request
    );
  }

  public updateTariff(branchId: string, tariffId: string, request: UpdateTariffRequest): Promise<Tariff> {
    return this.send<Tariff>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/tariffs/${encodeURIComponent(tariffId)}`,
      request
    );
  }

  public updateTariffVersion(branchId: string, tariffId: string, tariffVersionId: string, request: UpdateTariffVersionRequest): Promise<TariffVersion> {
    return this.send<TariffVersion>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/tariffs/${encodeURIComponent(tariffId)}/versions/${encodeURIComponent(tariffVersionId)}`,
      request
    );
  }

  public getCatalog(branchId: string): Promise<PosProduct[]> {
    return this.send<PosProduct[]>('GET', `/api/branches/${encodeURIComponent(branchId)}/pos/catalog`);
  }

  public createProductCategory(branchId: string, request: CreateProductCategoryRequest): Promise<PosProductCategory> {
    return this.send<PosProductCategory>('POST', `/api/branches/${encodeURIComponent(branchId)}/pos/categories`, request);
  }

  public createProduct(branchId: string, request: CreateProductRequest): Promise<PosProduct> {
    return this.send<PosProduct>('POST', `/api/branches/${encodeURIComponent(branchId)}/pos/products`, request);
  }

  public updateProduct(branchId: string, productId: string, request: UpdateProductRequest): Promise<PosProduct> {
    return this.send<PosProduct>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/pos/products/${encodeURIComponent(productId)}`,
      request
    );
  }

  public getPackageOptions(branchId: string): Promise<PackageOption[]> {
    return this.send<PackageOption[]>('GET', `/api/branches/${encodeURIComponent(branchId)}/packages/options`);
  }

  public createPackageDefinition(branchId: string, request: CreatePackageDefinitionRequest): Promise<PackageDefinition> {
    return this.send<PackageDefinition>('POST', `/api/branches/${encodeURIComponent(branchId)}/packages`, request);
  }

  public updatePackageDefinition(branchId: string, packageDefinitionId: string, request: UpdatePackageDefinitionRequest): Promise<PackageDefinition> {
    return this.send<PackageDefinition>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/packages/${encodeURIComponent(packageDefinitionId)}`,
      request
    );
  }

  private async send<T>(
    method: string,
    path: string,
    body?: unknown,
    extraHeaders?: Record<string, string>
  ): Promise<T> {
    const response = await this.sendRaw(method, path, body, extraHeaders);
    return this.readJson<T>(response);
  }

  private async sendRaw(
    method: string,
    path: string,
    body?: unknown,
    extraHeaders?: Record<string, string>
  ): Promise<Response> {
    let response = await this.dispatch(method, path, body, extraHeaders);
    if (response.status === 401 && this.session !== null) {
      const refreshed = await this.refreshTokenOnce();
      if (refreshed !== null) {
        response = await this.dispatch(method, path, body, extraHeaders);
      }
    }
    if (!response.ok) {
      throw await toApiError(response);
    }
    return response;
  }

  private dispatch(
    method: string,
    path: string,
    body?: unknown,
    extraHeaders?: Record<string, string>
  ): Promise<Response> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...extraHeaders
    };
    if (this.session !== null && this.session.accessToken.length > 0) {
      headers.Authorization = `Bearer ${this.session.accessToken}`;
    }
    const init: RequestInit = { method, headers };
    if (body !== undefined) {
      init.body = JSON.stringify(body);
    }
    return this.fetchImpl(`${this.baseUrl}${path}`, init);
  }

  private async refreshTokenOnce(): Promise<StaffSession | null> {
    if (this.inflightRefresh !== null) {
      return this.inflightRefresh;
    }
    this.inflightRefresh = (async (): Promise<StaffSession | null> => {
      if (this.session === null) {
        return null;
      }
      try {
        const response = await this.fetchImpl(`${this.baseUrl}/api/auth/staff/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken: this.session.refreshToken })
        });
        if (!response.ok) {
          this.applySession(null);
          return null;
        }
        const body = (await response.json()) as StaffSignInResponse;
        const refreshed = staffSessionFromSignInResponse(body);
        this.applySession(refreshed);
        return refreshed;
      } finally {
        this.inflightRefresh = null;
      }
    })();
    return this.inflightRefresh;
  }

  private async readJson<T>(response: Response): Promise<T> {
    if (response.status === 204) {
      return undefined as unknown as T;
    }
    const text = await response.text();
    return text.length === 0 ? (undefined as unknown as T) : (JSON.parse(text) as T);
  }

  private applySession(session: StaffSession | null): void {
    this.session = session;
    if (session === null) {
      clearStaffSession();
    } else {
      writeStaffSession(session);
    }
    this.onSessionChanged(session);
  }
}

async function toApiError(response: Response): Promise<PlatformApiError> {
  let message = 'Club API call failed.';
  let code: string | null = null;
  try {
    const text = await response.text();
    if (text.length > 0) {
      const parsed = JSON.parse(text) as { error?: string; status?: string };
      if (typeof parsed.error === 'string' && parsed.error.length > 0) {
        message = parsed.error;
        code = parsed.error;
      }
      if (typeof parsed.status === 'string' && parsed.status.length > 0) {
        message = `${message} (${parsed.status})`;
      }
    }
  } catch {
    // Keep the fallback when the API returns non-JSON content.
  }
  return new PlatformApiError(response.status, message, code);
}
