import type { MessageKey } from '@afk4/i18n';

export const INSTALL_CODE_LIFETIMES: { hours: number; labelKey: MessageKey }[] = [
  { hours: 24, labelKey: 'op.network.install.codes.lifetime.day' },
  { hours: 72, labelKey: 'op.network.install.codes.lifetime.threeDays' },
  { hours: 168, labelKey: 'op.network.install.codes.lifetime.week' }
];

export const DEFAULT_INSTALL_CODE_DEVICES = 30;
export const MAX_INSTALL_CODE_DEVICES = 200;

const FALLBACK_INSTALLER = 'afk4-client.exe';

/** Имя файла установщика из ссылки на него: команда для скрипта пишется с тем же именем, что скачается. */
export function installerFileName(installerUrl: string | null): string {
  if (installerUrl === null) return FALLBACK_INSTALLER;
  try {
    const name = decodeURIComponent(new URL(installerUrl).pathname.split('/').pop() ?? '');
    return name.toLowerCase().endsWith('.exe') ? name : FALLBACK_INSTALLER;
  } catch {
    return FALLBACK_INSTALLER;
  }
}

export function silentInstallCommand(fileName: string, code: string): string {
  return `${fileName} /quiet AFK4_INSTALL_CODE=${code}`;
}

/** Число ПК из поля ввода; null — не число в допустимых пределах. */
export function parseDeviceCount(value: string): number | null {
  if (!/^\d+$/.test(value.trim())) return null;
  const count = Number(value.trim());
  return count >= 1 && count <= MAX_INSTALL_CODE_DEVICES ? count : null;
}
