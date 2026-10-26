/**
 * Какие разделы каталога Панели не нужны: приложение игрока, экран ПК, Platform Control и мастер
 * установки. Из них Панель показывает сама только то, что в `PANEL_KEPT_PREFIXES`. Список общий для
 * генератора среза и теста Панели, который запрещает ей тянуть ключи из исключённых разделов.
 */
export const PANEL_FOREIGN_PREFIXES = ['customer.', 'playerShell.', 'platform.', 'setup.'] as const;
export const PANEL_KEPT_PREFIXES = ['platform.messages.', 'setup.wizard.branding.color.'] as const;

export function isPanelKey(key: string): boolean {
  return PANEL_KEPT_PREFIXES.some((prefix) => key.startsWith(prefix))
    || !PANEL_FOREIGN_PREFIXES.some((prefix) => key.startsWith(prefix));
}
