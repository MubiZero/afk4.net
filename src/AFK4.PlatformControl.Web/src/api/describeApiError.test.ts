import { describe, expect, it } from 'bun:test';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { describeApiError } from './describeApiError';
import { PlatformApiError, PlatformStaleClientError } from './platformTransport';
import { messages } from '@/i18n/messages';

const t = ((key: string, values?: Record<string, string | number>) => {
  const template = (messages.ru as Record<string, string>)[key] ?? key;
  return values === undefined
    ? template
    : template.replace(/\{(\w+)\}/gu, (_, name: string) => String(values[name] ?? ''));
}) as Parameters<typeof describeApiError>[1];

describe('describeApiError', () => {
  it('never surfaces the transport’s technical message', () => {
    const text = describeApiError(new PlatformApiError(500, 'Sign-in failed.'), t);
    expect(text).not.toContain('Sign-in failed');
    expect(text).toBe(messages.ru['state.error.server']);
  });

  it('separates “no connection” from “server said no”', () => {
    expect(describeApiError(new PlatformApiError(0, 'x'), t)).toBe(messages.ru['state.error.network']);
    expect(describeApiError(new TypeError('fetch failed'), t)).toBe(messages.ru['state.error.network']);
    expect(describeApiError(new PlatformApiError(503, 'x'), t)).toBe(messages.ru['state.error.server']);
  });

  it('lets the calling screen name a status in its own words', () => {
    const text = describeApiError(new PlatformApiError(401, 'Sign-in failed.'), t, { 401: 'auth.error.invalid' });
    expect(text).toBe(messages.ru['auth.error.invalid']);
  });

  it('falls back to the access wording for 401 and 403 without an override', () => {
    expect(describeApiError(new PlatformApiError(401, 'x'), t)).toBe(messages.ru['state.error.forbidden']);
    expect(describeApiError(new PlatformApiError(403, 'x'), t)).toBe(messages.ru['state.error.forbidden']);
  });

  // The API and the panel bundle deploy as two independent Coolify apps with no shared release
  // step (see the platform-admin-directory-2fa review), so a version-mismatched response is a real
  // window, not a hypothetical. It must read as "reload the page", not a generic server error, and
  // no per-status override can redirect it — the fix is the same no matter which route hit it.
  it('reports a client/server version mismatch as "reload the page", ignoring overrides', () => {
    const text = describeApiError(new PlatformStaleClientError(), t, { 401: 'auth.error.invalid' });
    expect(text).toBe(messages.ru['auth.error.staleClient']);
  });
});

// Гвард против возврата класса ошибок: транспорт носит английские технические строки, и любой
// экран, печатающий `cause.message` напрямую, снова покажет их пользователю.
describe('user-facing error copy', () => {
  it('no screen prints a raw error message', () => {
    const offenders: string[] = [];
    const root = join(import.meta.dir, '..');

    const walk = (dir: string) => {
      for (const entry of readdirSync(dir)) {
        const full = join(dir, entry);
        if (statSync(full).isDirectory()) { walk(full); continue; }
        if (!entry.endsWith('.tsx') || entry.includes('.test.')) continue;
        const source = readFileSync(full, 'utf8');
        if (/\bcause\.message\b|\berror\.message\b/u.test(source)) {
          offenders.push(full.slice(root.length + 1));
        }
      }
    };

    walk(root);
    expect(offenders).toEqual([]);
  });
});
