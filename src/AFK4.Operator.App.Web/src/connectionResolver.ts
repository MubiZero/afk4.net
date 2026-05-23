export interface ResolveOperatorConnectionRequest {
  organizationSlug?: string | null;
  branchSlug?: string | null;
  setupCode?: string | null;
}

export interface ResolveOperatorConnectionResponse {
  organizationId: string;
  organizationSlug: string;
  organizationName: string;
  organizationStatus: string;
  organizationStatusReason: string | null;
  branchId: string;
  branchSlug: string;
  branchName: string;
  branchCity: string;
  source: 'slug' | 'setup_code';
}

export const OperatorTenantStatus = {
  Active: 'active',
  Suspended: 'suspended',
  DeletionPending: 'deletion_pending'
} as const;

export class ConnectionResolutionError extends Error {
  public readonly status: number;

  public constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

export interface ConnectionResolverOptions {
  baseUrl: string;
  fetchImpl?: typeof fetch;
}

export class ConnectionResolver {
  private readonly baseUrl: string;
  private readonly fetchImpl: typeof fetch;

  public constructor(options: ConnectionResolverOptions) {
    this.baseUrl = options.baseUrl.replace(/\/+$/u, '');
    this.fetchImpl = options.fetchImpl ?? fetch.bind(globalThis);
  }

  public async resolveBySlugPair(
    organizationSlug: string,
    branchSlug: string
  ): Promise<ResolveOperatorConnectionResponse> {
    return this.dispatch({ organizationSlug, branchSlug, setupCode: null });
  }

  public async resolveBySetupCode(
    setupCode: string
  ): Promise<ResolveOperatorConnectionResponse> {
    return this.dispatch({ organizationSlug: null, branchSlug: null, setupCode });
  }

  private async dispatch(request: ResolveOperatorConnectionRequest): Promise<ResolveOperatorConnectionResponse> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/operator-connections/resolve`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });

    if (!response.ok) {
      throw await ConnectionResolver.toError(response);
    }

    const body = (await response.json()) as ResolveOperatorConnectionResponse;
    return body;
  }

  private static async toError(response: Response): Promise<ConnectionResolutionError> {
    let message = 'Failed to resolve operator connection.';
    try {
      const text = await response.text();
      if (text.length > 0) {
        const parsed = JSON.parse(text) as { error?: string };
        if (typeof parsed.error === 'string' && parsed.error.length > 0) {
          message = parsed.error;
        }
      }
    } catch {
      // Fall back to default message.
    }
    return new ConnectionResolutionError(response.status, message);
  }
}

const STORAGE_KEY = 'afk4.operator.connection';

export interface ResolvedOperatorConnection {
  organizationId: string;
  organizationSlug: string;
  organizationName: string;
  branchId: string;
  branchSlug: string;
  branchName: string;
  branchCity: string;
  storedAtUtc: string;
}

export interface StorageLike {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

function defaultStorage(): StorageLike | null {
  if (typeof globalThis === 'undefined') {
    return null;
  }
  const candidate = (globalThis as { localStorage?: StorageLike }).localStorage;
  return candidate ?? null;
}

export function readStoredConnection(storage: StorageLike | null = defaultStorage()): ResolvedOperatorConnection | null {
  if (storage === null) {
    return null;
  }
  const raw = storage.getItem(STORAGE_KEY);
  if (raw === null || raw === '') {
    return null;
  }
  try {
    const parsed = JSON.parse(raw) as ResolvedOperatorConnection;
    if (typeof parsed.organizationId !== 'string' || parsed.organizationId.length === 0) {
      return null;
    }
    if (typeof parsed.branchId !== 'string' || parsed.branchId.length === 0) {
      return null;
    }
    return parsed;
  } catch {
    return null;
  }
}

export function writeStoredConnection(
  connection: ResolveOperatorConnectionResponse,
  storage: StorageLike | null = defaultStorage(),
  now: Date = new Date()
): ResolvedOperatorConnection {
  const stored: ResolvedOperatorConnection = {
    organizationId: connection.organizationId,
    organizationSlug: connection.organizationSlug,
    organizationName: connection.organizationName,
    branchId: connection.branchId,
    branchSlug: connection.branchSlug,
    branchName: connection.branchName,
    branchCity: connection.branchCity,
    storedAtUtc: now.toISOString()
  };
  storage?.setItem(STORAGE_KEY, JSON.stringify(stored));
  return stored;
}

export function clearStoredConnection(storage: StorageLike | null = defaultStorage()): void {
  storage?.removeItem(STORAGE_KEY);
}
