import { describe, expect, it } from 'bun:test';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const read = (name: string) => readFileSync(join(import.meta.dir, name), 'utf8');

const authCss = read('auth.css');
const kitCss = read('kit.css');
const tableCss = read('table.css');
const barrelCss = read('styles.css');

describe('@afk4/ui shared layer', () => {
  it('ships every layer through the barrel', () => {
    const layers = readdirSync(import.meta.dir)
      .filter(name => name.endsWith('.css') && name !== 'styles.css')
      .sort();
    for (const layer of layers) {
      expect(barrelCss).toContain(`@import "./${layer}";`);
    }
  });

  // Ниже — визуальные гварды, переехавшие вместе со стилями из оператора: мелкий плотный текст
  // обязан сидеть на токенах, дающих контраст AA, иначе на светлой теме он «выцветает».
  it('keeps dense small text on AA-capable foreground tokens', () => {
    expect(authCss).toMatch(/\.auth-link-inline\s*\{[^}]*color:\s*var\(--accent-bright\)/s);
    expect(kitCss).toMatch(/\.ui-scanner-badge\s*\{[^}]*color:\s*var\(--accent-bright\)/s);
  });

  // Правая колонка таблиц «Управления» не прижимает саму ячейку к краю: с justify-self: end ячейка
  // растёт до ширины текста и наезжает на соседние колонки (карточка ПК в «Залах и ПК»).
  it('keeps end-aligned management cells inside their own column', () => {
    const mgmtCss = read('mgmt.css');
    expect(mgmtCss).not.toMatch(/\.mgmt-cell--end\s*\{[^}]*justify-self:\s*end/s);
    expect(mgmtCss).toMatch(/\.mgmt-cell-text\s*\{[^}]*text-overflow:\s*ellipsis/s);
  });

  // Неактивная строка гасится подложкой, а не opacity: opacity топит и текст, и границу,
  // роняя контраст ниже AA сразу во всей строке.
  it('dims inactive table rows by surface, not opacity', () => {
    expect(tableCss).toMatch(/\.ctable-row\.inactive\s*\{[^}]*background:/s);
    expect(tableCss).not.toMatch(/\.ctable-row\.inactive\s*\{[^}]*opacity:/s);
  });

  // Пакет описывает ВНЕШНИЙ ВИД и обязан брать цвет из @afk4/tokens. Собственное определение
  // переменной здесь означает вторую палитру — ровно ту болезнь, ради которой слой и выносили.
  it('defines no colour variables of its own', () => {
    for (const layer of readdirSync(import.meta.dir).filter(name => name.endsWith('.css'))) {
      const declarations = read(layer).match(/^\s*--[a-z0-9-]+:/gm) ?? [];
      expect(declarations).toEqual([]);
    }
  });

  // Мышь и клавиатура должны видеть одно и то же. Если у элемента описано наведение, а фокус —
  // нет, то человек, идущий по экрану табом, теряет место: подсветка есть только под курсором.
  // Исключение одно и оно осознанное — пункт палитры команд: фокус остаётся в поле ввода,
  // а активный пункт помечается aria-activedescendant, поэтому :focus-visible на нём не сработает.
  it('gives every hovered element a focus state too', () => {
    const FOCUS_LIVES_ELSEWHERE = ['.command-palette-option'];
    const HOVER = /([.#][^{},:]*?):hover/g;
    const FOCUS = /([.#][^{},:]*?):focus(-visible|-within)?/g;
    const hovered = new Map<string, string>();
    const focused = new Set<string>();
    for (const layer of readdirSync(import.meta.dir).filter(name => name.endsWith('.css'))) {
      // :not(...) вырезаем до разбора — иначе селектор рвётся по двоеточию внутри скобок.
      const css = read(layer).replace(/\/\*[\s\S]*?\*\//g, '').replace(/:not\([^)]*\)/g, '');
      for (const [, sel] of css.matchAll(HOVER)) if (!hovered.has(sel.trim())) hovered.set(sel.trim(), layer);
      for (const [, sel] of css.matchAll(FOCUS)) focused.add(sel.trim());
    }
    // Модификатор наследует фокус базы: .ui-btn--primary опирается на .ui-btn, .mgmt-menu-item.is-danger — на .mgmt-menu-item.
    const roots = (sel: string) => [sel, sel.replace(/--[\w-]+$/, ''), sel.replace(/\.[\w-]+$/, '')];
    const blind = [...hovered]
      .filter(([sel]) => !FOCUS_LIVES_ELSEWHERE.includes(sel) && !roots(sel).some(root => focused.has(root)))
      .map(([sel, layer]) => `${layer}: ${sel}`);
    expect(blind).toEqual([]);
  });

  // Оконные контролы, resize-хендлы и сетка оболочки принадлежат десктопному хосту. Если они
  // просочатся в общий слой, браузерная панель получит стили несуществующего у неё окна.
  it('carries no desktop-host chrome', () => {
    for (const layer of readdirSync(import.meta.dir).filter(name => name.endsWith('.css'))) {
      // Комментарии вырезаем: гвард стережёт правила, а не прозу — в барелле эти имена
      // упомянуты именно как «чего здесь нет».
      const css = read(layer).replace(/\/\*[\s\S]*?\*\//g, '');
      expect(css).not.toContain('.window-controls');
      expect(css).not.toContain('.window-resize-handle');
      expect(css).not.toContain('.operator-shell');
    }
  });
});
