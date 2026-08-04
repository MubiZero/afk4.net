import { useEffect, useRef } from 'react';

// A sign-in challenge token dies after 2 minutes server-side, but the server can't tell the UI
// when that happens — a request against a dead challenge just answers 401, identically to a wrong
// code. Left alone, someone who sits on the QR/code screen past the window types the CORRECT code
// and reads "invalid code, check your phone's clock" — a dead end, since retrying never helps.
//
// This hook schedules `onExpired` to fire once, exactly when the window runs out, so both 2FA
// screens can bounce the person back to the password step with an honest "your session expired,
// sign in again" instead of a misleading invalid-code error. `active` lets a screen stop the timer
// once the challenge has already been redeemed (e.g. once recovery codes are on screen, the
// original challenge no longer matters — the real session's own TTL governs from there).
export function useChallengeExpiry(expiresAtUtc: string, onExpired: () => void, active: boolean): void {
  // `onExpired` is typically a fresh closure every render; reading it through a ref lets the timer
  // always call the latest version without restarting the countdown every time an unrelated
  // render happens.
  const onExpiredRef = useRef(onExpired);
  onExpiredRef.current = onExpired;

  useEffect(() => {
    if (!active) return undefined;
    const msRemaining = Date.parse(expiresAtUtc) - Date.now();
    const timer = window.setTimeout(() => onExpiredRef.current(), Math.max(msRemaining, 0));
    return () => window.clearTimeout(timer);
  }, [expiresAtUtc, active]);
}
