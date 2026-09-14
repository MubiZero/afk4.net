import { describe, expect, it } from 'bun:test';
import { canSelfVoidSale, SELF_VOID_WINDOW_MS } from './selfVoid';

const NOW = Date.parse('2026-09-13T20:00:00Z');

const input = (overrides: Partial<Parameters<typeof canSelfVoidSale>[0]> = {}) => ({
  createdByStaffUserId: 'cashier',
  createdAtUtc: new Date(NOW - 60_000).toISOString(),
  saleShiftId: 'shift-open',
  actorStaffUserId: 'cashier',
  openShiftId: 'shift-open',
  nowMs: NOW,
  ...overrides
});

describe('canSelfVoidSale', () => {
  it('свой свежий чек в открытой смене — можно', () => {
    expect(canSelfVoidSale(input())).toBe(true);
  });

  it('чужой чек — нельзя', () => {
    expect(canSelfVoidSale(input({ createdByStaffUserId: 'someone-else' }))).toBe(false);
  });

  it('чек из другой смены — нельзя', () => {
    expect(canSelfVoidSale(input({ saleShiftId: 'shift-closed' }))).toBe(false);
  });

  it('без открытой смены — нельзя', () => {
    expect(canSelfVoidSale(input({ openShiftId: null }))).toBe(false);
  });

  it('ровно на границе окна — ещё можно', () => {
    expect(canSelfVoidSale(input({
      createdAtUtc: new Date(NOW - SELF_VOID_WINDOW_MS).toISOString()
    }))).toBe(true);
  });

  it('за границей окна — уже нельзя', () => {
    expect(canSelfVoidSale(input({
      createdAtUtc: new Date(NOW - SELF_VOID_WINDOW_MS - 1000).toISOString()
    }))).toBe(false);
  });

  // Часы разъехались: продажа «из будущего» не должна открывать бессрочное окно.
  it('чек из будущего — нельзя', () => {
    expect(canSelfVoidSale(input({ createdAtUtc: new Date(NOW + 60_000).toISOString() }))).toBe(false);
  });

  it('нечитаемая дата — нельзя', () => {
    expect(canSelfVoidSale(input({ createdAtUtc: 'не дата' }))).toBe(false);
  });

  it('окно совпадает с серверным', () => {
    expect(SELF_VOID_WINDOW_MS).toBe(5 * 60 * 1000);
  });
});
