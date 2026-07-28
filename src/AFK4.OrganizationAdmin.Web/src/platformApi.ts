type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

export interface PlatformApiOptions {
  baseUrl: string;
  getAccessToken: () => string | null | Promise<string | null>;
  fetchImpl?: FetchLike;
  pathPrefix?: string;
}

export interface QueryValue {
  toString(): string;
}

export type QueryParams = Record<string, string | number | boolean | Date | null | undefined>;

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

  constructor(options: PlatformApiOptions) {
    this.baseUrl = new URL(options.baseUrl);
    this.getAccessToken = options.getAccessToken;
    this.fetchImpl = options.fetchImpl ?? ((input, init) => globalThis.fetch(input, init));
    this.pathPrefix = options.pathPrefix?.replace(/\/$/, '') ?? '';
  }

  forOrganization(organizationId: string): PlatformApiClient {
    return new PlatformApiClient({
      baseUrl: this.baseUrl.toString(),
      getAccessToken: this.getAccessToken,
      fetchImpl: this.fetchImpl,
      pathPrefix: `/api/organizations/${organizationId}`
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
  async postForm<TResponse>(path: string, formData: FormData): Promise<TResponse> {
    const response = await this.fetchAuthorizedRaw('POST', path, formData);
    await ensureSuccess(response);
    if (response.status === 204) {
      return null as TResponse;
    }
    return await response.json() as TResponse;
  }

  async getWithEtag<TResponse>(path: string, query?: QueryParams): Promise<{ value: TResponse; etag: string | null }> {
    const response = await this.fetchAuthorized('GET', path, undefined, query);
    await ensureSuccess(response);
    const etag = response.headers.get('ETag');
    const value = await response.json() as TResponse;
    return { value, etag };
  }

  async put<TResponse, TRequest = unknown>(path: string, body: TRequest, options?: { ifMatch?: string }): Promise<TResponse> {
    const extraHeaders = options?.ifMatch ? { 'If-Match': options.ifMatch } : undefined;
    const response = await this.fetchAuthorized('PUT', path, body, undefined, extraHeaders);
    await ensureSuccess(response);
    if (response.status === 204) return null as TResponse;
    return await response.json() as TResponse;
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

  private async send<TResponse>(
    method: string,
    path: string,
    body?: unknown,
    query?: QueryParams,
    nullStatuses: number[] = []
  ): Promise<TResponse> {
    const response = await this.fetchAuthorized(method, path, body, query);
    if (nullStatuses.includes(response.status)) {
      return null as TResponse;
    }

    await ensureSuccess(response);
    if (response.status === 204) {
      return null as TResponse;
    }

    return await response.json() as TResponse;
  }

  private async sendText(method: string, path: string, query?: QueryParams): Promise<string> {
    const response = await this.fetchAuthorized(method, path, undefined, query);
    await ensureSuccess(response);
    return await response.text();
  }

  private async fetchAuthorized(
    method: string,
    path: string,
    body?: unknown,
    query?: QueryParams,
    extraHeaders?: Record<string, string>
  ): Promise<Response> {
    const accessToken = await this.getAccessToken();
    if (!accessToken) {
      throw new Error('Operator access token is missing.');
    }

    const headers = new Headers({
      Authorization: `Bearer ${accessToken}`
    });
    if (extraHeaders) {
      for (const [name, value] of Object.entries(extraHeaders)) {
        headers.set(name, value);
      }
    }
    let requestBody: BodyInit | undefined;
    if (body !== undefined && body !== null) {
      headers.set('Content-Type', 'application/json');
      requestBody = JSON.stringify(body);
    }

    return await this.fetchImpl(this.buildUrl(path, query), {
      method,
      headers,
      body: requestBody
    });
  }

  private async fetchAuthorizedRaw(method: string, path: string, body: BodyInit): Promise<Response> {
    const accessToken = await this.getAccessToken();
    if (!accessToken) {
      throw new Error('Operator access token is missing.');
    }

    // No Content-Type set here on purpose — the caller's BodyInit (e.g. FormData)
    // dictates it, and forcing one here would drop the multipart boundary.
    const headers = new Headers({
      Authorization: `Bearer ${accessToken}`
    });

    return await this.fetchImpl(this.buildUrl(path), {
      method,
      headers,
      body
    });
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
