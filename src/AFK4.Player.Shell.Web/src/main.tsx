import { StrictMode, type ReactNode } from 'react';
import { createRoot } from 'react-dom/client';
import { ShellI18nProvider } from './i18n/ShellI18nProvider';
import { App } from './App';
import './styles/shell.css';

async function start() {
  let frame: (app: ReactNode) => ReactNode = (app) => app;

  // Учебный хост — только в dev-сборке и в публичном демо (`bun run build:demo`,
  // docs/operations/demo-panel.md): без WPF и агента экран листается сценариями. Оба условия
  // подставляются при сборке константами, поэтому боевая сборка выбрасывает и ветку, и модули.
  if ((import.meta.env.DEV || import.meta.env.VITE_AFK4_DEMO === '1') && !window.chrome?.webview) {
    const { installDevHost } = await import('./host/devHost');
    const control = installDevHost();
    if (import.meta.env.VITE_AFK4_DEMO === '1') {
      const { DemoFrame } = await import('./demo/DemoFrame');
      frame = (app) => <DemoFrame control={control}>{app}</DemoFrame>;
    }
  }

  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <ShellI18nProvider>
        {frame(<App />)}
      </ShellI18nProvider>
    </StrictMode>
  );
}

void start();
