import { describe, expect, test } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const ROOT = join(import.meta.dir, '..', '..');
const tokensCss = readFileSync(join(import.meta.dir, 'tokens.css'), 'utf8');

const APP_STYLES = [
  'src/AFK4.Operator.App.Web/src/styles.css',
  'src/AFK4.SetupWizard.Web/src/styles.css',
];

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
  for (const rel of APP_STYLES) {
    test(rel, () => {
      const css = readFileSync(join(ROOT, rel), 'utf8');
      const localDefs = definedVars(css);
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
  const map: Record<string, string> = {};
  for (const m of block[1].matchAll(/(--[a-z0-9-]+)\s*:\s*([^;]+);/gi)) map[m[1]] = m[2].trim();
  return map;
}

describe('WCAG contrast', () => {
  const surfaces = ['--surface-canvas', '--surface-elevated', '--surface-card'];
  for (const theme of ['dark', 'light']) {
    const v = themeVars(tokensCss, theme);
    for (const bg of surfaces) {
      test(`${theme}: text-primary on ${bg} >= 4.5`, () => {
        expect(contrast(v['--text-primary'], v[bg])).toBeGreaterThanOrEqual(4.5);
      });
      test(`${theme}: text-secondary on ${bg} >= 4.5`, () => {
        expect(contrast(v['--text-secondary'], v[bg])).toBeGreaterThanOrEqual(4.5);
      });
      test(`${theme}: text-tertiary on ${bg} >= 3`, () => {
        expect(contrast(v['--text-tertiary'], v[bg])).toBeGreaterThanOrEqual(3);
      });
    }
  }
});
