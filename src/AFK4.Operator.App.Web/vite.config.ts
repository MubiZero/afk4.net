import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// NOTE: dev-only staging proxy lives here. Its companion is the dev host-bridge stub
// (src/devHostBridge.ts), installed only under import.meta.env.DEV from main.tsx — both are
// inert/absent in the production build. Lets the operator UI run in a plain browser against
// afk4.staging.mubi.dev (avoids CORS; the native WebView2 host is not needed).
export default defineConfig({
  base: './',
  plugins: [react()],
  server: {
    host: '127.0.0.1',
    port: 5174,
    proxy: {
      '/api': { target: 'https://afk4.staging.mubi.dev', changeOrigin: true, secure: true },
      '/hubs': { target: 'https://afk4.staging.mubi.dev', changeOrigin: true, secure: true, ws: true }
    }
  }
});
