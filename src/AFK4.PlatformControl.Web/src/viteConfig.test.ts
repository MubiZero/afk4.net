import { describe, expect, it } from 'bun:test';
import config from '../vite.config';

describe('Vite deployment config', () => {
  it('uses root-relative asset URLs so direct SPA route loads work behind nginx fallback', () => {
    expect(config.base).toBe('/');
  });
});
