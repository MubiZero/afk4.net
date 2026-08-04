import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, waitFor } from '@testing-library/react';
import { useChallengeExpiry } from './useChallengeExpiry';

afterEach(cleanup);

// Tiny harness: exercises the hook directly instead of through a full 2FA screen, so the
// "stops firing once deactivated" guarantee isn't at the mercy of async network/typing timing in
// TwoFactorSetup's own tests (see TwoFactorSetup.test.tsx for the integration-level coverage).
function Harness({ expiresAtUtc, active, onExpired }: { expiresAtUtc: string; active: boolean; onExpired: () => void }) {
  useChallengeExpiry(expiresAtUtc, onExpired, active);
  return null;
}

describe('useChallengeExpiry', () => {
  it('вызывает onExpired ровно один раз по достижении срока', async () => {
    let calls = 0;
    render(<Harness expiresAtUtc={new Date(Date.now() + 5).toISOString()} active onExpired={() => { calls += 1; }} />);

    await waitFor(() => expect(calls).toBe(1));
    await new Promise(resolve => setTimeout(resolve, 30));
    expect(calls).toBe(1);
  });

  it('не запускает таймер, пока active=false', async () => {
    let calls = 0;
    render(<Harness expiresAtUtc={new Date(Date.now() + 5).toISOString()} active={false} onExpired={() => { calls += 1; }} />);

    await new Promise(resolve => setTimeout(resolve, 30));
    expect(calls).toBe(0);
  });

  it('останавливает уже запущенный таймер, когда active переключается на false до истечения срока', async () => {
    let calls = 0;
    const expiresAtUtc = new Date(Date.now() + 30).toISOString();
    const { rerender } = render(
      <Harness expiresAtUtc={expiresAtUtc} active onExpired={() => { calls += 1; }} />
    );

    // Deactivate well before the 30ms window elapses — mirrors TwoFactorSetup disabling the
    // countdown the instant recovery codes are on screen.
    rerender(<Harness expiresAtUtc={expiresAtUtc} active={false} onExpired={() => { calls += 1; }} />);

    await new Promise(resolve => setTimeout(resolve, 60));
    expect(calls).toBe(0);
  });
});
