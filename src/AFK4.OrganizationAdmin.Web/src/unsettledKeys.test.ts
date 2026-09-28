import { describe, expect, it } from 'bun:test';
import { PlatformApiError } from './platformApi';
import { isOutcomeUnknown, UnsettledKeys } from './unsettledKeys';

const lostResponse = () => Promise.reject(new TypeError('Failed to fetch'));
const failure = (status: number) => () => Promise.reject(new PlatformApiError('failed', status, '', '{}'));

async function keyOf(keys: UnsettledKeys, intent: unknown, fail?: () => Promise<never>, operation = 'wallet-top-up'): Promise<string> {
  let sent = '';
  await keys.send(operation, intent, (key) => {
    sent = key;
    return fail ? fail() : Promise.resolve('ok');
  }).catch(() => {});
  return sent;
}

const topUp = { playerAccountId: 'p-1', minorUnits: 10_000, reason: 'Наличные' };

describe('UnsettledKeys', () => {
  it('repeats the key after a lost response, so the server recognises the retry', async () => {
    const keys = new UnsettledKeys();
    const first = await keyOf(keys, topUp, lostResponse);

    expect(first.startsWith('wallet-top-up-')).toBe(true);
    expect(await keyOf(keys, { ...topUp })).toBe(first);
  });

  // 502 от прокси: сервер мог успеть зачислить, исход неизвестен.
  it('keeps the key after a server error', async () => {
    const keys = new UnsettledKeys();
    const first = await keyOf(keys, topUp, failure(502));

    expect(await keyOf(keys, topUp)).toBe(first);
  });

  it('forgets the key once the server answered: success or a refusal', async () => {
    const keys = new UnsettledKeys();
    const done = await keyOf(keys, topUp);
    expect(await keyOf(keys, topUp)).not.toBe(done);

    const refused = await keyOf(keys, topUp, failure(409));
    expect(await keyOf(keys, topUp)).not.toBe(refused);
  });

  it('gives another amount, another player or another action its own key', async () => {
    const keys = new UnsettledKeys();
    const first = await keyOf(keys, topUp, lostResponse);

    expect(await keyOf(keys, { ...topUp, minorUnits: 20_000 })).not.toBe(first);
    expect(await keyOf(keys, { ...topUp, playerAccountId: 'p-2' })).not.toBe(first);
    expect(await keyOf(keys, topUp, undefined, 'debt-payment')).not.toBe(first);
  });

  it('forgets on the operator\'s word: «New attempt» gets a new key', async () => {
    const keys = new UnsettledKeys();
    const first = await keyOf(keys, topUp, lostResponse);
    keys.forget('wallet-top-up', topUp);

    expect(await keyOf(keys, topUp)).not.toBe(first);
  });

  // Цена тарифа «с этого момента»: повтор обязан прислать ту же дату, что и первое нажатие.
  it('hands the retry the moment of the first attempt', async () => {
    let nowMs = 1_000;
    const keys = new UnsettledKeys(() => nowMs);
    const seen: number[] = [];
    const attempt = (fail: boolean) => keys.send('tariff-version-create', topUp, (_key, at) => {
      seen.push(at.getTime());
      return fail ? lostResponse() : Promise.resolve('ok');
    }).catch(() => {});
    await attempt(true);
    nowMs = 31_000;
    await attempt(false);

    expect(seen).toEqual([1_000, 1_000]);
  });

  it('treats a retry after the window as a new intent', async () => {
    let nowMs = 0;
    const keys = new UnsettledKeys(() => nowMs);
    const first = await keyOf(keys, topUp, lostResponse);
    nowMs = 3 * 60_000;

    expect(await keyOf(keys, topUp)).not.toBe(first);
  });
});

describe('isOutcomeUnknown', () => {
  it('counts transport and retryable HTTP failures as unknown, a domain 4xx as an answer', () => {
    expect(isOutcomeUnknown(new TypeError('network lost'))).toBe(true);
    for (const status of [408, 425, 429, 500, 502, 503, 504]) {
      expect(isOutcomeUnknown(new PlatformApiError('retryable', status, 'Failure', '{}'))).toBe(true);
    }
    for (const status of [400, 401, 403, 404, 409, 422]) {
      expect(isOutcomeUnknown(new PlatformApiError('domain', status, 'Failure', '{}'))).toBe(false);
    }
  });
});
