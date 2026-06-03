import { afterEach, expect } from 'bun:test';
import { GlobalRegistrator } from '@happy-dom/global-registrator';
import * as matchers from '@testing-library/jest-dom/matchers';

// happy-dom must be registered before @testing-library is imported: testing-library's
// `screen` binds to `document.body` at module-evaluation time, so a dynamic import keeps
// that evaluation after the DOM globals exist.
GlobalRegistrator.register({ url: 'http://localhost/' });
expect.extend(matchers);

const { cleanup } = await import('@testing-library/react');

// App.test.tsx registers a process-wide mock.module('./operatorRealtime', ...) that bun
// cannot reliably restore for sibling files; worse, mock.module retroactively mutates the
// exports of any previously-imported namespace object. The shared preload runs before any
// test file (and therefore before that mock), so snapshot the genuine function/value
// references here — direct references survive the later namespace mutation — letting
// operatorRealtime.test.ts exercise the real client regardless of suite ordering.
(globalThis as typeof globalThis & {
  __afk4RealOperatorRealtime?: typeof import('../operatorRealtime');
}).__afk4RealOperatorRealtime = { ...(await import('../operatorRealtime')) };

afterEach(() => {
  cleanup();
  // Isolate persisted web storage between tests: the i18n provider seeds locale from localStorage,
  // and the offline floor-map cache / action outbox persist to both stores (§6.5).
  try { localStorage.clear(); sessionStorage.clear(); } catch { /* no web storage in this env */ }
});
