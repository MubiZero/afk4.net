import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type { FloorMapDto } from '@afk4/contracts';
export type {
  FloorMapDto,
  FloorMapZoneDto,
  SeatStatusDto,
} from '@afk4/contracts';

export function createFloorMapClient(api: PlatformApiClient) {
  return {
    getFloorMap(branchId: Guid): Promise<FloorMapDto> {
      return api.get<FloorMapDto>(`branches/${branchId}/floor-map`);
    }
  };
}
