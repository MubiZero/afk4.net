// Образцы дат и чисел экранов. Запускается отдельным процессом: `--miniblink` урезает родной Intl до одного `en`,
// как в Miniblink, и ставит полифилы. Отдельный процесс — потому что полифилы подменяют глобальный Intl насовсем.
import { formatCurrency, formatDateParts, formatNumber } from '../index';
import { loadIntlPolyfills } from '../intlPolyfills';

if (process.argv.includes('--miniblink')) {
  const onlyEn = (locales: string | readonly string[]) =>
    ([] as string[]).concat(locales as string[]).filter((locale) => locale === 'en' || locale === 'en-US');
  for (const ctor of [Intl.DateTimeFormat, Intl.NumberFormat, Intl.PluralRules] as unknown as { supportedLocalesOf: unknown }[]) {
    Object.defineProperty(ctor, 'supportedLocalesOf', { value: onlyEn, configurable: true, writable: true });
  }
  await loadIntlPolyfills();
}

const at = '2026-10-31T09:05:00.000Z';
console.log(JSON.stringify({
  polyfilled: '__setDefaultTimeZone' in Intl.DateTimeFormat,
  number: formatNumber(1480.5, 'ru-RU'),
  tajikNumber: formatNumber(1480.5, 'tg-TJ'),
  enNumber: formatNumber(1480.5, 'en-US'),
  currency: formatCurrency(4250, 'TJS', 'ru-RU'),
  date: formatDateParts(at, 'ru-RU', { dateStyle: 'medium', timeStyle: 'short' }),
  showcase: formatDateParts(at, 'ru-RU', { weekday: 'short', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' }),
  offerUntil: formatDateParts(at, 'ru-RU', { day: 'numeric', month: 'long', year: 'numeric' }),
  clubTime: formatDateParts(at, 'ru-RU', { hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Dushanbe' }),
  tajikDate: formatDateParts(at, 'tg-TJ', { weekday: 'short', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' }),
  enDate: formatDateParts(at, 'en-GB', { day: 'numeric', month: 'long', year: 'numeric' }),
  clock: new Date(at).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' }),
  localTime: new Date(at).toLocaleString('ru-RU'),
}));
