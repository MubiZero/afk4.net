// Tajikistan numbers are country code 992 + 9 local digits. The wizard ships only to TJ clubs, so the
// +992 prefix is fixed (shown as a non-editable affix in the field) and the input holds ONLY the 9
// local digits — a country code typed/pasted by the user is dropped. Shared by the sign-in and
// password-reset screens so the phone field looks and behaves identically on both.

export function localPhoneDigits(value: string): string {
  const digits = value.replace(/\D/g, '');
  return (digits.startsWith('992') ? digits.slice(3) : digits).slice(0, 9);
}

// Mask the local part as "93 738 00 70" (2-3-2-2). The +992 prefix lives outside the input, so the
// field value is the local part only (empty when nothing is typed).
export function formatLocal(value: string): string {
  const local = localPhoneDigits(value);
  const groups = [local.slice(0, 2), local.slice(2, 5), local.slice(5, 7), local.slice(7, 9)].filter(Boolean);
  return groups.join(' ');
}

// Full dialable digits sent to the backend: fixed 992 country code + the 9 local digits.
export function fullPhoneDigits(value: string): string {
  return `992${localPhoneDigits(value)}`;
}
