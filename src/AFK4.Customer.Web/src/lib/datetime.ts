// Localized day+time for list rows and receipts. Locale/timezone come from the
// runtime (Intl); tests assert structure, not exact localized text, to stay TZ-stable.
export function formatDateTime(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  return date.toLocaleString('ru-RU', {
    day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit'
  });
}

// Whole-minute duration between two instants (end defaults to now for open visits),
// rendered "Hч Mм" / "Mм". Pure integer math — deterministic across timezones.
export function formatDuration(startIso: string, endIso: string | null): string {
  const start = Date.parse(startIso);
  const end = endIso ? Date.parse(endIso) : Date.now();
  if (Number.isNaN(start) || Number.isNaN(end)) return '';
  const totalMinutes = Math.max(0, Math.round((end - start) / 60_000));
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return hours > 0 ? `${hours}ч ${minutes}м` : `${minutes}м`;
}
