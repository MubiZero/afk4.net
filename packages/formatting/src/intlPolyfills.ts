/**
 * Языки, которые экраны отдают `Intl`: ru-RU (и числа таджикского), en-US, en-GB (раскладка дат таджикского и
 * английского на экране игрока). Таджикского `Intl` не получает — см. `formatTajikDate`.
 */
const INTL_LOCALES = ['ru', 'en', 'en-GB'];

/**
 * Не знает хоть одного из языков. Своя проверка, а не `shouldPolyfill` FormatJS: тот несёт списки всех языков
 * CLDR и утяжелил бы каждый экран, хотя полифилы нужны только Miniblink.
 */
const lacksLocales = (format: { supportedLocalesOf(locales: string[]): string[] }) =>
  format.supportedLocalesOf(INTL_LOCALES).length < INTL_LOCALES.length;

/**
 * Русские даты и числа там, где движок их не знает. Miniblink (экраны программ на ПК, Windows 7–11) собран с
 * `Intl` только для `en`: дата выходит «M11 6, Fri», сумма — «1,480.5». Тогда, и только тогда, `Intl` заменяется
 * полифилами FormatJS с данными ru и en; браузеры и WebView2 ничего не качают. Код форматирования экранов не
 * меняется: `Intl.*Format` и `toLocale*String` после замены пишут по-русски.
 *
 * Звать до первого показа экрана. Не бросает: без полифилов экран покажется с английскими датами, а не пустым.
 */
export async function loadIntlPolyfills(): Promise<void> {
  try {
    await polyfillIntl();
  } catch (error) {
    console.error('Intl polyfills failed to load', error);
  }
}

/** Порядок — по зависимостям полифилов. */
async function polyfillIntl(): Promise<void> {
  if (typeof Intl === 'undefined') return;
  const nativeTimeZone = currentTimeZone();

  if (typeof Intl.getCanonicalLocales !== 'function') await import('@formatjs/intl-getcanonicallocales/polyfill.js');
  if (typeof Intl.Locale !== 'function') await import('@formatjs/intl-locale/polyfill.js');

  if (lacksLocales(Intl.PluralRules)) {
    await import('@formatjs/intl-pluralrules/polyfill-force.js');
    await Promise.all([
      import('@formatjs/intl-pluralrules/locale-data/ru.js'),
      import('@formatjs/intl-pluralrules/locale-data/en.js'),
    ]);
  }

  if (lacksLocales(Intl.NumberFormat)) {
    await import('@formatjs/intl-numberformat/polyfill-force.js');
    await Promise.all([
      import('@formatjs/intl-numberformat/locale-data/ru.js'),
      import('@formatjs/intl-numberformat/locale-data/en.js'),
      import('@formatjs/intl-numberformat/locale-data/en-GB.js'),
    ]);
  }

  if (lacksLocales(Intl.DateTimeFormat)) {
    await import('@formatjs/intl-datetimeformat/polyfill-force.js');
    await Promise.all([
      import('@formatjs/intl-datetimeformat/locale-data/ru.js'),
      import('@formatjs/intl-datetimeformat/locale-data/en.js'),
      import('@formatjs/intl-datetimeformat/locale-data/en-GB.js'),
      // Основные пояса, среди них Asia/Dushanbe — пояс клубов. Все пояса — вдвое больше.
      import('@formatjs/intl-datetimeformat/add-golden-tz.js'),
    ]);
    setDefaultTimeZone(nativeTimeZone);
  }
}

/** Пояс ПК, пока `Intl` ещё родной: полифил о нём не знает и без подсказки считает всё в UTC. */
function currentTimeZone(): string {
  try {
    const zone = new Intl.DateTimeFormat().resolvedOptions().timeZone;
    if (zone) return zone;
  } catch {
    // Родной Intl без поясов — ниже пояс по смещению.
  }
  return offsetTimeZone(new Date().getTimezoneOffset());
}

/** «+05:00» для UTC+5 (у `getTimezoneOffset` знак обратный). Полифил понимает такие пояса без данных о них. */
export function offsetTimeZone(offsetMinutes: number): string {
  const east = -offsetMinutes;
  const abs = Math.abs(east);
  const two = (n: number) => String(n).padStart(2, '0');
  return `${east < 0 ? '-' : '+'}${two(Math.floor(abs / 60))}:${two(abs % 60)}`;
}

function setDefaultTimeZone(zone: string): void {
  const polyfilled = Intl.DateTimeFormat as unknown as { __setDefaultTimeZone?: (zone: string) => void };
  if (!polyfilled.__setDefaultTimeZone) return;
  try {
    polyfilled.__setDefaultTimeZone(zone);
  } catch {
    // Пояса нет среди основных — смещение часов ПК.
    polyfilled.__setDefaultTimeZone(offsetTimeZone(new Date().getTimezoneOffset()));
  }
}
