import type { AcceptOwnerInviteRequest, FetchLike } from './types';
import { PlatformApiError } from './platformApi';

export interface OwnerInviteAcceptanceApiOptions {
  baseUrl: string;
  fetchImpl?: FetchLike;
}

/** Public onboarding client. A successful response may contain staff tokens,
 * but browser Platform Web deliberately does not persist or expose them. */
export class OwnerInviteAcceptanceApi {
  private readonly baseUrl: string;
  private readonly fetchImpl: FetchLike;

  public constructor(options: OwnerInviteAcceptanceApiOptions) {
    this.baseUrl = options.baseUrl.replace(/\/+$/u, '');
    this.fetchImpl = options.fetchImpl ?? fetch.bind(globalThis);
  }

  public async accept(request: AcceptOwnerInviteRequest): Promise<void> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/platform/owner-invites/accept`, {
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
