import { describe, expect, it } from 'bun:test';
import { isLowStock } from './BackendPosWorkspace';

describe('isLowStock', () => {
  it('backend, stockOnHand=3, reorderThreshold=5 → true (ниже порога)', () => {
    expect(isLowStock({ source: 'backend', stockOnHand: 3, reorderThreshold: 5 })).toBe(true);
  });

  it('backend, stockOnHand=3, reorderThreshold=2 → false (выше своего порога; старый хардкод <=2 считал бы low)', () => {
    expect(isLowStock({ source: 'backend', stockOnHand: 3, reorderThreshold: 2 })).toBe(false);
  });

  it('backend, stockOnHand=0, reorderThreshold=0 → false (порог 0 = без алертинга)', () => {
    expect(isLowStock({ source: 'backend', stockOnHand: 0, reorderThreshold: 0 })).toBe(false);
  });

  it('fixture, stockOnHand=0, reorderThreshold=5 → false (фикстуры не алертят)', () => {
    expect(isLowStock({ source: 'fixture', stockOnHand: 0, reorderThreshold: 5 })).toBe(false);
  });

  it('backend, stockOnHand=5, reorderThreshold=5 → true (граница <=)', () => {
    expect(isLowStock({ source: 'backend', stockOnHand: 5, reorderThreshold: 5 })).toBe(true);
  });
});
