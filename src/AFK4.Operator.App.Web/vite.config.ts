import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// NOTE: dev-only staging proxy lives here (+ host-bridge stub in index.html).
// Working-tree only — do not commit. Lets the operator UI run in a plain browser
// against afk4.staging.mubi.dev (avoids CORS; the native WebView2 host is not needed).
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
