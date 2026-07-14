import { describe, expect, it } from 'bun:test';
import { isLowStock } from './BackendPosWorkspace';

describe('isLowStock', () => {
  it('backend, stockOnHand=3, reorderThreshold=5 → true (ниже порога)', () => {
    expect(isLowStock({ source: 'backend', trackStock: true, stockOnHand: 3, reorderThreshold: 5 })).toBe(true);
  });

  it('backend, stockOnHand=3, reorderThreshold=2 → false (выше своего порога; старый хардкод <=2 считал бы low)', () => {
    expect(isLowStock({ source: 'backend', trackStock: true, stockOnHand: 3, reorderThreshold: 2 })).toBe(false);
  });

  it('backend without stock tracking does not alert at zero stock', () => {
    expect(isLowStock({ source: 'backend', trackStock: false, stockOnHand: 0, reorderThreshold: 0 })).toBe(false);
  });

  it('backend with stock tracking alerts at zero stock even when threshold alerts are disabled', () => {
    expect(isLowStock({ source: 'backend', trackStock: true, stockOnHand: 0, reorderThreshold: 0 })).toBe(true);
  });

  it('backend with stock tracking and a disabled threshold does not alert above zero', () => {
    expect(isLowStock({ source: 'backend', trackStock: true, stockOnHand: 3, reorderThreshold: 0 })).toBe(false);
  });

  it('fixture, stockOnHand=0, reorderThreshold=5 → false (фикстуры не алертят)', () => {
    expect(isLowStock({ source: 'fixture', trackStock: false, stockOnHand: 0, reorderThreshold: 5 })).toBe(false);
  });

  it('backend, stockOnHand=5, reorderThreshold=5 → true (граница <=)', () => {
    expect(isLowStock({ source: 'backend', trackStock: true, stockOnHand: 5, reorderThreshold: 5 })).toBe(true);
  });
});
