import { expect, it } from 'bun:test';
import { join } from 'node:path';
import { offsetTimeZone } from './intlPolyfills';

const sample = join(import.meta.dir, 'test', 'intlSamples.ts');

function samples(...args: string[]): Record<string, string | boolean> {
  // Пояс Windows у клубов — Душанбе; без него полифил считал бы всё в UTC и тест это бы поймал.
  const run = Bun.spawnSync(['bun', sample, ...args], { env: { ...process.env, TZ: 'Asia/Dushanbe' } });
  if (run.exitCode !== 0) throw new Error(run.stderr.toString());
  return JSON.parse(run.stdout.toString());
}

/**
 * Данные CLDR у полифилов новее, чем у ICU в bun: другой пробел (U+202F вместо U+00A0 или обычного) и
 * «Сб, 31 октября в 14:05» против «сб, 31 октября, 14:05». У WebView2 своя версия — это не ошибка, сравниваем по сути.
 */
function sameCldr(values: Record<string, string | boolean>): Record<string, string> {
  return Object.fromEntries(
    Object.entries(values).map(([key, value]) => [key, String(value).replace(/\s/g, ' ').replace(' в ', ', ').toLowerCase()])
  );
}

// Miniblink знает в Intl только `en`: без полифилов сумма «1,480.5», дата «M10 31». С ними — как в браузере.
it('в движке без русского Intl даты и числа пишутся как в браузере', () => {
  const { polyfilled: nativePolyfilled, ...native } = samples();
  const { polyfilled, ...miniblink } = samples('--miniblink');

  expect(nativePolyfilled).toBe(false);
  expect(polyfilled).toBe(true);
  expect(sameCldr(miniblink)).toEqual(sameCldr(native));
  expect(native.clock).toBe('14:05');
});

it('пояс по смещению часов ПК — в виде «+05:00»', () => {
  expect(offsetTimeZone(-300)).toBe('+05:00');
  expect(offsetTimeZone(-330)).toBe('+05:30');
  expect(offsetTimeZone(0)).toBe('+00:00');
  expect(offsetTimeZone(180)).toBe('-03:00');
});
