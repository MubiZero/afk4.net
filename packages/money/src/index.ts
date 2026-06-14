/** Backend stores money as integer minor units (e.g. kopecks). These helpers
 * convert to/from the major units shown in and entered through the UI. */
export function minorToMajor(minorUnits: number): number {
  return minorUnits / 100;
}

export function majorToMinor(major: number): number {
  // Round on a value nudged by a tiny epsilon so that values like 1.005 — which
  // are stored as 1.00499999… in IEEE-754 — round up as a human expects.
  return Math.round((major + Number.EPSILON) * 100);
}

/** Short, human-facing currency signs shown in the UI instead of raw ISO codes
 * (which read as technical jargon). Falls back to the ISO code for currencies
 * not listed here. */
export const currencySymbols: Readonly<Record<string, string>> = {
  TJS: 'с.',
  USD: '$',
  EUR: '€',
  RUB: '₽'
};

export function currencySymbol(currencyCode: string): string {
  return currencySymbols[currencyCode.toUpperCase()] ?? currencyCode;
}
