import { describe, expect, it } from 'bun:test';
import { nextSearchIndex } from './searchModel';

describe('nextSearchIndex', () => {
  it('wraps keyboard selection in both directions', () => {
    expect(nextSearchIndex(-1, 1, 3)).toBe(0);
    expect(nextSearchIndex(2, 1, 3)).toBe(0);
    expect(nextSearchIndex(0, -1, 3)).toBe(2);
    expect(nextSearchIndex(0, 1, 0)).toBe(-1);
  });
});
