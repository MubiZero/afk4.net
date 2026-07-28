import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import { ThemeProvider } from './theme/ThemeProvider';
import { I18nProvider } from './i18n/I18nProvider';
import { ToastProvider } from './components/ui/toast';
import './index.css';
import './styles.css';

const container = document.getElementById('root');
if (container === null) {
  throw new Error('Root element not found.');
}

document.title = 'AFK4.NET Platform Control Plane';

createRoot(container).render(
  <StrictMode>
    <ThemeProvider>
      <I18nProvider>
        <ToastProvider>
          <App apiBaseUrl={resolveApiBaseUrl()} />
        </ToastProvider>
      </I18nProvider>
    </ThemeProvider>
  </StrictMode>
);

function resolveApiBaseUrl(): string {
  const meta = (
    import.meta as unknown as { env?: Record<string, string | undefined> }
  ).env;
  const override = meta?.VITE_PLATFORM_API_BASE_URL;
  if (typeof override === 'string' && override.length > 0) {
    return override;
  }
  if (typeof window !== 'undefined') {
    return `${window.location.protocol}//${window.location.host}`;
  }
  return 'http://localhost:5000';
}
