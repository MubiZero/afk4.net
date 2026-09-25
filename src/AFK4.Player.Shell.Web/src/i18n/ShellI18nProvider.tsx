import type { ReactNode } from 'react';
import { I18nCatalogProvider, type Locale } from '@afk4/i18n';
import { playerShellCatalog } from '@afk4/i18n/catalogs/player-shell';

/**
 * Строки экрана ПК — только `playerShell.*`. Весь каталог — строки Панели, приложения и мастера —
 * больше мегабайта JS, который киоск разбирал бы на каждом холодном старте ради 4% ключей. Тесты
 * рендерят с этим же провайдером: ключ из чужого раздела покажется сырым и уронит тест, а не
 * доедет до ПК клуба.
 */
export function ShellI18nProvider({ children, initialLocale }: { children: ReactNode; initialLocale?: Locale }) {
  return (
    <I18nCatalogProvider catalog={playerShellCatalog} initialLocale={initialLocale}>
      {children}
    </I18nCatalogProvider>
  );
}
