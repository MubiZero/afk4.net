import path from 'node:path';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

export default defineConfig({
  base: '/',
  plugins: [react()],
  resolve: {
    alias: { '@': path.resolve(import.meta.dirname, './src') }
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          const match = /packages\/i18n\/src\/messages\.(ru|en|tg)\.ts$/u.exec(id.replaceAll('\\', '/'));
          return match === null ? undefined : `i18n-${match[1]}`;
        }
      }
    }
  }
});
