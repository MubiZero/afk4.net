import { describe, expect, it, mock } from 'bun:test';
import { AccountActivationApi } from './accountActivationApi';

describe('AccountActivationApi', () => {
  it('accepts an account activation without storing the returned staff session', async () => {
    const fetchImpl = mock(async (_input: RequestInfo | URL, _init?: RequestInit) => new Response(JSON.stringify({ accessToken: 'must-not-be-stored' }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    }));
    const client = new AccountActivationApi({ baseUrl: 'https://api.test', fetchImpl });

    await client.accept({ code: 'code-1', userName: 'owner@example.test', displayName: '', password: '246813' }, 'organization-owner');

    expect(fetchImpl).toHaveBeenCalledTimes(1);
    expect(fetchImpl.mock.calls[0]?.[0]).toBe('https://api.test/api/account-activation/organization-owner');
    expect(sessionStorage.length).toBe(0);
  });

  // Приглашение администратора платформы уходит по своему маршруту. Пока его не было, обе
  // активации шли на путь владельца — с кодом администратора он отвечал «код не найден».
  it('sends a platform administrator activation to its own route', async () => {
    const fetchImpl = mock(async (_input: RequestInfo | URL, _init?: RequestInit) => new Response(null, { status: 204 }));
    const client = new AccountActivationApi({ baseUrl: 'https://api.test', fetchImpl });

    await client.accept({ code: 'code-1', userName: 'support1', displayName: 'Первая поддержка', password: '246813' }, 'platform-admin');

    expect(fetchImpl.mock.calls[0]?.[0]).toBe('https://api.test/api/account-activation/platform-admin');
  });

  // 400 приходит и на негодный код, и на негодные данные — без кода ошибки экран их не различит.
  it('carries the error code out of the body so the screen can tell two 400s apart', async () => {
    const fetchImpl = mock(async (_input: RequestInfo | URL, _init?: RequestInit) => new Response(JSON.stringify({ error: 'invalid_details', message: 'no' }), {
      status: 400,
      headers: { 'Content-Type': 'application/json' }
    }));
    const client = new AccountActivationApi({ baseUrl: 'https://api.test', fetchImpl });

    const failure = await client
      .accept({ code: 'code-1', userName: 'support1', displayName: 'Имя', password: 'short' }, 'platform-admin')
      .then(() => null, (cause: unknown) => cause as { status: number; errorCode: string | null });

    expect(failure?.status).toBe(400);
    expect(failure?.errorCode).toBe('invalid_details');
  });
});
