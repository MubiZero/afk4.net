import { describe, expect, it } from 'vitest';
import { slugify } from './slugify';

describe('slugify', () => {
  it('lowercases and hyphenates latin input', () => {
    expect(slugify('AFK4 Demo Club')).toBe('afk4-demo-club');
  });

  it('transliterates cyrillic to latin', () => {
    expect(slugify('AFK4 Душанбе')).toBe('afk4-dushanbe');
  });

  it('collapses repeats and trims edge hyphens', () => {
    expect(slugify('  Привет,  Мир!! ')).toBe('privet-mir');
  });

  it('returns empty string when nothing maps', () => {
    expect(slugify('——')).toBe('');
  });
});
