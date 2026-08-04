import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, waitFor } from '@testing-library/react';
import { CLOCK_SKEW_TOLERANCE_MS, useChallengeExpiry } from './useChallengeExpiry';

afterEach(cleanup);

// Tiny harness: exercises the hook directly instead of through a full 2FA screen, so the
// "stops firing once deactivated" guarantee isn't at the mercy of async network/typing timing in
// TwoFactorSetup's own tests (see TwoFactorSetup.test.tsx for the integration-level coverage).
function Harness({ expiresAtUtc, active, onExpired }: { expiresAtUtc: string; active: boolean; onExpired: () => void }) {
  useChallengeExpiry(expiresAtUtc, onExpired, active);
  return null;
}

// Builds a timestamp that, once CLOCK_SKEW_TOLERANCE_MS is added back by the hook, leaves
// `remainingMs` of real wall-clock time before the timer fires — lets tests ask for a short,
// deterministic real delay without hardcoding the tolerance value twice.
function expiresInAfterTolerance(remainingMs: number): string {
  return new Date(Date.now() + remainingMs - CLOCK_SKEW_TOLERANCE_MS).toISOString();
}

describe('useChallengeExpiry', () => {
  it('вызывает onExpired ровно один раз, когда срок истёк далеко за пределами допуска на рассинхрон часов', async () => {
    let calls = 0;
    const longExpired = new Date(Date.now() - CLOCK_SKEW_TOLERANCE_MS - 60_000).toISOString();
    render(<Harness expiresAtUtc={longExpired} active onExpired={() => { calls += 1; }} />);

    await waitFor(() => expect(calls).toBe(1));
    await new Promise(resolve => setTimeout(resolve, 30));
    expect(calls).toBe(1);
  });

  it('не запускает таймер, пока active=false', async () => {
    let calls = 0;
    render(<Harness expiresAtUtc={expiresInAfterTolerance(5)} active={false} onExpired={() => { calls += 1; }} />);

    await new Promise(resolve => setTimeout(resolve, 30));
    expect(calls).toBe(0);
  });

  it('останавливает уже запущенный таймер, когда active переключается на false до истечения срока', async () => {
    let calls = 0;
    const expiresAtUtc = expiresInAfterTolerance(30);
    const { rerender } = render(
      <Harness expiresAtUtc={expiresAtUtc} active onExpired={() => { calls += 1; }} />
    );

    // Deactivate well before the 30ms window elapses — mirrors TwoFactorSetup disabling the
    // countdown the instant recovery codes are on screen.
    rerender(<Harness expiresAtUtc={expiresAtUtc} active={false} onExpired={() => { calls += 1; }} />);

    await new Promise(resolve => setTimeout(resolve, 60));
    expect(calls).toBe(0);
  });

  // Находка Раунда 2, сценарий 1: a fast client clock must not evict someone typing a still-valid
  // code. A deadline that's technically "in the past" but within the skew tolerance must NOT fire
  // — better to let the person submit and have the server be the one to say no, with a real reason.
  it('не считает срок истёкшим, если он в прошлом, но в пределах допуска на рассинхрон часов', async () => {
    let calls = 0;
    // Expired 5s ago by the client's clock — comfortably inside the 30s tolerance, so the
    // effective remaining time is still ~25s. Nowhere near firing during this test's short wait.
    const withinTolerance = new Date(Date.now() - 5_000).toISOString();
    render(<Harness expiresAtUtc={withinTolerance} active onExpired={() => { calls += 1; }} />);

    await new Promise(resolve => setTimeout(resolve, 50));
    expect(calls).toBe(0);
  });

  // Находка Раунда 2, сценарий 2: `Date.parse` on an unparsable timestamp yields NaN, and
  // `setTimeout(fn, NaN)` fires on the very next tick per spec — that would evict someone the
  // instant the 2FA screen mounts, in a silent unrecoverable loop. No timer must arm at all.
  it('не вызывает onExpired и не ставит таймер при непарсибельной метке времени', async () => {
    let calls = 0;
    render(<Harness expiresAtUtc="not-a-real-timestamp" active onExpired={() => { calls += 1; }} />);

    await new Promise(resolve => setTimeout(resolve, 50));
    expect(calls).toBe(0);
  });
});
