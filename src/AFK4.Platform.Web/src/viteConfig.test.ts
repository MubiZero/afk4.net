import { describe, expect, it } from 'vitest';
import config from '../vite.config';

describe('Vite deployment config', () => {
  it('uses root-relative asset URLs so direct SPA route loads work behind nginx fallback', () => {
    expect(config.base).toBe('/');
  });
});
