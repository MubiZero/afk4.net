export interface PlatformApiOptions {
  baseUrl: string;
  getAccessToken: () => string | null | Promise<string | null>;
  fetchImpl?: typeof fetch;
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
  private readonly fetchImpl: typeof fetch;

  constructor(options: PlatformApiOptions) {
    this.baseUrl = new URL(options.baseUrl);
    this.getAccessToken = options.getAccessToken;
    this.fetchImpl = options.fetchImpl ?? fetch;
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

  buildUrl(path: string, query?: QueryParams): string {
    const url = new URL(path, this.baseUrl);

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
    query?: QueryParams
  ): Promise<Response> {
    const accessToken = await this.getAccessToken();
    if (!accessToken) {
      throw new Error('Operator access token is missing.');
    }

    const headers = new Headers({
      Authorization: `Bearer ${accessToken}`
    });
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
