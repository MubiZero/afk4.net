import { describe, expect, it } from 'bun:test';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { CLUB_AUDIT_ACTION_LABELS } from '@afk4/i18n';
import { organizationAdminCatalog } from '@afk4/i18n/catalogs/organization-admin';
import {
  PANEL_FOREIGN_PREFIXES,
  PANEL_KEPT_PREFIXES,
  isPanelKey
} from '@afk4/i18n/catalogs/organization-admin-scope';

// Панель грузит каталог без чужих разделов (PanelI18nProvider). Ключ из исключённого раздела на
// экране вышел бы сырым, а большая часть тестов рендерит с полным каталогом и этого не заметит.
// Поэтому исходники Панели не должны упоминать исключённые разделы вовсе — даже собирая ключ из кусков.

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return sourceFiles(path);
    return /\.(ts|tsx)$/.test(name) && !/\.test\.tsx?$/.test(name) && !path.includes('/test/') ? [path] : [];
  });
}

describe('Panel catalog scope', () => {
  it('Panel sources never reach for a key outside the Panel catalog', () => {
    const offenders: string[] = [];
    for (const file of sourceFiles(import.meta.dir)) {
      const text = readFileSync(file, 'utf8');
      for (const prefix of PANEL_FOREIGN_PREFIXES) {
        const pattern = new RegExp(`["'\`]${prefix.replace('.', '\\.')}[\\w.$\{]*`, 'g');
        for (const match of text.matchAll(pattern)) {
          const key = match[0].slice(1);
          if (!PANEL_KEPT_PREFIXES.some((kept) => key.startsWith(kept))) {
            offenders.push(`${file.slice(import.meta.dir.length + 1)}: ${key}`);
          }
        }
      }
    }

    expect(offenders).toEqual([]);
  });

  it('the shared audit-action labels the Panel shows all live in the Panel catalog', () => {
    const missing = Object.values(CLUB_AUDIT_ACTION_LABELS).filter((key) => !isPanelKey(key));
    expect(missing).toEqual([]);
  });

  it('the Panel catalog still carries every Panel key in every language', () => {
    for (const locale of ['ru', 'en', 'tg'] as const) {
      expect(organizationAdminCatalog[locale]['op.floor.offlineBanner' as never]).toBeDefined();
      expect(organizationAdminCatalog[locale]['platform.messages.title' as never]).toBeDefined();
      expect(organizationAdminCatalog[locale]['customer.achievement.firstVisit' as never]).toBeUndefined();
    }
  });
});
