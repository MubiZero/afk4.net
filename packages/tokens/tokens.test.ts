import { describe, expect, test } from 'bun:test';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const ROOT = join(import.meta.dir, '..', '..');
const tokensCss = readFileSync(join(import.meta.dir, 'tokens.css'), 'utf8');

// Барелл `styles.css` состоит из одних @import, поэтому раньше гвард читал пустоту и
// пропустил 53 обращения к токенам, которых нет (весь раздел «Отчёты» остался без границ
// и без фона: невалидный var() выбрасывает свойство целиком, молча). Читаем сами стили.
const APP_STYLE_DIRS = [
  'src/AFK4.OrganizationAdmin.Web/src',
  'src/AFK4.PlatformControl.Web/src',
  'src/AFK4.SetupWizard.Web/src',
  'src/AFK4.Player.Shell.Web/src',
];

function appFiles(appDir: string, ext: string): string[] {
  const out: string[] = [];
  const walk = (dir: string) => {
    for (const entry of readdirSync(join(ROOT, dir), { withFileTypes: true })) {
      const rel = `${dir}/${entry.name}`;
      if (entry.isDirectory()) walk(rel);
      else if (entry.name.endsWith(ext)) out.push(rel);
    }
  };
  walk(appDir);
  return out;
}

function definedVars(css: string): Set<string> {
  const out = new Set<string>();
  for (const m of css.matchAll(/(--[a-z0-9-]+)\s*:/gi)) out.add(m[1]);
  return out;
}

function usedVars(css: string): Set<string> {
  const out = new Set<string>();
  for (const m of css.matchAll(/var\((--[a-z0-9-]+)/gi)) out.add(m[1]);
  return out;
}

// Every var(--x) a consuming app references must be defined either in the shared package
// or locally in that app's own stylesheet (covers app-local aliases like --panel / --chart-*).
describe('used vars are defined', () => {
  const pkgDefs = definedVars(tokensCss);
  const kitCss = readdirSync(join(ROOT, 'packages/ui'))
    .filter((name) => name.endsWith('.css'))
    .map((name) => readFileSync(join(ROOT, 'packages/ui', name), 'utf8'))
    .join('\n');
  for (const appDir of APP_STYLE_DIRS) {
    test(appDir, () => {
      const sheets = appFiles(appDir, '.css');
      // Переменная может объявляться в одном файле приложения, а использоваться в другом —
      // поэтому локальные определения собираем по всему приложению разом.
      const css = sheets.map((rel) => readFileSync(join(ROOT, rel), 'utf8')).join('\n');
      // Часть переменных приложение задаёт из кода (ширина колонки оболочки, высота полосы
      // вкладок, число метрик кассы) — они существуют, просто не в таблице стилей.
      const fromCode = appFiles(appDir, '.tsx')
        .map((rel) => readFileSync(join(ROOT, rel), 'utf8'))
        .join('\n');
      const setFromCode = [...fromCode.matchAll(/['"](--[a-z0-9-]+)['"]\s*:/gi)].map((m) => m[1]);
      const localDefs = new Set([...definedVars(`${css}
${kitCss}`), ...setFromCode]);
      const missing = [...usedVars(css)].filter(
        (v) => !pkgDefs.has(v) && !localDefs.has(v) && !v.startsWith('--chart-'),
      );
      expect(missing).toEqual([]);
    });
  }
});

// ── WCAG contrast ──────────────────────────────────────────────────────────────
type RGBA = [number, number, number, number];

function parseColor(c: string): RGBA {
  const s = c.trim();
  if (s.startsWith('#')) {
    const n = parseInt(s.slice(1), 16);
    return [(n >> 16) & 255, (n >> 8) & 255, n & 255, 1];
  }
  const m = s.match(/rgba?\(([^)]+)\)/i);
  if (!m) throw new Error(`unparseable color: ${c}`);
  const p = m[1].split(',').map((x) => parseFloat(x.trim()));
  return [p[0], p[1], p[2], p[3] ?? 1];
}

// Composite a (possibly translucent) foreground over an opaque background.
function over(fg: RGBA, bg: RGBA): RGBA {
  const a = fg[3];
  return [fg[0] * a + bg[0] * (1 - a), fg[1] * a + bg[1] * (1 - a), fg[2] * a + bg[2] * (1 - a), 1];
}

function luminance([r, g, b]: RGBA): number {
  const f = (v: number) => {
    const x = v / 255;
    return x <= 0.03928 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
}

function contrast(fg: string, bg: string): number {
  const bgc = parseColor(bg);
  const l1 = luminance(over(parseColor(fg), bgc));
  const l2 = luminance(bgc);
  const [hi, lo] = l1 > l2 ? [l1, l2] : [l2, l1];
  return (hi + 0.05) / (lo + 0.05);
}

function themeVars(css: string, theme: string): Record<string, string> {
  const block = css.match(new RegExp(`\\[data-theme="${theme}"\\]\\s*\\{([^}]+)\\}`));
  if (!block) throw new Error(`no ${theme} block`);
  // Комментарии вырезаются ДО разбора. Иначе фраза вида «отдельно от --border-default:» внутри
  // /* ... */ читается как объявление, а `[^;]+` дотягивает его значение до ближайшей точки с
  // запятой — проглатывая следующий настоящий токен целиком. Токен молча пропадает из карты, и
  // тест падает на `undefined` вместо того, чтобы проверить цвет.
  const declarations = block[1].replace(/\/\*[\s\S]*?\*\//g, '');
  const map: Record<string, string> = {};
  for (const m of declarations.matchAll(/(--[a-z0-9-]+)\s*:\s*([^;]+);/gi)) map[m[1]] = m[2].trim();
  return map;
}

// Что проверяется и почему именно так.
//
// Прошлая версия этого блока смотрела на три поверхности из семи, требовала от --text-tertiary
// 3:1 вместо 4.5 и не знала ни про --accent-text, ни про плейсхолдеры, ни про канты контролов,
// ни про надпись на акцентной кнопке. Она была зелёной, пока в светлой теме суммы в кассе шли
// на 3.0:1, плейсхолдеры на 2.3:1, а кант кнопки на 1.3:1. Зелёный тест, называющий вещь,
// которая сломана, хуже отсутствия теста: он закрывает вопрос.
//
// Поверхности разделены на две группы. «Обычные» — те, на которых лежит серо-зелёная шкала
// текста. surface-accent-soft вынесена отдельно: это подсветка с собственной парой текста
// (--accent-on-soft / --accent-bright), и тянуть под неё общую шкалу значило бы перекрасить
// весь интерфейс ради одной плашки.
const PLAIN_SURFACES = [
  '--surface-canvas',
  '--surface-elevated',
  '--surface-card',
  '--surface-muted',
  '--surface-sunken',
  '--surface-hover',
] as const;

const AA_TEXT = 4.5; // WCAG 1.4.3 — обычный текст
const AA_NON_TEXT = 3; // WCAG 1.4.11 — границы контролов, кольца фокуса

describe('WCAG contrast', () => {
  for (const theme of ['dark', 'light', 'player']) {
    const v = themeVars(tokensCss, theme);

    // Текстовая шкала. --text-quaternary сознательно НЕ здесь: это доля «выключено» из
    // Material, а неактивные контролы 1.4.3 не покрывает. Именно поэтому плейсхолдер получил
    // свой токен: он выглядел как «приглушённый текст», а на деле был долей disabled.
    for (const token of ['--text-primary', '--text-strong', '--text-secondary', '--text-tertiary', '--text-placeholder']) {
      for (const bg of PLAIN_SURFACES) {
        test(`${theme}: ${token} on ${bg} >= ${AA_TEXT}`, () => {
          expect(contrast(v[token], v[bg])).toBeGreaterThanOrEqual(AA_TEXT);
        });
      }
    }

    // Этим цветом набраны суммы в кассе, остатки склада и надписи ghost-кнопок.
    for (const bg of PLAIN_SURFACES) {
      test(`${theme}: --accent-text on ${bg} >= ${AA_TEXT}`, () => {
        expect(contrast(v['--accent-text'], v[bg])).toBeGreaterThanOrEqual(AA_TEXT);
      });
    }

    // Статусы читаются и на своей мягкой подложке, и просто на поверхности.
    for (const tone of ['danger', 'warning', 'success']) {
      test(`${theme}: --${tone}-text on --${tone}-soft-bg >= ${AA_TEXT}`, () => {
        expect(contrast(v[`--${tone}-text`], v[`--${tone}-soft-bg`])).toBeGreaterThanOrEqual(AA_TEXT);
      });
      for (const bg of PLAIN_SURFACES) {
        test(`${theme}: --${tone}-text on ${bg} >= ${AA_TEXT}`, () => {
          expect(contrast(v[`--${tone}-text`], v[bg])).toBeGreaterThanOrEqual(AA_TEXT);
        });
      }
    }

    // Подсветка со своей парой текста.
    for (const token of ['--accent-on-soft', '--accent-bright']) {
      test(`${theme}: ${token} on --surface-accent-soft >= ${AA_TEXT}`, () => {
        expect(contrast(v[token], v['--surface-accent-soft'])).toBeGreaterThanOrEqual(AA_TEXT);
      });
    }

    // Надпись на акцентной кнопке — во ВСЕХ трёх состояниях. Наведение не транзиентно: оно
    // держится, пока курсор на кнопке, и попадает под 1.4.3 наравне с покоем.
    for (const state of ['--accent', '--accent-hover', '--accent-pressed']) {
      test(`${theme}: --text-on-accent on ${state} >= ${AA_TEXT}`, () => {
        expect(contrast(v['--text-on-accent'], v[state])).toBeGreaterThanOrEqual(AA_TEXT);
      });
    }

    // 1.4.11: кант контрола — то, чем кнопка и поле вообще опознаются как контрол; кольцо
    // фокуса — то, чем клавиатурный пользователь видит, где он находится.
    for (const token of ['--border-control', '--focus-outline']) {
      for (const bg of PLAIN_SURFACES) {
        test(`${theme}: ${token} on ${bg} >= ${AA_NON_TEXT}`, () => {
          expect(contrast(v[token], v[bg])).toBeGreaterThanOrEqual(AA_NON_TEXT);
        });
      }
    }
  }
});
