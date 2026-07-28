// Номера Таджикистана: код страны 992 + 9 локальных цифр. Operator работает только по TJ-клубам,
// поэтому префикс +992 фиксирован (несменяемый affix у поля), а в инпут идут только 9 локальных цифр.
// Общий код для экранов входа и сброса пароля, чтобы поле телефона выглядело и вело себя одинаково.

export function localPhoneDigits(value: string): string {
  const digits = value.replace(/\D/g, '');
  return (digits.startsWith('992') ? digits.slice(3) : digits).slice(0, 9);
}

// Маска локальной части «93 738 00 70» (2-3-2-2). Префикс +992 живёт вне инпута.
export function formatLocal(value: string): string {
  const local = localPhoneDigits(value);
  const groups = [local.slice(0, 2), local.slice(2, 5), local.slice(5, 7), local.slice(7, 9)].filter(Boolean);
  return groups.join(' ');
}

// Полные цифры на бэк: фиксированный код 992 + 9 локальных.
export function fullPhoneDigits(value: string): string {
  return `992${localPhoneDigits(value)}`;
}
