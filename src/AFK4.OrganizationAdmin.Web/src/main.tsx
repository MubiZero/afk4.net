import { StrictMode, type ReactElement } from 'react';
import { createRoot } from 'react-dom/client';
import { I18nProvider } from '@afk4/i18n';
import { App } from './App';
import { OperatorThemeProvider } from './operatorTheme';
import { getOperatorConfig } from './operatorConfig';
import { redeemSupportTicket, writeSupportSession } from './support/supportSession';
import { SupportAccessErrorScreen } from './support/SupportAccessErrorScreen';
import { clearStoredSession } from './auth/staffSessionStore';
import '@afk4/tokens/tokens.css';
import './styles.css';

function mount(children: ReactElement): void {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <I18nProvider>
        <OperatorThemeProvider>
          {children}
        </OperatorThemeProvider>
      </I18nProvider>
    </StrictMode>
  );
}

function render(): void {
  mount(<App />);
}

// Support links land on /support-access?ticket=<one-shot ticket>. The ticket is a secret that only
// works once and only lives 60 seconds server-side, so: exchange it for a support session, store the
// session, and — success or failure — always strip it from the address bar so it can't linger in
// browser history or the tab title. Returns false when the caller should render the error screen
// instead of the app (redeem failed, so there's neither a staff session nor a support session yet).
async function acceptSupportTicketIfPresent(): Promise<boolean> {
  if (window.location.pathname !== '/support-access') {
    return true;
  }

  const ticket = new URLSearchParams(window.location.search).get('ticket');
  if (!ticket) {
    window.history.replaceState(null, '', '/');
    return true;
  }

  try {
    const session = await redeemSupportTicket(getOperatorConfig().platformBaseUrl, ticket);
    // A support session replaces a staff login in this tab, not layers on top of it — the
    // transport already prefers the support grant header unconditionally, but leaving a stale
    // staff session in storage would let it silently take over again the moment the support
    // session is cleared/expires, well past whatever the support visit was scoped for.
    clearStoredSession();
    writeSupportSession(session);
    return true;
  } catch {
    return false;
  } finally {
    window.history.replaceState(null, '', '/');
  }
}

async function start(): Promise<void> {
  const ticketAccepted = await acceptSupportTicketIfPresent();
  if (!ticketAccepted) {
    mount(<SupportAccessErrorScreen />);
    return;
  }

  render();
}

// In a plain-browser dev run the native WebView2 host bridge is absent, so install the dev-only
// stub before mounting. The dynamic import is gated on import.meta.env.DEV, so Vite drops both the
// branch and the module from the production build — the stub never ships in the MSI.
if (import.meta.env.DEV) {
  void import('./devHostBridge').then(({ installDevHostBridge }) => {
    installDevHostBridge();
    void start();
  });
} else {
  void start();
}
