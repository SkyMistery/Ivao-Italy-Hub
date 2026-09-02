/// <reference types="vitest/config" />
import { readdir, readFile } from 'node:fs/promises';
import { join, posix, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

import tailwindcss from '@tailwindcss/vite';
import { tanstackRouter } from '@tanstack/router-plugin/vite';
import react from '@vitejs/plugin-react';
import { defineConfig, type Plugin } from 'vite';

/** The backend during development; Vite proxies the host endpoints to it. */
const KESTREL_ORIGIN = 'http://localhost:5000';

/** Language files live at the root of the repository and are shared with the backend. */
const LOCALES_DIR = fileURLToPath(new URL('../locales', import.meta.url));

async function listFiles(directory: string): Promise<string[]> {
  const entries = await readdir(directory, { withFileTypes: true, recursive: true });
  return entries.filter((entry) => entry.isFile()).map((entry) => join(entry.parentPath, entry.name));
}

/**
 * Serves `/locales/**` from the repository root during development and copies the same files into
 * `dist/locales` at build time, so that the published `wwwroot` carries them without a second
 * mechanism. There is exactly one set of language files (design M0 section 7.6).
 */
function divisionLocales(): Plugin {
  return {
    name: 'ivao-hub-locales',
    configureServer(server) {
      server.middlewares.use('/locales', (request, response, next) => {
        const requested = (request.url ?? '/').split('?')[0] ?? '/';
        const file = resolve(LOCALES_DIR, `.${requested}`);
        if (!file.startsWith(LOCALES_DIR)) {
          next();
          return;
        }
        readFile(file).then(
          (content) => {
            response.setHeader('Content-Type', 'application/json; charset=utf-8');
            response.end(content);
          },
          () => next(),
        );
      });
    },
    async generateBundle() {
      for (const file of await listFiles(LOCALES_DIR)) {
        this.emitFile({
          type: 'asset',
          fileName: posix.join('locales', relative(LOCALES_DIR, file).split(sep).join('/')),
          source: await readFile(file),
        });
      }
    },
  };
}

export default defineConfig({
  plugins: [
    tanstackRouter({ target: 'react', autoCodeSplitting: true }),
    react(),
    tailwindcss(),
    divisionLocales(),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': KESTREL_ORIGIN,
      '/auth': KESTREL_ORIGIN,
      '/health': KESTREL_ORIGIN,
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
    globals: false,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
  },
});
