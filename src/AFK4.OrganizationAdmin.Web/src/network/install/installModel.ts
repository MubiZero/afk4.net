import type { OperatorConfig } from '../../operatorConfig';

export function getInstallerUrl(config: Pick<OperatorConfig, 'setupInstallerUrl'>): string | null {
  const url = config.setupInstallerUrl;
  return typeof url === 'string' && url.trim().length > 0 ? url.trim() : null;
}
