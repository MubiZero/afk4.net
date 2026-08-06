import { describe, expect, it } from 'bun:test';
import { AdminsApi } from './admins';
import { PlatformTransport } from '../platformTransport';

function transportWith(recorder: { method?: string; path?: string; body?: unknown }): PlatformTransport {
  return {
    send: async (method: string, path: string, body?: unknown) => {
      recorder.method = method;
      recorder.path = path;
      recorder.body = body;
      return [] as unknown;
    }
  } as unknown as PlatformTransport;
}

describe('AdminsApi', () => {
  it('патчит сотрудника по идентификатору', async () => {
    const recorder: { method?: string; path?: string; body?: unknown } = {};
    const api = new AdminsApi(transportWith(recorder));

    await api.updateAdmin('11111111-1111-1111-1111-111111111111', { isActive: false });

    expect(recorder.method).toBe('PATCH');
    expect(recorder.path).toBe('/api/platform/admins/11111111-1111-1111-1111-111111111111');
    expect(recorder.body).toEqual({ role: undefined, isActive: false });
  });

  it('создаёт приглашение с ролью и сроком', async () => {
    const recorder: { method?: string; path?: string; body?: unknown } = {};
    const api = new AdminsApi(transportWith(recorder));

    await api.invite('platform_support', 72);

    expect(recorder.method).toBe('POST');
    expect(recorder.path).toBe('/api/platform/admins/invitations');
    expect(recorder.body).toEqual({ role: 'platform_support', lifetimeHours: 72 });
  });
});
