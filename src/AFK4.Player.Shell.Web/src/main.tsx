import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { ShellI18nProvider } from './i18n/ShellI18nProvider';
import { App } from './App';
import './styles/shell.css';

async function start() {
  // Учебный хост — только в dev-сборке: без WPF и агента экран листается сценариями.
  if (import.meta.env.DEV && !window.chrome?.webview) {
    const { installDevHost } = await import('./host/devHost');
    installDevHost();
  }

  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <ShellI18nProvider>
        <App />
      </ShellI18nProvider>
    </StrictMode>
  );
}

void start();
