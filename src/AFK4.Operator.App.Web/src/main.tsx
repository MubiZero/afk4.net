import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { I18nProvider } from '@afk4/i18n';
import { App } from './App';
import { OperatorThemeProvider } from './operatorTheme';
import './styles.css';

function render(): void {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <I18nProvider>
        <OperatorThemeProvider>
          <App />
        </OperatorThemeProvider>
      </I18nProvider>
    </StrictMode>
  );
}

// In a plain-browser dev run the native WebView2 host bridge is absent, so install the dev-only
// stub before mounting. The dynamic import is gated on import.meta.env.DEV, so Vite drops both the
// branch and the module from the production build — the stub never ships in the MSI.
if (import.meta.env.DEV) {
  void import('./devHostBridge').then(({ installDevHostBridge }) => {
    installDevHostBridge();
    render();
  });
} else {
  render();
}
