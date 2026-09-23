import { describe, expect, it } from 'bun:test';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Каждый класс, который мастер пишет в разметку, должен быть где-то описан.
 *
 * Без этой проверки в мастере жили восемь классов, не определённых нигде: `wizard-button`,
 * `is-ghost`, `wizard-error`, `wizard-hint`, `wizard-spin`, `wizard-input`, `wizard-staff-name`,
 * `wizard-fallback-link`. Четыре экрана из девяти рисовали системные кнопки посреди оформленного
 * мастера — и ни один тест этого не замечал: разметка собиралась, тесты находили кнопки по
 * подписям, а выглядело это сломанным только глазами, которых у ворот нет.
 */
const wizardSrc = import.meta.dir;
const repoRoot = join(wizardSrc, '..', '..', '..');

/**
 * Весь каскад мастера, а не один файл: `styles.css` — барелл, и половина имён теперь приходит
 * из общего кита. Слои читаются по самому баррелу, чтобы новый `@import` не пришлось дублировать
 * здесь руками — иначе проверка начнёт врать на первом же добавленном слое.
 */
function readCss(): string {
  const barrel = join(wizardSrc, 'styles.css');
  const sources = [readFileSync(barrel, 'utf8'), readFileSync(join(repoRoot, 'packages', 'tokens', 'tokens.css'), 'utf8')];
  for (const [, specifier] of sources[0].matchAll(/@import '([^']+)'/g)) {
    const path = specifier.startsWith('@afk4/')
      ? join(repoRoot, 'packages', specifier.slice('@afk4/'.length))
      : join(wizardSrc, specifier);
    sources.push(readFileSync(path, 'utf8'));
  }
  return sources.join('\n');
}

function tsxFiles(): string[] {
  return readdirSync(wizardSrc)
    .filter((name) => name.endsWith('.tsx') && !name.endsWith('.test.tsx'))
    .map((name) => join(wizardSrc, name));
}

/**
 * Классы из разметки. Берутся только строковые литералы внутри `className=`: в них и живут
 * настоящие имена, а собранное из переменных проверить статически нельзя — такие места
 * перечислены в исключениях ниже поимённо.
 */
function usedClasses(source: string): string[] {
  const classes: string[] = [];
  const attribute = /className=(\{[^}]*\}|"[^"]*")/g;
  for (const [, value] of source.matchAll(attribute)) {
    for (const [, literal] of value.matchAll(/["'`]([^"'`]*)["'`]/g)) {
      // Кусок шаблонной строки вида `is-close` + ${...}: имя обрывается на подстановке, и
      // проверять его целиком нечего.
      if (literal.includes('${')) continue;
      // Только имена с дефисом. В `className={mode === 'phone' ? 'wizard-phone-field' : undefined}`
      // строковых литералов два, и один из них — не класс, а значение сравнения. Разобрать это
      // без разбора выражения нельзя, а все имена мастера дефисные без единого исключения
      // (`wizard-*`, `is-*`), поэтому правило «есть дефис — это класс» ловит весь тот класс
      // ошибок, ради которого проверка и написана, и не выдумывает промахов на ровном месте.
      classes.push(...literal.split(/\s+/).filter((name) => name.includes('-')));
    }
  }
  return classes;
}

// Имена, которые склеиваются из подстановки и статически не читаются. Каждое здесь названо
// вручную, чтобы список нельзя было пополнить молча.
const COMPOSED = new Set(['is-close', 'is-armed']);

describe('классы мастера', () => {
  const css = readCss();
  const defined = new Set([...css.matchAll(/\.([a-zA-Z][a-zA-Z0-9_-]*)/g)].map((match) => match[1]));

  it('находит свою разметку и свои стили', () => {
    expect(tsxFiles().length).toBeGreaterThan(5);
    expect(defined.size).toBeGreaterThan(50);
  });

  it('не использует ни одного класса, которого нет в стилях', () => {
    const missing = new Map<string, string[]>();
    for (const file of tsxFiles()) {
      for (const name of usedClasses(readFileSync(file, 'utf8'))) {
        if (defined.has(name) || COMPOSED.has(name)) continue;
        const where = missing.get(name) ?? [];
        where.push(file.split('/').pop()!);
        missing.set(name, where);
      }
    }

    expect([...missing.keys()]).toEqual([]);
  });

  // `.ui-phone-field` — это рамка вокруг «+992» и поля. Своя рамка есть и у голого `<input>`, и
  // гасит её только `ui-phone-input`. На экране сотрудников класса не было: внутри поля стояла
  // вторая узкая рамка, а клик по правой части поля не ставил курсор. Класс-проверка выше этого
  // не видит — все имена в разметке существуют, просто одного не хватает.
  it('в каждом поле телефона поле ввода нарисовано китом', () => {
    const broken: string[] = [];
    for (const file of tsxFiles()) {
      const source = readFileSync(file, 'utf8');
      for (const match of source.matchAll(/ui-phone-field/g)) {
        const rest = source.slice(match.index);
        const input = rest.slice(rest.indexOf('<input'), rest.indexOf('/>', rest.indexOf('<input')));
        if (!input.includes('ui-phone-input')) broken.push(file.split('/').pop()!);
      }
    }

    expect(broken).toEqual([]);
  });
});
