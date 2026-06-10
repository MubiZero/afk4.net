import type { ApiTransport } from '../apiTransport';
import type {
  DeviceInventoryItem,
  FloorMapBulkUpdateRequest,
  FloorMapBulkUpdateResponse,
  FloorMapRead
} from '../types';

export class VenueApi {
  public constructor(private readonly transport: ApiTransport) {}

  public async getFloorMap(branchId: string): Promise<FloorMapRead> {
    const response = await this.transport.sendRaw('GET', `/api/branches/${encodeURIComponent(branchId)}/floor-map`);
    const floorMap = await this.transport.readJson<FloorMapRead['floorMap']>(response);
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
    return this.transport.send<FloorMapBulkUpdateResponse>(
      'PUT',
      `/api/branches/${encodeURIComponent(branchId)}/floor-map`,
      request,
      etag === null ? undefined : { 'If-Match': etag }
    );
  }

  public listDevices(branchId: string): Promise<DeviceInventoryItem[]> {
    return this.transport.send<DeviceInventoryItem[]>('GET', `/api/branches/${encodeURIComponent(branchId)}/devices`);
  }

  public listPendingDevices(branchId: string): Promise<DeviceInventoryItem[]> {
    return this.transport.send<DeviceInventoryItem[]>('GET', `/api/branches/${encodeURIComponent(branchId)}/devices/pending`);
  }

  public approveDevice(deviceId: string, organizationId: string, reason: string): Promise<DeviceInventoryItem> {
    return this.transport.send<DeviceInventoryItem>(
      'POST',
      `/api/devices/${encodeURIComponent(deviceId)}/approve`,
      { organizationId, reason }
    );
  }

  public rejectDevice(deviceId: string, organizationId: string, reason: string): Promise<DeviceInventoryItem> {
    return this.transport.send<DeviceInventoryItem>(
      'POST',
      `/api/devices/${encodeURIComponent(deviceId)}/reject`,
      { organizationId, reason }
    );
  }

  public renameDevice(deviceId: string, organizationId: string, displayName: string): Promise<DeviceInventoryItem> {
    return this.transport.send<DeviceInventoryItem>(
      'POST',
      `/api/devices/${encodeURIComponent(deviceId)}/rename`,
      { organizationId, displayName }
    );
  }

  public moveDeviceSeat(deviceId: string, organizationId: string, seatId: string): Promise<DeviceInventoryItem> {
    return this.transport.send<DeviceInventoryItem>(
      'POST',
      `/api/devices/${encodeURIComponent(deviceId)}/move-seat`,
      { organizationId, seatId }
    );
  }

  public removeDevice(deviceId: string, organizationId: string, reason: string): Promise<DeviceInventoryItem> {
    return this.transport.send<DeviceInventoryItem>(
      'POST',
      `/api/devices/${encodeURIComponent(deviceId)}/remove`,
      { organizationId, reason }
    );
  }
}
