import type { AccountActivationRequest, FetchLike } from '../api/types';
import { PlatformApiError } from '../api/platformApi';

export interface AccountActivationApiOptions {
  baseUrl: string;
  fetchImpl?: FetchLike;
}

/**
 * Кого активируем. Два приглашения ведут в разные приложения и в разные таблицы на сервере, и
 * угадать это по самому коду нельзя — коды намеренно неразличимы, иначе их можно было бы
 * перебирать. Параметр обязателен без значения по умолчанию: ровно потому, что путь администратора
 * платформы уже один раз потерялся — сервер его принимал, а позвать было неоткуда.
 */
export type AccountActivationKind = 'organization-owner' | 'platform-admin';

/** Public onboarding client. A successful response may contain staff tokens,
 * but browser Platform Control deliberately does not persist or expose them. */
export class AccountActivationApi {
  private readonly baseUrl: string;
  private readonly fetchImpl: FetchLike;

  public constructor(options: AccountActivationApiOptions) {
    this.baseUrl = options.baseUrl.replace(/\/+$/u, '');
    this.fetchImpl = options.fetchImpl ?? fetch.bind(globalThis);
  }

  public async accept(request: AccountActivationRequest, kind: AccountActivationKind): Promise<void> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/account-activation/${kind}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    if (!response.ok) throw await toApiError(response);
  }
}

async function toApiError(response: Response): Promise<PlatformApiError> {
  let message = 'Setup code acceptance failed.';
  // Код ошибки нужен отдельно от текста: обе неудачи активации администратора платформы приходят
  // как 400, и различить «код не подошёл» и «пароль слишком короткий» можно только по нему.
  let errorCode: string | null = null;
  try {
    const body = await response.json() as { error?: string };
    if (typeof body.error === 'string' && body.error.length > 0) {
      message = body.error;
      errorCode = body.error;
    }
  } catch {
    // Preserve the safe fallback for empty or non-JSON error bodies.
  }
  return new PlatformApiError(response.status, message, errorCode);
}
