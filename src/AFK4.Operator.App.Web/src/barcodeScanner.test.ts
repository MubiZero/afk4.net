import { describe, it, expect } from 'bun:test';
import { feedScanner, EMPTY_SCANNER, matchByBarcode } from './barcodeScanner';

function run(keys: Array<[string, number]>) {
  let state = EMPTY_SCANNER;
  let scanned: string | undefined;
  let captures = 0;
  for (const [key, t] of keys) {
    const step = feedScanner(state, key, t);
    state = step.state;
    if (step.scanned) scanned = step.scanned;
    if (step.capture) captures++;
  }
  return { scanned, captures };
}

describe('feedScanner', () => {
  it('fast digits + Enter → scanned code', () => {
    const { scanned } = run([['4', 0], ['6', 10], ['0', 20], ['1', 30], ['Enter', 40]]);
    expect(scanned).toBe('4601');
  });

  it('slow human typing + Enter → no scan', () => {
    const { scanned } = run([['4', 0], ['6', 300], ['0', 600], ['1', 900], ['Enter', 1200]]);
    expect(scanned).toBeUndefined();
  });

  it('Enter alone → no scan', () => {
    const { scanned } = run([['Enter', 0]]);
    expect(scanned).toBeUndefined();
  });

  it('too short fast burst + Enter → no scan (below minLength)', () => {
    const { scanned } = run([['1', 0], ['2', 10], ['Enter', 20]]);
    expect(scanned).toBeUndefined();
  });

  it('captures fast keystrokes so they do not leak into focused fields', () => {
    const { captures } = run([['4', 0], ['6', 10], ['0', 20], ['1', 30], ['Enter', 40]]);
    expect(captures).toBeGreaterThan(0);
  });

  it('ignores modifier/navigation keys, keeps digit buffer', () => {
    const { scanned } = run([['4', 0], ['Shift', 10], ['6', 20], ['0', 30], ['1', 40], ['Enter', 50]]);
    expect(scanned).toBe('4601');
  });
});

describe('matchByBarcode', () => {
  const catalog = [
    { name: 'Cola', barcodes: ['111', '222'] },
    { name: 'Water', barcodes: ['333'] },
    { name: 'NoCode' }
  ];

  it('finds item by matching barcode', () => {
    expect(matchByBarcode(catalog, '111')?.name).toBe('Cola');
    expect(matchByBarcode(catalog, '222')?.name).toBe('Cola');
    expect(matchByBarcode(catalog, '333')?.name).toBe('Water');
  });

  it('returns undefined for unknown code', () => {
    expect(matchByBarcode(catalog, '999')).toBeUndefined();
  });

  it('handles items without barcodes field', () => {
    expect(matchByBarcode(catalog, 'NoCode')).toBeUndefined();
  });

  it('returns undefined for empty catalog', () => {
    expect(matchByBarcode([], '111')).toBeUndefined();
  });
});
