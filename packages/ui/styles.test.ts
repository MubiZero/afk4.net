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
