export interface OperatorConfig {
  runtime: string;
  shellMode: string;
  platformBaseUrl: string;
  currencyCode: string;
  organizationId?: string;
  branchId?: string;
}

const fallbackConfig: OperatorConfig = {
  runtime: 'browser-dev',
  shellMode: 'vite-dev',
  platformBaseUrl: 'http://localhost:5074/',
  currencyCode: 'TJS'
};

export function getOperatorConfig(): OperatorConfig {
  return window.__AFK4_OPERATOR_CONFIG__ ?? fallbackConfig;
}

declare global {
  interface Window {
    __AFK4_OPERATOR_CONFIG__?: OperatorConfig;
  }
}
