import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { I18nProvider } from '@afk4/i18n';
import { App } from './App';
import './index.css';

const initialLocale = (globalThis.localStorage?.getItem('afk4.player.locale') as 'ru' | 'en' | null) ?? 'ru';

const root = document.getElementById('root');
if (root) {
  createRoot(root).render(
    <StrictMode>
      <I18nProvider initialLocale={initialLocale}>
        <App />
      </I18nProvider>
    </StrictMode>
  );
}

if (import.meta.env.PROD) {
  void import('./pwa/registerSW').then((m) => m.registerServiceWorker());
}
