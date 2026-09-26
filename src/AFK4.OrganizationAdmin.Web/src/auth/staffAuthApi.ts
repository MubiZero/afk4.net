import { organizationAdminHeaders } from '../organizationAdminCompatibility';
import type {
  AcceptStaffInviteResponse,
  StaffSignInChooseClubResponse,
  StaffSignInNextStepResponse,
  StaffSignInResponse,
  StaffSignInStepName
} from '@afk4/contracts';
export type { StaffSignInResponse } from '@afk4/contracts';

export interface ClubChoice { organizationId: string; name: string; }

export class ChooseClubError extends Error {
  constructor(public readonly clubs: ClubChoice[]) {
    super('Multiple clubs matched; choose one.');
    this.name = 'ChooseClubError';
  }
}

// Структурная ошибка не-ok ответа: несёт HTTP-статус и разобранное тело (если оно было JSON),
// чтобы вызывающий код мог различить 401 (сессия действительно недействительна) от сети/5xx
// (транзиентный сбой) и читать коды бэка (invalid_code/code_expired/too_many_attempts и т.п.)
// вместо строкового парсинга сообщения. Зеркалит подход `toApiError`/`PlatformApiError` из
// AFK4.PlatformControl.Web (см. src/api/staffAuthApi.ts, platformApi.ts).
export class StaffAuthApiError extends Error {
  constructor(public readonly status: number, public readonly body: unknown) {
    super(`Auth request failed: ${status}`);
    this.name = 'StaffAuthApiError';
  }
}

export function isUnauthorizedStaffAuthError(error: unknown): boolean {
  return error instanceof StaffAuthApiError && error.status === 401;
}

async function toStaffAuthApiError(res: Response): Promise<StaffAuthApiError> {
  let body: unknown = null;
  try {
    const text = await res.text();
    if (text.length > 0) {
      body = JSON.parse(text);
    }
  } catch {
    // Non-JSON (or empty) error body — leave body as null, status alone still carries meaning.
  }
  return new StaffAuthApiError(res.status, body);
}

type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

export class StaffAuthApi {
  private readonly base: URL;
  private readonly fetchImpl: FetchLike;
  constructor(baseUrl: string, fetchImpl?: FetchLike) {
    this.base = new URL(baseUrl);
    this.fetchImpl = fetchImpl ?? ((i, init) => globalThis.fetch(i, init));
  }

  private async post<T>(
    path: string,
    body: unknown,
    on409?: (r: Response) => Promise<never>,
    bearerToken?: string
  ): Promise<T> {
    const res = await this.fetchImpl(new URL(path, this.base).toString(), {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...organizationAdminHeaders(),
        ...(bearerToken ? { Authorization: `Bearer ${bearerToken}` } : {})
      },
      body: JSON.stringify(body)
    });
    if (res.status === 409 && on409) return on409(res);
    if (!res.ok) throw await toStaffAuthApiError(res);
    return res.status === 204 ? (null as T) : (await res.json() as T);
  }

  /**
   * Вход по логину или почте. Панель, подключённая к клубу, спрашивает свой клуб; браузерная клуба
   * не знает — сервер находит его по логину, а совпадение в нескольких клубах приходит 409 со
   * списком, из которого человек выбирает.
   */
  signInByLogin(organizationId: string | null, login: string, password: string): Promise<StaffSignInResponse> {
    return organizationId
      ? this.post<StaffSignInResponse>(`api/organizations/${organizationId}/auth/staff/sign-in-by-login`, { login, password })
      : this.post<StaffSignInResponse>('api/auth/staff/sign-in-by-login', { login, password }, async (res) => {
          const body = await res.json() as StaffSignInChooseClubResponse;
          throw new ChooseClubError(body.clubs.map((club) => ({ organizationId: club.organizationId, name: club.name })));
        });
  }

  /** Вход по номеру. Номер уникален по сети, клуб выводится из него; подключённая панель чужой клуб не пускает (403). */
  signInByPhone(organizationId: string | null, phoneNumber: string, password: string): Promise<StaffSignInResponse> {
    return this.post<StaffSignInResponse>(
      organizationId ? `api/organizations/${organizationId}/auth/staff/sign-in-by-phone` : 'api/auth/staff/sign-in-by-phone',
      { phoneNumber, password });
  }

  /** Первый шаг входа: что спросить у этого номера — ПИН или код первого входа. */
  async nextStep(phoneNumber: string): Promise<StaffSignInStepName> {
    const response = await this.post<StaffSignInNextStepResponse>('api/auth/staff/next-step', { phoneNumber });
    return response.step as StaffSignInStepName;
  }

  signInToClub(organizationId: string, login: string, password: string): Promise<StaffSignInResponse> {
    return this.post<StaffSignInResponse>(
      `api/organizations/${organizationId}/auth/staff/sign-in`,
      { organizationId, userName: login, password });
  }

  refresh(organizationId: string, refreshToken: string): Promise<StaffSignInResponse> {
    return this.post<StaffSignInResponse>(
      `api/organizations/${organizationId}/auth/staff/refresh`,
      { organizationId, refreshToken });
  }

  /**
   * Выход гасит пару токенов на сервере. Access передаётся заголовком: сервер отзывает именно тот
   * токен, которым подписан запрос, и смена того же сотрудника на другой машине не обрывается.
   */
  signOut(organizationId: string, refreshToken: string, accessToken: string): Promise<void> {
    return this.post<void>(
      `api/organizations/${organizationId}/auth/staff/sign-out`,
      { organizationId, refreshToken },
      undefined,
      accessToken);
  }

  forgotByEmail(userNameOrEmail: string) { return this.post<void>('api/auth/staff/forgot-password', { userNameOrEmail }); }
  resetByEmail(userNameOrEmail: string, code: string, newPassword: string) { return this.post<void>('api/auth/staff/reset-password', { userNameOrEmail, code, newPassword }); }
  forgotByPhone(phoneNumber: string) { return this.post<void>('api/auth/staff/forgot-password-by-phone', { phoneNumber }); }
  resetByPhone(phoneNumber: string, code: string, newPassword: string) { return this.post<void>('api/auth/staff/reset-password-by-phone', { phoneNumber, code, newPassword }); }

  /** Сверить код первого входа до того, как человек придумывает ПИН. */
  checkInvite(phoneNumber: string, code: string): Promise<void> {
    return this.post<void>('api/staff/invites/check', { phoneNumber, code });
  }

  /** Первый вход: код от руководителя и новый ПИН. Ответ сразу несёт вход. */
  acceptInvite(phoneNumber: string, code: string, password: string): Promise<AcceptStaffInviteResponse> {
    return this.post<AcceptStaffInviteResponse>('api/staff/invites/accept', { phoneNumber, code, password });
  }
}
