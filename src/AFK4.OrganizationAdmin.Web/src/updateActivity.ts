import { PlatformApiClient } from './platformApi';

const criticalMutationMethods = new Set(['post', 'patch', 'put', 'delete', 'postForm']);
const criticalPathSegments = [
  /(^|\/)sessions(?:\/|$)/,
  /(^|\/)pos(?:\/|$)/,
  /(^|\/)(?:devices|device-enrollment-codes)(?:\/|$)/,
  /(^|\/)shifts(?:\/|$)/,
  /(^|\/)money-actions(?:\/|$)/,
  /(^|\/)wallet(?:\/|$)/,
  /(^|\/)debts\/payments(?:\/|$)/,
  /(^|\/)ledger\/(?:manual-corrections|[^/]+\/refunds)(?:\/|$)/,
  /(^|\/)packages\/purchases(?:\/|$)/
];

export function withCriticalUpdateActivity(api: PlatformApiClient): PlatformApiClient {
  return new Proxy(api, {
    get(target, property) {
      const value = Reflect.get(target, property, target);
      if (typeof value !== 'function') return value;

      const method = String(property);
      const bound = value.bind(target) as (...args: unknown[]) => unknown;
      if (!criticalMutationMethods.has(method)) return bound;

      return (...args: unknown[]) => {
        const path = typeof args[0] === 'string' ? args[0] : '';
        const request = () => Promise.resolve(bound(...args));
        return isCriticalMutationPath(path) ? trackCriticalUpdateActivity(request) : request();
      };
    }
  });
}

export function isCriticalMutationPath(path: string): boolean {
  return criticalPathSegments.some(pattern => pattern.test(path));
}

async function trackCriticalUpdateActivity<T>(request: () => Promise<T>): Promise<T> {
  const operationId = window.crypto?.randomUUID?.()
    ?? `update-${Date.now()}-${Math.random().toString(16).slice(2)}`;
  postActivity('update-activity:start', operationId);
  try {
    return await request();
  } finally {
    postActivity('update-activity:finish', operationId);
  }
}

function postActivity(type: 'update-activity:start' | 'update-activity:finish', operationId: string): void {
  window.chrome?.webview?.postMessage({ type, operationId });
}
