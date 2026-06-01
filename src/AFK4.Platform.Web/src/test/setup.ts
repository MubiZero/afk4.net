import { afterEach, expect } from 'bun:test';
import { GlobalRegistrator } from '@happy-dom/global-registrator';
import * as matchers from '@testing-library/jest-dom/matchers';

GlobalRegistrator.register({ url: 'http://localhost/' });
expect.extend(matchers);

const { cleanup } = await import('@testing-library/react');

afterEach(() => {
  cleanup();
  // Isolate locale persistence between tests (the i18n provider seeds from localStorage).
  try { localStorage.clear(); } catch { /* no localStorage in this env */ }
});
