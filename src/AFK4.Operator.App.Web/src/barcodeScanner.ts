export interface ScannerState { buffer: string; lastKeyMs: number }
export const EMPTY_SCANNER: ScannerState = { buffer: '', lastKeyMs: 0 };

export interface ScannerOptions { minLength?: number; maxInterKeyMs?: number }
export interface ScannerStep { state: ScannerState; scanned?: string; capture: boolean }

export const MIN_CODE_LENGTH = 3;
export const MAX_INTER_KEY_MS = 50;

const IGNORED = new Set(['Shift', 'Control', 'Alt', 'Meta', 'CapsLock', 'Tab', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown']);

// HID-сканер «печатает» символы код очень быстро и завершает Enter'ом.
// Быстрый ввод (gap ≤ maxInterKeyMs) копим и помечаем capture=true (хук сделает preventDefault,
// чтобы цифры не утекли в сфокусированное поле). Медленный ввод человеком — отбрасываем.
export function feedScanner(state: ScannerState, key: string, timeMs: number, opts: ScannerOptions = {}): ScannerStep {
  const minLength = opts.minLength ?? MIN_CODE_LENGTH;
  const maxGap = opts.maxInterKeyMs ?? MAX_INTER_KEY_MS;

  if (key === 'Enter') {
    const code = state.buffer;
    const fastEnough = code.length >= minLength;
    if (fastEnough) return { state: EMPTY_SCANNER, scanned: code, capture: true };
    return { state: EMPTY_SCANNER, capture: false };
  }
  if (IGNORED.has(key)) return { state, capture: false };
  if (key.length !== 1) return { state: EMPTY_SCANNER, capture: false }; // неизвестная спец-клавиша → сброс

  const gap = timeMs - state.lastKeyMs;
  const continuing = state.buffer.length > 0 && gap <= maxGap;
  const buffer = continuing ? state.buffer + key : key;
  // capture только когда уверены, что это сканер: ≥2 быстрых символа подряд
  const capture = continuing;
  return { state: { buffer, lastKeyMs: timeMs }, capture };
}
