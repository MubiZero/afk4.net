import { describe, expect, it } from 'bun:test';
import { PlatformApiError } from './platformApi';
import { UnsettledKeys } from './unsettledKeys';

const lostResponse = () => Promise.reject(new TypeError('Failed to fetch'));
const refused = () => Promise.reject(new PlatformApiError('conflict', 409, 'Conflict', '{}'));

async function keyOf(keys: UnsettledKeys, slot: string, fail?: () => Promise<never>): Promise<string> {
  let sent = '';
  await keys.send(slot, 'device-reboot', (key) => {
    sent = key;
    return fail ? fail() : Promise.resolve('ok');
  }).catch(() => {});
  return sent;
}

describe('UnsettledKeys', () => {
  it('repeats the key after a lost response, so the server recognises the retry', async () => {
    const keys = new UnsettledKeys();
    const first = await keyOf(keys, 'pc-1|reboot', lostResponse);
    const retry = await keyOf(keys, 'pc-1|reboot');

    expect(retry).toBe(first);
  });

  it('forgets the key once the server answered, success or refusal', async () => {
    const keys = new UnsettledKeys();
    const done = await keyOf(keys, 'pc-1|reboot');
    expect(await keyOf(keys, 'pc-1|reboot')).not.toBe(done);

    const rejected = await keyOf(keys, 'pc-1|lock', refused);
    expect(await keyOf(keys, 'pc-1|lock')).not.toBe(rejected);
  });

  it('keeps other PCs and other actions apart', async () => {
    const keys = new UnsettledKeys();
    const reboot = await keyOf(keys, 'pc-1|reboot', lostResponse);

    expect(await keyOf(keys, 'pc-2|reboot')).not.toBe(reboot);
    expect(await keyOf(keys, 'pc-1|shutdown')).not.toBe(reboot);
  });

  it('treats a retry after the window as a new intent', async () => {
    let nowMs = 0;
    const keys = new UnsettledKeys(() => nowMs);
    const first = await keyOf(keys, 'pc-1|reboot', lostResponse);
    nowMs = 3 * 60_000;

    expect(await keyOf(keys, 'pc-1|reboot')).not.toBe(first);
  });
});
