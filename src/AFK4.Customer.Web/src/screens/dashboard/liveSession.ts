export function elapsedSeconds(startedAtUtc: string, now: Date = new Date()): number {
  const started = Date.parse(startedAtUtc);
  if (Number.isNaN(started)) return 0;
  return Math.max(0, Math.floor((now.getTime() - started) / 1000));
}

export function projectRemainingSeconds(
  remainingAtFetch: number,
  fetchedAt: Date,
  now: Date = new Date()
): number {
  const drift = Math.floor((now.getTime() - fetchedAt.getTime()) / 1000);
  return Math.max(0, remainingAtFetch - drift);
}

export function formatClock(totalSeconds: number): string {
  const s = Math.max(0, Math.floor(totalSeconds));
  const hh = String(Math.floor(s / 3600)).padStart(2, '0');
  const mm = String(Math.floor((s % 3600) / 60)).padStart(2, '0');
  const ss = String(s % 60).padStart(2, '0');
  return `${hh}:${mm}:${ss}`;
}
