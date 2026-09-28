import { organizationAdminHeaders } from './organizationAdminCompatibility';
import { readSupportSession, SUPPORT_GRANT_HEADER_NAME, type SupportSession } from './support/supportSession';

type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

export interface PlatformApiOptions {
  baseUrl: string;
  getAccessToken: () => string | null | Promise<string | null>;
  fetchImpl?: FetchLike;
  pathPrefix?: string;
  timeoutMs?: number;
  /**
   * Новый токен, когда сервер отказал в текущем (401). Запрос повторяется один раз: 401 сервер
   * отдаёт до того, как что-то сделать, поэтому повтор тем же телом безопасен.
   */
  renewAccessToken?: () => Promise<string | null>;
}

export type QueryParams = Record<string, string | number | boolean | Date | null | undefined>;

/**
 * Сколько ждать ответа. Без предела подвисшее соединение — обычное дело в клубе — держало экран на
 * «Сохраняю…» вечно: ни ошибки, ни разблокированной кнопки, пока браузер сам не оборвёт связь.
 */
const REQUEST_TIMEOUT_MS = 20_000;
// Фото зала на медленной сети грузится дольше обычного запроса.
const UPLOAD_TIMEOUT_MS = 120_000;

export class PlatformApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly statusText: string,
    public readonly body: string
  ) {
    super(message);
    this.name = 'PlatformApiError';
  }
}

export class PlatformApiClient {
  private readonly baseUrl: URL;
  private readonly getAccessToken: PlatformApiOptions['getAccessToken'];
  private readonly fetchImpl: FetchLike;
  private readonly pathPrefix: string;
  private readonly timeoutMs: number;
  private readonly renewAccessToken: PlatformApiOptions['renewAccessToken'];

  constructor(options: PlatformApiOptions) {
    this.baseUrl = new URL(options.baseUrl);
    this.getAccessToken = options.getAccessToken;
    this.fetchImpl = options.fetchImpl ?? ((input, init) => globalThis.fetch(input, init));
    this.pathPrefix = options.pathPrefix?.replace(/\/$/, '') ?? '';
    this.timeoutMs = options.timeoutMs ?? REQUEST_TIMEOUT_MS;
    this.renewAccessToken = options.renewAccessToken;
  }

  forOrganization(organizationId: string): PlatformApiClient {
    return new PlatformApiClient({
      baseUrl: this.baseUrl.toString(),
      getAccessToken: this.getAccessToken,
      fetchImpl: this.fetchImpl,
      pathPrefix: `/api/organizations/${organizationId}`,
      timeoutMs: this.timeoutMs,
      renewAccessToken: this.renewAccessToken
    });
  }

  get<TResponse>(path: string, query?: QueryParams): Promise<TResponse> {
    return this.send<TResponse>('GET', path, undefined, query);
  }

  getOptional<TResponse>(path: string, query?: QueryParams): Promise<TResponse | null> {
    return this.send<TResponse>('GET', path, undefined, query, [204, 404]);
  }

  getText(path: string, query?: QueryParams): Promise<string> {
    return this.sendText('GET', path, query);
  }

  post<TResponse, TRequest = unknown>(path: string, body?: TRequest): Promise<TResponse> {
    return this.send<TResponse>('POST', path, body);
  }

  patch<TResponse, TRequest = unknown>(path: string, body?: TRequest): Promise<TResponse> {
    return this.send<TResponse>('PATCH', path, body);
  }

  delete<TResponse>(path: string, query?: QueryParams): Promise<TResponse> {
    return this.send<TResponse>('DELETE', path, undefined, query);
  }

  // Multipart uploads (e.g. media). Body is sent as-is so the browser sets the
  // `Content-Type: multipart/form-data; boundary=...` header itself — forcing
  // `application/json` (like the other methods do) would break the boundary.
  postForm<TResponse>(path: string, formData: FormData): Promise<TResponse> {
    return withTimeout(Math.max(this.timeoutMs, UPLOAD_TIMEOUT_MS), async (signal) => {
      const response = await this.fetchAuthorizedRaw('POST', path, formData, signal);
      await ensureSuccess(response);
      if (response.status === 204) {
        return null as TResponse;
      }
      return await response.json() as TResponse;
    });
  }

  put<TResponse, TRequest = unknown>(path: string, body: TRequest): Promise<TResponse> {
    return this.send<TResponse>('PUT', path, body);
  }

  buildUrl(path: string, query?: QueryParams): string {
    const normalizedPath = path.replace(/^\//, '');
    const scopedPath = this.pathPrefix
      ? `${this.pathPrefix}/${normalizedPath}`
      : `/${normalizedPath}`;
    const url = new URL(scopedPath, this.baseUrl);

    for (const [name, value] of Object.entries(query ?? {})) {
      if (value === null || value === undefined || value === '') {
        continue;
      }

      url.searchParams.set(name, value instanceof Date ? value.toISOString() : String(value));
    }

    return url.toString();
  }

  private send<TResponse>(
    method: string,
    path: string,
    body?: unknown,
    query?: QueryParams,
    nullStatuses: number[] = []
  ): Promise<TResponse> {
    return withTimeout(this.timeoutMs, async (signal) => {
      const response = await this.fetchAuthorized(method, path, body, query, signal);
      if (nullStatuses.includes(response.status)) {
        return null as TResponse;
      }

      await ensureSuccess(response);
      if (response.status === 204) {
        return null as TResponse;
      }

      return await response.json() as TResponse;
    });
  }

  private sendText(method: string, path: string, query?: QueryParams): Promise<string> {
    return withTimeout(this.timeoutMs, async (signal) => {
      const response = await this.fetchAuthorized(method, path, undefined, query, signal);
      await ensureSuccess(response);
      return await response.text();
    });
  }

  private async fetchAuthorized(
    method: string,
    path: string,
    body?: unknown,
    query?: QueryParams,
    signal?: AbortSignal
  ): Promise<Response> {
    const headers = new Headers(organizationAdminHeaders());
    const supportSession = readSupportSession();
    const accessToken = supportSession ? null : await this.getAccessToken();
    const [headerName, headerValue] = resolveAuthHeader(supportSession, accessToken);
    headers.set(headerName, headerValue);
    let requestBody: BodyInit | undefined;
    if (body !== undefined && body !== null) {
      headers.set('Content-Type', 'application/json');
      requestBody = JSON.stringify(body);
    }

    const url = this.buildUrl(path, query);
    const init: RequestInit = { method, headers, body: requestBody, signal };
    return await this.retryWithRenewedToken(await this.fetchImpl(url, init), supportSession, url, init);
  }

  private async fetchAuthorizedRaw(method: string, path: string, body: BodyInit, signal?: AbortSignal): Promise<Response> {
    // No Content-Type set here on purpose — the caller's BodyInit (e.g. FormData)
    // dictates it, and forcing one here would drop the multipart boundary.
    const headers = new Headers(organizationAdminHeaders());
    const supportSession = readSupportSession();
    const accessToken = supportSession ? null : await this.getAccessToken();
    const [headerName, headerValue] = resolveAuthHeader(supportSession, accessToken);
    headers.set(headerName, headerValue);

    const url = this.buildUrl(path);
    const init: RequestInit = { method, headers, body, signal };
    return await this.retryWithRenewedToken(await this.fetchImpl(url, init), supportSession, url, init);
  }

  private async retryWithRenewedToken(
    response: Response,
    supportSession: SupportSession | null,
    url: string,
    init: RequestInit
  ): Promise<Response> {
    if (response.status !== 401 || supportSession !== null || this.renewAccessToken === undefined) {
      return response;
    }

    const renewed = await this.renewAccessToken();
    if (!renewed) {
      return response;
    }

    const headers = new Headers(init.headers);
    headers.set('Authorization', `Bearer ${renewed}`);
    return await this.fetchImpl(url, { ...init, headers });
  }
}

// Shared by fetchAuthorized/fetchAuthorizedRaw. Deliberately synchronous — both callers already
// resolve `accessToken` (or skip resolving it) via their own single `await this.getAccessToken()`
// before calling this, so wrapping the decision itself in an `async` method would add a second,
// unnecessary microtask tick on every call and has previously broken timing-sensitive callers.
//
// A support session (platform staff impersonating the organization for a fixed window) has no
// staff access token at all — that's expected, not an error. It authenticates with its own grant
// header instead of `Authorization: Bearer`. Outside support mode, a missing token is still fatal:
// it means the caller is signed out and the request would otherwise reach the API unauthenticated.
function resolveAuthHeader(
  supportSession: SupportSession | null,
  accessToken: string | null
): [name: string, value: string] {
  if (supportSession) {
    return [SUPPORT_GRANT_HEADER_NAME, supportSession.sessionToken];
  }
  if (!accessToken) {
    throw new Error('Operator access token is missing.');
  }
  return ['Authorization', `Bearer ${accessToken}`];
}

/**
 * Запрос целиком — и ответ, и чтение тела — укладывается в предел. Превышение становится
 * PlatformApiError со статусом 0: исход неизвестен (запрос мог дойти), у экранов уже есть разбор
 * по статусу, и повтор уйдёт с тем же ключом.
 */
async function withTimeout<T>(timeoutMs: number, run: (signal: AbortSignal) => Promise<T>): Promise<T> {
  const signal = AbortSignal.timeout(timeoutMs);
  try {
    return await run(signal);
  } catch (cause) {
    if (signal.aborted && !(cause instanceof PlatformApiError)) {
      throw new PlatformApiError('Platform API did not answer in time.', 0, 'Timeout', '');
    }
    throw cause;
  }
}

async function ensureSuccess(response: Response): Promise<void> {
  if (response.ok) {
    return;
  }

  const body = await response.text();
  throw new PlatformApiError(
    `Platform API returned ${response.status} ${response.statusText}: ${body}`,
    response.status,
    response.statusText,
    body
  );
}
