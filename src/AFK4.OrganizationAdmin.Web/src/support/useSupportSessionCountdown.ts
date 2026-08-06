import { useEffect, useRef, useState } from 'react';

// Same slack and rationale as CLOCK_SKEW_TOLERANCE_MS in
// AFK4.PlatformControl.Web/src/components/useChallengeExpiry.ts: the deadline is computed against
// the SERVER's timestamp but the BROWSER's clock, and a fast local clock shouldn't end someone's
// support session early just because their machine's clock lies. The two apps don't share a
// package for this hook, so the constant and the NaN-guard are duplicated deliberately — same
// approach, not reinvented.
export const CLOCK_SKEW_TOLERANCE_MS = 30_000;

function remainingMsFor(expiresAtUtc: string): number {
  const expiresAtMs = Date.parse(expiresAtUtc);
  // A garbage/unparsable timestamp must never arm a bogus countdown: treat it as already expired
  // (0 remaining) rather than let `NaN - Date.now()` propagate into `Math.max`, which returns NaN,
  // not 0 — the same trap useChallengeExpiry.ts guards against for its setTimeout delay. This
  // mirrors isSupportSessionExpired's fail-safe convention (garbage => expired) rather than
  // useChallengeExpiry's "disarm and do nothing" convention, because revoking access early is the
  // safe direction to fail in here, not the risky one.
  if (Number.isNaN(expiresAtMs)) {
    return 0;
  }
  return Math.max(expiresAtMs - Date.now() + CLOCK_SKEW_TOLERANCE_MS, 0);
}

// Ticking countdown (recomputed once a second, not just a single deadline timer) for the support
// mode banner. Fires `onExpired` once, the instant the remaining time reaches zero, so the banner
// can end the session itself without waiting for the next API call to bounce with a stale grant.
export function useSupportSessionCountdown(expiresAtUtc: string, onExpired: () => void): number {
  const onExpiredRef = useRef(onExpired);
  onExpiredRef.current = onExpired;

  const [remainingMs, setRemainingMs] = useState(() => remainingMsFor(expiresAtUtc));

  useEffect(() => {
    const initial = remainingMsFor(expiresAtUtc);
    setRemainingMs(initial);
    if (initial <= 0) {
      // Already expired (or an unparsable timestamp, fail-safe) by the time the banner mounts —
      // don't make someone wait out a full tick to be told the obvious.
      onExpiredRef.current();
      return undefined;
    }

    const timer = window.setInterval(() => {
      const next = remainingMsFor(expiresAtUtc);
      setRemainingMs(next);
      if (next <= 0) {
        window.clearInterval(timer);
        onExpiredRef.current();
      }
    }, 1000);

    return () => window.clearInterval(timer);
  }, [expiresAtUtc]);

  return remainingMs;
}

// mm:ss for under an hour (the common case), h:mm:ss once a grant runs an hour or longer.
export function formatSupportCountdown(remainingMs: number): string {
  const totalSeconds = Math.floor(remainingMs / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  const pad = (value: number) => String(value).padStart(2, '0');

  return hours > 0
    ? `${hours}:${pad(minutes)}:${pad(seconds)}`
    : `${pad(minutes)}:${pad(seconds)}`;
}
