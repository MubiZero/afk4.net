import type { BranchProfileDto, UpdateBranchProfileRequest } from '../../api/clients/settings';
import { normalizeWorkingHours } from './workingHours';
import type { ClubProfileForm } from './ClubProfileFields';

const blankToNull = (value: string): string | null => (value.trim() === '' ? null : value.trim());

// Пустое поле — это «координаты не заданы», а не ноль: нулевые широта и долгота поставили бы клуб
// в Атлантический океан. Недонабранное («38.») и одинокая половина пары тоже читаются как «не
// задано» — клуб останется в списке, просто без точки на карте.
function coordinatePair(form: ClubProfileForm): { latitude: number | null; longitude: number | null } {
  const parse = (value: string): number | null => {
    const trimmed = value.trim().replace(',', '.');
    if (trimmed === '') return null;
    const parsed = Number(trimmed);
    return Number.isFinite(parsed) ? parsed : null;
  };
  const latitude = parse(form.latitude);
  const longitude = parse(form.longitude);
  return latitude === null || longitude === null
    ? { latitude: null, longitude: null }
    : { latitude, longitude };
}

// Профиль с сервера (camelCase DTO) → форма. Один источник маппинга для загрузки и для эха после
// save (ClubDestination), и для частичного обновления name/city (BranchesDestination rename) —
// updateBranchProfile — это full-record PATCH (все поля обязательны на бэке), поэтому даже
// «переименовать» обязано отправить весь профиль, не только name/city.
export function mapProfileToForm(profile: BranchProfileDto): ClubProfileForm {
  return {
    name: profile.name || 'AFK4',
    city: profile.city ?? '',
    description: profile.description ?? '',
    address: profile.address ?? '',
    phone: profile.phone ?? '',
    telegram: profile.telegram ?? '',
    website: profile.website ?? '',
    instagram: profile.instagram ?? '',
    logoUrl: profile.logoUrl ?? null,
    logoMediaId: profile.logoMediaId ?? null,
    coverImageUrl: profile.coverImageUrl ?? null,
    coverMediaId: profile.coverMediaId ?? null,
    photos: (profile.photos ?? []).map((photo) => ({
      url: photo.url,
      mediaId: photo.mediaId ?? null
    })),
    latitude: profile.latitude === null || profile.latitude === undefined ? '' : String(profile.latitude),
    longitude: profile.longitude === null || profile.longitude === undefined ? '' : String(profile.longitude),
    timeZone: profile.timeZone || 'Asia/Dushanbe',
    locale: profile.locale || 'ru',
    workingHours: normalizeWorkingHours(profile.workingHours)
  };
}

export function buildUpdateBranchProfileRequest(organizationId: string, form: ClubProfileForm): UpdateBranchProfileRequest {
  return {
    organizationId,
    name: form.name.trim(),
    city: form.city.trim(),
    description: blankToNull(form.description),
    address: blankToNull(form.address),
    phone: blankToNull(form.phone),
    telegram: blankToNull(form.telegram),
    website: blankToNull(form.website),
    instagram: blankToNull(form.instagram),
    logoUrl: form.logoUrl,
    logoMediaId: form.logoMediaId,
    coverImageUrl: form.coverImageUrl,
    coverMediaId: form.coverMediaId,
    photos: form.photos,
    // Координаты нужны парой: одна широта ставит клуб на нулевой меридиан, и бэкенд такую
    // пару отклонит — отправляем обе или ни одной.
    latitude: coordinatePair(form).latitude,
    longitude: coordinatePair(form).longitude,
    timeZone: form.timeZone,
    locale: form.locale,
    workingHours: form.workingHours.map((day) => (day.isClosed
      ? { ...day, openTime: null, closeTime: null }
      : day))
  };
}
