import type { AccountActivationRequest, FetchLike } from '../api/types';
import { PlatformApiError } from '../api/platformApi';

export interface AccountActivationApiOptions {
  baseUrl: string;
  fetchImpl?: FetchLike;
}

/** Public onboarding client. A successful response may contain staff tokens,
 * but browser Platform Control deliberately does not persist or expose them. */
export class AccountActivationApi {
  private readonly baseUrl: string;
  private readonly fetchImpl: FetchLike;

  public constructor(options: AccountActivationApiOptions) {
    this.baseUrl = options.baseUrl.replace(/\/+$/u, '');
    this.fetchImpl = options.fetchImpl ?? fetch.bind(globalThis);
  }

  public async accept(request: AccountActivationRequest): Promise<void> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/account-activation/organization-owner`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    if (!response.ok) throw await toApiError(response);
  }
}

async function toApiError(response: Response): Promise<PlatformApiError> {
  let message = 'Setup code acceptance failed.';
  try {
    const body = await response.json() as { error?: string };
    if (typeof body.error === 'string' && body.error.length > 0) message = body.error;
  } catch {
    // Preserve the safe fallback for empty or non-JSON error bodies.
  }
  return new PlatformApiError(response.status, message);
}
