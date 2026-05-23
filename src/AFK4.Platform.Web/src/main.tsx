import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import './styles.css';

const container = document.getElementById('root');
if (container === null) {
  throw new Error('Root element not found.');
}

createRoot(container).render(
  <StrictMode>
    <App apiBaseUrl={resolveApiBaseUrl()} />
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
