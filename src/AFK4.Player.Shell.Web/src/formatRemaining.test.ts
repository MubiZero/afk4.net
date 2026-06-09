import { describe, expect, it } from 'bun:test';
import { formatRemaining } from './formatRemaining';

describe('formatRemaining', () => {
  it('formats seconds as H:MM:SS', () => {
    expect(formatRemaining(3661)).toBe('1:01:01');
  });

  it('formats sub-hour as M:SS', () => {
    expect(formatRemaining(125)).toBe('2:05');
  });

  it('renders a dash when null', () => {
    expect(formatRemaining(null)).toBe('—');
  });
});
