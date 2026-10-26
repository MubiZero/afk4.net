import type { ReactNode } from 'react';
import { I18nCatalogProvider, type Locale } from '@afk4/i18n';
import { organizationAdminCatalog } from '@afk4/i18n/catalogs/organization-admin';

/**
 * Строки Панели — каталог без чужих разделов (приложение игрока, экран ПК, Platform Control,
 * мастер установки). Полный каталог трёх языков был больше половины кода Панели, и слабый кассовый
 * ПК разбирал его на каждом запуске ради строк, которых Панель не показывает. Ключ из исключённого
 * раздела покажется сырым — поэтому тест Панели запрещает их упоминать (panelCatalogScope.test).
 */
export function PanelI18nProvider({ children, initialLocale }: { children: ReactNode; initialLocale?: Locale }) {
  return (
    <I18nCatalogProvider catalog={organizationAdminCatalog} initialLocale={initialLocale}>
      {children}
    </I18nCatalogProvider>
  );
}
