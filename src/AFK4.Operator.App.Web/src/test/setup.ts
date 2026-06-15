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

// Same hazard for operatorApiClients: PaymentGatewaysWorkspace/PhoneVerificationCard tests install
// a partial mock.module('./operatorApiClients') that bun keeps for the rest of the run and that
// mutates the shared namespace, breaking App.test.tsx and operatorApiClients.test.ts. Snapshot the
// genuine factories here so those files can restore/read the real implementation.
(globalThis as typeof globalThis & {
  __afk4RealOperatorApiClients?: typeof import('../operatorApiClients');
}).__afk4RealOperatorApiClients = { ...(await import('../operatorApiClients')) };

// Same hazard for operatorHelpers: ShopOrdersWorkspace's test installs a partial
// mock.module('./operatorHelpers') to stub createAuthenticatedOperatorClients. Snapshot the
// genuine helpers here so that file can restore the real implementation afterwards.
(globalThis as typeof globalThis & {
  __afk4RealOperatorHelpers?: typeof import('../operatorHelpers');
}).__afk4RealOperatorHelpers = { ...(await import('../operatorHelpers')) };

// NB: App.test.tsx (the only broad integration suite) depends on the REAL authClient/
// operatorApiClients/operatorHelpers AND its own operatorRealtime mock. In one bun process the
// above sibling mock.module registrations leak across files non-deterministically (bun keeps them
// for the whole run and order is not fixed), so App's data layer intermittently sees stubs and the
// branch heading never renders. The snapshot/restore dance above only helps for one file order.
// The robust fix lives in package.json "test": App.test runs in its OWN bun invocation, isolated
// from sibling mocks. Keep that split — re-merging App.test into the shared run reintroduces the flake.
afterEach(() => {
  cleanup();
  // Isolate persisted web storage between tests: the i18n provider seeds locale from localStorage,
  // and the offline floor-map cache / action outbox persist to both stores (§6.5).
  try { localStorage.clear(); sessionStorage.clear(); } catch { /* no web storage in this env */ }
});
