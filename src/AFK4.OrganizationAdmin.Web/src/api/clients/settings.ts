import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type { CreateConsoleSeatRequest, DeviceInventoryItemDto } from '@afk4/contracts';
import type { PosProductDto, PosProductCategoryDto } from './pos';
import type {
  AddProductBarcodeRequest,
  AssignDeviceSeatRequest,
  BranchBookingSettingsDto,
  BranchProtectionProfileDto,
  UpdateBranchProtectionProfileRequest,
  StaffInviteSummaryDto,
  BranchProfileDto,
  BranchSettingsDto,
  CreatePackageDefinitionRequest,
  CreateProductCategoryRequest,
  CreateProductRequest,
  CreateSeatRequest,
  CreateStaffInviteRequest,
  CreateTariffRequest,
  CreateTariffVersionRequest,
  CreateZoneRequest,
  DeviceSeatAssignmentDto,
  PackageDefinitionDto,
  PackageOptionDto,
  ProductBarcodeDto,
  ReorderProductCategoriesRequest,
  ResetStaffUserPasswordRequest,
  SeatDto,
  StaffBranchCandidateDto,
  StaffInviteDto,
  StaffUserDto,
  TariffDto,
  TariffOptionDto,
  TariffVersionDto,
  UpdateBranchBookingSettingsRequest,
  UpdateBranchProfileRequest,
  UpdateBranchSettingsRequest,
  UpdatePackageDefinitionRequest,
  UpdateProductCategoryRequest,
  UpdateProductRequest,
  UpdateSeatRequest,
  UpdateStaffUserProfileRequest,
  UpdateStaffUserRolesRequest,
  UpdateStaffUserStateRequest,
  UpdateTariffRequest,
  UpdateTariffVersionRequest,
  UpdateZoneRequest,
  ZoneDto,
} from '@afk4/contracts';
export type {
  AddProductBarcodeRequest,
  AssignDeviceSeatRequest,
  BranchBookingSettingsDto,
  BranchProtectionProfileDto,
  UpdateBranchProtectionProfileRequest,
  BranchPhotoDto,
  BranchProfileDto,
  BranchSettingsDto,
  CreatePackageDefinitionRequest,
  CreateProductCategoryRequest,
  CreateProductRequest,
  CreateSeatRequest,
  CreateStaffInviteRequest,
  CreateTariffRequest,
  CreateTariffVersionRequest,
  CreateZoneRequest,
  DeviceSeatAssignmentDto,
  PackageDefinitionDto,
  PackageOptionDto,
  ProductBarcodeDto,
  ReorderProductCategoriesRequest,
  ResetStaffUserPasswordRequest,
  SeatDto,
  StaffBranchCandidateDto,
  StaffInviteDto,
  StaffUserDto,
  TariffDto,
  TariffOptionDto,
  TariffVersionDto,
  UpdateBranchBookingSettingsRequest,
  UpdateBranchProfileRequest,
  UpdateBranchSettingsRequest,
  UpdatePackageDefinitionRequest,
  UpdateProductCategoryRequest,
  UpdateProductRequest,
  UpdateSeatRequest,
  UpdateStaffUserProfileRequest,
  UpdateStaffUserRolesRequest,
  UpdateStaffUserStateRequest,
  UpdateTariffRequest,
  UpdateTariffVersionRequest,
  UpdateZoneRequest,
  ZoneDto,
} from '@afk4/contracts';

export interface BranchWorkingHoursDay {
  dayOfWeek: number; // 1=Пн … 7=Вс
  isClosed: boolean;
  openTime: string | null;
  closeTime: string | null;
}

export interface TariffSchedulePayload extends Record<string, unknown> {
  appliesOnDaysMask: number;
  appliesFromMinuteOfDay: number | null;
  appliesToMinuteOfDay: number | null;
}

export function createSettingsClient(api: PlatformApiClient) {
  return {
    getBranchProfile(branchId: Guid): Promise<BranchProfileDto> {
      return api.get<BranchProfileDto>(`branches/${branchId}/profile`);
    },
    updateBranchProfile(branchId: Guid, request: UpdateBranchProfileRequest): Promise<BranchProfileDto> {
      return api.patch<BranchProfileDto, UpdateBranchProfileRequest>(`branches/${branchId}/profile`, request);
    },
    // Настройки филиала. Ручное подтверждение новых ПК жило только здесь и не имело ни одного
    // клиента: включить проверку было нечем, поэтому очередь подтверждения никогда не наполнялась.
    getBranchSettings(branchId: Guid): Promise<BranchSettingsDto> {
      return api.get<BranchSettingsDto>(`branches/${branchId}/settings`);
    },
    updateBranchSettings(branchId: Guid, request: UpdateBranchSettingsRequest): Promise<BranchSettingsDto> {
      return api.put<BranchSettingsDto, UpdateBranchSettingsRequest>(`branches/${branchId}/settings`, request);
    },
    getBookingSettings(branchId: Guid): Promise<BranchBookingSettingsDto> {
      return api.get<BranchBookingSettingsDto>(`branches/${branchId}/booking-settings`);
    },
    updateBookingSettings(branchId: Guid, request: UpdateBranchBookingSettingsRequest): Promise<BranchBookingSettingsDto> {
      return api.put<BranchBookingSettingsDto, UpdateBranchBookingSettingsRequest>(`branches/${branchId}/booking-settings`, request);
    },
    // Профиль защиты ПК филиала (спека оболочки, §6.3): агенты перечитывают его по версии.
    getProtectionProfile(branchId: Guid): Promise<BranchProtectionProfileDto> {
      return api.get<BranchProtectionProfileDto>(`branches/${branchId}/settings/protection`);
    },
    updateProtectionProfile(branchId: Guid, request: UpdateBranchProtectionProfileRequest): Promise<BranchProtectionProfileDto> {
      return api.put<BranchProtectionProfileDto, UpdateBranchProtectionProfileRequest>(`branches/${branchId}/settings/protection`, request);
    },
    getStaffUsers(branchId: Guid): Promise<StaffUserDto[]> {
      return api.get<StaffUserDto[]>(`branches/${branchId}/staff`);
    },
    // Добавленные, но ещё не входившие сотрудники — со статусом кода первого входа.
    listStaffInvites(branchId: Guid): Promise<StaffInviteSummaryDto[]> {
      return api.get<StaffInviteSummaryDto[]>(`branches/${branchId}/staff/invites`);
    },
    revokeStaffInvite(branchId: Guid, staffInviteId: Guid): Promise<void> {
      return api.delete<void>(`branches/${branchId}/staff/invites/${staffInviteId}`);
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
    // Сотрудники сети, которых нет в этом филиале, — кого можно добавить сюда ролями (только владельцу).
    getStaffCandidates(branchId: Guid): Promise<StaffBranchCandidateDto[]> {
      return api.get<StaffBranchCandidateDto[]>(`branches/${branchId}/staff/candidates`);
    },
    // Снять с филиала: все роли человека здесь. Себя и владельца сервер не снимает.
    removeStaffFromBranch(branchId: Guid, staffUserId: Guid): Promise<void> {
      return api.delete<void>(`branches/${branchId}/staff/${staffUserId}`);
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
    // Список категорий филиала. До него категории собирались из каталога товаров: категория без
    // единого товара исчезала из выбора после перезагрузки, а её имя подставлялось как «категория
    // <первые 8 символов guid>».
    listProductCategories(branchId: Guid): Promise<PosProductCategoryDto[]> {
      return api.get<PosProductCategoryDto[]>(`branches/${branchId}/pos/categories`);
    },
    updateProductCategory(branchId: Guid, categoryId: Guid, request: UpdateProductCategoryRequest): Promise<PosProductCategoryDto> {
      return api.patch<PosProductCategoryDto, UpdateProductCategoryRequest>(`branches/${branchId}/pos/categories/${categoryId}`, request);
    },
    reorderProductCategories(branchId: Guid, request: ReorderProductCategoriesRequest): Promise<PosProductCategoryDto[]> {
      return api.post<PosProductCategoryDto[], ReorderProductCategoriesRequest>(`branches/${branchId}/pos/categories/order`, request);
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
    },
    // Консоль без агента на месте: сессию ведёт администратор.
    createConsoleSeat(branchId: Guid, request: CreateConsoleSeatRequest): Promise<DeviceInventoryItemDto> {
      return api.post<DeviceInventoryItemDto, CreateConsoleSeatRequest>(`branches/${branchId}/consoles`, request);
    }
  };
}
