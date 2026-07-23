export interface StaffSignInResponse {
  staffUserId: string;
  organizationId: string;
  displayName: string;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  branchIds: string[];
  permissions: string[];
  roleNames?: string[];
}

export interface ClubChoice { organizationId: string; name: string; }

export class ChooseClubError extends Error {
  constructor(public readonly clubs: ClubChoice[]) {
    super('Multiple clubs matched; choose one.');
    this.name = 'ChooseClubError';
  }
}

type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

export class StaffAuthApi {
  private readonly base: URL;
  private readonly fetchImpl: FetchLike;
  constructor(baseUrl: string, fetchImpl?: FetchLike) {
    this.base = new URL(baseUrl);
    this.fetchImpl = fetchImpl ?? ((i, init) => globalThis.fetch(i, init));
  }

  private async post<T>(path: string, body: unknown, on409?: (r: Response) => Promise<never>): Promise<T> {
    const res = await this.fetchImpl(new URL(path, this.base).toString(), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    });
    if (res.status === 409 && on409) return on409(res);
    if (!res.ok) throw new Error(`Auth request failed: ${res.status}`);
    return res.status === 204 ? (null as T) : (await res.json() as T);
  }

  signInByLogin(login: string, password: string): Promise<StaffSignInResponse> {
    return this.post<StaffSignInResponse>('api/auth/staff/sign-in-by-login', { login, password },
      async (res) => {
        const body = await res.json() as { clubs: ClubChoice[] };
        throw new ChooseClubError(body.clubs);
      });
  }

  signInToClub(organizationId: string, login: string, password: string): Promise<StaffSignInResponse> {
    return this.post<StaffSignInResponse>('api/auth/staff/sign-in', { organizationId, userName: login, password });
  }

  refresh(organizationId: string, refreshToken: string): Promise<StaffSignInResponse> {
    return this.post<StaffSignInResponse>('api/auth/staff/refresh', { organizationId, refreshToken });
  }

  forgotByEmail(userNameOrEmail: string) { return this.post<void>('api/auth/staff/forgot-password', { userNameOrEmail }); }
  resetByEmail(userNameOrEmail: string, code: string, newPassword: string) { return this.post<void>('api/auth/staff/reset-password', { userNameOrEmail, code, newPassword }); }
  forgotByPhone(phoneNumber: string) { return this.post<void>('api/auth/staff/forgot-password-by-phone', { phoneNumber }); }
  resetByPhone(phoneNumber: string, code: string, newPassword: string) { return this.post<void>('api/auth/staff/reset-password-by-phone', { phoneNumber, code, newPassword }); }
}
