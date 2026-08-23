import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type { PosProductDto, PosProductCategoryDto } from './pos';

export type StaffUserDto = Record<string, unknown>;
export type BranchProfileDto = Record<string, unknown>;
export type ZoneDto = Record<string, unknown>;
export type SeatDto = Record<string, unknown>;
export type TariffDto = Record<string, unknown>;
export type TariffVersionDto = Record<string, unknown>;
export type TariffOptionDto = Record<string, unknown>;
export interface PackageOptionDto extends Record<string, unknown> {
  packageDefinitionId: Guid;
  name: string;
  currencyCode: string;
  priceMinorUnits: number;
  includedSeconds: number;
  bonusSeconds: number;
  expiresAfterDays: number;
}
export type PackageDefinitionDto = Record<string, unknown>;
export type DeviceSeatAssignmentDto = Record<string, unknown>;

export interface CreateStaffInviteRequest extends Record<string, unknown> {
  organizationId: Guid;
  userName: string;
  displayName: string;
  /** Куда уйдёт код приглашения. Обязателен: почты у администратора зала может не быть. */
  phoneNumber: string;
  email: string | null;
  roleNames: string[];
}

export interface StaffInviteDto {
  staffInviteId: Guid;
  code: string;
  expiresAtUtc: string;
}

export interface UpdateStaffUserProfileRequest extends Record<string, unknown> {
  organizationId: Guid;
  userName: string;
  displayName: string;
}

export interface UpdateStaffUserRolesRequest extends Record<string, unknown> {
  organizationId: Guid;
  roleNames: string[];
}

export interface UpdateStaffUserStateRequest extends Record<string, unknown> {
  organizationId: Guid;
  isActive: boolean;
}

export interface ResetStaffUserPasswordRequest extends Record<string, unknown> {
  organizationId: Guid;
  newPassword: string;
}

export interface BranchWorkingHoursDay {
  dayOfWeek: number; // 1=Пн … 7=Вс
  isClosed: boolean;
  openTime: string | null;
  closeTime: string | null;
}

export interface UpdateBranchProfileRequest extends Record<string, unknown> {
  organizationId: Guid;
  name: string;
  city: string;
  description: string | null;
  address: string | null;
  phone: string | null;
  telegram: string | null;
  website: string | null;
  instagram: string | null;
  logoUrl: string | null;
  logoMediaId: string | null;
  timeZone: string;
  locale: string;
  workingHours: BranchWorkingHoursDay[];
  // Витрина клуба в приложении игрока: фото зала и точка на карте.
  coverImageUrl: string | null;
  coverMediaId: string | null;
  photos: Array<{ url: string; mediaId: string | null }>;
  latitude: number | null;
  longitude: number | null;
}

export interface CreateZoneRequest extends Record<string, unknown> {
  organizationId: Guid;
  name: string;
  sortOrder: number;
  // Чем зал оснащён — эту строку игрок видит в подробностях клуба; null = не указано.
  hardwareSummary?: string | null;
}

export interface UpdateZoneRequest extends Record<string, unknown> {
  organizationId: Guid;
  name: string;
  sortOrder: number;
  hardwareSummary?: string | null;
}

export interface CreateSeatRequest extends Record<string, unknown> {
  organizationId: Guid;
  zoneId: Guid;
  name: string;
  sortOrder: number;
}

export interface UpdateSeatRequest extends Record<string, unknown> {
  organizationId: Guid;
  zoneId: Guid;
  name: string;
  sortOrder: number;
}

export interface CreateTariffRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface CreateTariffVersionRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface TariffSchedulePayload extends Record<string, unknown> {
  appliesOnDaysMask: number;
  appliesFromMinuteOfDay: number | null;
  appliesToMinuteOfDay: number | null;
}

export interface UpdateTariffRequest extends Record<string, unknown> {
  organizationId: Guid;
  name: string;
  isActive: boolean;
  /** Не передано — расписание остаётся прежним. */
  schedule?: TariffSchedulePayload;
}

export interface UpdateTariffVersionRequest extends Record<string, unknown> {
  organizationId: Guid;
  currencyCode: string;
  pricePerMinuteMinorUnits: number;
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
  effectiveFromUtc: string;
  isActive: boolean;
}

export interface CreatePackageDefinitionRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface UpdatePackageDefinitionRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface CreateProductCategoryRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface CreateProductRequest extends Record<string, unknown> {
  organizationId: Guid;
  availableInShell?: boolean;
}

export interface UpdateProductRequest extends Record<string, unknown> {
  organizationId: Guid;
  availableInShell?: boolean;
}

export interface ProductBarcodeDto {
  barcodeId: Guid;
  productId: Guid;
  code: string;
  isPrimary: boolean;
}

export interface AddProductBarcodeRequest extends Record<string, unknown> {
  organizationId: Guid;
  code: string;
  isPrimary?: boolean;
}

export interface AssignDeviceSeatRequest extends Record<string, unknown> {
  organizationId: Guid;
  seatId: Guid;
}


// Настройки приёма гостей филиала (BranchBookingSettingsDto). `updatedAtUtc === null` означает
// «филиал ничего не настраивал» — значения не нулевые, а по умолчанию, и это разные вещи.
export interface BranchBookingSettingsDto {
  organizationId: Guid;
  branchId: Guid;
  acceptanceMode: string; // 'off' | 'manual' | 'auto'
  respondWithinMinutes: number;
  requirePrepaymentFromNewGuests: boolean;
  maxActiveReservationsForNewGuests: number;
  regularAfterVisits: number;
  holdSeatAfterStartMinutes: number;
  keepPrepaymentOnNoShow: boolean;
  updatedAtUtc: string | null;
}

export interface UpdateBranchBookingSettingsRequest extends Record<string, unknown> {
  organizationId: Guid;
  acceptanceMode: string;
  respondWithinMinutes: number;
  requirePrepaymentFromNewGuests: boolean;
  maxActiveReservationsForNewGuests: number;
  regularAfterVisits: number;
  holdSeatAfterStartMinutes: number;
  keepPrepaymentOnNoShow: boolean;
}

export function createSettingsClient(api: PlatformApiClient) {
  return {
    getBranchProfile(branchId: Guid): Promise<BranchProfileDto> {
      return api.get<BranchProfileDto>(`branches/${branchId}/profile`);
    },
    updateBranchProfile(branchId: Guid, request: UpdateBranchProfileRequest): Promise<BranchProfileDto> {
      return api.patch<BranchProfileDto, UpdateBranchProfileRequest>(`branches/${branchId}/profile`, request);
    },
    getBookingSettings(branchId: Guid): Promise<BranchBookingSettingsDto> {
      return api.get<BranchBookingSettingsDto>(`branches/${branchId}/booking-settings`);
    },
    updateBookingSettings(branchId: Guid, request: UpdateBranchBookingSettingsRequest): Promise<BranchBookingSettingsDto> {
      return api.put<BranchBookingSettingsDto, UpdateBranchBookingSettingsRequest>(`branches/${branchId}/booking-settings`, request);
    },
    getStaffUsers(branchId: Guid): Promise<StaffUserDto[]> {
      return api.get<StaffUserDto[]>(`branches/${branchId}/staff`);
    },
    createStaffInvite(branchId: Guid, request: CreateStaffInviteRequest): Promise<StaffInviteDto> {
      return api.post<StaffInviteDto, CreateStaffInviteRequest>(`branches/${branchId}/staff/invites`, request);
    },
    updateStaffUserProfile(branchId: Guid, staffUserId: Guid, request: UpdateStaffUserProfileRequest): Promise<StaffUserDto> {
      return api.patch<StaffUserDto, UpdateStaffUserProfileRequest>(`branches/${branchId}/staff/${staffUserId}/profile`, request);
    },
    updateStaffUserRoles(branchId: Guid, staffUserId: Guid, request: UpdateStaffUserRolesRequest): Promise<StaffUserDto> {
      return api.patch<StaffUserDto, UpdateStaffUserRolesRequest>(`branches/${branchId}/staff/${staffUserId}/roles`, request);
    },
    updateStaffUserState(branchId: Guid, staffUserId: Guid, request: UpdateStaffUserStateRequest): Promise<StaffUserDto> {
      return api.patch<StaffUserDto, UpdateStaffUserStateRequest>(`branches/${branchId}/staff/${staffUserId}/state`, request);
    },
    resetStaffUserPassword(branchId: Guid, staffUserId: Guid, request: ResetStaffUserPasswordRequest): Promise<StaffUserDto> {
      return api.post<StaffUserDto, ResetStaffUserPasswordRequest>(`branches/${branchId}/staff/${staffUserId}/password-reset`, request);
    },
    getLayoutZones(branchId: Guid): Promise<ZoneDto[]> {
      return api.get<ZoneDto[]>(`branches/${branchId}/layout/zones`);
    },
    createZone(branchId: Guid, request: CreateZoneRequest): Promise<ZoneDto> {
      return api.post<ZoneDto, CreateZoneRequest>(`branches/${branchId}/layout/zones`, request);
    },
    updateZone(branchId: Guid, zoneId: Guid, request: UpdateZoneRequest): Promise<ZoneDto> {
      return api.patch<ZoneDto, UpdateZoneRequest>(`branches/${branchId}/layout/zones/${zoneId}`, request);
    },
    deleteZone(branchId: Guid, zoneId: Guid, organizationId: Guid): Promise<void> {
      return api.delete<void>(`branches/${branchId}/layout/zones/${zoneId}`, { organizationId });
    },
    createSeat(branchId: Guid, request: CreateSeatRequest): Promise<SeatDto> {
      return api.post<SeatDto, CreateSeatRequest>(`branches/${branchId}/layout/seats`, request);
    },
    updateSeat(branchId: Guid, seatId: Guid, request: UpdateSeatRequest): Promise<SeatDto> {
      return api.patch<SeatDto, UpdateSeatRequest>(`branches/${branchId}/layout/seats/${seatId}`, request);
    },
    deleteSeat(branchId: Guid, seatId: Guid, organizationId: Guid): Promise<void> {
      return api.delete<void>(`branches/${branchId}/layout/seats/${seatId}`, { organizationId });
    },
    createTariff(branchId: Guid, request: CreateTariffRequest): Promise<TariffDto> {
      return api.post<TariffDto, CreateTariffRequest>(`branches/${branchId}/tariffs`, request);
    },
    createTariffVersion(branchId: Guid, tariffId: Guid, request: CreateTariffVersionRequest): Promise<TariffVersionDto> {
      return api.post<TariffVersionDto, CreateTariffVersionRequest>(`branches/${branchId}/tariffs/${tariffId}/versions`, request);
    },
    updateTariff(branchId: Guid, tariffId: Guid, request: UpdateTariffRequest): Promise<TariffDto> {
      return api.patch<TariffDto, UpdateTariffRequest>(`branches/${branchId}/tariffs/${tariffId}`, request);
    },
    updateTariffVersion(branchId: Guid, tariffId: Guid, tariffVersionId: Guid, request: UpdateTariffVersionRequest): Promise<TariffVersionDto> {
      return api.patch<TariffVersionDto, UpdateTariffVersionRequest>(`branches/${branchId}/tariffs/${tariffId}/versions/${tariffVersionId}`, request);
    },
    getTariffOptions(branchId: Guid): Promise<TariffOptionDto[]> {
      return api.get<TariffOptionDto[]>(`branches/${branchId}/tariffs/options`);
    },
    getPackageOptions(branchId: Guid): Promise<PackageOptionDto[]> {
      return api.get<PackageOptionDto[]>(`branches/${branchId}/packages/options`);
    },
    createPackageDefinition(branchId: Guid, request: CreatePackageDefinitionRequest): Promise<PackageDefinitionDto> {
      return api.post<PackageDefinitionDto, CreatePackageDefinitionRequest>(`branches/${branchId}/packages`, request);
    },
    updatePackageDefinition(branchId: Guid, packageDefinitionId: Guid, request: UpdatePackageDefinitionRequest): Promise<PackageDefinitionDto> {
      return api.patch<PackageDefinitionDto, UpdatePackageDefinitionRequest>(`branches/${branchId}/packages/${packageDefinitionId}`, request);
    },
    createProductCategory(branchId: Guid, request: CreateProductCategoryRequest): Promise<PosProductCategoryDto> {
      return api.post<PosProductCategoryDto, CreateProductCategoryRequest>(`branches/${branchId}/pos/categories`, request);
    },
    createProduct(branchId: Guid, request: CreateProductRequest): Promise<PosProductDto> {
      return api.post<PosProductDto, CreateProductRequest>(`branches/${branchId}/pos/products`, request);
    },
    updateProduct(branchId: Guid, productId: Guid, request: UpdateProductRequest): Promise<PosProductDto> {
      return api.patch<PosProductDto, UpdateProductRequest>(`branches/${branchId}/pos/products/${productId}`, request);
    },
    getProductBarcodes(branchId: Guid, productId: Guid): Promise<ProductBarcodeDto[]> {
      return api.get<ProductBarcodeDto[]>(`branches/${branchId}/pos/products/${productId}/barcodes`);
    },
    addProductBarcode(branchId: Guid, productId: Guid, request: AddProductBarcodeRequest): Promise<ProductBarcodeDto> {
      return api.post<ProductBarcodeDto, AddProductBarcodeRequest>(`branches/${branchId}/pos/products/${productId}/barcodes`, request);
    },
    deleteProductBarcode(branchId: Guid, productId: Guid, barcodeId: Guid): Promise<void> {
      return api.delete<void>(`branches/${branchId}/pos/products/${productId}/barcodes/${barcodeId}`);
    },
    assignDeviceSeat(branchId: Guid, deviceId: Guid, request: AssignDeviceSeatRequest): Promise<DeviceSeatAssignmentDto> {
      return api.post<DeviceSeatAssignmentDto, AssignDeviceSeatRequest>(`branches/${branchId}/devices/${deviceId}/seat-assignment`, request);
    }
  };
}
