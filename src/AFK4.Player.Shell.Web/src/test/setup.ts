import { afterEach, expect } from 'bun:test';
import { GlobalRegistrator } from '@happy-dom/global-registrator';
import * as matchers from '@testing-library/jest-dom/matchers';

// happy-dom must be registered before @testing-library is imported: testing-library's
// `screen` binds to `document.body` at module-evaluation time, so a dynamic import keeps
// that evaluation after the DOM globals exist.
GlobalRegistrator.register({ url: 'https://player.afk4.local/' });
expect.extend(matchers);

// Tell React 18+ that this is a test environment so it flushes state updates
// synchronously after async callbacks, making waitFor() reliable without flushSync.
(globalThis as any).IS_REACT_ACT_ENVIRONMENT = true;

const { cleanup } = await import('@testing-library/react');

afterEach(() => {
  cleanup();
});
