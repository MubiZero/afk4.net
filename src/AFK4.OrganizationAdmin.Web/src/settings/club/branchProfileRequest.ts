import { readString } from '../../operatorHelpers';
import type { BranchProfileDto, UpdateBranchProfileRequest } from '../../api/clients/settings';
import { normalizeWorkingHours } from './workingHours';
import type { ClubProfileForm } from './ClubProfileFields';

const blankToNull = (value: string): string | null => (value.trim() === '' ? null : value.trim());

// Профиль с сервера (camelCase DTO) → форма. Один источник маппинга для загрузки и для эха после
// save (ClubDestination), и для частичного обновления name/city (BranchesDestination rename) —
// updateBranchProfile — это full-record PATCH (все поля обязательны на бэке), поэтому даже
// «переименовать» обязано отправить весь профиль, не только name/city.
export function mapProfileToForm(profile: BranchProfileDto): ClubProfileForm {
  return {
    name: readString(profile, 'name', 'AFK4'),
    city: readString(profile, 'city', ''),
    description: readString(profile, 'description', ''),
    address: readString(profile, 'address', ''),
    phone: readString(profile, 'phone', ''),
    telegram: readString(profile, 'telegram', ''),
    website: readString(profile, 'website', ''),
    instagram: readString(profile, 'instagram', ''),
    logoUrl: (profile.logoUrl as string | null) ?? null,
    logoMediaId: (profile.logoMediaId as string | null) ?? null,
    timeZone: readString(profile, 'timeZone', 'Asia/Dushanbe'),
    locale: readString(profile, 'locale', 'ru'),
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
    timeZone: form.timeZone,
    locale: form.locale,
    workingHours: form.workingHours.map((day) => (day.isClosed
      ? { ...day, openTime: null, closeTime: null }
      : day))
  };
}
