import { useEffect, useRef } from 'react';

// The deadline is computed against the SERVER's timestamp but the BROWSER's clock — an unsynced
// client clock (common enough on real machines) can run ahead. Without slack, a fast clock would
// bounce someone out mid-way through typing a still-valid code, purely because their own laptop's
// clock lies. Erring toward "let the server have the final say" is deliberate: worst case here is
// the server rejects a late code with its own clear reason — the exact 401 this hook exists to
// route around when it genuinely IS the deadline, not a clock lie. Erring the other way (bouncing
// early) recreates the "invalid code" trap this hook was built to eliminate.
export const CLOCK_SKEW_TOLERANCE_MS = 30_000;

// A sign-in challenge token dies after 2 minutes server-side, but the server can't tell the UI
// when that happens — a request against a dead challenge just answers 401, identically to a wrong
// code. Left alone, someone who sits on the QR/code screen past the window types the CORRECT code
// and reads "invalid code, check your phone's clock" — a dead end, since retrying never helps.
//
// This hook schedules `onExpired` to fire once, exactly when the window runs out (plus
// CLOCK_SKEW_TOLERANCE_MS of slack), so both 2FA screens can bounce the person back to the
// password step with an honest "your session expired, sign in again" instead of a misleading
// invalid-code error. `active` lets a screen stop the timer once the challenge has already been
// redeemed (e.g. once recovery codes are on screen, the original challenge no longer matters — the
// real session's own TTL governs from there).
export function useChallengeExpiry(expiresAtUtc: string, onExpired: () => void, active: boolean): void {
  // `onExpired` is typically a fresh closure every render; reading it through a ref lets the timer
  // always call the latest version without restarting the countdown every time an unrelated
  // render happens.
  const onExpiredRef = useRef(onExpired);
  onExpiredRef.current = onExpired;

  useEffect(() => {
    if (!active) return undefined;
    const expiresAtMs = Date.parse(expiresAtUtc);
    // An unparsable timestamp must never arm the timer: `setTimeout` with a NaN delay fires on the
    // very next tick (per spec, NaN coerces to 0), which would bounce the person out the instant
    // this screen mounts — a silent, unrecoverable loop with no explanation. No timer at all just
    // degrades to the pre-existing behaviour (whatever error the server itself eventually gives),
    // which is the safe direction to fail in.
    if (Number.isNaN(expiresAtMs)) return undefined;
    const msRemaining = expiresAtMs - Date.now() + CLOCK_SKEW_TOLERANCE_MS;
    const timer = window.setTimeout(() => onExpiredRef.current(), Math.max(msRemaining, 0));
    return () => window.clearTimeout(timer);
  }, [expiresAtUtc, active]);
}
