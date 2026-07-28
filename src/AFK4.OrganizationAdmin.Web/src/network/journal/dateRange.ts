export interface DateRange {
  fromUtc: string;
  toUtc: string;
}

export type RangePreset = 'today' | '7d' | '30d';

export function presetRange(preset: RangePreset, now: Date): DateRange {
  const y = now.getUTCFullYear();
  const m = now.getUTCMonth();
  const d = now.getUTCDate();
  const back = preset === 'today' ? 0 : preset === '7d' ? 6 : 29;
  const start = new Date(Date.UTC(y, m, d - back, 0, 0, 0));
  const end = new Date(Date.UTC(y, m, d, 23, 59, 59));
  return { fromUtc: start.toISOString(), toUtc: end.toISOString() };
}

export function isoToDateInput(iso: string): string {
  return iso.slice(0, 10);
}

export function dateInputToFromUtc(date: string): string {
  return `${date}T00:00:00.000Z`;
}

export function dateInputToToUtc(date: string): string {
  return `${date}T23:59:59.000Z`;
}
