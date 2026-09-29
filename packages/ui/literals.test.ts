import { describe, expect, it } from 'bun:test';
import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { join, relative } from 'node:path';

// Храповик: литералы в обход токенов могут только убывать. Сюда же — моноширинный шрифт: по решению
// владельца он остаётся только кодам, а на числах его сменит tabular-nums экран за экраном.
//
// Аудит 29.09 насчитал в одной Панели 538 литеральных размеров шрифта при 55 обращениях к токенам
// и 447 отступов вне шкалы 4px. Каждый из них по отдельности безобиден, а вместе они и есть
// «одна роль — пять форм»: 10/11/12px текста на соседних кнопках, радиусы 5/7/8 рядом с токенами
// 4/6/8/12. Запретить их разом нельзя — это сотни правок по экранам. Можно не давать им расти.
//
// База — в literals-baseline.json, по файлу и виду литерала. Выросло — тест падает и называет
// токен, которым надо было воспользоваться. Убыло — тоже падает: база обновляется в том же PR,
// иначе отвоёванное место тихо займёт следующий литерал.
//   Обновить базу: AFK4_UPDATE_LITERALS=1 bun test literals.test.ts

const ROOT = join(import.meta.dir, '..', '..');
const BASELINE = join(import.meta.dir, 'literals-baseline.json');
const SOURCES = [
  'packages/ui',
  'src/AFK4.OrganizationAdmin.Web/src',
  'src/AFK4.PlatformControl.Web/src',
  'src/AFK4.SetupWizard.Web/src',
  'src/AFK4.Player.Shell.Web/src',
];

type Kind = 'font-size' | 'radius' | 'spacing' | 'hex' | 'mono';

const ADVICE: Record<Kind, string> = {
  'font-size': 'размер шрифта — var(--text-xs|sm|base|md|lg|xl|2xl), 11/13/14/16/20/26/32px',
  radius: 'радиус — var(--radius-xs|sm|md|lg|pill), 4/6/8/12/999px',
  spacing: 'отступ — var(--space-1..6), шкала 4px: 4/8/12/16/24/32',
  hex: 'цвет — токен из @afk4/tokens (var(--text-*), var(--surface-*), var(--status-*)…), не hex',
  mono: 'для чисел — .ui-num / .ui-money (tabular-nums основным шрифтом); моноширинный только кодам, .ui-code',
};

function cssFiles(dir: string): string[] {
  const out: string[] = [];
  for (const entry of readdirSync(join(ROOT, dir), { withFileTypes: true })) {
    if (entry.name === 'node_modules' || entry.name.startsWith('dist')) continue;
    const rel = `${dir}/${entry.name}`;
    if (entry.isDirectory()) out.push(...cssFiles(rel));
    else if (entry.name.endsWith('.css')) out.push(rel);
  }
  return out;
}

// Отступ в шкале — кратный 4px. 1–2px оставлены: волосяные поправки у бейджа и подчёркивания
// не шаг шкалы, а оптическая доводка, и токена под них нет сознательно.
const onScale = (px: number) => px % 4 === 0 || Math.abs(px) <= 2;

function countLiterals(css: string): Record<Kind, number> {
  const body = css.replace(/\/\*[\s\S]*?\*\//g, '');
  const counts: Record<Kind, number> = { 'font-size': 0, radius: 0, spacing: 0, hex: 0, mono: 0 };
  for (const [, prop, value] of body.matchAll(/([a-z-]+)\s*:\s*([^;{}]+)/g)) {
    const numbers = [...value.matchAll(/(?<![\w.-])(-?\d*\.?\d+)px/g)].map((m) => Number(m[1]));
    if (prop === 'font-size' && numbers.length > 0) counts['font-size'] += 1;
    else if (prop === 'font-family' && value.includes('--font-mono')) counts.mono += 1;
    else if (prop === 'border-radius') counts.radius += numbers.filter((n) => n !== 0 && n < 999).length;
    else if (/^(padding|margin)(-(top|right|bottom|left|inline|block)(-(start|end))?)?$|^(row-|column-)?gap$/.test(prop)) {
      counts.spacing += numbers.filter((n) => !onScale(n)).length;
    }
    // Маска — это альфа-канал, а не цвет: `#000` в mask-image значит «видно», токен тут ни при чём.
    if (!prop.includes('mask')) counts.hex += [...value.matchAll(/#[0-9a-f]{3,8}\b/gi)].length;
  }
  return counts;
}

function measure(): Record<string, Partial<Record<Kind, number>>> {
  const out: Record<string, Partial<Record<Kind, number>>> = {};
  for (const file of SOURCES.flatMap(cssFiles).sort()) {
    const counts = countLiterals(readFileSync(join(ROOT, file), 'utf8'));
    const nonZero = Object.fromEntries(Object.entries(counts).filter(([, n]) => n > 0));
    if (Object.keys(nonZero).length > 0) out[relative(ROOT, join(ROOT, file))] = nonZero;
  }
  return out;
}

describe('литералы в обход токенов', () => {
  it('counts what it claims to count', () => {
    expect(countLiterals('a{font-size:12px;border-radius:5px 0;padding:10px 8px;color:#fff;font-family:var(--font-mono)}')).toEqual({
      'font-size': 1, radius: 1, spacing: 1, hex: 1, mono: 1,
    });
    expect(countLiterals('a{font-size:var(--text-sm);border-radius:999px;gap:2px;mask-image:linear-gradient(#000,transparent)}')).toEqual({
      'font-size': 0, radius: 0, spacing: 0, hex: 0, mono: 0,
    });
  });

  it('do not grow, and a shrink is written down', () => {
    const current = measure();
    if (process.env.AFK4_UPDATE_LITERALS === '1') {
      writeFileSync(BASELINE, `${JSON.stringify(current, null, 2)}\n`);
    }
    const baseline = JSON.parse(readFileSync(BASELINE, 'utf8')) as Record<string, Partial<Record<Kind, number>>>;
    const grew: string[] = [];
    const shrank: string[] = [];
    for (const file of new Set([...Object.keys(current), ...Object.keys(baseline)])) {
      for (const kind of Object.keys(ADVICE) as Kind[]) {
        const now = current[file]?.[kind] ?? 0;
        const was = baseline[file]?.[kind] ?? 0;
        if (now > was) grew.push(`${file}: ${kind} ${was} → ${now}. Нужен ${ADVICE[kind]}`);
        if (now < was) shrank.push(`${file}: ${kind} ${was} → ${now}`);
      }
    }
    expect(grew).toEqual([]);
    // Меньше — хорошо, но база обязана это запомнить: AFK4_UPDATE_LITERALS=1 bun test literals.test.ts
    expect(shrank).toEqual([]);
  });
});
